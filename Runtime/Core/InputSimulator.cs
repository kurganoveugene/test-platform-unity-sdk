using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TestPlatform.SDK
{
    /// <summary>
    /// Simulates touch and pointer input for UI testing.
    /// </summary>
    public class InputSimulator
    {
        private PointerEventData _pointerData;
        private readonly List<RaycastResult> _raycastResults;

        public InputSimulator()
        {
            _raycastResults = new List<RaycastResult>();
        }

        private PointerEventData GetPointerData()
        {
            if (_pointerData == null || _pointerData.eventSystem != EventSystem.current)
            {
                if (EventSystem.current != null)
                {
                    _pointerData = new PointerEventData(EventSystem.current);
                }
            }
            return _pointerData;
        }

        /// <summary>
        /// Convert screen position from top-left origin (Vision AI) to bottom-left origin (Unity).
        /// </summary>
        private Vector2 ConvertScreenPosition(Vector2 position)
        {
            // Vision AI returns Y from top, Unity expects Y from bottom
            return new Vector2(position.x, Screen.height - position.y);
        }

        /// <summary>
        /// Simulate a tap at the given screen position.
        /// </summary>
        public async Task Tap(Vector2 screenPosition)
        {
            // Convert from top-left origin to bottom-left origin
            var unityPosition = ConvertScreenPosition(screenPosition);
            Debug.Log($"[TestPlatform] Tap at {screenPosition} (Unity: {unityPosition})");

            var target = GetTargetAtPosition(unityPosition);
            if (target != null)
            {
                Debug.Log($"[TestPlatform] Found target: {target.name}");
                SimulatePointerDown(target, unityPosition);
                await Task.Delay(50);
                SimulatePointerUp(target, unityPosition);
                SimulatePointerClick(target, unityPosition);
            }
            else
            {
                Debug.LogWarning($"[TestPlatform] No UI element at position {unityPosition}");
            }
        }

        /// <summary>
        /// Simulate a long press at the given screen position.
        /// </summary>
        public async Task LongPress(Vector2 screenPosition, int durationMs)
        {
            var unityPosition = ConvertScreenPosition(screenPosition);
            Debug.Log($"[TestPlatform] Long press at {screenPosition} (Unity: {unityPosition}) for {durationMs}ms");

            var target = GetTargetAtPosition(unityPosition);
            if (target != null)
            {
                SimulatePointerDown(target, unityPosition);
                await Task.Delay(durationMs);
                SimulatePointerUp(target, unityPosition);
            }
        }

        /// <summary>
        /// Simulate a swipe from one position to another.
        /// </summary>
        public async Task Swipe(Vector2 from, Vector2 to, int durationMs)
        {
            var unityFrom = ConvertScreenPosition(from);
            var unityTo = ConvertScreenPosition(to);
            Debug.Log($"[TestPlatform] Swipe from {from} to {to} (Unity: {unityFrom} to {unityTo}) over {durationMs}ms");

            var target = GetTargetAtPosition(unityFrom);
            var steps = Mathf.Max(10, durationMs / 16); // ~60fps
            var stepDelay = durationMs / steps;

            if (target != null)
            {
                SimulatePointerDown(target, unityFrom);
                SimulateBeginDrag(target, unityFrom);
            }

            for (int i = 1; i <= steps; i++)
            {
                var t = (float)i / steps;
                var current = Vector2.Lerp(unityFrom, unityTo, t);

                if (target != null)
                {
                    SimulateDrag(target, current);
                }

                await Task.Delay(stepDelay);
            }

            if (target != null)
            {
                SimulateEndDrag(target, unityTo);
                SimulatePointerUp(target, unityTo);
            }
        }

        private GameObject GetTargetAtPosition(Vector2 screenPosition)
        {
            var pointerData = GetPointerData();
            if (pointerData == null || EventSystem.current == null)
            {
                Debug.LogWarning("[TestPlatform] No EventSystem found");
                return null;
            }

            pointerData.position = screenPosition;
            _raycastResults.Clear();

            EventSystem.current.RaycastAll(pointerData, _raycastResults);

            Debug.Log($"[TestPlatform] Raycast found {_raycastResults.Count} results");
            for (int i = 0; i < Mathf.Min(_raycastResults.Count, 5); i++)
            {
                Debug.Log($"[TestPlatform]   [{i}] {_raycastResults[i].gameObject.name}");
            }

            if (_raycastResults.Count > 0)
            {
                return _raycastResults[0].gameObject;
            }

            return null;
        }

        private void SimulatePointerDown(GameObject target, Vector2 position)
        {
            var pointerData = GetPointerData();
            if (pointerData == null) return;

            UpdatePointerData(position);
            pointerData.pressPosition = position;
            pointerData.pointerPressRaycast = pointerData.pointerCurrentRaycast;

            var handler = ExecuteEvents.GetEventHandler<IPointerDownHandler>(target);
            if (handler != null)
            {
                Debug.Log($"[TestPlatform] PointerDown on {handler.name}");
                pointerData.pointerPress = handler;
                ExecuteEvents.Execute(handler, pointerData, ExecuteEvents.pointerDownHandler);
            }
        }

        private void SimulatePointerUp(GameObject target, Vector2 position)
        {
            var pointerData = GetPointerData();
            if (pointerData == null) return;

            UpdatePointerData(position);

            var handler = ExecuteEvents.GetEventHandler<IPointerUpHandler>(target);
            if (handler != null)
            {
                Debug.Log($"[TestPlatform] PointerUp on {handler.name}");
                ExecuteEvents.Execute(handler, pointerData, ExecuteEvents.pointerUpHandler);
            }

            pointerData.pointerPress = null;
        }

        private void SimulatePointerClick(GameObject target, Vector2 position)
        {
            var pointerData = GetPointerData();
            if (pointerData == null) return;

            UpdatePointerData(position);

            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
            if (handler != null)
            {
                Debug.Log($"[TestPlatform] PointerClick on {handler.name}");
                ExecuteEvents.Execute(handler, pointerData, ExecuteEvents.pointerClickHandler);
            }

            // Also try to submit if it's a selectable
            var submitHandler = ExecuteEvents.GetEventHandler<ISubmitHandler>(target);
            if (submitHandler != null)
            {
                Debug.Log($"[TestPlatform] Submit on {submitHandler.name}");
                ExecuteEvents.Execute(submitHandler, pointerData, ExecuteEvents.submitHandler);
            }
        }

        private void SimulateBeginDrag(GameObject target, Vector2 position)
        {
            var pointerData = GetPointerData();
            if (pointerData == null) return;

            UpdatePointerData(position);
            pointerData.dragging = true;

            var handler = ExecuteEvents.GetEventHandler<IBeginDragHandler>(target);
            if (handler != null)
            {
                pointerData.pointerDrag = handler;
                ExecuteEvents.Execute(handler, pointerData, ExecuteEvents.beginDragHandler);
            }
        }

        private void SimulateDrag(GameObject target, Vector2 position)
        {
            var pointerData = GetPointerData();
            if (pointerData == null) return;

            var delta = position - pointerData.position;
            UpdatePointerData(position);
            pointerData.delta = delta;

            if (pointerData.pointerDrag != null)
            {
                ExecuteEvents.Execute(pointerData.pointerDrag, pointerData, ExecuteEvents.dragHandler);
            }
        }

        private void SimulateEndDrag(GameObject target, Vector2 position)
        {
            var pointerData = GetPointerData();
            if (pointerData == null) return;

            UpdatePointerData(position);
            pointerData.dragging = false;

            if (pointerData.pointerDrag != null)
            {
                ExecuteEvents.Execute(pointerData.pointerDrag, pointerData, ExecuteEvents.endDragHandler);
            }

            // Check for drop
            var dropTarget = GetTargetAtPosition(position);
            if (dropTarget != null)
            {
                var dropHandler = ExecuteEvents.GetEventHandler<IDropHandler>(dropTarget);
                if (dropHandler != null)
                {
                    ExecuteEvents.Execute(dropHandler, pointerData, ExecuteEvents.dropHandler);
                }
            }

            pointerData.pointerDrag = null;
        }

        private void UpdatePointerData(Vector2 position)
        {
            var pointerData = GetPointerData();
            if (pointerData == null) return;

            pointerData.position = position;
            _raycastResults.Clear();
            EventSystem.current?.RaycastAll(pointerData, _raycastResults);

            if (_raycastResults.Count > 0)
            {
                pointerData.pointerCurrentRaycast = _raycastResults[0];
            }
        }
    }
}
