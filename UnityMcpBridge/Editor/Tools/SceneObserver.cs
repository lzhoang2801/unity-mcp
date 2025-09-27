using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Text;
using System.Reflection;
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
            var allRootObjects = new List<GameObject>();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                {
                    allRootObjects.AddRange(scene.GetRootGameObjects());
                }
            }

            GameObject tempObject = new GameObject("TempObject");
            DontDestroyOnLoad(tempObject);
            Scene dontDestroyOnLoadScene = tempObject.scene;
            
            allRootObjects.AddRange(dontDestroyOnLoadScene.GetRootGameObjects().Where(go => go != tempObject));

            Destroy(tempObject);

            var rootElements = new List<SceneElement>();
            var pathBuilder = new StringBuilder();

            foreach (var rootGo in allRootObjects.Distinct())
            {
                if (rootGo != null && rootGo.activeInHierarchy)
                {
                    rootElements.AddRange(ProcessNode(rootGo.transform, pathBuilder));
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
        
        private static List<SceneElement> ProcessNode(Transform nodeTransform, StringBuilder pathBuilder)
        {
            int originalLength = pathBuilder.Length;
            pathBuilder.Append("/").Append(nodeTransform.name);
            string currentPath = pathBuilder.ToString();

            var processedChildren = new List<SceneElement>();
            foreach (Transform childTransform in nodeTransform)
            {
                if (childTransform.gameObject.activeInHierarchy)
                {
                    processedChildren.AddRange(ProcessNode(childTransform, pathBuilder));
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
            element.interactiveDescendantCount = processedChildren.Sum(c => c.interactiveDescendantCount) + (isSelfInteractive ? 1 : 0);

            if (element.interactiveDescendantCount == 1 && !isSelfInteractive)
            {
                SceneElement singleInteractiveDescendant = null;
                foreach (var child in processedChildren)
                {
                    if (child.interactiveDescendantCount == 1)
                    {
                        singleInteractiveDescendant = (child.singleInteractiveDescendant ?? child);
                        break;
                    }
                }

                if (singleInteractiveDescendant != null)
                {
                    element.id = singleInteractiveDescendant.id;
                    element.actionType = singleInteractiveDescendant.actionType;
                    element.isInteractable = singleInteractiveDescendant.isInteractable;
                    
                    foreach (var child in processedChildren)
                    {
                        element.summary.AddRange(child.summary);
                    }
                    element.childElements.Clear();
                    return new List<SceneElement> { element };
                }
            }

            var finalChildren = new List<SceneElement>();
            foreach (var child in processedChildren)
            {
                bool isContextNode = string.IsNullOrEmpty(child.actionType) && child.interactiveDescendantCount == 0 && child.summary.Count > 0;

                if (isContextNode)
                {
                    element.summary.AddRange(child.summary);
                }
                else
                {
                    finalChildren.Add(child);
                }
            }

            element.childElements = finalChildren;

            if (isSelfInteractive || element.summary.Count > 0)
            {
                return new List<SceneElement> { element };
            }

            return finalChildren;
        }

        private static Component FindInteractiveComponent(GameObject go)
        {
            if (go.TryGetComponent<Selectable>(out var selectable)) return selectable;
            else if (go.TryGetComponent<TapableBehaviour>(out var tapableBehaviour)) return tapableBehaviour;
            else if (go.TryGetComponent<ScrollRect>(out var scrollRect)) return scrollRect;
            else if (go.TryGetComponent<IPointerClickHandler>(out var pointerClickHandler)) return (Component)pointerClickHandler;
                    
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
        
            try
            {
                Type componentType = interactiveComponent.GetType();
                var interactableField = componentType.GetField("interactable", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (interactableField != null && interactableField.FieldType == typeof(bool))
                {
                    isInteractable = (bool)interactableField.GetValue(interactiveComponent);
                }
            }
            catch
            {
                if (interactiveComponent is Selectable selectable)
                {
                    isInteractable = selectable.IsInteractable();
                }
                else if (interactiveComponent is Behaviour behaviour)
                {
                    isInteractable = behaviour.enabled;
                }
            }
            
            element.isInteractable = isInteractable;
            element.actionType = interactiveComponent.GetType().Name;
        }
    }
}