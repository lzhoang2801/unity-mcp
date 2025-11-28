using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using MCPForUnity.Editor.Helpers;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Threading.Tasks;
using MCPForUnity.Runtime.InputSimulator;

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// Handles input simulator actions in Unity Play Mode.
    /// </summary>
    public static class InputSimulator
    {
        private static InputActionSimulator Simulator => InputActionSimulator.Instance;

        public static async Task<object> HandleCommand(JObject commandParams)
        {
            if (!EditorApplication.isPlaying)
            {
                return Response.Error("NotInPlayMode", new { message = "Input simulator requires Play Mode." });
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
                    case "tap":
                        return await HandleTap(commandParams);
                    case "swipe":
                        return await HandleSwipe(commandParams);
                    case "pinchToZoom":
                        return await HandlePinchToZoom(commandParams);
                    case "wait":
                        return await HandleWait(commandParams);
                    default:
                        return Response.Error("UnknownAction", new { action });
                }
            }
            catch (Exception ex)
            {
                return Response.Error("Exception", new { message = ex.Message, stack = ex.StackTrace });
            }
        }

        private static async Task<object> HandleWait(JObject commandParams)
        {
            await Task.Delay(commandParams["time"]?.Value<int>() ?? 5 * 1000);
            return Response.Success("Wait completed");
        }

        private static async Task<object> HandleTap(JObject commandParams)
        {
            var x = commandParams["x"]?.Value<int>();
            var y = commandParams["y"]?.Value<int>();
            var clickCount = commandParams["clickCount"]?.Value<int>() ?? 1;

            if (!x.HasValue || !y.HasValue)
            {
                return Response.Error("MissingCoordinates", new { message = "x and y coordinates are required for tap action" });
            }

            Simulator.PerformAction(new ActionSpaceData { action = "tap", x = x.Value, y = y.Value, clickCount = clickCount });

            return Response.Success("Tap completed", new
            {
                x = x.Value,
                y = y.Value,
                clickCount = clickCount
            });
        }

        private static async Task<object> HandleSwipe(JObject commandParams)
        {
            var x = commandParams["x"]?.Value<int>();
            var y = commandParams["y"]?.Value<int>();
            var x2 = commandParams["x2"]?.Value<int>();
            var y2 = commandParams["y2"]?.Value<int>();

            if (!x.HasValue || !y.HasValue || !x2.HasValue || !y2.HasValue)
            {
                return Response.Error("MissingCoordinates", new { message = "x, y, x2, y2 coordinates are required for swipe action" });
            }

            Simulator.PerformAction(new ActionSpaceData { action = "swipe", x = x.Value, y = y.Value, x2 = x2.Value, y2 = y2.Value });

            return Response.Success("Swipe completed", new
            {
                x = x.Value,
                y = y.Value,
                x2 = x2.Value,
                y2 = y2.Value
            });
        }

        private static async Task<object> HandlePinchToZoom(JObject commandParams)
        {
            var x = commandParams["x"]?.Value<int>();
            var y = commandParams["y"]?.Value<int>();
            var x2 = commandParams["x2"]?.Value<int>();
            var y2 = commandParams["y2"]?.Value<int>();
            var scale = commandParams["scale"]?.Value<float>();

            if (!x.HasValue || !y.HasValue || !x2.HasValue || !y2.HasValue)
            {
                return Response.Error("MissingCoordinates", new { message = "x, y, x2, y2, scale are required for pinch to zoom action" });
            }

            Simulator.PerformAction(new ActionSpaceData { action = "pinchToZoom", x = x.Value, y = y.Value, x2 = x2.Value, y2 = y2.Value, scale = scale.Value });

            return Response.Success("Pinch to zoom completed", new
            {
                x = x.Value,
                y = y.Value,
                x2 = x2.Value,
                y2 = y2.Value,
                scale = scale.Value
            });
        }
    }
}