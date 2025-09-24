using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace MCPForUnity.Runtime.InputSimulation.CustomActionHandlers
{
    [RequireComponent(typeof(ScrollRect))]
    public class ScrollRectViewHandler : MonoBehaviour, IViewPortHandler
    {
        private ScrollRect _scrollRect;

        private void Awake()
        {
            _scrollRect = GetComponent<ScrollRect>();
        }

        public async Task<bool> BringIntoView(GameObject target)
        {
            if (_scrollRect == null || target == null) return false;

            var targetRect = target.GetComponent<RectTransform>();
            if (targetRect == null) return false;

            Vector2 targetNormalizedPos = CalculateTargetNormalizedPosition(targetRect);
            return await AnimateScroll(targetNormalizedPos);
        }

        private Vector2 CalculateTargetNormalizedPosition(RectTransform targetRect)
        {
            Vector3[] targetWorldCorners = new Vector3[4];
            targetRect.GetWorldCorners(targetWorldCorners);
            Vector3 targetWorldCenter = (targetWorldCorners[0] + targetWorldCorners[2]) * 0.5f;

            Vector3 localTargetPos = _scrollRect.content.InverseTransformPoint(targetWorldCenter);
            
            Vector2 contentSize = _scrollRect.content.rect.size;
            Vector2 viewportSize = _scrollRect.viewport.rect.size;
            Vector2 scrollableSize = contentSize - viewportSize;

            Vector2 targetNormalizedPos = _scrollRect.normalizedPosition;

            if (_scrollRect.horizontal && scrollableSize.x > 0)
            {
                float targetX = localTargetPos.x + contentSize.x * _scrollRect.content.pivot.x;
                float normalizedX = (targetX - viewportSize.x * 0.5f) / scrollableSize.x;
                targetNormalizedPos.x = Mathf.Clamp01(normalizedX);
            }

            if (_scrollRect.vertical && scrollableSize.y > 0)
            {
                float targetY = localTargetPos.y + contentSize.y * _scrollRect.content.pivot.y;
                float normalizedY = (targetY - viewportSize.y * 0.5f) / scrollableSize.y;
                targetNormalizedPos.y = Mathf.Clamp01(normalizedY);
            }

            return targetNormalizedPos;
        }

        private async Task<bool> AnimateScroll(Vector2 targetNormalizedPosition)
        {
            Vector2 startPosition = _scrollRect.normalizedPosition;
            float duration = VirtualCursor.CalculateDuration(startPosition * 1000, targetNormalizedPosition * 1000);
            if (duration <= 0.01f)
            {
                _scrollRect.normalizedPosition = targetNormalizedPosition;
                await Task.Yield();
                return true;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0, 1, t);
                _scrollRect.normalizedPosition = Vector2.Lerp(startPosition, targetNormalizedPosition, eased);
                await Task.Yield();
                elapsed += Time.deltaTime;
            }
            _scrollRect.normalizedPosition = targetNormalizedPosition;
            await Task.Yield();
            return true;
        }
    }
}
