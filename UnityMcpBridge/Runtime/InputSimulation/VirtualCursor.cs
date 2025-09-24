using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MCPForUnity.Runtime.InputSimulation
{
    /// <summary>
    /// Renders a visible cursor on top of the game via a Screen Space Overlay canvas
    /// and provides smooth movement and click feedback animations.
    /// </summary>
    public sealed class VirtualCursor
    {
        private const string CanvasName = "__VirtualCursorCanvas";
        private const string CursorName = "__VirtualCursor";

		private const float CursorSize = 28f;
		private const float MoveDistanceDivisor = 4000f;
		private const float MoveMinDuration = 0.025f;
		private const float MoveMaxDuration = 1.0f;

        private Canvas _canvas;
        private RectTransform _cursorRect;
        private Image _cursorImage;

        private bool _isFlashing;
        private const float FlashScaleAmplitude = 0.2f;

        public RectTransform RectTransform => _cursorRect;
        public bool IsReady => _cursorRect != null;

        public void EnsureCreated()
        {
            if (_canvas != null) return;

            var canvasGo = new GameObject(CanvasName)
            {
                layer = LayerMask.NameToLayer("UI")
            };
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = short.MaxValue;
            canvasGo.AddComponent<GraphicRaycaster>();
            Object.DontDestroyOnLoad(canvasGo);

            var cursorGo = new GameObject(CursorName);
            cursorGo.transform.SetParent(canvasGo.transform, false);
            _cursorRect = cursorGo.AddComponent<RectTransform>();
            _cursorRect.anchorMin = Vector2.zero;
            _cursorRect.anchorMax = Vector2.zero;
            _cursorRect.pivot = new Vector2(0.5f, 0.5f);
            _cursorRect.sizeDelta = new Vector2(CursorSize, CursorSize);

            _cursorImage = cursorGo.AddComponent<Image>();
            _cursorImage.raycastTarget = false;
            _cursorImage.sprite = CreateDefaultCursorSprite();
            _cursorImage.color = Color.white;

            var outline = cursorGo.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var shadow = cursorGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(2f, -2f);
        }

        public void SetScreenPosition(Vector2 screenPosition)
        {
            if (_cursorRect == null)
            {
                return;
            }
            _cursorRect.anchoredPosition = screenPosition;
        }

        public async Task MoveTo(MonoBehaviour runner, Vector2 to)
        {
            Vector2 from = _cursorRect.anchoredPosition;
            float duration = CalculateDuration(from, to);

            if (duration <= 0f)
            {
                SetScreenPosition(to);
                return;
            }

            await AnimationHelper.AnimateOverTime(duration, eased =>
            {
                Vector2 pos = Vector2.LerpUnclamped(from, to, eased);
                SetScreenPosition(pos);
            }, useUnscaledTime: true);
            SetScreenPosition(to);
        }

        public async Task ClickFlash(float duration = 0.10f)
        {
            if (_cursorImage == null || _cursorRect == null || _isFlashing)
            {
                return;
            }

            _isFlashing = true;

            Color baseColor = _cursorImage.color;
            Vector3 baseScale = _cursorRect.localScale;
            float startTime = Time.unscaledTime;
            float flashDuration = Mathf.Max(0.02f, duration);

            while (Time.unscaledTime - startTime < flashDuration)
            {
                float elapsed = Time.unscaledTime - startTime;
                float t = elapsed / flashDuration;
                float intensity = Mathf.Sin(t * Mathf.PI);

                float scale = 1f + (FlashScaleAmplitude * intensity);
                _cursorRect.localScale = baseScale * scale;
                _cursorImage.color = Color.Lerp(baseColor, Color.yellow, intensity);

                await Task.Yield();
            }

            _cursorRect.localScale = baseScale;
            _cursorImage.color = baseColor;
            _isFlashing = false;
        }

        public static float CalculateDuration(Vector2 start, Vector2 end)
        {
            float distance = Vector2.Distance(start, end);
            if (distance < 1.0f) return 0f;
            return Mathf.Clamp(distance / MoveDistanceDivisor, MoveMinDuration, MoveMaxDuration);
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