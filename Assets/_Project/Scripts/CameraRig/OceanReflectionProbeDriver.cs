using UnityEngine;

namespace Biscotte.CameraRig
{
    /// <summary>Refreshes a realtime reflection probe (ViaScripting) periodically, following the main camera horizontally.</summary>
    [RequireComponent(typeof(ReflectionProbe))]
    public class OceanReflectionProbeDriver : MonoBehaviour
    {
        public float refreshInterval = 2f;
        public float height = 6f;
        ReflectionProbe probe;
        float nextRefresh;

        void Awake() { probe = GetComponent<ReflectionProbe>(); }

        void Start() { Refresh(); }

        void Update()
        {
            if (Time.time < nextRefresh) return;
            Refresh();
        }

        void Refresh()
        {
            var cam = Camera.main;
            if (cam != null) transform.position = new Vector3(cam.transform.position.x, height, cam.transform.position.z);
            probe.RenderProbe();
            nextRefresh = Time.time + refreshInterval;
        }
    }
}
