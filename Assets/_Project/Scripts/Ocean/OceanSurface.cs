using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Biscotte.Ocean
{
    /// <summary>
    /// The ambient sea: owns the swell (<see cref="SwellParams"/>), publishes it to the water shader every frame, and
    /// draws the open ocean with one camera-centred polar mesh (fine under the player, coarser toward the horizon, no
    /// T-junctions, no tiles). The surf waves use the same shader and the same swell, so they grow out of this surface
    /// without a seam.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(-250)]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class OceanSurface : MonoBehaviour
    {
        public static OceanSurface Instance { get; private set; }

        [Header("Swell (a living sea, not a mirror)")]
        public SwellComponent[] components =
        {
            new SwellComponent { wavelength = 78f, amplitude = 0.32f, directionDeg = 0f, steepness = 0.35f, phase = 0f },
            new SwellComponent { wavelength = 47f, amplitude = 0.17f, directionDeg = 17f, steepness = 0.4f, phase = 1.3f },
            new SwellComponent { wavelength = 29f, amplitude = 0.11f, directionDeg = -24f, steepness = 0.45f, phase = 2.1f },
            new SwellComponent { wavelength = 13.5f, amplitude = 0.055f, directionDeg = 38f, steepness = 0.5f, phase = 4.0f },
            new SwellComponent { wavelength = 8.2f, amplitude = 0.03f, directionDeg = -52f, steepness = 0.5f, phase = 0.7f },
            new SwellComponent { wavelength = 5.1f, amplitude = 0.017f, directionDeg = 71f, steepness = 0.45f, phase = 5.2f },
        };
        [Tooltip("Vertex LOD: a component stops displacing the mesh beyond this many wavelengths from the camera")]
        public float lodWavelengths = 9f;
        public float timeScale = 1f;
        [Tooltip("Swell time used outside Play mode (edit-mode previews and captures)")]
        public float previewTime = 10f;

        [Header("Mesh")]
        public Material material;
        [Range(64, 512)] public int angularSegments = 256;
        public float innerRadius = 0.5f;
        public float outerRadius = 6000f;
        [Tooltip("Followed automatically when empty: the main camera")]
        public Transform follow;

        public SwellParams Params;

        static readonly int kSwellA = Shader.PropertyToID("_SwellA");
        static readonly int kSwellB = Shader.PropertyToID("_SwellB");
        static readonly int kSwellCount = Shader.PropertyToID("_SwellCount");
        static readonly int kWaterTime = Shader.PropertyToID("_WaterTime");
        readonly Vector4[] swellA = new Vector4[SwellParams.Max];
        readonly Vector4[] swellB = new Vector4[SwellParams.Max];
        Mesh mesh;
        int builtSegments = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        /// <summary>Swell time for a game time (Play mode) or the preview time (edit mode).</summary>
        public float SwellTime(double gameTime) => Application.isPlaying ? (float)(gameTime * timeScale) : previewTime;

        /// <summary>Swell time of the frame being rendered.</summary>
        public float RenderTime => SwellTime(Time.timeAsDouble);

        void OnEnable()
        {
            Instance = this;
            Rebake();
            BuildMesh();
            Publish();
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        void OnDestroy()
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
        }

        void OnValidate()
        {
            Rebake();
            if (isActiveAndEnabled && builtSegments != angularSegments) BuildMesh();
        }

        public void Rebake() { Params = SwellParams.Build(components, lodWavelengths); }

        void Update() { Publish(); }

        void LateUpdate()
        {
            Publish();
            var t = follow != null ? follow : (Camera.main != null ? Camera.main.transform : null);
            if (t != null) transform.SetPositionAndRotation(new Vector3(t.position.x, 0f, t.position.z), Quaternion.identity);
        }

        /// <summary>Pushes the swell and its clock to the water shader (globals shared by every water mesh).</summary>
        public void Publish()
        {
            for (int i = 0; i < SwellParams.Max; i++)
            {
                Params.Get(i, out float4 a, out float4 b);
                swellA[i] = i < Params.count ? (Vector4)a : Vector4.zero;
                swellB[i] = i < Params.count ? (Vector4)b : new Vector4(0f, 0f, 0f, 1f);
            }
            Shader.SetGlobalVectorArray(kSwellA, swellA);
            Shader.SetGlobalVectorArray(kSwellB, swellB);
            Shader.SetGlobalFloat(kSwellCount, Params.count);
            Shader.SetGlobalFloat(kWaterTime, RenderTime);
        }

        /// <summary>Ambient sea above the world point (height, normal, surface velocity).</summary>
        public void Sample(float3 worldPos, double gameTime, out float height, out float3 normal, out float3 velocity)
        {
            OceanSwell.Sample(Params, worldPos.xz, SwellTime(gameTime), out height, out normal, out velocity, out _);
        }

        public float Height(float3 worldPos, double gameTime)
        {
            float t = SwellTime(gameTime);
            return OceanSwell.Displacement(Params, OceanSwell.Undeform(Params, worldPos.xz, t), t).y;
        }

        void BuildMesh()
        {
            var mf = GetComponent<MeshFilter>();
            var mr = GetComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
            if (material != null) mr.sharedMaterial = material;

            int a = Mathf.Clamp(angularSegments, 64, 512);
            float growth = 1f + 2f * Mathf.PI / a;
            int rings = Mathf.CeilToInt(Mathf.Log(outerRadius / innerRadius) / Mathf.Log(growth)) + 1;
            var verts = new Vector3[1 + rings * a];
            var normals = new Vector3[verts.Length];
            verts[0] = Vector3.zero;
            float r = innerRadius;
            for (int i = 0; i < rings; i++, r *= growth)
            {
                float rr = Mathf.Min(r, outerRadius);
                for (int k = 0; k < a; k++)
                {
                    float th = 2f * Mathf.PI * k / a;
                    verts[1 + i * a + k] = new Vector3(rr * Mathf.Cos(th), 0f, rr * Mathf.Sin(th));
                }
            }
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;

            var idx = new int[a * 3 + (rings - 1) * a * 6];
            int n = 0;
            // winding: clockwise seen from above (front faces for a camera over the sea)
            for (int k = 0; k < a; k++)
            {
                idx[n++] = 0; idx[n++] = 1 + (k + 1) % a; idx[n++] = 1 + k;
            }
            for (int i = 0; i < rings - 1; i++)
            for (int k = 0; k < a; k++)
            {
                int k1 = (k + 1) % a;
                int v00 = 1 + i * a + k, v01 = 1 + i * a + k1, v10 = 1 + (i + 1) * a + k, v11 = 1 + (i + 1) * a + k1;
                idx[n++] = v00; idx[n++] = v01; idx[n++] = v10;
                idx[n++] = v01; idx[n++] = v11; idx[n++] = v10;
            }

            if (mesh == null) mesh = new Mesh { name = "OceanSurface", hideFlags = HideFlags.DontSave };
            mesh.Clear();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.triangles = idx;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(outerRadius * 2f, 20f, outerRadius * 2f));
            mf.sharedMesh = mesh;
            builtSegments = angularSegments;
        }
    }
}
