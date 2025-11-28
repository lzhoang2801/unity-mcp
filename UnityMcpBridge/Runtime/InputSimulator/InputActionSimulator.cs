using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace MCPForUnity.Runtime.InputSimulator
{
    [System.Serializable]
    public class ActionSpaceData
    {
        public string action = "tap";
        public int clickCount;
        public int x;
        public int y;
        public int x2;
        public int y2;
        public int sx;
        public int sy;
        public float scale;
    }

    public sealed class InputActionSimulator : MonoBehaviour
    {
        private static InputActionSimulator _instance;
        public static InputActionSimulator Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("InputActionSimulator");
                    _instance = go.AddComponent<InputActionSimulator>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private Touchscreen _virtualTouch;
        private int _touchId = 0;

        private void OnEnable()
        {
            _virtualTouch = InputSystem.GetDevice<Touchscreen>("InputSimulator_Onn") ?? InputSystem.AddDevice<Touchscreen>("InputSimulator_Onn");
            _virtualTouch.MakeCurrent();
        }

        private void OnDisable()
        {
            InputSystem.RemoveDevice(_virtualTouch);
            _touchId = 0;
        }

        public void PerformAction(ActionSpaceData data)
        {
            FocusGameView();
            _touchId++;
            StartCoroutine(ProcessAction(data));
        }

        private IEnumerator ProcessAction(ActionSpaceData data)
        {
            switch (data.action)
            {
                case "tap":
                    yield return SimulateTap(data);
                    break;
                
                case "swipe":
                    yield return SimulateSwipe(data);
                    break;

                case "pinchToZoom":
                    yield return SimulatePinchToZoom(data);
                    break;

                default:
                    Debug.LogError($"[InputActionSimulator] Unknown action: {data.action}");
                    break;
            }
        }
        
        private IEnumerator SimulateTap(ActionSpaceData data)
        {
            Vector2 screenPos = new Vector2(data.x, data.y);

            for (int tapCount = 1; tapCount <= data.clickCount; tapCount++)
            {
                QueueTouchState(_touchId, screenPos, UnityEngine.InputSystem.TouchPhase.Began);
                yield return null; 

                QueueTouchState(_touchId, screenPos, UnityEngine.InputSystem.TouchPhase.Ended);
                yield return null;
            }
        }

        private IEnumerator SimulateSwipe(ActionSpaceData data)
        {
            Vector2 startPos = new Vector2(data.x, data.y);
            Vector2 destPos = new Vector2(data.x2, data.y2);
            
            QueueTouchState(_touchId, startPos, UnityEngine.InputSystem.TouchPhase.Began);
            yield return null;

            QueueTouchState(_touchId, destPos, UnityEngine.InputSystem.TouchPhase.Moved);
            yield return null;

            QueueTouchState(_touchId, destPos, UnityEngine.InputSystem.TouchPhase.Ended);
            yield return null;
        }

        private IEnumerator SimulatePinchToZoom(ActionSpaceData data)
        {
            Vector2 startPos1 = new Vector2(data.x, data.y);
            Vector2 startPos2 = new Vector2(data.x2, data.y2);

            Vector2 center = (startPos1 + startPos2) * 0.5f;
            
            Vector2 dir1 = startPos1 - center;
            Vector2 dir2 = startPos2 - center;

            Vector2 endPos1 = center + dir1 * data.scale;
            Vector2 endPos2 = center + dir2 * data.scale;

            int touchId1 = _touchId;
            _touchId++; 
            int touchId2 = _touchId;

            QueueTouchState(touchId1, startPos1, UnityEngine.InputSystem.TouchPhase.Began);
            QueueTouchState(touchId2, startPos2, UnityEngine.InputSystem.TouchPhase.Began);
            yield return null;

            QueueTouchState(touchId1, endPos1, UnityEngine.InputSystem.TouchPhase.Moved);
            QueueTouchState(touchId2, endPos2, UnityEngine.InputSystem.TouchPhase.Moved);
            yield return null;

            QueueTouchState(touchId1, endPos1, UnityEngine.InputSystem.TouchPhase.Ended);
            QueueTouchState(touchId2, endPos2, UnityEngine.InputSystem.TouchPhase.Ended);
            yield return null;
        }

        private void QueueTouchState(int touchId, Vector2 position, UnityEngine.InputSystem.TouchPhase phase)
        {        
            var touchState = new TouchState
            {
                touchId = touchId,
                position = position,
                phase = phase
            };
            
            InputSystem.QueueStateEvent(_virtualTouch, touchState);
        }

        private static void FocusGameView()
        {
            var assembly = typeof(EditorWindow).Assembly;
            var type = assembly.GetType("UnityEditor.GameView");
            if (type != null)
            {
                var window = EditorWindow.GetWindow(type);
                if (window != null)
                {
                    window.Focus();
                }
            }
        }
    }
}