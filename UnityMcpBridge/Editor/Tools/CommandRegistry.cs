using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using MCPForUnity.Editor.Tools.MenuItems;

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// Registry for all MCP command handlers (Refactored Version)
    /// </summary>
    public static class CommandRegistry
    {
        // Maps command names (matching those called from Python via ctx.bridge.unity_editor.HandlerName)
        // to the corresponding static HandleCommand method in the appropriate tool class.
        private static readonly Dictionary<string, Func<JObject, Task<object>>> _handlers = new()
        {
            { "HandleManageScript", @params => Task.FromResult(ManageScript.HandleCommand(@params)) },
            { "HandleManageScene", @params => Task.FromResult(ManageScene.HandleCommand(@params)) },
            { "HandleManageEditor", @params => Task.FromResult(ManageEditor.HandleCommand(@params)) },
            { "HandleManageGameObject", @params => Task.FromResult(ManageGameObject.HandleCommand(@params)) },
            { "HandleManageAsset", @params => Task.FromResult(ManageAsset.HandleCommand(@params)) },
            { "HandleReadConsole", @params => Task.FromResult(ReadConsole.HandleCommand(@params)) },
            { "HandleManageMenuItem", @params => Task.FromResult(ManageMenuItem.HandleCommand(@params)) },
            { "HandleManageShader", @params => Task.FromResult(ManageShader.HandleCommand(@params)) },
            { "HandleInputSimulator", InputSimulator.HandleCommand },
            { "HandleSceneObserver", SceneObserver.HandleCommand },
        };

        /// <summary>
        /// Gets a command handler by name.
        /// </summary>
        /// <param name="commandName">Name of the command handler (e.g., "HandleManageAsset").</param>
        /// <returns>The command handler function if found, null otherwise.</returns>
        public static Func<JObject, Task<object>> GetHandler(string commandName)
        {
            // Use case-insensitive comparison for flexibility, although Python side should be consistent
            return _handlers.TryGetValue(commandName, out var handler) ? handler : null;
            // Consider adding logging here if a handler is not found
            /*
            if (_handlers.TryGetValue(commandName, out var handler)) {
                return handler;
            } else {
                UnityEngine.Debug.LogError($\"[CommandRegistry] No handler found for command: {commandName}\");
                return null;
            }
            */
        }
    }
}

