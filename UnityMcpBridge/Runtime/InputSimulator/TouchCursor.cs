using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace MCPForUnity.Runtime.InputSimulator
{
    /// <summary>
    /// Visualizes active touches using Unity's Input System.
    /// </summary>
    public sealed class TouchCursor : MonoBehaviour
    {
        private static TouchCursor _instance;
        private Canvas _canvas;
        private readonly Dictionary<int, RectTransform> _activeCursors = new Dictionary<int, RectTransform>();
        private Sprite _cursorSprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (_instance != null) return;
            
            var existing = FindObjectOfType<TouchCursor>();
            if (existing != null)
            {
                _instance = existing;
                return;
            }

            var go = new GameObject("TouchCursor");
            _instance = go.AddComponent<TouchCursor>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            gameObject.layer = LayerMask.NameToLayer("UI");
            
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = short.MaxValue;
        }

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
            if (_canvas != null) _canvas.enabled = true;
        }

        private void OnDisable()
        {
            EnhancedTouchSupport.Disable();
            
            if (_canvas != null) _canvas.enabled = false;
            
            foreach (var cursor in _activeCursors.Values)
            {
                if (cursor != null) Destroy(cursor.gameObject);
            }
            _activeCursors.Clear();
        }

        private void Update()
        {
            UpdateCursors();
        }

        private void UpdateCursors()
        {
            var currentTouchIds = new HashSet<int>();

            foreach (var touch in Touch.activeTouches)
            {
                currentTouchIds.Add(touch.touchId);

                if (!_activeCursors.TryGetValue(touch.touchId, out RectTransform cursorRect))
                {
                    cursorRect = CreateCursor(touch.touchId);
                    if (cursorRect != null)
                    {
                        _activeCursors.Add(touch.touchId, cursorRect);
                    }
                }

                if (cursorRect != null)
                {
                    cursorRect.anchoredPosition = touch.screenPosition;
                }
            }

            var toRemove = new List<int>();
            foreach (var kvp in _activeCursors)
            {
                if (!currentTouchIds.Contains(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var id in toRemove)
            {
                if (_activeCursors[id] != null)
                {
                    StartCoroutine(FadeOutAndDestroy(_activeCursors[id]));
                }
                _activeCursors.Remove(id);
            }
        }

        private IEnumerator FadeOutAndDestroy(RectTransform cursorRect, float duration = 1.5f)
        {
            if (cursorRect == null) yield break;

            var image = cursorRect.GetComponent<Image>();
            var outline = cursorRect.GetComponent<Outline>();
            var shadow = cursorRect.GetComponent<Shadow>();
            
            if (image == null) 
            {
                Destroy(cursorRect.gameObject);
                yield break;
            }

            Color startColor = image.color;
            Color startOutlineColor = outline != null ? outline.effectColor : Color.black;
            Color startShadowColor = shadow != null ? shadow.effectColor : new Color(0f, 0f, 0f, 0.45f);
            
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (cursorRect == null) yield break;

                elapsed += Time.unscaledDeltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / duration);

                image.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                
                if (outline != null)
                {
                    outline.effectColor = new Color(startOutlineColor.r, startOutlineColor.g, startOutlineColor.b, alpha);
                }

                if (shadow != null)
                {
                    shadow.effectColor = new Color(startShadowColor.r, startShadowColor.g, startShadowColor.b, startShadowColor.a * alpha);
                }

                yield return null;
            }

            if (cursorRect != null)
            {
                Destroy(cursorRect.gameObject);
            }
        }

        private RectTransform CreateCursor(int id)
        {
            if (_canvas == null) return null;

            var cursorGo = new GameObject($"Cursor_{id}");
            cursorGo.transform.SetParent(transform, false);
            
            var rect = cursorGo.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(28f, 28f);

            var image = cursorGo.AddComponent<Image>();
            image.raycastTarget = false;
            if (_cursorSprite == null) _cursorSprite = CreateDefaultCursorSprite();
            image.sprite = _cursorSprite;
            image.color = Color.white;

            var outline = cursorGo.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var shadow = cursorGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(2f, -2f);

            return rect;
        }

        private static Sprite CreateDefaultCursorSprite()
        {
            const int size = 28;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var transparent = new Color(0, 0, 0, 0);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    tex.SetPixel(x, y, transparent);
                }
            }
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float outerRadius = (size * 0.5f) - 1f;
            float borderWidth = 3f;
            float innerRadius = Mathf.Max(0f, outerRadius - borderWidth);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist <= outerRadius)
                    {
                        if (dist >= innerRadius)
                        {
                            tex.SetPixel(x, y, Color.black);
                        }
                        else
                        {
                            tex.SetPixel(x, y, Color.white);
                        }
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}