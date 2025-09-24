using System.Threading.Tasks;
using UnityEngine;

namespace MCPForUnity.Runtime.InputSimulation
{
	/// <summary>
	/// Shared animation helper to drive time-based interpolations.
	/// </summary>
	public static class AnimationHelper
	{
		public static async Task AnimateOverTime(float duration, System.Action<float> onUpdate, bool useUnscaledTime = false)
		{
			if (onUpdate == null)
			{
				return;
			}

			duration = Mathf.Max(0f, duration);
			if (duration <= 0f)
			{
				onUpdate(1f);
				await Task.Yield();
				return;
			}

			if (useUnscaledTime)
			{
				float startTime = Time.unscaledTime;
				while (Time.unscaledTime - startTime < duration)
				{
					float elapsed = Time.unscaledTime - startTime;
					float t = Mathf.Clamp01(elapsed / duration);
					float eased = Mathf.SmoothStep(0f, 1f, t);
					onUpdate(eased);
					await Task.Yield();
				}
				onUpdate(1f);
			}
			else
			{
				float elapsed = 0f;
				while (elapsed < duration)
				{
					float t = Mathf.Clamp01(elapsed / duration);
					float eased = Mathf.SmoothStep(0f, 1f, t);
					onUpdate(eased);
					await Task.Yield();
					elapsed += Time.deltaTime;
				}
				onUpdate(1f);
			}
		}
	}
}

