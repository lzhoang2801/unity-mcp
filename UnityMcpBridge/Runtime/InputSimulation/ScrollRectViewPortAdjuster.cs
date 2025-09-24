
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MCPForUnity.Runtime.InputSimulation
{
    [RequireComponent(typeof(ScrollRect))]
    public class ScrollRectViewPortAdjuster : MonoBehaviour, IViewPortAdjuster
    {
        ScrollRect m_ScrollRect;
        RectTransform m_ScrollRectTransform;
        RectTransform m_Content;

        void Awake()
        {
            m_ScrollRect = GetComponent<ScrollRect>();
            m_ScrollRectTransform = m_ScrollRect.transform as RectTransform;
            m_Content = m_ScrollRect.content;
        }

        public IEnumerator AdjustView(RectTransform targetElement)
        {
            // Calculate the position of the target element and the viewport in world space.
            var targetWorldCorners = new Vector3[4];
            targetElement.GetWorldCorners(targetWorldCorners);
            var targetWorldRect = new Rect(targetWorldCorners[0], targetWorldCorners[2] - targetWorldCorners[0]);

            var viewportWorldCorners = new Vector3[4];
            m_ScrollRectTransform.GetWorldCorners(viewportWorldCorners);
            var viewportWorldRect = new Rect(viewportWorldCorners[0], viewportWorldCorners[2] - viewportWorldCorners[0]);

            // If the target is already fully visible within the viewport, no need to scroll.
            if (viewportWorldRect.Contains(targetWorldRect.min) && viewportWorldRect.Contains(targetWorldRect.max))
            {
                yield break;
            }

            // Calculate the desired scroll position to center the target element.
            var targetLocalPos = m_Content.InverseTransformPoint(targetElement.position);
            var contentSize = m_Content.rect.size;

            var normalizedTargetPos = new Vector2(
                (targetLocalPos.x + contentSize.x * m_Content.pivot.x) / contentSize.x,
                (targetLocalPos.y + contentSize.y * m_Content.pivot.y) / contentSize.y
            );

            var newNormalizedPos = new Vector2(
                m_ScrollRect.horizontal ? Mathf.Clamp01(normalizedTargetPos.x) : m_ScrollRect.normalizedPosition.x,
                m_ScrollRect.vertical ? Mathf.Clamp01(normalizedTargetPos.y) : m_ScrollRect.normalizedPosition.y
            );
            
            // TODO: Replace with a smooth lerp over time instead of an instant jump.
            m_ScrollRect.normalizedPosition = newNormalizedPos;

            // Wait a frame to allow the UI to update.
            yield return null;
        }
    }
}
