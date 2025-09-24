namespace MCPForUnity.Runtime.InputSimulation
{
    public static class InputSimulationConstants
    {
        public const string ActionClick = "click";
        public const string ActionDrag = "drag";
        public const string ActionScroll = "scroll";
        
        public const string ErrorCodeNotInPlayMode = "NotInPlayMode";
        public const string ErrorCodeMissingAction = "MissingAction";
        public const string ErrorCodeUnknownAction = "UnknownAction";
        public const string ErrorCodeInvalidInstanceID = "InvalidInstanceID";
        public const string ErrorCodeCannotDeterminePosition = "CannotDeterminePosition";
        public const string ErrorCodeTargetOutOfViewport = "TargetOutOfViewport";
        public const string ErrorCodeMissingCoordinates = "MissingCoordinates";
        public const string ErrorCodeNoInteractableObjectFound = "NoInteractableObjectFound";
        public const string ErrorCodeTargetNotInteractable = "TargetNotInteractable";
        public const string ErrorCodeMissingEndCoordinates = "MissingEndCoordinates";
        public const string ErrorCodeEndPositionOutOfViewport = "EndPositionOutOfViewport";
        public const string ErrorCodeException = "Exception";
    }
}
