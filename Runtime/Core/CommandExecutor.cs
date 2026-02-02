using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace TestPlatform.SDK
{
    /// <summary>
    /// Executes test commands on UI elements.
    /// </summary>
    public class CommandExecutor
    {
        private readonly ElementLocator _locator;
        private readonly InputSimulator _input;

        public CommandExecutor(ElementLocator locator)
        {
            _locator = locator;
            _input = new InputSimulator();
        }

        /// <summary>
        /// Execute a command and return when complete.
        /// </summary>
        public async Task Execute(Command command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            Debug.Log($"[TestPlatform] Executing command: {command.Action}");

            switch (command.Action?.ToLower())
            {
                case "tap":
                    await ExecuteTap(command);
                    break;
                case "swipe":
                    await ExecuteSwipe(command);
                    break;
                case "drag":
                    await ExecuteDrag(command);
                    break;
                case "wait":
                    await ExecuteWait(command);
                    break;
                case "assert":
                    ExecuteAssert(command);
                    break;
                case "input_text":
                    await ExecuteInputText(command);
                    break;
                case "screenshot":
                    await ExecuteScreenshot(command);
                    break;
                case "set_slider":
                    ExecuteSetSlider(command);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown command action: {command.Action}");
            }
        }

        private async Task ExecuteTap(Command command)
        {
            // If selector provided, try to click UI element directly first
            // Note: JsonUtility creates empty objects instead of null, so check for actual values
            if (command.Selector != null && !string.IsNullOrEmpty(command.Selector.Value))
            {
                var element = _locator.Find(command.Selector);
                if (element == null)
                {
                    throw new ElementNotFoundException(command.Selector);
                }

                if (await TryClickElement(element))
                {
                    return;
                }

                // Try parent elements
                var parent = element.transform.parent;
                while (parent != null)
                {
                    if (await TryClickElement(parent.gameObject))
                    {
                        return;
                    }
                    parent = parent.parent;
                }

                throw new InvalidOperationException($"Cannot tap element: {element.name} - no clickable component found");
            }
            else if (command.From != null)
            {
                var position = GetAbsolutePosition(command.From);
                Debug.Log($"[TestPlatform] Tap at position {position}");

                if (command.HoldDuration > 0)
                {
                    await _input.LongPress(position, command.HoldDuration);
                }
                else
                {
                    await _input.Tap(position);
                }
            }
            else
            {
                throw new InvalidOperationException("Tap command requires either a selector or position");
            }
        }

        private async Task<bool> TryClickElement(GameObject element)
        {
            // 1. Try Button.onClick
            var button = element.GetComponent<Button>();
            if (button != null && button.interactable)
            {
                Debug.Log($"[TestPlatform] Clicking Button: {element.name}");
                button.onClick?.Invoke();
                await Task.Delay(50);
                return true;
            }

            // 2. Try Toggle
            var toggle = element.GetComponent<Toggle>();
            if (toggle != null && toggle.interactable)
            {
                Debug.Log($"[TestPlatform] Toggling: {element.name}");
                toggle.isOn = !toggle.isOn;
                await Task.Delay(50);
                return true;
            }

            // 3. Try using ExecuteEvents to simulate pointer click (works for EventTrigger, etc.)
            var eventData = new PointerEventData(EventSystem.current)
            {
                position = element.transform.position
            };

            // Check if element can handle pointer events
            if (ExecuteEvents.CanHandleEvent<IPointerClickHandler>(element) ||
                ExecuteEvents.CanHandleEvent<IPointerDownHandler>(element) ||
                ExecuteEvents.CanHandleEvent<IPointerUpHandler>(element))
            {
                Debug.Log($"[TestPlatform] Simulating pointer events: {element.name}");

                // Simulate full click sequence
                ExecuteEvents.Execute(element, eventData, ExecuteEvents.pointerEnterHandler);
                ExecuteEvents.Execute(element, eventData, ExecuteEvents.pointerDownHandler);
                await Task.Delay(50);
                ExecuteEvents.Execute(element, eventData, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(element, eventData, ExecuteEvents.pointerClickHandler);
                ExecuteEvents.Execute(element, eventData, ExecuteEvents.pointerExitHandler);

                return true;
            }

            // 4. Try EventTrigger
            var eventTrigger = element.GetComponent<EventTrigger>();
            if (eventTrigger != null)
            {
                Debug.Log($"[TestPlatform] Invoking EventTrigger: {element.name}");
                foreach (var entry in eventTrigger.triggers)
                {
                    if (entry.eventID == EventTriggerType.PointerClick)
                    {
                        entry.callback?.Invoke(new BaseEventData(EventSystem.current));
                        await Task.Delay(50);
                        return true;
                    }
                }
            }

            return false;
        }

        private async Task ExecuteSwipe(Command command)
        {
            Vector2 from, to;

            if (command.From != null && command.To != null)
            {
                from = GetAbsolutePosition(command.From);
                to = GetAbsolutePosition(command.To);
            }
            else if (command.Selector != null && command.To != null)
            {
                var screenPos = _locator.GetScreenPosition(command.Selector);
                if (screenPos == null)
                {
                    throw new ElementNotFoundException(command.Selector);
                }
                from = screenPos.Value;
                to = GetAbsolutePosition(command.To);
            }
            else
            {
                throw new InvalidOperationException("Swipe command requires from/to positions or selector with to position");
            }

            var duration = command.Duration > 0 ? command.Duration : 300;
            await _input.Swipe(from, to, duration);
        }

        private async Task ExecuteDrag(Command command)
        {
            if (command.Selector == null)
            {
                throw new InvalidOperationException("Drag command requires a selector for the element to drag");
            }

            var element = _locator.Find(command.Selector);
            if (element == null)
            {
                throw new ElementNotFoundException(command.Selector);
            }

            // Get start position from element using proper screen position calculation
            var startPosNullable = _locator.GetScreenPositionOfGameObject(element);
            if (startPosNullable == null)
            {
                throw new InvalidOperationException($"Could not get screen position of element: {element.name}");
            }
            Vector2 startPos = startPosNullable.Value;

            // Determine end position
            Vector2 endPos;
            if (command.To != null)
            {
                endPos = GetAbsolutePosition(command.To);
            }
            else
            {
                throw new InvalidOperationException("Drag command requires a 'to' position");
            }

            Debug.Log($"[TestPlatform] Dragging {element.name} from {startPos} to {endPos}");

            var eventData = new PointerEventData(EventSystem.current)
            {
                position = startPos,
                pressPosition = startPos,
                button = PointerEventData.InputButton.Left
            };

            // Check if element supports drag events
            bool hasDragHandler = ExecuteEvents.CanHandleEvent<IBeginDragHandler>(element) ||
                                  ExecuteEvents.CanHandleEvent<IDragHandler>(element);

            if (!hasDragHandler)
            {
                // Try to find draggable component in children or parents
                var draggable = element.GetComponentInChildren<IBeginDragHandler>() as MonoBehaviour;
                if (draggable == null)
                {
                    draggable = element.GetComponentInParent<IBeginDragHandler>() as MonoBehaviour;
                }

                if (draggable != null)
                {
                    element = draggable.gameObject;
                    Debug.Log($"[TestPlatform] Found draggable component on: {element.name}");
                }
                else
                {
                    Debug.LogWarning($"[TestPlatform] Element {element.name} doesn't have drag handlers, attempting raw drag simulation");
                }
            }

            // Begin drag
            ExecuteEvents.Execute(element, eventData, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(element, eventData, ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.Execute(element, eventData, ExecuteEvents.beginDragHandler);

            // Simulate drag movement over time
            var duration = command.Duration > 0 ? command.Duration : 300;
            var steps = Mathf.Max(10, duration / 16); // ~60fps
            var stepDuration = duration / steps;

            for (int i = 1; i <= steps; i++)
            {
                var t = (float)i / steps;
                var currentPos = Vector2.Lerp(startPos, endPos, t);

                eventData.position = currentPos;
                eventData.delta = currentPos - (i == 1 ? startPos : Vector2.Lerp(startPos, endPos, (float)(i - 1) / steps));

                ExecuteEvents.Execute(element, eventData, ExecuteEvents.dragHandler);

                await Task.Delay(stepDuration);
            }

            // End drag
            eventData.position = endPos;
            ExecuteEvents.Execute(element, eventData, ExecuteEvents.endDragHandler);
            ExecuteEvents.Execute(element, eventData, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(element, eventData, ExecuteEvents.dropHandler);

            Debug.Log($"[TestPlatform] Drag complete");
        }

        private async Task ExecuteWait(Command command)
        {
            if (command.Condition == null)
            {
                throw new InvalidOperationException("Wait command requires a condition");
            }

            var timeout = command.Timeout > 0 ? command.Timeout : 10000;
            var startTime = DateTime.UtcNow;

            switch (command.Condition.Type?.ToLower())
            {
                case "element_visible":
                    await WaitForCondition(
                        () => _locator.IsVisible(command.Condition.Selector),
                        timeout,
                        $"Element not visible: {command.Condition.Selector?.Value}"
                    );
                    break;

                case "element_gone":
                    await WaitForCondition(
                        () => !_locator.Exists(command.Condition.Selector),
                        timeout,
                        $"Element still exists: {command.Condition.Selector?.Value}"
                    );
                    break;

                case "scene_loaded":
                    await WaitForCondition(
                        () => SceneManager.GetActiveScene().name == command.Condition.SceneName,
                        timeout,
                        $"Scene not loaded: {command.Condition.SceneName}"
                    );
                    break;

                case "delay":
                    var ms = command.Condition.Ms > 0 ? command.Condition.Ms : 1000;
                    await Task.Delay(ms);
                    break;

                default:
                    throw new InvalidOperationException($"Unknown wait condition: {command.Condition.Type}");
            }
        }

        private void ExecuteAssert(Command command)
        {
            if (command.Selector == null)
            {
                throw new InvalidOperationException("Assert command requires a selector");
            }

            var property = command.Property ?? "exists";
            var op = command.Operator ?? "equals";
            var expected = command.Expected;

            string actual;

            if (property.ToLower() == "exists")
            {
                actual = _locator.Exists(command.Selector).ToString();
                expected = expected ?? "True";
            }
            else
            {
                actual = _locator.GetProperty(command.Selector, property);
                if (actual == null)
                {
                    throw new AssertionFailedException($"Property '{property}' not found on element");
                }
            }

            bool passed = EvaluateAssertion(actual, op, expected);

            if (!passed)
            {
                throw new AssertionFailedException(
                    $"Assertion failed: {property} {op} {expected} (actual: {actual})"
                );
            }

            Debug.Log($"[TestPlatform] Assertion passed: {property} {op} {expected}");
        }

        private async Task ExecuteInputText(Command command)
        {
            if (command.Selector == null)
            {
                throw new InvalidOperationException("InputText command requires a selector");
            }

            var element = _locator.Find(command.Selector);
            if (element == null)
            {
                throw new ElementNotFoundException(command.Selector);
            }

            // Try TMP InputField
            var tmpInput = element.GetComponent<TMP_InputField>();
            if (tmpInput != null)
            {
                if (command.ClearFirst)
                {
                    tmpInput.text = "";
                }
                tmpInput.text += command.Text ?? "";
                tmpInput.onValueChanged?.Invoke(tmpInput.text);
                return;
            }

            // Try legacy InputField
            var legacyInput = element.GetComponent<InputField>();
            if (legacyInput != null)
            {
                if (command.ClearFirst)
                {
                    legacyInput.text = "";
                }
                legacyInput.text += command.Text ?? "";
                legacyInput.onValueChanged?.Invoke(legacyInput.text);
                return;
            }

            throw new InvalidOperationException("Element is not an input field");
        }

        private async Task ExecuteScreenshot(Command command)
        {
            var name = command.Name ?? $"screenshot_{DateTime.UtcNow:yyyyMMdd_HHmmss}";

            // Wait for end of frame to capture
            await Task.Yield();

            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            texture.Apply();

            var bytes = texture.EncodeToPNG();
            UnityEngine.Object.Destroy(texture);

            // Store screenshot for sending back to server
            ScreenshotCapture.LastScreenshot = bytes;
            ScreenshotCapture.LastScreenshotName = name;

            Debug.Log($"[TestPlatform] Screenshot captured: {name}");
        }

        private void ExecuteSetSlider(Command command)
        {
            if (command.Selector == null)
            {
                throw new InvalidOperationException("SetSlider command requires a selector");
            }

            var element = _locator.Find(command.Selector);
            if (element == null)
            {
                throw new ElementNotFoundException(command.Selector);
            }

            // Try Unity UI Slider
            var slider = element.GetComponent<Slider>();
            if (slider == null)
            {
                // Try to find Slider in parent (for Handle Slide Area, Fill Area, etc.)
                slider = element.GetComponentInParent<Slider>();
            }

            if (slider != null)
            {
                var value = command.SliderValue;
                // Clamp value between min and max
                value = Mathf.Clamp(value, slider.minValue, slider.maxValue);

                Debug.Log($"[TestPlatform] Setting slider {element.name} to {value} (range: {slider.minValue}-{slider.maxValue})");

                slider.value = value;
                slider.onValueChanged?.Invoke(value);
                return;
            }

            throw new InvalidOperationException($"Element {element.name} is not a Slider");
        }

        private Vector2 GetAbsolutePosition(Position pos)
        {
            if (pos.Relative)
            {
                return new Vector2(pos.X * Screen.width, pos.Y * Screen.height);
            }
            return new Vector2(pos.X, pos.Y);
        }

        private async Task WaitForCondition(Func<bool> condition, int timeoutMs, string errorMessage)
        {
            var startTime = DateTime.UtcNow;

            while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeoutMs)
            {
                if (condition())
                {
                    return;
                }
                await Task.Delay(100);
            }

            throw new TimeoutException(errorMessage);
        }

        private bool EvaluateAssertion(string actual, string op, string expected)
        {
            switch (op.ToLower())
            {
                case "equals":
                case "eq":
                    return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

                case "not_equals":
                case "ne":
                    return !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

                case "contains":
                    return actual?.Contains(expected) ?? false;

                case "not_contains":
                    return !(actual?.Contains(expected) ?? true);

                case "starts_with":
                    return actual?.StartsWith(expected) ?? false;

                case "ends_with":
                    return actual?.EndsWith(expected) ?? false;

                case "gt":
                    if (float.TryParse(actual, out var aGt) && float.TryParse(expected, out var eGt))
                    {
                        return aGt > eGt;
                    }
                    return false;

                case "gte":
                    if (float.TryParse(actual, out var aGte) && float.TryParse(expected, out var eGte))
                    {
                        return aGte >= eGte;
                    }
                    return false;

                case "lt":
                    if (float.TryParse(actual, out var aLt) && float.TryParse(expected, out var eLt))
                    {
                        return aLt < eLt;
                    }
                    return false;

                case "lte":
                    if (float.TryParse(actual, out var aLte) && float.TryParse(expected, out var eLte))
                    {
                        return aLte <= eLte;
                    }
                    return false;

                case "exists":
                    return actual != null && bool.TryParse(actual, out var exists) && exists;

                case "true":
                    return bool.TryParse(actual, out var isTrue) && isTrue;

                case "false":
                    return bool.TryParse(actual, out var isFalse) && !isFalse;

                default:
                    throw new InvalidOperationException($"Unknown assertion operator: {op}");
            }
        }
    }

    /// <summary>
    /// Static holder for screenshot data to send back to server.
    /// </summary>
    public static class ScreenshotCapture
    {
        public static byte[] LastScreenshot { get; set; }
        public static string LastScreenshotName { get; set; }

        public static void Clear()
        {
            LastScreenshot = null;
            LastScreenshotName = null;
        }
    }

    /// <summary>
    /// Exception thrown when an element cannot be found.
    /// </summary>
    public class ElementNotFoundException : Exception
    {
        public ElementSelector Selector { get; }

        public ElementNotFoundException(ElementSelector selector)
            : base($"Element not found: {selector?.Strategy}='{selector?.Value}'")
        {
            Selector = selector;
        }
    }

    /// <summary>
    /// Exception thrown when an assertion fails.
    /// </summary>
    public class AssertionFailedException : Exception
    {
        public AssertionFailedException(string message) : base(message) { }
    }
}
