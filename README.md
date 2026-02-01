# Test Platform Unity SDK

Unity SDK for Test Platform - automated testing framework for Unity games.

## Installation

### Via Unity Package Manager (UPM)

Add to your `manifest.json`:

```json
{
  "dependencies": {
    "com.testplatform.sdk": "https://github.com/kurganoveugene/test-platform-unity-sdk.git"
  }
}
```

Or via Unity Editor:
1. Window → Package Manager
2. Click "+" → "Add package from git URL"
3. Enter: `https://github.com/kurganoveugene/test-platform-unity-sdk.git`

### Manual Installation

Copy the `Runtime` folder into your project's `Packages/com.testplatform.sdk/` directory.

## Setup

1. Create an empty GameObject in your scene
2. Add the `TestSDK` component to it
3. Configure the server URL (default: `ws://localhost:4000/test`)
4. Enable "Auto Connect" if you want automatic connection on start

```csharp
// Or connect programmatically
TestSDK.Instance.Connect("ws://your-server:4000/test");
```

## Requirements

- Unity 2020.3 or later
- TextMeshPro (for text element detection)

## Supported Commands

### Tap
Tap on a UI element or screen position.

```json
{
  "action": "tap",
  "selector": { "strategy": "name", "value": "PlayButton" }
}
```

With hold duration for long press:
```json
{
  "action": "tap",
  "selector": { "strategy": "text", "value": "Start" },
  "holdDuration": 1000
}
```

### Swipe
Swipe from one position to another.

```json
{
  "action": "swipe",
  "from": { "x": 0.5, "y": 0.8, "relative": true },
  "to": { "x": 0.5, "y": 0.2, "relative": true },
  "duration": 300
}
```

### Wait
Wait for conditions.

```json
{
  "action": "wait",
  "condition": {
    "type": "element_visible",
    "selector": { "strategy": "name", "value": "MainMenu" }
  },
  "timeout": 5000
}
```

Wait types:
- `element_visible` - wait for element to be visible
- `element_gone` - wait for element to disappear
- `scene_loaded` - wait for scene to load
- `delay` - fixed delay in milliseconds

### Assert
Verify element properties.

```json
{
  "action": "assert",
  "selector": { "strategy": "name", "value": "ScoreText" },
  "property": "text",
  "operator": "equals",
  "expected": "100"
}
```

Operators: `equals`, `contains`, `gt`, `lt`, `gte`, `lte`, `exists`, `true`, `false`

### Input Text
Enter text into input fields.

```json
{
  "action": "input_text",
  "selector": { "strategy": "name", "value": "UsernameField" },
  "text": "testuser",
  "clearFirst": true
}
```

### Screenshot
Capture a screenshot.

```json
{
  "action": "screenshot",
  "name": "main_menu"
}
```

## Element Selectors

### Strategies

| Strategy | Description | Example |
|----------|-------------|---------|
| `name` | GameObject name | `{ "strategy": "name", "value": "PlayButton" }` |
| `tag` | GameObject tag | `{ "strategy": "tag", "value": "Player" }` |
| `path` | Hierarchy path | `{ "strategy": "path", "value": "Canvas/MainMenu/PlayButton" }` |
| `text` | Text content | `{ "strategy": "text", "value": "Play" }` |
| `component` | Component type | `{ "strategy": "component", "value": "Button" }` |

### Index
When multiple elements match, use `index` to select one (0-based):

```json
{
  "strategy": "tag",
  "value": "Enemy",
  "index": 2
}
```

## Events

```csharp
TestSDK.Instance.OnConnected += () => Debug.Log("Connected");
TestSDK.Instance.OnDisconnected += () => Debug.Log("Disconnected");
TestSDK.Instance.OnError += (error) => Debug.LogError(error);
```

## Build Configuration

For automated builds, configure the SDK via environment variables or script:

```csharp
#if TEST_BUILD
[RuntimeInitializeOnLoadMethod]
static void InitTestSDK()
{
    var go = new GameObject("TestSDK");
    var sdk = go.AddComponent<TestSDK>();
    // Configure as needed
}
#endif
```

## Protocol

The SDK communicates with the Test Platform server via WebSocket using JSON messages.

### Message Types

**From SDK:**
- `session_ready` - SDK is ready to receive commands
- `step_result` - Result of a command execution
- `test_complete` - Test run finished

**From Server:**
- `init_session` - Initialize test session
- `execute_step` - Execute a test command
- `abort` - Abort current test

## License

MIT
