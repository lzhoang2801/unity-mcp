using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Reflection;
using TMPro;
using SceneObserverData;
using UnityEditor;
using Newtonsoft.Json.Linq;
using MCPForUnity.Editor.Helpers;

namespace SceneObserverData
{
    [Serializable]
    public class BlockerInfo
    {
        public int id;
        public string name;
    }

    [Serializable]
    public class SummaryInfo
    {
        public string type;
        public string value;
        public string sourceNodeName;
    }

    [Serializable]
    public class SceneElement
    {
        public int id;
        public string name;
        public string path;
        public string actionType;
        public bool isInteractable;
        public bool isBlocked;
        public bool isParentBlocked;
        public List<BlockerInfo> blockedBy = new List<BlockerInfo>();
        public List<SummaryInfo> summary = new List<SummaryInfo>();
        public List<SceneElement> childElements = new List<SceneElement>();

        [NonSerialized] public GameObject gameObject;
    }

    [Serializable]
    public class RenderGroup
    {
        public int renderOrder;
        public List<SceneElement> elements = new List<SceneElement>();
    }

    [Serializable]
    public class SceneLayer
    {
        public int layerId;
        public List<RenderGroup> renderGroups = new List<RenderGroup>();
    }

    [Serializable]
    public class SceneState
    {
        public List<SceneLayer> sceneLayers = new List<SceneLayer>();
    }
}

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// Handles the scene state capture.
    /// </summary>
    public class SceneObserver : MonoBehaviour
    {
        private List<GraphicRaycaster> raycasters = new List<GraphicRaycaster>();
        private PointerEventData pointerEventData;
        private EventSystem eventSystem;

        private class BlockingRect
        {
            public Rect rect;
            public GameObject go;
        }
        
        private readonly List<BlockingRect> _topBlockingRects = new List<BlockingRect>();
        private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();
        private Dictionary<GameObject, SceneElement> _elementMap;
        
        private int _topBlockingOrder = int.MinValue;
        
        void Awake()
        {
            eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();
            }
            pointerEventData = new PointerEventData(eventSystem);
        }
        
        public static object HandleCommand(JObject @params)
        {
            if (!EditorApplication.isPlaying)
            {
                return Response.Error("NotInPlayMode", new { message = "Scene observer requires Play Mode." });
            }

            try
            {
                return Response.Success("Scene state retrieved successfully.", GetSceneState());
            }
            catch (Exception ex)
            {
                return Response.Error("Exception", new { message = ex.Message, stack = ex.StackTrace });
            }
        }

        public static SceneState GetSceneState()
        {
            var elementMap = new Dictionary<GameObject, SceneElement>();
            var raycasters = new List<GraphicRaycaster>();
            var topBlockingRects = new List<BlockingRect>();
            var raycastResults = new List<RaycastResult>();
            var topBlockingOrder = int.MinValue;
            
            var rootElements = new List<SceneElement>();
            var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var rootGo in rootObjects)
            {
                if (rootGo.activeInHierarchy)
                {
                    rootElements.Add(BuildFullTreeRecursively(rootGo.transform, null, elementMap, raycasters));
                }
            }

            raycasters = raycasters.OrderByDescending(r => r.GetComponent<Canvas>().sortingOrder).ToList();
            BuildTopBlockingRects(raycasters, topBlockingRects, ref topBlockingOrder);

            foreach (var rootElement in rootElements)
            {
                ProcessNodeHierarchy(rootElement, null, "", elementMap, raycasters, topBlockingRects, raycastResults);
            }
            
            rootElements = PruneAndFlattenHierarchy(rootElements);
            
            var currentState = new SceneState();
            var nodesGroupedByLayer = rootElements
                .GroupBy(n => n.gameObject.layer)
                .OrderBy(g => g.Key);

            foreach (var layerGroup in nodesGroupedByLayer)
            {
                var sceneLayer = new SceneLayer { layerId = layerGroup.Key };

                var renderGroups = layerGroup
                    .GroupBy(n => GetSortOrder(n.gameObject.transform))
                    .OrderBy(g => g.Key)
                    .Select(renderOrderGroup => new RenderGroup
                    {
                        renderOrder = renderOrderGroup.Key,
                        elements = renderOrderGroup.ToList()
                    })
                    .ToList();
                
                sceneLayer.renderGroups = renderGroups;
                currentState.sceneLayers.Add(sceneLayer);
            }

            return currentState;
        }
        
        private static SceneElement BuildFullTreeRecursively(Transform nodeTransform, SceneElement parentElement, Dictionary<GameObject, SceneElement> elementMap, List<GraphicRaycaster> raycasters)
        {
            var go = nodeTransform.gameObject;
            var element = new SceneElement
            {
                name = go.name,
                gameObject = go,
            };
            elementMap[go] = element;

            if (parentElement != null)
            {
                parentElement.childElements.Add(element);
            }

            if (go.TryGetComponent<GraphicRaycaster>(out var raycaster))
            {
                raycasters.Add(raycaster);
            }
            
            var interactiveComponent = FindInteractiveComponent(go);
        if (interactiveComponent != null)
        {
            bool isInteractable = true;
            if (interactiveComponent is Selectable selectable)
            {
                isInteractable = selectable.IsInteractable();
            }
            else if (interactiveComponent is Behaviour behaviour)
            {
                isInteractable = behaviour.enabled;
            }
            
            element.id = interactiveComponent.GetInstanceID();
            element.isInteractable = isInteractable;
            element.actionType = interactiveComponent.GetType().Name;
        }
            
            foreach (Transform childTransform in nodeTransform)
            {
                if (childTransform.gameObject.activeInHierarchy)
                {
                    BuildFullTreeRecursively(childTransform, element, elementMap, raycasters);
                }
            }
            return element;
        }

        private static Component FindInteractiveComponent(GameObject go)
        {
            if (go.TryGetComponent<Selectable>(out var selectable)) return selectable;
            if (go.TryGetComponent<IPointerClickHandler>(out var pointerClickHandler)) return (Component)pointerClickHandler;
            if (go.TryGetComponent<ScrollRect>(out var scrollRect)) return scrollRect;
            if (go.TryGetComponent<TapableBehaviour>(out var tapableBehaviour)) return tapableBehaviour;
                    
            return null;
        }
        
        private static List<SceneElement> PruneAndFlattenHierarchy(List<SceneElement> elements)
        {
            var resultElements = new List<SceneElement>();
            foreach (var element in elements)
            {
                element.childElements = PruneAndFlattenHierarchy(element.childElements);

                bool isInteractive = !string.IsNullOrEmpty(element.actionType);
                bool isScrollRect = element.actionType == "ScrollRect";

                if (isInteractive && !isScrollRect)
                {
                    element.childElements.Clear();
                    resultElements.Add(element);
                }
                else
                {
                    bool hasSummary = element.summary.Any();
                    bool isBlockSource = element.isBlocked;

                    if (hasSummary || isBlockSource || isScrollRect)
                    {
                        resultElements.Add(element);
                    }
                    else
                    {
                        resultElements.AddRange(element.childElements);
                    }
                }
            }
            return resultElements;
        }

        private static void ProcessNodeHierarchy(SceneElement element, SceneElement parentElement, string currentPath, Dictionary<GameObject, SceneElement> elementMap, List<GraphicRaycaster> raycasters, List<BlockingRect> topBlockingRects, List<RaycastResult> raycastResults)
        {
            element.path = currentPath + "/" + element.name;

            SummarizeChildren(element.gameObject, element, elementMap);

            CheckIfBlocked(element, element.gameObject, raycasters, topBlockingRects, raycastResults);
            if (parentElement != null && (parentElement.isBlocked || parentElement.isParentBlocked))
            {
                element.isParentBlocked = true;
                element.isBlocked = false;
                element.blockedBy.Clear();
            }

            foreach (var child in element.childElements)
            {
                ProcessNodeHierarchy(child, element, element.path, elementMap, raycasters, topBlockingRects, raycastResults);
            }
        }

        private static void SummarizeChildren(GameObject parentGo, SceneElement parentElement, Dictionary<GameObject, SceneElement> elementMap)
        {
            bool isParentInteractive = !string.IsNullOrEmpty(parentElement.actionType);

            foreach (Transform childTransform in parentGo.transform)
            {
                var childGo = childTransform.gameObject;
                if (!childGo.activeInHierarchy) continue;

                if (elementMap.TryGetValue(childGo, out var childElement) && !string.IsNullOrEmpty(childElement.actionType))
                {
                    continue;
                }

                if (childTransform.TryGetComponent<Text>(out var text) && !string.IsNullOrWhiteSpace(text.text))
                {
                    parentElement.summary.Add(new SummaryInfo { type = "text", value = text.text.Trim(), sourceNodeName = childGo.name });
                }
                else if (childTransform.TryGetComponent<TextMeshProUGUI>(out var tmp) && !string.IsNullOrWhiteSpace(tmp.text))
                {
                    parentElement.summary.Add(new SummaryInfo { type = "text", value = tmp.text.Trim(), sourceNodeName = childGo.name });
                }
                else if (childTransform.TryGetComponent<Image>(out var image) && image.sprite != null)
                {
                    parentElement.summary.Add(new SummaryInfo { type = "image", value = image.sprite.name, sourceNodeName = childGo.name });
                }

                if (isParentInteractive && parentGo.GetComponent<ScrollRect>() == null)
                {
                    SummarizeChildren(childGo, parentElement, elementMap);
                }
            }
        }
        
        private static void BuildTopBlockingRects(List<GraphicRaycaster> raycasters, List<BlockingRect> topBlockingRects, ref int topBlockingOrder)
        {
            topBlockingRects.Clear();
            topBlockingOrder = int.MinValue;

            var topCanvas = raycasters.Select(r => r.GetComponent<Canvas>()).FirstOrDefault();
            if (topCanvas == null) return;

            var blockingGraphics = topCanvas.GetComponentsInChildren<MaskableGraphic>(true)
                .Where(g => g.raycastTarget && g.isActiveAndEnabled);

            bool addedAny = false;
            Camera cam = topCanvas.worldCamera;

            foreach (var graphic in blockingGraphics)
            {
                var rt = graphic.rectTransform;
                Vector3[] wc = new Vector3[4];
                rt.GetWorldCorners(wc);
                
                var sp0 = RectTransformUtility.WorldToScreenPoint(cam, wc[0]);
                var sp2 = RectTransformUtility.WorldToScreenPoint(cam, wc[2]);
                var rect = new Rect(sp0.x, sp0.y, sp2.x - sp0.x, sp2.y - sp0.y);

                if (rect.width > 0 && rect.height > 0)
                {
                    topBlockingRects.Add(new BlockingRect { rect = rect, go = topCanvas.gameObject });
                    addedAny = true;
                }
            }

            if (addedAny)
            {
                topBlockingOrder = topCanvas.sortingOrder;
            }
        }

        private static void CheckIfBlocked(SceneElement element, GameObject go, List<GraphicRaycaster> raycasters, List<BlockingRect> topBlockingRects, List<RaycastResult> raycastResults)
        {
            Vector3 centerWorld;
            Camera cameraForRaycast;

            var rectTransform = go.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                Vector3[] corners = new Vector3[4];
                rectTransform.GetWorldCorners(corners);
                centerWorld = (corners[0] + corners[2]) / 2;
                var canvas = go.GetComponentInParent<Canvas>();
                cameraForRaycast = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            }
            else
            {
                cameraForRaycast = Camera.main;
                var rend = go.GetComponent<Renderer>();
                if (rend != null)
                {
                    centerWorld = rend.bounds.center;
                }
                else
                {
                    var col = go.GetComponent<Collider>();
                    centerWorld = col != null ? col.bounds.center : go.transform.position;
                }
            }

            if (cameraForRaycast == null && go.GetComponentInParent<Canvas>()?.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                element.isBlocked = false;
                return;
            }
            Vector2 screenPoint = cameraForRaycast != null ? cameraForRaycast.WorldToScreenPoint(centerWorld) : new Vector2(centerWorld.x, centerWorld.y);

            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                element.isBlocked = false;
                return;
            }
            
            var pointerEventData = new PointerEventData(eventSystem);
            pointerEventData.position = screenPoint;
            raycastResults.Clear();
            foreach (var raycaster in raycasters)
            {
                raycaster.Raycast(pointerEventData, raycastResults);
                if (raycastResults.Count > 0)
                {
                    var topHitUI = raycastResults[0].gameObject;
                    if (topHitUI != go && !topHitUI.transform.IsChildOf(go.transform) && !go.transform.IsChildOf(topHitUI.transform))
                    {
                        element.isBlocked = true;
                        var blockerCanvas = topHitUI.GetComponentInParent<Canvas>();
                        var blockerGo = blockerCanvas != null ? blockerCanvas.gameObject : topHitUI;
                        element.blockedBy.Add(new BlockerInfo { id = blockerGo.GetInstanceID(), name = blockerGo.name });
                        return;
                    }
                    break;
                }
            }

            if (rectTransform == null && cameraForRaycast != null && !string.IsNullOrEmpty(element.actionType))
            {
                Ray ray = cameraForRaycast.ScreenPointToRay(screenPoint);
                if (Physics.Raycast(ray, out RaycastHit hitInfo, cameraForRaycast.farClipPlane))
                {
                    GameObject hitObject = hitInfo.collider.gameObject;
                    if (hitObject != go && !hitObject.transform.IsChildOf(go.transform) && !go.transform.IsChildOf(hitObject.transform))
                    {
                        element.isBlocked = true;
                        element.blockedBy.Add(new BlockerInfo { id = hitObject.GetInstanceID(), name = hitObject.name });
                        return;
                    }
                }
            }
            
            element.isBlocked = false;
            element.blockedBy.Clear();
        }
        
        private static int GetSortOrder(Transform target)
        {
            var canvas = target.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                return canvas.sortingOrder;
            }

            var renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                return renderer.sortingOrder;
            }

            return 0;
        }

        private static GameObject FindLogicalRoot(GameObject startObject)
        {
            Transform current = startObject.transform;
            while (current.parent != null && current.parent.name.EndsWith("Button", StringComparison.OrdinalIgnoreCase))
            {
                current = current.parent;
            }
            return current.gameObject;
        }
    }
}