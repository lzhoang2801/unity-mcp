from mcp.server.fastmcp import FastMCP, Context
from typing import Dict, Any
from unity_connection import send_command_with_retry

def register_scene_observer_tools(mcp: FastMCP):
    """Register the scene observer tool."""

    @mcp.tool()
    def scene_observer(ctx: Context) -> Dict[str, Any]:
        """
        Retrieves the current Unity game scene during Play Mode to help the AI agent "see" and understand all visible UI elements and interactive objects.
        Use this before taking any action to ensure you have the most up-to-date and complete view of what is on screen.

        Returns:
          A dictionary with the operation results ('success', 'message', 'data').
        """
        try:
            response = send_command_with_retry("scene_observer", {})
            return response if isinstance(response, dict) else {"success": False, "message": str(response)}
        except Exception as e:
            return {"success": False, "message": f"Python error in scene_observer: {str(e)}"}
