from mcp.server.fastmcp import FastMCP, Context
from typing import Dict, Any
from unity_connection import send_command_with_retry

def register_input_simulation_tools(mcp: FastMCP):
    """Register input simulation tools."""

    @mcp.tool()
    def input_simulation(
        ctx: Context,
        action: str,
        clickCount: int = 1,
        x: float = None,
        y: float = None,
        sx: float = None,
        sy: float = None,
        ex: float = None,
        ey: float = None,
        dx: float = None,
        dy: float = None,
    ) -> Dict[str, Any]:
        """Simulates user actions in the Unity Editor's Play Mode.

        Supported Actions: 'click', 'drag', 'scroll'.

        Args:
          action: The simulation action to perform (e.g., 'click').
          clickCount: The number of times to click the target. Defaults to 1.
          x/y: The screen coordinates for the action.
            For click actions: x,y are the click coordinates.
            For scroll actions: x,y are the scroll position coordinates.
          sx/sy: The starting screen coordinates for a drag operation.
          ex/ey: The ending screen coordinates for a drag operation.
          dx/dy: The scroll delta for the scroll action.

        Returns:
          A dictionary with the operation results ('success', 'message', 'data').
        """

        params = {
            "action": action,
            "clickCount": clickCount,
            "x": x,
            "y": y,
            "sx": sx,
            "sy": sy,
            "ex": ex,
            "ey": ey,
            "dx": dx,
            "dy": dy,
        }
        params = {k: v for k, v in params.items() if v is not None}
        try:
            response = send_command_with_retry("input_simulation", params)
            return response if isinstance(response, dict) else {"success": False, "message": str(response)}
        except Exception as e:
            return {"success": False, "message": f"Python error in input_simulation: {str(e)}"}