
using System.Collections;
using UnityEngine;

namespace MCPForUnity.Runtime.InputSimulation
{
    /// <summary>
    /// An interface for UI containers that can adjust their viewport to make a specific child element visible.
    /// For example, a ScrollRect could implement this to automatically scroll to a button that is currently off-screen.
    /// </summary>
    public interface IViewPortAdjuster
    {
        /// <summary>
        /// Adjusts the viewport of the UI container to make the target element visible.
        /// </summary>
        /// <param name="targetElement">The transform of the element to bring into view.</param>
        /// <returns>An IEnumerator to be used in a coroutine, allowing for asynchronous operations like animated scrolling.</returns>
        IEnumerator AdjustView(RectTransform targetElement);
    }
}
