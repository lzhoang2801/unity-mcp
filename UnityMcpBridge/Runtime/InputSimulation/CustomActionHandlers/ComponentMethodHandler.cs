namespace MCPForUnity.Runtime.InputSimulation.CustomActionHandlers
{
    using UnityEngine;

    /// <summary>
    /// Configurable handler that interacts with a specified component and calls a method on it
    /// in response to a specific action type.
    /// </summary>
    public class ComponentMethodHandler : ICustomActionHandler
    {
        private readonly string _componentName;
        private readonly string _methodName;
        private readonly string _actionType;

        public ComponentMethodHandler(string componentName, string methodName, string actionType)
        {
            _componentName = componentName;
            _methodName = methodName;
            _actionType = actionType;
        }

        public bool CanHandle(GameObject target, string actionType)
        {
            if (target == null || string.IsNullOrEmpty(_componentName) || actionType != _actionType)
            {
                return false;
            }
            return target.GetComponent(_componentName) != null;
        }
        
        public bool Handle(GameObject target, string actionType, object[] args = null)
        {
            if (target == null || string.IsNullOrEmpty(_componentName) || string.IsNullOrEmpty(_methodName)) return false;

            var component = target.GetComponent(_componentName);
            if (component == null) return false;

            var method = component.GetType().GetMethod(_methodName);
            if (method == null)
            {
                Debug.LogWarning($"[CustomActionHandlers] Found component {_componentName} on {target.name}, but could not find method {_methodName}().");
                return false;
            }
            
            Debug.Log($"[CustomActionHandlers] Found {_componentName} on {target.name}. Calling {_methodName}().");
            method.Invoke(component, args);
            return true;
        }
    }
}
