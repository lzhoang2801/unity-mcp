using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using System.Linq;
using MCPForUnity.Runtime.InputSimulation.CustomActionHandlers;
using UnityEngine.UI;

namespace MCPForUnity.Runtime.InputSimulation
{
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

		private const float ScrollSpeed = 2000f;

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

        private List<RaycastResult> RaycastUI(Vector2 position)
        {
            if (EventSystem.current == null) return new List<RaycastResult>();
        
            var pointerData = new PointerEventData(EventSystem.current) { position = position };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);
            return results;
        }

        private RaycastHit[] RaycastPhysics(Vector2 position)
        {
            var mainCamera = Camera.main;
            if (mainCamera == null) return Array.Empty<RaycastHit>();
        
            Ray ray = mainCamera.ScreenPointToRay(position);
            var hits = Physics.RaycastAll(ray);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            return hits;
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
            var uiResults = RaycastUI(screenPosition);
            if (uiResults.Count > 0 && uiResults[0].gameObject != null)
            {
                return uiResults[0].gameObject;
            }

            var physicsHits = RaycastPhysics(screenPosition);
            if (physicsHits.Length > 0)
            {
                return physicsHits[0].collider.gameObject;
            }

            return null;
        }

        public List<string> GetBlockingObjectNames(Vector2 screenPosition, GameObject expectedTarget)
        {
            var blockingObjects = new List<string>();
            var uniqueBlockingNames = new HashSet<string>();

            var uiResults = RaycastUI(screenPosition);
            foreach (var result in uiResults)
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

            var physicsHits = RaycastPhysics(screenPosition);
            foreach (var hit in physicsHits)
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

        public async Task<ActionValidationResult> ResolveAndPrepareTarget(string action, int instanceId, float? x, float? y, float? x2 = null, float? y2 = null)
        {
            var (initialTarget, initialPosition, error) = ResolveInitialTargetAndPosition(instanceId, x, y);
            if (error != null) return error;

            var visibilityResult = await EnsureTargetIsVisibleAsync(initialTarget, initialPosition);
            if (visibilityResult.error != null) return visibilityResult.error;
            var finalPosition = visibilityResult.finalPosition;

            var interactabilityResult = ValidateInteractability(initialTarget, finalPosition, instanceId == 0);
            if (interactabilityResult.error != null) return interactabilityResult.error;
            var finalTarget = interactabilityResult.finalTarget;

            var actionParamsResult = ValidateActionSpecificParameters(action, x2, y2);
            if (actionParamsResult.error != null) return actionParamsResult.error;
            
            return new ActionValidationResult
            {
                IsValid = true,
                Target = finalTarget,
                Position = finalPosition,
                EndPosition = actionParamsResult.endPosition,
                ScrollDelta = actionParamsResult.scrollDelta
            };
        }
        
        private (GameObject target, Vector2 position, ActionValidationResult error) ResolveInitialTargetAndPosition(int instanceId, float? x, float? y)
        {
            if (instanceId != 0)
            {
                var target = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
                if (target == null)
                {
                    return (null, Vector2.zero, new ActionValidationResult { ErrorCode = InputSimulationConstants.ErrorCodeInvalidInstanceID, Message = $"Instance ID {instanceId} is not a valid GameObject." });
                }
                
                if (!GetScreenPositionForTarget(target, out var position))
                {
                    return (target, Vector2.zero, new ActionValidationResult { ErrorCode = InputSimulationConstants.ErrorCodeCannotDeterminePosition, Message = $"Could not determine screen position for '{target.name}'.", Target = target });
                }
                return (target, position, null);
            }
            
            if (!x.HasValue || !y.HasValue)
            {
                return (null, Vector2.zero, new ActionValidationResult { ErrorCode = InputSimulationConstants.ErrorCodeMissingCoordinates, Message = "Screen coordinates (x, y) are required when no instanceID is provided." });
            }
            return (null, new Vector2(x.Value, y.Value), null);
        }

        private async Task<(Vector2 finalPosition, ActionValidationResult error)> EnsureTargetIsVisibleAsync(GameObject target, Vector2 initialPosition)
        {
            var finalPosition = initialPosition;
            if (target != null && !IsPositionInViewport(initialPosition))
            {
                bool broughtIntoView = await BringTargetIntoView(target);
                if (broughtIntoView)
                {
                    await Task.Delay(100); // Wait for scroll to settle.
                    if (!GetScreenPositionForTarget(target, out finalPosition))
                    {
                        var error = new ActionValidationResult { ErrorCode = InputSimulationConstants.ErrorCodeCannotDeterminePosition, Message = $"Could not determine screen position for '{target.name}' after scrolling into view.", Target = target };
                        return (initialPosition, error);
                    }
                }
            }
            
            if (!IsPositionInViewport(finalPosition))
            {
                var error = new ActionValidationResult { ErrorCode = InputSimulationConstants.ErrorCodeTargetOutOfViewport, Message = $"Target position ({finalPosition}) is outside the screen's viewport.", Position = finalPosition, Target = target };
                return (finalPosition, error);
            }

            return (finalPosition, null);
        }
        
        private (GameObject finalTarget, ActionValidationResult error) ValidateInteractability(GameObject initialTarget, Vector2 position, bool resolveTargetFromPosition)
        {
            var interactableObject = GetInteractableObjectAtPosition(position);
            var finalTarget = resolveTargetFromPosition ? interactableObject : initialTarget;

            if (finalTarget == null)
            {
                var error = new ActionValidationResult { ErrorCode = InputSimulationConstants.ErrorCodeNoInteractableObjectFound, Message = $"No interactable object found at {position}.", Position = position };
                return (null, error);
            }
            
            var blockers = GetBlockingObjectNames(position, finalTarget);
            if (blockers.Count > 0)
            {
                var message = $"Target '{finalTarget.name}' is occluded by: {string.Join(", ", blockers)}.";
                var error = new ActionValidationResult { ErrorCode = InputSimulationConstants.ErrorCodeTargetNotInteractable, Message = message, Target = finalTarget, Position = position };
                return (finalTarget, error);
            }

            return (finalTarget, null);
        }

        private (Vector2 endPosition, Vector2 scrollDelta, ActionValidationResult error) ValidateActionSpecificParameters(string action, float? x2, float? y2)
        {
            Vector2 endPosition = Vector2.zero;
            Vector2 scrollDelta = Vector2.zero;

            switch (action.ToLowerInvariant())
            {
                case InputSimulationConstants.ActionClick:
                    break;
                case InputSimulationConstants.ActionDrag:
                    if (!x2.HasValue || !y2.HasValue)
                    {
                        var error = new ActionValidationResult { ErrorCode = InputSimulationConstants.ErrorCodeMissingEndCoordinates, Message = "End coordinates (ex, ey) are required for a drag action." };
                        return (endPosition, scrollDelta, error);
                    }
                    endPosition = new Vector2(x2.Value, y2.Value);
                    if (!IsPositionInViewport(endPosition))
                    {
                        var error = new ActionValidationResult { ErrorCode = InputSimulationConstants.ErrorCodeEndPositionOutOfViewport, Message = $"Drag end position {endPosition} is outside the screen's viewport." };
                        return (endPosition, scrollDelta, error);
                    }
                    break;
                case InputSimulationConstants.ActionScroll:
                    scrollDelta = new Vector2(x2 ?? 0f, y2 ?? 0f);
                    break;
                default:
                    var unknownActionError = new ActionValidationResult { ErrorCode = InputSimulationConstants.ErrorCodeUnknownAction, Message = $"Action '{action}' is not supported for validation." };
                    return (endPosition, scrollDelta, unknownActionError);
            }
            return (endPosition, scrollDelta, null);
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

                var wasHandled = responsibleHandler != null && responsibleHandler.Handle(target, "click", null);

                if (wasHandled) continue;

                if (_input != null && _input.IsInitialized)
                {
                    _input.SetMousePosition(screenPosition);
                    _input.LeftButtonPress();
                    _input.LeftButtonRelease();
                }
                else
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

            await AnimationHelper.AnimateOverTime(duration, eased =>
            {
                Vector2 pos = Vector2.LerpUnclamped(start, end, eased);
                dragUpdater(pos);
                _cursor.SetScreenPosition(pos);
            }, useUnscaledTime: true);

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

            float duration = Mathf.Clamp(delta.magnitude / ScrollSpeed, 0.1f, 2.0f);
            
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

        public async Task<bool> BringTargetIntoView(GameObject target)
        {
            if (target == null) return false;

            var scrollRect = target.GetComponentInParent<ScrollRect>();
            if (scrollRect == null)
            {
                return false;
            }

            var targetRect = target.GetComponent<RectTransform>();
            if (targetRect == null) return false;

            Vector2 targetNormalizedPos = CalculateTargetNormalizedPosition(scrollRect, targetRect);
            return await AnimateScroll(scrollRect, targetNormalizedPos);
        }

        private Vector2 CalculateTargetNormalizedPosition(ScrollRect scrollRect, RectTransform targetRect)
        {
            Vector3[] targetWorldCorners = new Vector3[4];
            targetRect.GetWorldCorners(targetWorldCorners);
            Vector3 targetWorldCenter = (targetWorldCorners[0] + targetWorldCorners[2]) * 0.5f;

            Vector3 localTargetPos = scrollRect.content.InverseTransformPoint(targetWorldCenter);
            
            Vector2 contentSize = scrollRect.content.rect.size;
            Vector2 viewportSize = scrollRect.viewport.rect.size;
            Vector2 scrollableSize = contentSize - viewportSize;

            Vector2 targetNormalizedPos = scrollRect.normalizedPosition;

            if (scrollRect.horizontal && scrollableSize.x > 0)
            {
                float targetX = localTargetPos.x + contentSize.x * scrollRect.content.pivot.x;
                float normalizedX = (targetX - viewportSize.x * 0.5f) / scrollableSize.x;
                targetNormalizedPos.x = Mathf.Clamp01(normalizedX);
            }

            if (scrollRect.vertical && scrollableSize.y > 0)
            {
                float targetY = localTargetPos.y + contentSize.y * scrollRect.content.pivot.y;
                float normalizedY = (targetY - viewportSize.y * 0.5f) / scrollableSize.y;
                targetNormalizedPos.y = Mathf.Clamp01(normalizedY);
            }

            return targetNormalizedPos;
        }

        private async Task<bool> AnimateScroll(ScrollRect scrollRect, Vector2 targetNormalizedPosition)
        {
            Vector2 startPosition = scrollRect.normalizedPosition;
            float duration = VirtualCursor.CalculateDuration(startPosition * 1000, targetNormalizedPosition * 1000);
            if (duration <= 0.01f)
            {
                scrollRect.normalizedPosition = targetNormalizedPosition;
                await Task.Yield();
                return true;
            }

            await AnimationHelper.AnimateOverTime(duration, eased =>
            {
                scrollRect.normalizedPosition = Vector2.Lerp(startPosition, targetNormalizedPosition, eased);
            }, useUnscaledTime: true);
            scrollRect.normalizedPosition = targetNormalizedPosition;
            await Task.Yield();
            return true;
        }

        private bool IsInputSystemUIModuleActive()
        {
            if (EventSystem.current == null) return false;
            
            return EventSystem.current.currentInputModule is InputSystemUIInputModule;
        }
    }
}