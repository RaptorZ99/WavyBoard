using UnityEngine;

namespace Biscotte.Ocean
{
    /// <summary>
    /// Keeps the ocean under the camera, so it has no edge.
    ///
    /// The Storm Breakers ocean is a fixed grid of tiles around its own transform, which is fine while the player
    /// stays near the origin and turns into a small raft of water surrounded by sky the moment they do not. The
    /// surface itself is a pure function of WORLD position, though, so the grid can be slid to follow the camera
    /// with no visible change at all — as long as it is moved in whole tile steps, every vertex lands exactly where
    /// another vertex used to be and the water is bit-for-bit identical. That makes the ocean effectively infinite
    /// for the cost of the tiles already in the scene.
    /// </summary>
    [DefaultExecutionOrder(-240)]
    public class OceanFollow : MonoBehaviour
    {
        [Tooltip("Size of one ocean tile (m). The grid only ever moves in whole multiples of this, so the surface\nnever shifts under the player. Storm Breakers' tiles are 128 m.")]
        public float tileSize = 128f;

        [Tooltip("Followed automatically when empty: the main camera.")]
        public Transform target;

        Vector3 origin;
        bool captured;

        void OnEnable()
        {
            if (!captured) { origin = transform.position; captured = true; }
        }

        void OnDisable()
        {
            if (captured) transform.position = origin;
        }

        void LateUpdate()
        {
            if (target == null)
            {
                var cam = Camera.main;
                if (cam == null) return;
                target = cam.transform;
            }
            if (tileSize < 1f) return;

            Vector3 d = target.position - origin;
            float x = Mathf.Round(d.x / tileSize) * tileSize;
            float z = Mathf.Round(d.z / tileSize) * tileSize;
            Vector3 want = new Vector3(origin.x + x, origin.y, origin.z + z);
            if ((want - transform.position).sqrMagnitude > 0.001f) transform.position = want;
        }
    }
}
