using System;
using UnityEngine;

namespace TestPlatform.SDK
{
    /// <summary>
    /// Main entry point for Test Platform SDK.
    /// Add this component to a GameObject in your scene to enable test automation.
    /// </summary>
    public class TestSDK : MonoBehaviour
    {
        [Header("Connection Settings")]
        [SerializeField] private string serverUrl = "ws://localhost:4000/test";
        [SerializeField] private bool autoConnect = true;
        [SerializeField] private bool persistAcrossScenes = true;

        [Header("Debug")]
        [SerializeField] private bool enableLogging = true;

        public static TestSDK Instance { get; private set; }

        public bool IsConnected => _connection?.IsConnected ?? false;
        public string TestRunId { get; private set; }

        private WebSocketConnection _connection;
        private CommandExecutor _executor;
        private ElementLocator _locator;

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnError;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (persistAcrossScenes)
            {
                DontDestroyOnLoad(gameObject);
            }

            Initialize();
        }

        private void Initialize()
        {
            _locator = new ElementLocator();
            _executor = new CommandExecutor(_locator);
            _connection = new WebSocketConnection(serverUrl, enableLogging);

            _connection.OnConnected += HandleConnected;
            _connection.OnDisconnected += HandleDisconnected;
            _connection.OnError += HandleError;
            _connection.OnMessage += HandleMessage;
        }

        private void Start()
        {
            if (autoConnect)
            {
                Connect();
            }
        }

        private void OnDestroy()
        {
            Disconnect();

            if (_connection != null)
            {
                _connection.OnConnected -= HandleConnected;
                _connection.OnDisconnected -= HandleDisconnected;
                _connection.OnError -= HandleError;
                _connection.OnMessage -= HandleMessage;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Connect()
        {
            Log("Connecting to Test Platform...");
            _connection?.Connect();
        }

        public void Connect(string url)
        {
            serverUrl = url;
            _connection?.SetUrl(url);
            Connect();
        }

        public void Disconnect()
        {
            _connection?.Disconnect();
        }

        private void HandleConnected()
        {
            Log("Connected to Test Platform");
            SendSessionReady();
            OnConnected?.Invoke();
        }

        private void HandleDisconnected()
        {
            Log("Disconnected from Test Platform");
            OnDisconnected?.Invoke();
        }

        private void HandleError(string error)
        {
            LogError($"Connection error: {error}");
            OnError?.Invoke(error);
        }

        private async void HandleMessage(Message message)
        {
            Log($"Received message: {message.Type}");

            switch (message.Type)
            {
                case MessageTypes.Connected:
                    Log("Server acknowledged connection");
                    break;

                case MessageTypes.InitSession:
                    await HandleInitSession(message);
                    break;

                case MessageTypes.ExecuteStep:
                    await HandleExecuteStep(message);
                    break;

                case MessageTypes.ExecuteCommand:
                    await HandleExecuteCommand(message);
                    break;

                case MessageTypes.Abort:
                    HandleAbort(message);
                    break;

                default:
                    Log($"Unknown message type: {message.Type}");
                    break;
            }
        }

        private async System.Threading.Tasks.Task HandleInitSession(Message message)
        {
            var payload = message.GetPayload<InitSessionPayload>();
            TestRunId = payload.TestRunId;
            Log($"Test session initialized: {TestRunId}");
        }

        private async System.Threading.Tasks.Task HandleExecuteStep(Message message)
        {
            var payload = message.GetPayload<ExecuteStepPayload>();
            var startTime = DateTime.UtcNow;

            try
            {
                await _executor.Execute(payload.Command);

                var duration = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
                SendStepResult(payload.StepId, "passed", duration);
            }
            catch (Exception ex)
            {
                var duration = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
                SendStepResult(payload.StepId, "failed", duration, ex.Message);
            }
        }

        private void HandleAbort(Message message)
        {
            var payload = message.GetPayload<AbortPayload>();
            Log($"Test aborted: {payload.Reason}");
        }

        private async System.Threading.Tasks.Task HandleExecuteCommand(Message message)
        {
            Log("Executing debug command...");
            var payload = message.GetPayload<ExecuteCommandPayload>();

            if (payload?.Command == null)
            {
                LogError("Received execute_command with null command");
                return;
            }

            var startTime = DateTime.UtcNow;

            try
            {
                await _executor.Execute(payload.Command);
                var duration = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
                Log($"Command executed successfully in {duration}ms: {payload.Command.Action}");

                // Send result back - include screenshot if it was a screenshot command
                if (payload.Command.Action?.ToLower() == "screenshot" && ScreenshotCapture.LastScreenshot != null)
                {
                    var screenshotBase64 = Convert.ToBase64String(ScreenshotCapture.LastScreenshot);
                    _connection?.Send(MessageTypes.CommandResult, new
                    {
                        status = "passed",
                        action = payload.Command.Action,
                        durationMs = duration,
                        screenshot = screenshotBase64
                    });
                    ScreenshotCapture.Clear();
                }
                else
                {
                    _connection?.Send(MessageTypes.CommandResult, new
                    {
                        status = "passed",
                        action = payload.Command.Action,
                        durationMs = duration
                    });
                }
            }
            catch (Exception ex)
            {
                var duration = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
                LogError($"Command failed: {ex.Message}");

                _connection?.Send(MessageTypes.CommandResult, new
                {
                    status = "failed",
                    action = payload.Command.Action,
                    durationMs = duration,
                    error = ex.Message
                });
            }
        }

        private void SendSessionReady()
        {
            var payload = new SessionReadyPayload
            {
                TestRunId = TestRunId ?? "",
                SdkVersion = "0.1.0",
                UnityVersion = Application.unityVersion,
                DeviceInfo = new DeviceInfo
                {
                    Platform = Application.platform.ToString(),
                    OsVersion = SystemInfo.operatingSystem,
                    DeviceModel = SystemInfo.deviceModel,
                    ScreenWidth = Screen.width,
                    ScreenHeight = Screen.height
                }
            };

            _connection?.Send(MessageTypes.SessionReady, payload);
        }

        private void SendStepResult(string stepId, string status, int durationMs, string error = null)
        {
            var payload = new StepResultPayload
            {
                TestRunId = TestRunId,
                StepId = stepId,
                Status = status,
                DurationMs = durationMs,
                Error = error
            };

            _connection?.Send(MessageTypes.StepResult, payload);
        }

        public void SendTestComplete(string status, int totalDurationMs)
        {
            var payload = new TestCompletePayload
            {
                TestRunId = TestRunId,
                Status = status,
                TotalDurationMs = totalDurationMs
            };

            _connection?.Send(MessageTypes.TestComplete, payload);
        }

        private void Log(string message)
        {
            if (enableLogging)
            {
                Debug.Log($"[TestPlatform] {message}");
            }
        }

        private void LogError(string message)
        {
            Debug.LogError($"[TestPlatform] {message}");
        }
    }
}
