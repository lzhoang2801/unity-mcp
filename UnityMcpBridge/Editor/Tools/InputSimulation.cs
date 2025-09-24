using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Runtime.InputSimulation;
using System.Threading.Tasks;
using MCPForUnity.Runtime.InputSimulation;
using UnityEngine;

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
                return Response.Error(InputSimulationConstants.ErrorCodeNotInPlayMode, new { message = "Input simulation requires Play Mode." });
            }

            var command = InputCommand.FromJObject(commandParams);
            
            if (string.IsNullOrEmpty(command.Action))
            {
                return Response.Error(InputSimulationConstants.ErrorCodeMissingAction, new { message = "'action' is required" });
            }

            try
            {
                var mgr = InputSimulationManager.Instance;
                var validationResult = await mgr.ResolveAndPrepareTarget(command);

                if (!validationResult.IsValid)
                {
                    return CreateErrorResponse(validationResult);
                }
                
                switch (command.Action.ToLowerInvariant())
                {
                    case InputSimulationConstants.ActionClick:
                        await mgr.ClickAt(validationResult.Target, validationResult.Position, command.ClickCount);
                        break;

                    case InputSimulationConstants.ActionDrag:
                        await mgr.DragTo(validationResult.Target, validationResult.Position, validationResult.EndPosition);
                        break;

                    case InputSimulationConstants.ActionScroll:
                        await mgr.Scroll(validationResult.Position, validationResult.ScrollDelta);
                        break;

                    default:
                        return Response.Error(InputSimulationConstants.ErrorCodeUnknownAction, new { command.Action });
                }
                
                return CreateSuccessResponse(command.Action, validationResult);
            }
            catch (Exception ex)
            {
                return Response.Error(InputSimulationConstants.ErrorCodeException, new { message = ex.Message, stack = ex.StackTrace });
            }
        }

        private static object CreateErrorResponse(ActionValidationResult result)
        {
            return Response.Error(result.ErrorCode, new {
                message = result.Message,
                x = result.Position.x,
                y = result.Position.y,
                target = result.Target?.name
            });
        }

        private static object CreateSuccessResponse(string action, ActionValidationResult result)
        {
            var responseData = new JObject
            {
                ["target"] = result.Target?.name
            };

            switch (action.ToLowerInvariant())
            {
                case InputSimulationConstants.ActionClick:
                    responseData["x"] = result.Position.x;
                    responseData["y"] = result.Position.y;
                    responseData["clickCount"] = result.ClickCount;
                    break;
                case InputSimulationConstants.ActionDrag:
                    responseData["sx"] = result.Position.x;
                    responseData["sy"] = result.Position.y;
                    responseData["ex"] = result.EndPosition.x;
                    responseData["ey"] = result.EndPosition.y;
                    break;
                case InputSimulationConstants.ActionScroll:
                    responseData["x"] = result.Position.x;
                    responseData["y"] = result.Position.y;
                    responseData["dx"] = result.ScrollDelta.x;
                    responseData["dy"] = result.ScrollDelta.y;
                    break;
            }
            
            return Response.Success($"{char.ToUpper(action[0]) + action.Substring(1)} completed", responseData);
        }
    }
}