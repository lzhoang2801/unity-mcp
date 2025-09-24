using System.Threading.Tasks;
using UnityEngine;

namespace MCPForUnity.Runtime.InputSimulation.CustomActionHandlers
{
    /// <summary>
    /// Defines a contract for components that can manipulate their viewport 
    /// to make a specific child target visible on screen.
    /// </summary>
    public interface IViewPortHandler
    {
        /// <summary>
        /// Asynchronously performs the necessary actions (e.g., scrolling, dragging)
        /// to bring the specified target GameObject into the visible area.
        /// </summary>
        /// <param name="target">The child GameObject to make visible.</param>
        /// <returns>A task that resolves to true if the operation was successful, false otherwise.</returns>
        Task<bool> BringIntoView(GameObject target);
    }
}
