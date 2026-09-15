using UnityEngine;

namespace SpaceXonix.Hazards
{
    public sealed class LaserPresentation : MonoBehaviour
    {
        public void Configure(Vector3 center, LaserAxis axis, float length, float width)
        {
            transform.position = center;
            transform.rotation = Quaternion.Euler(0f, 0f, axis == LaserAxis.Horizontal ? 0f : 90f);
            transform.localScale = new Vector3(length, width, width);
        }
    }
}
