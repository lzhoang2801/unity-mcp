using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Runtime.InputSimulation;
using System.Threading.Tasks;

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// Handles Input Simulation actions in Unity Play Mode.
    /// </summary>
    public static class InputSimulation
    {
        public static async Task<object> HandleCommand(JObject @params)
        {
            if (!EditorApplication.isPlaying)
            {
                return Response.Error("NotInPlayMode", new { message = "Input simulation requires Play Mode." });
            }

            string action = @params["action"]?.ToString();
            if (string.IsNullOrEmpty(action))
            {
                return Response.Error("MissingAction", new { message = "'action' is required" });
            }

            try
            {
                switch (action.ToLowerInvariant())
                {
                    case "click":
                    {
                        float? x = @params["x"]?.ToObject<float?>();
                        float? y = @params["y"]?.ToObject<float?>();
                        int instanceId = @params["instanceID"]?.ToObject<int>() ?? 0;
                        int clickCount = @params["clickCount"]?.ToObject<int>() ?? 1;

                        var validationResult = InputSimulationManager.Instance.ValidateActionTarget("click", instanceId, x, y);
                        if (!validationResult.IsValid && validationResult.ErrorCode == "TargetOutOfViewport")
                        {
                            bool broughtIntoView = await InputSimulationManager.Instance.BringTargetIntoView(validationResult.Target);
                            if (broughtIntoView)
                            {
                                await Task.Delay(100);
                                validationResult = InputSimulationManager.Instance.ValidateActionTarget("click", instanceId, null, null);
                            }
                        }

                        if (!validationResult.IsValid)
                        {
                            return Response.Error(validationResult.ErrorCode, new {
                                message = validationResult.Message,
                                x = validationResult.Position.x,
                                y = validationResult.Position.y,
                                target = validationResult.Target?.name
                            });
                        }

                        await InputSimulationManager.Instance.ClickAt(validationResult.Target, validationResult.Position, clickCount);
                        
                        return Response.Success("Click completed", new {
                            x = validationResult.Position.x,
                            y = validationResult.Position.y,
                            target = validationResult.Target?.name,
                            clickCount
                        });
                    }
                    case "drag":
                    {
                        float? sx = @params["sx"]?.ToObject<float?>();
                        float? sy = @params["sy"]?.ToObject<float?>();
                        float? ex = @params["ex"]?.ToObject<float?>();
                        float? ey = @params["ey"]?.ToObject<float?>();
                        int instanceId = @params["instanceID"]?.ToObject<int>() ?? 0;

                        var validationResult = InputSimulationManager.Instance.ValidateActionTarget("drag", instanceId, sx, sy, ex, ey);

                        if (!validationResult.IsValid && validationResult.ErrorCode == "TargetOutOfViewport")
                        {
                            bool broughtIntoView = await InputSimulationManager.Instance.BringTargetIntoView(validationResult.Target);
                            if (broughtIntoView)
                            {
                                await Task.Delay(100);
                                validationResult = InputSimulationManager.Instance.ValidateActionTarget("drag", instanceId, null, null, ex, ey);
                            }
                        }

                        if (!validationResult.IsValid)
                        {
                            return Response.Error(validationResult.ErrorCode, new {
                                message = validationResult.Message,
                                sx = validationResult.Position.x,
                                sy = validationResult.Position.y,
                                target = validationResult.Target?.name
                            });
                        }

                        await InputSimulationManager.Instance.DragTo(validationResult.Target, validationResult.Position, validationResult.EndPosition);

                        return Response.Success("Drag completed", new { 
                            sx = validationResult.Position.x,
                            sy = validationResult.Position.y,
                            ex = validationResult.EndPosition.x,
                            ey = validationResult.EndPosition.y,
                            target = validationResult.Target?.name
                        });
                    }
                    case "scroll":
                    {
                        float? x = @params["x"]?.ToObject<float?>();
                        float? y = @params["y"]?.ToObject<float?>();
                        int instanceId = @params["instanceID"]?.ToObject<int>() ?? 0;
                        float dx = @params["dx"]?.ToObject<float>() ?? 0f;
                        float dy = @params["dy"]?.ToObject<float>() ?? 0f;

                        if (instanceId == 0 && !x.HasValue && !y.HasValue)
                        {
                            var cursorPos = InputSimulationManager.Instance.GetCursorPosition();
                            x = cursorPos.x;
                            y = cursorPos.y;
                        }

                        var validationResult = InputSimulationManager.Instance.ValidateActionTarget("scroll", instanceId, x, y, dx, dy);
                        
                        if (!validationResult.IsValid)
                        {
                            return Response.Error(validationResult.ErrorCode, new {
                                message = validationResult.Message,
                                x = validationResult.Position.x,
                                y = validationResult.Position.y,
                                target = validationResult.Target?.name
                            });
                        }

                        await InputSimulationManager.Instance.Scroll(validationResult.Position, validationResult.ScrollDelta);

                        return Response.Success("Scroll completed", new {
                            x = validationResult.Position.x,
                            y = validationResult.Position.y,
                            dx,
                            dy,
                            target = validationResult.Target?.name
                        });
                    }
                    default:
                        return Response.Error("UnknownAction", new { action });
                }
            }
            catch (Exception ex)
            {
                return Response.Error("Exception", new { message = ex.Message, stack = ex.StackTrace });
            }
        }
    }
}