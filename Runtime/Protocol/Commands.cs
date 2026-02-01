using System;

namespace TestPlatform.SDK
{
    [Serializable]
    public class TestPlan
    {
        public string version;
        public TestPlanMetadata metadata;
        public TestPlanConfig config;
        public TestStep[] steps;
    }

    [Serializable]
    public class TestPlanMetadata
    {
        public string testCaseId;
        public string name;
        public string compiledAt;
    }

    [Serializable]
    public class TestPlanConfig
    {
        public int defaultTimeout = 10000;
        public bool screenshotOnStep;
        public bool screenshotOnFailure = true;
    }

    [Serializable]
    public class TestStep
    {
        public string id;
        public string description;
        public Command command;
        public string onFailure; // "abort", "continue", "retry"
        public int retryCount;
        public int timeout;
    }

    [Serializable]
    public class Command
    {
        public string action; // "tap", "swipe", "wait", "assert", "input_text", "screenshot"
        public ElementSelector selector;

        // Tap
        public int holdDuration;

        // Swipe
        public Position from;
        public Position to;
        public int duration;

        // Wait
        public WaitCondition condition;
        public int timeout;

        // Assert
        public string property;
        public string @operator; // "equals", "contains", "gt", "lt", "exists"
        public string expected;

        // Input
        public string text;
        public bool clearFirst;

        // Screenshot
        public string name;

        // Property accessors for compatibility
        public string Action => action;
        public ElementSelector Selector => selector;
        public int HoldDuration => holdDuration;
        public Position From => from;
        public Position To => to;
        public int Duration => duration;
        public WaitCondition Condition => condition;
        public int Timeout => timeout;
        public string Property => property;
        public string Operator => @operator;
        public string Expected => expected;
        public string Text => text;
        public bool ClearFirst => clearFirst;
        public string Name => name;
    }

    [Serializable]
    public class ElementSelector
    {
        public string strategy; // "name", "tag", "path", "text", "component"
        public string value;
        public int index;

        public string Strategy => strategy;
        public string Value => value;
        public int Index => index;
    }

    [Serializable]
    public class Position
    {
        public float x;
        public float y;
        public bool relative; // If true, X/Y are 0-1 relative to screen

        public float X => x;
        public float Y => y;
        public bool Relative => relative;
    }

    [Serializable]
    public class WaitCondition
    {
        public string type; // "element_visible", "element_gone", "scene_loaded", "delay"
        public ElementSelector selector;
        public string sceneName;
        public int ms;

        public string Type => type;
        public ElementSelector Selector => selector;
        public string SceneName => sceneName;
        public int Ms => ms;
    }
}
