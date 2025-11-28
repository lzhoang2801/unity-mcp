from mcp.server.fastmcp import FastMCP, Context
from typing import Dict, Any
from unity_connection import send_command_with_retry

def register_input_simulator_tools(mcp: FastMCP):
    """Register input simulator tools."""

    @mcp.tool()
    def input_simulator(
        ctx: Context,
        action: str,
        clickCount: int = 1,
        x: int = None,
        y: int = None,
        x2: int = None,
        y2: int = None,
        scale: float = None,
    ) -> Dict[str, Any]:
        """Simulates user actions in the Unity Editor's Play Mode using touch input.

        Supported Actions: 'tap', 'swipe', 'pinchToZoom', 'wait'.

        Args:
          action: The simulator action to perform (e.g., 'tap').
          clickCount: The number of times to tap the target. Defaults to 1.
          x/y/x2/y2: The screen coordinates for the action.
            For tap actions: x,y are the tap coordinates.
            For swipe actions: x,y are the start coordinates, x2,y2 are the end coordinates.
            For pinchToZoom actions: x,y are the first finger coordinates, x2,y2 are the second finger coordinates.
          scale: The scale factor for the pinch to zoom action.
          time: The time to wait after the action in seconds.

        Returns:
          A dictionary with the operation results ('success', 'message', 'data').
        """

        params = {
            "action": action,
            "clickCount": clickCount,
            "x": x,
            "y": y,
            "x2": x2,
            "y2": y2,
            "scale": scale,
        }
        params = {k: v for k, v in params.items() if v is not None}
        try:
            response = send_command_with_retry("input_simulator", params)
            return response if isinstance(response, dict) else {"success": False, "message": str(response)}
        except Exception as e:
            return {"success": False, "message": f"Python error in input_simulator: {str(e)}"}