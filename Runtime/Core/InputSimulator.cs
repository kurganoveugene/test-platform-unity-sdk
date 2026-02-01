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
        private readonly PointerEventData _pointerData;
        private readonly List<RaycastResult> _raycastResults;

        public InputSimulator()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                _pointerData = new PointerEventData(eventSystem);
            }
            _raycastResults = new List<RaycastResult>();
        }

        /// <summary>
        /// Simulate a tap at the given screen position.
        /// </summary>
        public async Task Tap(Vector2 screenPosition)
        {
            Debug.Log($"[TestPlatform] Tap at {screenPosition}");

            var target = GetTargetAtPosition(screenPosition);
            if (target != null)
            {
                SimulatePointerDown(target, screenPosition);
                await Task.Delay(50);
                SimulatePointerUp(target, screenPosition);
                SimulatePointerClick(target, screenPosition);
            }
            else
            {
                Debug.LogWarning($"[TestPlatform] No UI element at position {screenPosition}");
            }
        }

        /// <summary>
        /// Simulate a long press at the given screen position.
        /// </summary>
        public async Task LongPress(Vector2 screenPosition, int durationMs)
        {
            Debug.Log($"[TestPlatform] Long press at {screenPosition} for {durationMs}ms");

            var target = GetTargetAtPosition(screenPosition);
            if (target != null)
            {
                SimulatePointerDown(target, screenPosition);
                await Task.Delay(durationMs);
                SimulatePointerUp(target, screenPosition);
            }
        }

        /// <summary>
        /// Simulate a swipe from one position to another.
        /// </summary>
        public async Task Swipe(Vector2 from, Vector2 to, int durationMs)
        {
            Debug.Log($"[TestPlatform] Swipe from {from} to {to} over {durationMs}ms");

            var target = GetTargetAtPosition(from);
            var steps = Mathf.Max(10, durationMs / 16); // ~60fps
            var stepDelay = durationMs / steps;

            if (target != null)
            {
                SimulatePointerDown(target, from);
                SimulateBeginDrag(target, from);
            }

            for (int i = 1; i <= steps; i++)
            {
                var t = (float)i / steps;
                var current = Vector2.Lerp(from, to, t);

                if (target != null)
                {
                    SimulateDrag(target, current);
                }

                await Task.Delay(stepDelay);
            }

            if (target != null)
            {
                SimulateEndDrag(target, to);
                SimulatePointerUp(target, to);
            }
        }

        private GameObject GetTargetAtPosition(Vector2 screenPosition)
        {
            if (_pointerData == null || EventSystem.current == null)
            {
                return null;
            }

            _pointerData.position = screenPosition;
            _raycastResults.Clear();

            EventSystem.current.RaycastAll(_pointerData, _raycastResults);

            if (_raycastResults.Count > 0)
            {
                return _raycastResults[0].gameObject;
            }

            return null;
        }

        private void SimulatePointerDown(GameObject target, Vector2 position)
        {
            UpdatePointerData(position);
            _pointerData.pressPosition = position;
            _pointerData.pointerPressRaycast = _pointerData.pointerCurrentRaycast;

            var handler = ExecuteEvents.GetEventHandler<IPointerDownHandler>(target);
            if (handler != null)
            {
                _pointerData.pointerPress = handler;
                ExecuteEvents.Execute(handler, _pointerData, ExecuteEvents.pointerDownHandler);
            }
        }

        private void SimulatePointerUp(GameObject target, Vector2 position)
        {
            UpdatePointerData(position);

            var handler = ExecuteEvents.GetEventHandler<IPointerUpHandler>(target);
            if (handler != null)
            {
                ExecuteEvents.Execute(handler, _pointerData, ExecuteEvents.pointerUpHandler);
            }

            _pointerData.pointerPress = null;
        }

        private void SimulatePointerClick(GameObject target, Vector2 position)
        {
            UpdatePointerData(position);

            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
            if (handler != null)
            {
                ExecuteEvents.Execute(handler, _pointerData, ExecuteEvents.pointerClickHandler);
            }

            // Also try to submit if it's a selectable
            var submitHandler = ExecuteEvents.GetEventHandler<ISubmitHandler>(target);
            if (submitHandler != null)
            {
                ExecuteEvents.Execute(submitHandler, _pointerData, ExecuteEvents.submitHandler);
            }
        }

        private void SimulateBeginDrag(GameObject target, Vector2 position)
        {
            UpdatePointerData(position);
            _pointerData.dragging = true;

            var handler = ExecuteEvents.GetEventHandler<IBeginDragHandler>(target);
            if (handler != null)
            {
                _pointerData.pointerDrag = handler;
                ExecuteEvents.Execute(handler, _pointerData, ExecuteEvents.beginDragHandler);
            }
        }

        private void SimulateDrag(GameObject target, Vector2 position)
        {
            var delta = position - _pointerData.position;
            UpdatePointerData(position);
            _pointerData.delta = delta;

            if (_pointerData.pointerDrag != null)
            {
                ExecuteEvents.Execute(_pointerData.pointerDrag, _pointerData, ExecuteEvents.dragHandler);
            }
        }

        private void SimulateEndDrag(GameObject target, Vector2 position)
        {
            UpdatePointerData(position);
            _pointerData.dragging = false;

            if (_pointerData.pointerDrag != null)
            {
                ExecuteEvents.Execute(_pointerData.pointerDrag, _pointerData, ExecuteEvents.endDragHandler);
            }

            // Check for drop
            var dropTarget = GetTargetAtPosition(position);
            if (dropTarget != null)
            {
                var dropHandler = ExecuteEvents.GetEventHandler<IDropHandler>(dropTarget);
                if (dropHandler != null)
                {
                    ExecuteEvents.Execute(dropHandler, _pointerData, ExecuteEvents.dropHandler);
                }
            }

            _pointerData.pointerDrag = null;
        }

        private void UpdatePointerData(Vector2 position)
        {
            if (_pointerData == null) return;

            _pointerData.position = position;
            _raycastResults.Clear();
            EventSystem.current?.RaycastAll(_pointerData, _raycastResults);

            if (_raycastResults.Count > 0)
            {
                _pointerData.pointerCurrentRaycast = _raycastResults[0];
            }
        }
    }
}
