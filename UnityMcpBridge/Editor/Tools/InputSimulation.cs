using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Runtime.InputSimulation;
using System.Threading.Tasks;
using MCPForUnity.Editor.Models;
using UnityEngine;

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
                return Response.Error(InputSimulationConstants.ErrorCodeNotInPlayMode, new { message = "Input simulation requires Play Mode." });
            }

            var command = InputCommand.FromJObject(@params);
            
            if (string.IsNullOrEmpty(command.Action))
            {
                return Response.Error(InputSimulationConstants.ErrorCodeMissingAction, new { message = "'action' is required" });
            }

            try
            {
                var mgr = InputSimulationManager.Instance;
                
                string lowerAction = command.Action.ToLowerInvariant();
                
                var startPos = command.StartPosition;
                if (lowerAction == InputSimulationConstants.ActionScroll && command.InstanceID == 0 && !startPos.HasValue)
                {
                    startPos = mgr.GetCursorPosition();
                }

                float? x2 = lowerAction == InputSimulationConstants.ActionDrag ? command.EndPosition?.x : (lowerAction == InputSimulationConstants.ActionScroll ? command.Delta?.x : null);
                float? y2 = lowerAction == InputSimulationConstants.ActionDrag ? command.EndPosition?.y : (lowerAction == InputSimulationConstants.ActionScroll ? command.Delta?.y : null);

                var validationResult = await mgr.ResolveAndPrepareTarget(
                    command.Action, command.InstanceID, startPos?.x, startPos?.y, x2, y2);

                if (!validationResult.IsValid)
                {
                    return Response.Error(validationResult.ErrorCode, new {
                        message = validationResult.Message,
                        x = validationResult.Position.x,
                        y = validationResult.Position.y,
                        target = validationResult.Target?.name
                    });
                }
                
                switch (lowerAction)
                {
                    case InputSimulationConstants.ActionClick:
                        await mgr.ClickAt(validationResult.Target, validationResult.Position, command.ClickCount);
                        return Response.Success("Click completed", new {
                            x = validationResult.Position.x,
                            y = validationResult.Position.y,
                            target = validationResult.Target?.name,
                            command.ClickCount
                        });

                    case InputSimulationConstants.ActionDrag:
                        await mgr.DragTo(validationResult.Target, validationResult.Position, validationResult.EndPosition);
                        return Response.Success("Drag completed", new { 
                            sx = validationResult.Position.x,
                            sy = validationResult.Position.y,
                            ex = validationResult.EndPosition.x,
                            ey = validationResult.EndPosition.y,
                            target = validationResult.Target?.name
                        });

                    case InputSimulationConstants.ActionScroll:
                        await mgr.Scroll(validationResult.Position, validationResult.ScrollDelta);
                        return Response.Success("Scroll completed", new {
                            x = validationResult.Position.x,
                            y = validationResult.Position.y,
                            dx = command.Delta?.x ?? 0f,
                            dy = command.Delta?.y ?? 0f,
                            target = validationResult.Target?.name
                        });

                    default:
                        return Response.Error(InputSimulationConstants.ErrorCodeUnknownAction, new { command.Action });
                }
            }
            catch (Exception ex)
            {
                return Response.Error(InputSimulationConstants.ErrorCodeException, new { message = ex.Message, stack = ex.StackTrace });
            }
        }
    }
}