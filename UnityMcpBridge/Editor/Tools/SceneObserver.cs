using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Text;
using TMPro;
using SceneObserverData;
using UnityEditor;
using Newtonsoft.Json.Linq;
using MCPForUnity.Editor.Helpers;

namespace SceneObserverData
{
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
        public List<SummaryInfo> summary = new List<SummaryInfo>();
        public List<SceneElement> childElements = new List<SceneElement>();

        [NonSerialized] public GameObject gameObject;
        
        [NonSerialized] public int interactiveDescendantCount;
        [NonSerialized] public SceneElement singleInteractiveDescendant;
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
            var rootElements = new List<SceneElement>();
            var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            var pathBuilder = new StringBuilder();

            foreach (var rootGo in rootObjects)
            {
                if (rootGo.activeInHierarchy)
                {
                    rootElements.AddRange(ProcessNodeRecursively(rootGo.transform, pathBuilder));
                }
            }
            
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
        
        private static List<SceneElement> ProcessNodeRecursively(Transform nodeTransform, StringBuilder pathBuilder)
        {
            int originalLength = pathBuilder.Length;
            pathBuilder.Append("/").Append(nodeTransform.name);
            string currentPath = pathBuilder.ToString();

            var processedChildren = new List<SceneElement>();
            foreach (Transform childTransform in nodeTransform)
            {
                if (childTransform.gameObject.activeInHierarchy)
                {
                    processedChildren.AddRange(ProcessNodeRecursively(childTransform, pathBuilder));
                }
            }
            
            pathBuilder.Length = originalLength;

            var go = nodeTransform.gameObject;
            var element = new SceneElement
            {
                id = go.GetInstanceID(),
                name = go.name,
                path = currentPath,
                gameObject = go
            };

            var interactiveComponent = FindInteractiveComponent(go);
            if (interactiveComponent != null)
            {
                PopulateInteractiveProperties(element, interactiveComponent);
            }

            if (go.TryGetComponent<Text>(out var text) && !string.IsNullOrWhiteSpace(text.text))
            {
                element.summary.Add(new SummaryInfo { type = "text", value = text.text.Trim(), sourceNodeName = go.name });
            }
            else if (go.TryGetComponent<TextMeshProUGUI>(out var tmp) && !string.IsNullOrWhiteSpace(tmp.text))
            {
                element.summary.Add(new SummaryInfo { type = "text", value = tmp.text.Trim(), sourceNodeName = go.name });
            }
            else if (go.TryGetComponent<Image>(out var image) && image.sprite != null)
            {
                element.summary.Add(new SummaryInfo { type = "image", value = image.sprite.name, sourceNodeName = go.name });
            }

            bool isSelfInteractive = !string.IsNullOrEmpty(element.actionType);
            if (isSelfInteractive)
            {
                element.interactiveDescendantCount = 1;
                element.singleInteractiveDescendant = element;
            }
            else
            {
                element.interactiveDescendantCount = 0;
                foreach (var child in processedChildren)
                {
                    element.interactiveDescendantCount += child.interactiveDescendantCount;
                    if (child.interactiveDescendantCount == 1)
                    {
                        element.singleInteractiveDescendant = (element.singleInteractiveDescendant == null) ? child.singleInteractiveDescendant : null;
                    }
                    else if (child.interactiveDescendantCount > 1)
                    {
                        element.singleInteractiveDescendant = null;
                    }
                }
            }

            var finalChildren = new List<SceneElement>();
            foreach (var child in processedChildren)
            {
                bool isContextNode = string.IsNullOrEmpty(child.actionType) && 
                                    child.interactiveDescendantCount == 0 && 
                                    child.summary.Count > 0;

                if (isContextNode)
                {
                    element.summary.AddRange(child.summary);
                }
                else
                {
                    finalChildren.Add(child);
                }
            }

            if (!isSelfInteractive && element.interactiveDescendantCount == 1 && element.singleInteractiveDescendant != null)
            {
                var nodeToMerge = element.singleInteractiveDescendant;
                element.id = nodeToMerge.id;
                element.actionType = nodeToMerge.actionType;
                element.isInteractable = nodeToMerge.isInteractable;
                
                var directChildContainer = finalChildren.FirstOrDefault(c => c.singleInteractiveDescendant == nodeToMerge || c == nodeToMerge);
                if (directChildContainer != null)
                {
                    element.summary.AddRange(directChildContainer.summary);
                }

                finalChildren.Clear();
            }

            bool isNowInteractive = !string.IsNullOrEmpty(element.actionType);
            bool hasSummary = element.summary.Count > 0;
            bool isScrollRect = element.actionType == "ScrollRect";

            if (isNowInteractive || hasSummary || isScrollRect)
            {
                element.childElements = finalChildren;
                if (isNowInteractive && !isScrollRect)
                {
                    element.childElements.Clear();
                }
                return new List<SceneElement> { element };
            }
            else
            {
                return finalChildren;
            }
        }

        private static Component FindInteractiveComponent(GameObject go)
        {
            if (go.TryGetComponent<Selectable>(out var selectable)) return selectable;
            if (go.TryGetComponent<IPointerClickHandler>(out var pointerClickHandler)) return (Component)pointerClickHandler;
            if (go.TryGetComponent<ScrollRect>(out var scrollRect)) return scrollRect;
            if (go.TryGetComponent<TapableBehaviour>(out var tapableBehaviour)) return tapableBehaviour;
                    
            return null;
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

        private static void PopulateInteractiveProperties(SceneElement element, Component interactiveComponent)
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
            
            element.isInteractable = isInteractable;
            element.actionType = interactiveComponent.GetType().Name;
        }
    }
}