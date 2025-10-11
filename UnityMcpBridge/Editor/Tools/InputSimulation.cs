using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using MCPForUnity.Editor.Helpers;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Threading.Tasks;

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// Handles Input Simulation actions in Unity Play Mode.
    /// </summary>
    public static class InputSimulation
    {
        public static async Task<object> HandleCommand(JObject commandParams)
        {
            if (!EditorApplication.isPlaying)
            {
                return Response.Error("NotInPlayMode", new { message = "Input simulation requires Play Mode." });
            }

            try
            {
                var action = commandParams["action"]?.ToString();
                if (string.IsNullOrEmpty(action))
                {
                    return Response.Error("MissingAction", new { message = "'action' is required" });
                }

                switch (action.ToLowerInvariant())
                {
                    case "click":
                        return await HandleClick(commandParams);
                    case "drag":
                        return await HandleDrag(commandParams);
                    case "scroll":
                        return await HandleScroll(commandParams);
                    default:
                        return Response.Error("UnknownAction", new { action });
                }
            }
            catch (Exception ex)
            {
                return Response.Error("Exception", new { message = ex.Message, stack = ex.StackTrace });
            }
        }

        private static async Task<object> HandleClick(JObject commandParams)
        {
            var x = commandParams["x"]?.Value<float>();
            var y = commandParams["y"]?.Value<float>();
            var clickCount = commandParams["clickCount"]?.Value<int>() ?? 1;

            if (!x.HasValue || !y.HasValue)
            {
                return Response.Error("MissingCoordinates", new { message = "x and y coordinates are required for click action" });
            }

            var screenPos = new Vector2(x.Value, y.Value);
            await SimulateClick(screenPos, clickCount);

            return Response.Success("Click completed", new
            {
                x = x.Value,
                y = y.Value,
                clickCount = clickCount
            });
        }

        private static async Task<object> HandleDrag(JObject commandParams)
        {
            var sx = commandParams["sx"]?.Value<float>();
            var sy = commandParams["sy"]?.Value<float>();
            var ex = commandParams["ex"]?.Value<float>();
            var ey = commandParams["ey"]?.Value<float>();

            if (!sx.HasValue || !sy.HasValue || !ex.HasValue || !ey.HasValue)
            {
                return Response.Error("MissingCoordinates", new { message = "sx, sy, ex, ey coordinates are required for drag action" });
            }

            var startPos = new Vector2(sx.Value, sy.Value);
            var endPos = new Vector2(ex.Value, ey.Value);
            await SimulateDrag(startPos, endPos);

            return Response.Success("Drag completed", new
            {
                sx = sx.Value,
                sy = sy.Value,
                ex = ex.Value,
                ey = ey.Value
            });
        }

        private static async Task<object> HandleScroll(JObject commandParams)
        {
            var x = commandParams["x"]?.Value<float>();
            var y = commandParams["y"]?.Value<float>();
            var dx = commandParams["dx"]?.Value<float>();
            var dy = commandParams["dy"]?.Value<float>();

            if (!x.HasValue || !y.HasValue || !dx.HasValue || !dy.HasValue)
            {
                return Response.Error("MissingCoordinates", new { message = "x, y, dx, dy are required for scroll action" });
            }

            var scrollPos = new Vector2(x.Value, y.Value);
            var scrollDelta = new Vector2(dx.Value, dy.Value);
            await SimulateScroll(scrollPos, scrollDelta);

            return Response.Success("Scroll completed", new
            {
                x = x.Value,
                y = y.Value,
                dx = dx.Value,
                dy = dy.Value
            });
        }

        private static async Task SimulateClick(Vector2 screenPosition, int clickCount)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;

            for (int i = 0; i < clickCount; i++)
            {
                var pointerData = new PointerEventData(eventSystem)
                {
                    position = screenPosition,
                    button = PointerEventData.InputButton.Left
                };

                ExecuteEvents.ExecuteHierarchy(pointerData.pointerCurrentRaycast.gameObject, pointerData, ExecuteEvents.pointerDownHandler);
                await Task.Delay(50);
                ExecuteEvents.ExecuteHierarchy(pointerData.pointerCurrentRaycast.gameObject, pointerData, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.ExecuteHierarchy(pointerData.pointerCurrentRaycast.gameObject, pointerData, ExecuteEvents.pointerClickHandler);

                if (i < clickCount - 1)
                {
                    await Task.Delay(100);
                }
            }
        }

        private static async Task SimulateDrag(Vector2 startPosition, Vector2 endPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;

            var pointerData = new PointerEventData(eventSystem)
            {
                position = startPosition,
                button = PointerEventData.InputButton.Left
            };

            ExecuteEvents.ExecuteHierarchy(pointerData.pointerCurrentRaycast.gameObject, pointerData, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(pointerData.pointerCurrentRaycast.gameObject, pointerData, ExecuteEvents.beginDragHandler);

            var steps = 10;
            for (int i = 1; i <= steps; i++)
            {
                var t = (float)i / steps;
                var currentPos = Vector2.Lerp(startPosition, endPosition, t);
                pointerData.position = currentPos;
                
                ExecuteEvents.ExecuteHierarchy(pointerData.pointerCurrentRaycast.gameObject, pointerData, ExecuteEvents.dragHandler);
                await Task.Delay(16);
            }

            pointerData.position = endPosition;
            ExecuteEvents.ExecuteHierarchy(pointerData.pointerCurrentRaycast.gameObject, pointerData, ExecuteEvents.endDragHandler);
            ExecuteEvents.ExecuteHierarchy(pointerData.pointerCurrentRaycast.gameObject, pointerData, ExecuteEvents.pointerUpHandler);
        }

        private static async Task SimulateScroll(Vector2 scrollPosition, Vector2 scrollDelta)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;

            var pointerData = new PointerEventData(eventSystem)
            {
                position = scrollPosition,
                scrollDelta = scrollDelta
            };

            ExecuteEvents.ExecuteHierarchy(pointerData.pointerCurrentRaycast.gameObject, pointerData, ExecuteEvents.scrollHandler);
            await Task.Delay(16);
        }
    }
}