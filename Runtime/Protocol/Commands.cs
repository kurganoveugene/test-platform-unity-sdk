using System;

namespace TestPlatform.SDK
{
    [Serializable]
    public class TestPlan
    {
        public string Version;
        public TestPlanMetadata Metadata;
        public TestPlanConfig Config;
        public TestStep[] Steps;
    }

    [Serializable]
    public class TestPlanMetadata
    {
        public string TestCaseId;
        public string Name;
        public string CompiledAt;
    }

    [Serializable]
    public class TestPlanConfig
    {
        public int DefaultTimeout = 10000;
        public bool ScreenshotOnStep;
        public bool ScreenshotOnFailure = true;
    }

    [Serializable]
    public class TestStep
    {
        public string Id;
        public string Description;
        public Command Command;
        public string OnFailure; // "abort", "continue", "retry"
        public int RetryCount;
        public int Timeout;
    }

    [Serializable]
    public class Command
    {
        public string Action; // "tap", "swipe", "wait", "assert", "input_text", "screenshot"
        public ElementSelector Selector;

        // Tap
        public int HoldDuration;

        // Swipe
        public Position From;
        public Position To;
        public int Duration;

        // Wait
        public WaitCondition Condition;
        public int Timeout;

        // Assert
        public string Property;
        public string Operator; // "equals", "contains", "gt", "lt", "exists"
        public string Expected;

        // Input
        public string Text;
        public bool ClearFirst;

        // Screenshot
        public string Name;
    }

    [Serializable]
    public class ElementSelector
    {
        public string Strategy; // "name", "tag", "path", "text", "component"
        public string Value;
        public int Index;
    }

    [Serializable]
    public class Position
    {
        public float X;
        public float Y;
        public bool Relative; // If true, X/Y are 0-1 relative to screen
    }

    [Serializable]
    public class WaitCondition
    {
        public string Type; // "element_visible", "element_gone", "scene_loaded", "delay"
        public ElementSelector Selector;
        public string SceneName;
        public int Ms;
    }
}
