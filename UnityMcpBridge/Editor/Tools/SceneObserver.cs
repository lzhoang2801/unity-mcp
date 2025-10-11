using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using MCPForUnity.Editor.Helpers;
using System;
using System.IO;

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// Handles scene screenshot capture.
    /// </summary>
    public class SceneObserver : MonoBehaviour
    {
        private static readonly string SCREENSHOT_DIR = Path.Combine(Application.dataPath, "..", "Screenshots");

        public static object HandleCommand(JObject @params)
        {
            if (!EditorApplication.isPlaying)
            {
                return Response.Error("NotInPlayMode", new { message = "Scene observer requires Play Mode." });
            }

            try
            {
                return Response.Success("Scene captured successfully.", CaptureScene());
            }
            catch (Exception ex)
            {
                return Response.Error("Exception", new { message = ex.Message, stack = ex.StackTrace });
            }
        }

        public static object CaptureScene()
        {
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            var data = tex.EncodeToPNG();
            var base64 = Convert.ToBase64String(data);
            var result = SaveScreenshot(data);
            
            UnityEngine.Object.DestroyImmediate(tex);
            
            return new
            {
                screenshot = base64,
                width = Screen.width,
                height = Screen.height,
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                saved = result.success,
                filePath = result.filePath
            };
        }

        private static (bool success, string filePath) SaveScreenshot(byte[] data)
        {
            try
            {
                if (!Directory.Exists(SCREENSHOT_DIR))
                {
                    Directory.CreateDirectory(SCREENSHOT_DIR);
                }
                
                var time = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
                var name = $"{time}.png";
                var path = Path.Combine(SCREENSHOT_DIR, name);
                
                File.WriteAllBytes(path, data);
                
                return (true, path);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to save scene screenshot: {ex.Message}");
                return (false, null);
            }
        }
    }
}