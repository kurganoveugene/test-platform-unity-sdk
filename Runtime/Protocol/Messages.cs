using System;
using UnityEngine;

namespace TestPlatform.SDK
{
    public static class MessageTypes
    {
        // Server → SDK
        public const string Connected = "connected";
        public const string InitSession = "init_session";
        public const string ExecuteStep = "execute_step";
        public const string ExecuteCommand = "execute_command";
        public const string Abort = "abort";

        // SDK → Server
        public const string SessionReady = "session_ready";
        public const string StepResult = "step_result";
        public const string TestComplete = "test_complete";
        public const string CommandResult = "command_result";
        public const string Heartbeat = "heartbeat";
    }

    [Serializable]
    public class Message
    {
        public string type;
        public string id;
        public long timestamp;

        // Raw JSON string for payload (parsed separately)
        [NonSerialized]
        public string PayloadJson;

        public string Type => type;

        public T GetPayload<T>()
        {
            if (string.IsNullOrEmpty(PayloadJson)) return default;
            return JsonUtility.FromJson<T>(PayloadJson);
        }

        public static Message Parse(string json)
        {
            // First parse the basic message structure
            var message = JsonUtility.FromJson<Message>(json);

            // Extract payload JSON manually
            var payloadStart = json.IndexOf("\"payload\":");
            if (payloadStart >= 0)
            {
                payloadStart += 10; // length of "payload":

                // Skip whitespace
                while (payloadStart < json.Length && char.IsWhiteSpace(json[payloadStart]))
                    payloadStart++;

                if (payloadStart < json.Length)
                {
                    // Find the matching closing brace
                    int depth = 0;
                    int payloadEnd = payloadStart;
                    bool inString = false;

                    for (int i = payloadStart; i < json.Length; i++)
                    {
                        char c = json[i];

                        if (c == '"' && (i == 0 || json[i-1] != '\\'))
                        {
                            inString = !inString;
                        }
                        else if (!inString)
                        {
                            if (c == '{' || c == '[') depth++;
                            else if (c == '}' || c == ']')
                            {
                                depth--;
                                if (depth == 0)
                                {
                                    payloadEnd = i + 1;
                                    break;
                                }
                            }
                        }
                    }

                    message.PayloadJson = json.Substring(payloadStart, payloadEnd - payloadStart);
                }
            }

            return message;
        }
    }

    // === Server → SDK Payloads ===

    [Serializable]
    public class InitSessionPayload
    {
        public string TestRunId;
        public TestPlan TestPlan;
        public SessionConfig Config;
    }

    [Serializable]
    public class SessionConfig
    {
        public int StepTimeout = 10000;
        public bool ScreenshotOnFailure = true;
        public bool VideoRecording = true;
    }

    [Serializable]
    public class ExecuteStepPayload
    {
        public string StepId;
        public Command Command;
    }

    [Serializable]
    public class AbortPayload
    {
        public string Reason;
    }

    [Serializable]
    public class ExecuteCommandPayload
    {
        public Command command;
        public Command Command => command;
    }

    // === SDK → Server Payloads ===

    [Serializable]
    public class SessionReadyPayload
    {
        public string TestRunId;
        public string SdkVersion;
        public string UnityVersion;
        public DeviceInfo DeviceInfo;
    }

    [Serializable]
    public class DeviceInfo
    {
        public string Platform;
        public string OsVersion;
        public string DeviceModel;
        public int ScreenWidth;
        public int ScreenHeight;
    }

    [Serializable]
    public class StepResultPayload
    {
        public string TestRunId;
        public string StepId;
        public string Status; // "passed", "failed", "skipped"
        public int DurationMs;
        public string Error;
        public string Screenshot; // Base64
    }

    [Serializable]
    public class TestCompletePayload
    {
        public string TestRunId;
        public string Status; // "passed", "failed", "error"
        public int TotalDurationMs;
        public string VideoPath;
    }
}
