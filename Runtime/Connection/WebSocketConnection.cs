using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TestPlatform.SDK
{
    public class WebSocketConnection
    {
        private string _url;
        private readonly bool _enableLogging;
        private ClientWebSocket _socket;
        private CancellationTokenSource _cts;
        private readonly Queue<Message> _messageQueue = new Queue<Message>();
        private readonly object _queueLock = new object();

        public bool IsConnected => _socket?.State == WebSocketState.Open;

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnError;
        public event Action<Message> OnMessage;

        public WebSocketConnection(string url, bool enableLogging = true)
        {
            _url = url;
            _enableLogging = enableLogging;
        }

        public void SetUrl(string url)
        {
            _url = url;
        }

        public async void Connect()
        {
            if (IsConnected)
            {
                Log("Already connected");
                return;
            }

            try
            {
                _cts = new CancellationTokenSource();
                _socket = new ClientWebSocket();

                await _socket.ConnectAsync(new Uri(_url), _cts.Token);

                Log($"Connected to {_url}");
                OnConnected?.Invoke();

                _ = ReceiveLoop();
            }
            catch (Exception ex)
            {
                LogError($"Connection failed: {ex.Message}");
                OnError?.Invoke(ex.Message);
            }
        }

        public async void Disconnect()
        {
            if (_socket == null) return;

            try
            {
                _cts?.Cancel();

                if (_socket.State == WebSocketState.Open)
                {
                    await _socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closing",
                        CancellationToken.None
                    );
                }

                _socket.Dispose();
                _socket = null;

                Log("Disconnected");
                OnDisconnected?.Invoke();
            }
            catch (Exception ex)
            {
                LogError($"Disconnect error: {ex.Message}");
            }
        }

        public async void Send<T>(string type, T payload)
        {
            if (!IsConnected)
            {
                LogError("Cannot send: not connected");
                return;
            }

            try
            {
                // Build JSON manually to include payload
                var payloadJson = JsonUtility.ToJson(payload);
                var json = $"{{\"type\":\"{type}\",\"id\":\"{Guid.NewGuid()}\",\"timestamp\":{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()},\"payload\":{payloadJson}}}";
                var bytes = Encoding.UTF8.GetBytes(json);

                await _socket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    _cts.Token
                );

                Log($"Sent: {type}");
            }
            catch (Exception ex)
            {
                LogError($"Send error: {ex.Message}");
                OnError?.Invoke(ex.Message);
            }
        }

        private async Task ReceiveLoop()
        {
            var buffer = new byte[4096];

            try
            {
                while (IsConnected && !_cts.Token.IsCancellationRequested)
                {
                    var result = await _socket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        _cts.Token
                    );

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Log("Server closed connection");
                        OnDisconnected?.Invoke();
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        Log($"Raw JSON: {json}");

                        var message = Message.Parse(json);
                        Log($"Received: {message.Type}");

                        // Queue message for main thread processing
                        lock (_queueLock)
                        {
                            _messageQueue.Enqueue(message);
                        }

                        // Dispatch on main thread
                        MainThreadDispatcher.Enqueue(() => ProcessMessageQueue());
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
            }
            catch (Exception ex)
            {
                LogError($"Receive error: {ex.Message}");
                OnError?.Invoke(ex.Message);
                OnDisconnected?.Invoke();
            }
        }

        private void ProcessMessageQueue()
        {
            lock (_queueLock)
            {
                while (_messageQueue.Count > 0)
                {
                    var message = _messageQueue.Dequeue();
                    OnMessage?.Invoke(message);
                }
            }
        }

        private void Log(string message)
        {
            if (_enableLogging)
            {
                Debug.Log($"[TestPlatform.WS] {message}");
            }
        }

        private void LogError(string message)
        {
            Debug.LogError($"[TestPlatform.WS] {message}");
        }
    }
}
