using UnityEngine;

namespace YgomSystem.UI
{
	public class TweenStepRotation : Tween
	{
		public Vector3 from;

		public Vector3 to;

		public Vector3 origin;

		public Vector3 step;

		protected override void OnSetValue(float par)
		{
            // Rotate in steps: target angle = from + step * n, with n determined by par.
            Vector3 target = Vector3.Lerp(from, to, par);

            Vector3 snapped = target;
            if (step.x > 0f)
                snapped.x = from.x + Mathf.Round((target.x - from.x) / step.x) * step.x;
            if (step.y > 0f)
                snapped.y = from.y + Mathf.Round((target.y - from.y) / step.y) * step.y;
            if (step.z > 0f)
                snapped.z = from.z + Mathf.Round((target.z - from.z) / step.z) * step.z;

            // Support rotation around the origin pivot in local coordinates.
            if (origin != Vector3.zero)
            {
                Vector3 pos = transform.localPosition - origin;
                Quaternion rot = Quaternion.Euler(snapped);
                transform.localPosition = origin + rot * pos;
            }
            transform.localRotation = Quaternion.Euler(snapped);
        }
	}
}
