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
        public string Type;
        public string Id;
        public long Timestamp;
        public object Payload;

        public T GetPayload<T>()
        {
            if (Payload == null) return default;
            var json = JsonUtility.ToJson(Payload);
            return JsonUtility.FromJson<T>(json);
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
        public Command Command;
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
