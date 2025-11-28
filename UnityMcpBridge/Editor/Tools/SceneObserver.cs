using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using System;
using MCPForUnity.Editor.Helpers;
using System.Collections;
using System.Threading.Tasks;

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// Handles scene screenshot capture.
    /// </summary>
    public static class SceneObserver
    {
        private const int SuperSize = 1;
        private static SceneObserverRunner _runner;

        public static async Task<object> HandleCommand(JObject @params)
        {
            if (!EditorApplication.isPlaying)
            {
                return Response.Error("NotInPlayMode", new { message = "Scene observer requires Play Mode." });
            }

            try
            {
                var data = await GetRunner().CaptureSceneAsync();
                return Response.Success("Scene captured successfully.", data);
            }
            catch (Exception ex)
            {
                return Response.Error("Exception", new { message = ex.Message, stack = ex.StackTrace });
            }
        }

        private static SceneObserverRunner GetRunner()
        {
            if (_runner != null)
            {
                return _runner;
            }

            var runnerObject = new GameObject("[SceneObserverRunner]")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            UnityEngine.Object.DontDestroyOnLoad(runnerObject);
            _runner = runnerObject.AddComponent<SceneObserverRunner>();
            return _runner;
        }

        private class SceneObserverRunner : MonoBehaviour
        {
            public Task<object> CaptureSceneAsync()
            {
                var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
                StartCoroutine(CaptureSceneRoutine(tcs));
                return tcs.Task;
            }

            private IEnumerator CaptureSceneRoutine(TaskCompletionSource<object> tcs)
            {
                yield return new WaitForEndOfFrame();

                Texture2D screenshot = null;

                try
                {
                    screenshot = ScreenCapture.CaptureScreenshotAsTexture(SuperSize);
                    var data = screenshot.EncodeToPNG();
                    var base64 = Convert.ToBase64String(data);

                    tcs.SetResult(new
                    {
                        screenshot = base64,
                        width = screenshot.width,
                        height = screenshot.height
                    });
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
                finally
                {
                    if (screenshot != null)
                    {
                        Destroy(screenshot);
                    }
                }
            }
        }
    }
}