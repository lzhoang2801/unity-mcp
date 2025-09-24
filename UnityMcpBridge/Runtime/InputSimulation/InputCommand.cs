using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MCPForUnity.Runtime.InputSimulation
{
    /// <summary>
    /// Represents a deserialized and structured input simulation command,
    /// encapsulating the logic of parsing parameters.
    /// </summary>
    public class InputCommand
    {
        public string Action { get; private set; }
        public int InstanceID { get; private set; }
        public Vector2? StartPosition { get; private set; }
        public Vector2? EndPosition { get; private set; }
        public Vector2? Delta { get; private set; }
        public int ClickCount { get; private set; } = 1;

        private InputCommand() { }

        public static InputCommand FromJObject(JObject jObject)
        {
            var command = new InputCommand
            {
                Action = jObject["action"]?.ToString() ?? string.Empty,
                InstanceID = jObject["instanceID"]?.ToObject<int>() ?? 0,
                ClickCount = jObject["clickCount"]?.ToObject<int>() ?? 1
            };

            float? x = jObject["x"]?.ToObject<float?>();
            float? y = jObject["y"]?.ToObject<float?>();
            float? sx = jObject["sx"]?.ToObject<float?>();
            float? sy = jObject["sy"]?.ToObject<float?>();
            float? ex = jObject["ex"]?.ToObject<float?>();
            float? ey = jObject["ey"]?.ToObject<float?>();
            float dx = jObject["dx"]?.ToObject<float>() ?? 0f;
            float dy = jObject["dy"]?.ToObject<float>() ?? 0f;

            float? finalStartX = sx ?? x;
            float? finalStartY = sy ?? y;
            if (finalStartX.HasValue || finalStartY.HasValue)
            {
                command.StartPosition = new Vector2(finalStartX.Value, finalStartY.Value);
            }

            if (ex.HasValue || ey.HasValue)
            {
                command.EndPosition = new Vector2(ex.Value, ey.Value);
            }

            if (jObject["dx"] != null || jObject["dy"] != null)
            {
                command.Delta = new Vector2(dx, dy);
            }

            return command;
        }
    }
}
