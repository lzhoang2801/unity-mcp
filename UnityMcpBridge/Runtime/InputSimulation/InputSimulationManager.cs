using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using System.Collections.Generic;
using System.Linq;
using MCPForUnity.Runtime.InputSimulation.CustomActionHandlers;

namespace MCPForUnity.Runtime.InputSimulation
{
    /// <summary>
    /// Central coordinator for AI interaction with the Unity Editor's UI.
    /// </summary>
    public sealed class InputSimulationManager : MonoBehaviour
    {
        private static InputSimulationManager _instance;
        public static InputSimulationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("__InputSimulationManager");
                    _instance = go.AddComponent<InputSimulationManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private VirtualInputController _input;
        private VirtualCursor _cursor;
        private LegacyInputController _legacyInput;
        private bool isInputSystemUIModuleActive;
        private List<ICustomActionHandler> _customActionHandlers;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            isInputSystemUIModuleActive = IsInputSystemUIModuleActive();
        }

        private void OnDestroy()
        {
            _input?.Dispose();
        }

        private void EnsureReady()
        {
            if (_cursor == null)
            {
                _cursor = new VirtualCursor();
                _cursor.EnsureCreated();
            }
            
            if (_customActionHandlers == null)
            {
                InitializeCustomActionHandlers();
            }

            if (isInputSystemUIModuleActive)
            {
                if (_input == null)
                {
                    _input = new VirtualInputController();
                    _input.Initialize();
                }
            }
            else
            {
                if (_legacyInput == null)
                {
                    _legacyInput = new LegacyInputController();
                }
            }
        }

        private void InitializeCustomActionHandlers()
        {
            _customActionHandlers = new List<ICustomActionHandler>
            {
                new ComponentMethodHandler("TapableBehaviour", "OnTapped", "click")
            };
        }

        public Vector2 GetCursorPosition()
        {
            return _cursor != null ? _cursor.RectTransform.anchoredPosition : Vector2.zero;
        }

        public static bool IsPositionInViewport(Vector2 screenPosition)
        {
            return screenPosition.x >= 0 && screenPosition.x <= Screen.width &&
                   screenPosition.y >= 0 && screenPosition.y <= Screen.height;
        }

        public static bool GetScreenPositionForTarget(GameObject target, out Vector2 screenPosition)
        {
            screenPosition = Vector2.zero;
            if (target == null) return false;

            RectTransform rectTransform = target.GetComponent<RectTransform>();

            Vector3 worldPoint;
            if (rectTransform != null)
            {
                worldPoint = rectTransform.TransformPoint(rectTransform.rect.center);
            }
            else
            {
                worldPoint = target.transform.position;
            }

            Camera camera = null;
            var canvas = target.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    camera = canvas.worldCamera;
                }
            }
            else
            {
                camera = Camera.main;
            }
            
            if (camera == null && (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay))
            {
                return false;
            }

            if (camera != null)
            {
                var screenPoint = camera.WorldToScreenPoint(worldPoint);
                if (screenPoint.z < 0)
                {
                    return false;
                }
                screenPosition = new Vector2(screenPoint.x, screenPoint.y);
            }
            else
            {
                screenPosition = RectTransformUtility.WorldToScreenPoint(null, worldPoint);
            }
            
            return true;
        }

        public GameObject GetInteractableObjectAtPosition(Vector2 screenPosition)
        {
            if (EventSystem.current != null)
            {
            var pointerData = new PointerEventData(EventSystem.current) { position = screenPosition };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

                if (results.Count > 0 && results[0].gameObject != null)
                {
                    return results[0].gameObject;
                }
            }

            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return null;
            }
            
            Ray ray = mainCamera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                return hit.collider.gameObject;
            }

            return null;
        }

        public bool VerifyInteractableTargetAtPosition(Vector2 screenPosition, GameObject expected)
        {
            var topObject = GetInteractableObjectAtPosition(screenPosition);

            if (topObject == null)
            {
                return false;
            }

            var current = topObject.transform;
            while (current != null)
            {
                if (current.gameObject == expected)
                {
                    return true;
                }
                current = current.parent;
            }

            return false;
        }

        public List<string> GetBlockingObjectNames(Vector2 screenPosition, GameObject expectedTarget)
        {
            var blockingObjects = new List<string>();
            var uniqueBlockingNames = new HashSet<string>();

            if (EventSystem.current != null)
            {
            var pointerData = new PointerEventData(EventSystem.current) { position = screenPosition };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

                foreach (var result in results)
                {
                    if (result.gameObject == null) continue;
                    
                    if (IsTargetOrChild(result.gameObject, expectedTarget))
                    {
                        return blockingObjects;
                    }
                    
                    if (uniqueBlockingNames.Add(result.gameObject.name))
                    {
                        blockingObjects.Add(result.gameObject.name);
                    }
                }
            }

            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                Ray ray = mainCamera.ScreenPointToRay(screenPosition);
                var hits = Physics.RaycastAll(ray);
                Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                foreach (var hit in hits)
                {
                    if (hit.collider.gameObject == null) continue;
                    
                    if (IsTargetOrChild(hit.collider.gameObject, expectedTarget))
                    {
                        return blockingObjects;
                    }

                    if (uniqueBlockingNames.Add(hit.collider.gameObject.name))
                    {
                        blockingObjects.Add(hit.collider.gameObject.name);
                    }
                }
            }
            
            return blockingObjects;
        }

        private bool IsTargetOrChild(GameObject obj, GameObject target)
        {
            var current = obj.transform;
            while (current != null)
            {
                if (current.gameObject == target)
                {
                    return true;
                }
                current = current.parent;
            }
            return false;
        }

        public class ActionValidationResult
        {
            public bool IsValid;
            public string ErrorCode;
            public string Message;
            public Vector2 Position;
            public Vector2 EndPosition;
            public Vector2 ScrollDelta;
            public GameObject Target;
        }

        public ActionValidationResult ValidateActionTarget(string action, int instanceId, float? x, float? y, float? x2 = null, float? y2 = null)
        {
            Vector2 position;
            GameObject target = null;

            if (instanceId != 0)
            {
                target = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
                if (target == null)
                {
                    return new ActionValidationResult { ErrorCode = "InvalidInstanceID", Message = $"Instance ID {instanceId} is not a valid GameObject." };
                }

                if (x.HasValue && y.HasValue)
                {
                    position = new Vector2(x.Value, y.Value);
                }
                else if (!GetScreenPositionForTarget(target, out position))
                {
                    return new ActionValidationResult { ErrorCode = "CannotDeterminePosition", Message = $"Could not determine screen position for '{target.name}'." };
                }

                if (!IsPositionInViewport(position))
                {
                    return new ActionValidationResult { ErrorCode = "TargetOutOfViewport", Message = $"Target '{target.name}' is outside the screen's viewport.", Position = position };
                }

                if (!VerifyInteractableTargetAtPosition(position, target))
                {
                    var blockers = GetBlockingObjectNames(position, target);
                    var message = blockers.Count > 0
                        ? $"Target is occluded by: {string.Join(", ", blockers)}."
                        : "Target is not the primary interactable object at its screen position.";
                    return new ActionValidationResult { ErrorCode = "TargetNotInteractable", Message = message };
                }
            }
            else
            {
                if (!x.HasValue || !y.HasValue)
                {
                    return new ActionValidationResult { ErrorCode = "MissingCoordinates", Message = "Screen coordinates (x, y) are required when no instanceID is provided." };
                }
                position = new Vector2(x.Value, y.Value);
                target = GetInteractableObjectAtPosition(position);
                if (target == null)
                {
                    return new ActionValidationResult { ErrorCode = "NoInteractableObjectFound", Message = $"No interactable object found at {position}." };
                }
            }
            
            var result = new ActionValidationResult { IsValid = true, Position = position, Target = target };

            switch (action.ToLowerInvariant())
            {
                case "click":
                    break;
                case "drag":
                    if (!x2.HasValue || !y2.HasValue)
                    {
                        return new ActionValidationResult { ErrorCode = "MissingEndCoordinates", Message = "End coordinates (ex, ey) are required for a drag action." };
                    }
                    result.EndPosition = new Vector2(x2.Value, y2.Value);
                    if (!IsPositionInViewport(result.EndPosition))
                    {
                        return new ActionValidationResult { ErrorCode = "EndPositionOutOfViewport", Message = $"Drag end position is outside the screen's viewport." };
                    }
                    break;
                case "scroll":
                    result.ScrollDelta = new Vector2(x2 ?? 0f, y2 ?? 0f);
                    break;
                default:
                    return new ActionValidationResult { ErrorCode = "UnknownAction", Message = $"Action '{action}' is not supported for validation."};
            }

            return result;
        }

        public async Task ClickAt(GameObject target, Vector2 screenPosition, int clickCount = 1)
        {
            EnsureReady();

            await _cursor.MoveTo(this, screenPosition);

            var responsibleHandler = _customActionHandlers.FirstOrDefault(h => h.CanHandle(target, "click"));
            
            for (int i = 0; i < clickCount; i++)
            {
                if (i == 0 || i == clickCount - 1)
                {
                    await _cursor.ClickFlash();
                }

                var wasHandled = responsibleHandler != null && responsibleHandler.Handle(target, "click");

                if (wasHandled) continue;

                if (_input != null && _input.IsInitialized)
                {
                    _input.SetMousePosition(screenPosition);
                    _input.LeftButtonPress();
                    await Task.Delay(100);
                    _input.LeftButtonRelease();
                }
                {
                    _legacyInput.Click(target, screenPosition);
                }
            }
        }

        public async Task DragTo(GameObject target, Vector2 start, Vector2 end)
        {
            EnsureReady();

            var duration = VirtualCursor.CalculateDuration(start, end);
            
            Action<Vector2> dragUpdater;

            if (_input != null && _input.IsInitialized)
            {
                _cursor.SetScreenPosition(start);
                _input.SetMousePosition(start);
                _input.LeftButtonPress();
                dragUpdater = (pos) => _input.SetMousePosition(pos);
            }
            else
            {
                var ped = _legacyInput.InitializeDrag(target, start);
                if (ped == null) return;

                _cursor.SetScreenPosition(start);
                dragUpdater = (pos) => _legacyInput.UpdateDrag(ped, pos);
            }
            
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0, 1, t);
                Vector2 pos = Vector2.LerpUnclamped(start, end, eased);
                dragUpdater(pos);
                _cursor.SetScreenPosition(pos);
                await Task.Yield();
                elapsed += Time.deltaTime;
            }

            _cursor.SetScreenPosition(end);
            
            if (_input != null && _input.IsInitialized)
            {
                _input.SetMousePosition(end);
                _input.LeftButtonRelease();
            }
            else
            {
                var ped = new PointerEventData(EventSystem.current);
                _legacyInput.EndDrag(ped, end);
            }
        }

        public async Task Scroll(Vector2 screenPosition, Vector2 delta)
        {
            EnsureReady();

            await _cursor.MoveTo(this, screenPosition);

            const float scrollSpeed = 1000f;
            float duration = Mathf.Clamp(delta.magnitude / scrollSpeed, 0.1f, 2.0f);
            
            Action<Vector2> scrollAction;

            if (_input != null && _input.IsInitialized)
            {
                _input.SetMousePosition(screenPosition);
                scrollAction = (frameDelta) => _input.Scroll(frameDelta);
            }
            else
            {
                scrollAction = (frameDelta) => _legacyInput.Scroll(screenPosition, frameDelta);
            }
            
            float elapsed = 0f;
            while (elapsed < duration)
            {
                var frameDelta = (delta / duration) * Time.deltaTime;
                scrollAction(frameDelta);
                await Task.Yield();
                elapsed += Time.deltaTime;
            }
        }

        private bool IsInputSystemUIModuleActive()
        {
            if (EventSystem.current == null) return false;
            
            return EventSystem.current.currentInputModule is InputSystemUIInputModule;
        }
    }
}