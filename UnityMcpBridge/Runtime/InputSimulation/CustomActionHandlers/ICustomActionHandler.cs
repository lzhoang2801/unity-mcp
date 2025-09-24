namespace MCPForUnity.Runtime.InputSimulation.CustomActionHandlers
{
    using UnityEngine;

    public interface ICustomActionHandler
    {
        bool CanHandle(GameObject target, string actionType);
        bool Handle(GameObject target, string actionType, object[] args = null);
    }
}
