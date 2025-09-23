using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MCPForUnity.Runtime.InputSimulation
{
    /// <summary>
    /// Manages and dispatches simulated UI events for the legacy StandaloneInputModule.
    /// </summary>
    public sealed class LegacyInputController
    {
        public bool Click(GameObject target, Vector2 screenPosition)
        {
            if (EventSystem.current == null) return false;

            var ped = new PointerEventData(EventSystem.current) { position = screenPosition };
            
            var downHandler = ExecuteEvents.GetEventHandler<IPointerDownHandler>(target);
            if (downHandler != null) ExecuteEvents.Execute(downHandler, ped, ExecuteEvents.pointerDownHandler);

            var upHandler = ExecuteEvents.GetEventHandler<IPointerUpHandler>(target);
            if (upHandler != null) ExecuteEvents.Execute(upHandler, ped, ExecuteEvents.pointerUpHandler);
            
            var clickHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
            if (clickHandler != null) ExecuteEvents.Execute(clickHandler, ped, ExecuteEvents.pointerClickHandler);

            return downHandler != null || upHandler != null || clickHandler != null;
        }

        public void Scroll(Vector2 screenPosition, Vector2 scrollDelta)
        {
            if (EventSystem.current == null) return;

            var ped = new PointerEventData(EventSystem.current)
            {
                position = screenPosition,
                scrollDelta = scrollDelta
            };

            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(ped, results);

            if (results.Count > 0)
            {
                var scrollHandler = ExecuteEvents.GetEventHandler<IScrollHandler>(results[0].gameObject);
                if (scrollHandler != null)
                {
                    ExecuteEvents.Execute(scrollHandler, ped, ExecuteEvents.scrollHandler);
                }
            }
        }

        public PointerEventData InitializeDrag(GameObject target, Vector2 screenPosition)
        {
            if (EventSystem.current == null || target == null) return null;

            var ped = new PointerEventData(EventSystem.current)
            {
                position = screenPosition,
                button = PointerEventData.InputButton.Left,
                useDragThreshold = true,
            };

            var dragHandler = ExecuteEvents.GetEventHandler<IDragHandler>(target);
            if (dragHandler == null) return null;

            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(ped, results);
            
            ped.pointerPressRaycast = results.Count > 0 ? results[0] : new RaycastResult();
            ped.pointerCurrentRaycast = ped.pointerPressRaycast;
            ped.pressPosition = screenPosition;
            ped.pointerPress = dragHandler;
            ped.rawPointerPress = target;
            ped.pointerDrag = dragHandler;

            ExecuteEvents.Execute(dragHandler, ped, ExecuteEvents.beginDragHandler);
            return ped;
        }

        public void UpdateDrag(PointerEventData ped, Vector2 screenPosition)
        {
            if (ped == null || ped.pointerDrag == null) return;
            
            ped.position = screenPosition;
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(ped, results);
            ped.pointerCurrentRaycast = results.Count > 0 ? results[0] : new RaycastResult();

            ExecuteEvents.Execute(ped.pointerDrag, ped, ExecuteEvents.dragHandler);
        }

        public void EndDrag(PointerEventData ped, Vector2 screenPosition)
        {
            if (ped == null || ped.pointerDrag == null) return;

            ped.position = screenPosition;

            ExecuteEvents.Execute(ped.pointerDrag, ped, ExecuteEvents.endDragHandler);

            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(ped, results);
            ped.pointerCurrentRaycast = results.Count > 0 ? results[0] : new RaycastResult();

            var dropHandler = results.Count > 0
                ? ExecuteEvents.GetEventHandler<IDropHandler>(results[0].gameObject)
                : null;

            if (dropHandler != null)
            {
                ExecuteEvents.Execute(dropHandler, ped, ExecuteEvents.dropHandler);
            }
        }
    }
}