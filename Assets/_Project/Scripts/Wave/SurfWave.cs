using Biscotte.Ocean;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Biscotte.Wave
{
    /// <summary>One breaking wave: CPU-authoritative mesh (Burst job) + analytic sampling for gameplay (spec §5).</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [DefaultExecutionOrder(-100)]
    public class SurfWave : MonoBehaviour
    {
        [Header("Mesh resolution")]
        public int Ns = 160;
        public int Nxi = 60;
        public int Nv = 12;

        public SurfWaveParams Params;
        public bool IsAlive { get; private set; }
        public double SpawnTime { get; private set; }
        public float WaveTime => (float)(Time.timeAsDouble - SpawnTime);
        public float FixedWaveTime => (float)(Time.fixedTimeAsDouble - SpawnTime);
        public SurfSpotConfig Spot { get; private set; }

        NativeArray<float4> profile;
        NativeArray<SurfVertex> verts;
        Mesh mesh;
        MeshFilter mf;
        MeshRenderer mr;
        JobHandle handle;
        bool jobScheduled;
        int vertexCount;
        float maxTravel;

        static readonly VertexAttributeDescriptor[] k_Layout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 2),
        };

        void Awake()
        {
            EnsureTopology();
            mr.enabled = false;
        }

        void EnsureTopology()
        {
            if (mesh != null) return;
            mf = GetComponent<MeshFilter>();
            mr = GetComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
            BuildTopology();
        }

        void OnDestroy()
        {
            if (jobScheduled) handle.Complete();
            if (profile.IsCreated) profile.Dispose();
            if (verts.IsCreated) verts.Dispose();
            if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        }

        /// <summary>Editor/preview helper: spawns and builds the mesh synchronously for a given wave time (no Play mode, no ambient).</summary>
        public void BuildNow(SurfSpotConfig spot, float tw, float heightScale = 1f, int id = 999)
        {
            EnsureTopology();
            Spot = spot;
            Params = spot.BuildParams(id);
            maxTravel = spot.maxTravel;
            if (!profile.IsCreated) profile = new NativeArray<float4>(96, Allocator.Persistent);
            spot.FillProfile(profile, heightScale);
            SpawnTime = Time.timeAsDouble - tw;
            IsAlive = true;
            mr.enabled = true;
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            var job = new SurfWaveMeshJob
            {
                P = Params, Profile = profile, Ambient = default, TimeW = tw, AmbientTime = 0f,
                Ns = Ns, Nxi = Nxi, Nv = Nv, GroundDepth = 200f, Verts = verts
            };
            job.Schedule(vertexCount, 64).Complete();
            Upload(tw);
        }

        /// <summary>Min / max / mean of the face vertex colour channels (energy, foam, tubeAO, whitewater) and the y range, for diagnostics.</summary>
        public string VertexStats()
        {
            if (!verts.IsCreated) return "no verts";
            int n = Ns * Nxi;
            float4 mn = new float4(float.MaxValue), mx = new float4(float.MinValue), sum = float4.zero;
            float ymin = float.MaxValue, ymax = float.MinValue; int foamy = 0;
            for (int i = 0; i < n; i++)
            {
                var v = verts[i];
                mn = math.min(mn, v.col); mx = math.max(mx, v.col); sum += v.col;
                ymin = math.min(ymin, v.pos.y); ymax = math.max(ymax, v.pos.y);
                if (v.col.y + v.col.w > 0.05f) foamy++;
            }
            return $"face verts {n}: col min {mn} max {mx} mean {sum / n} | y [{ymin:0.00}, {ymax:0.00}] | foamy {foamy} ({100f * foamy / n:0.0}%)";
        }

        void Upload(float tw)
        {
            mesh.SetVertexBufferData(verts, 0, 0, vertexCount, 0, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);
            float crest = Params.CrestOffset(tw);
            float3 c = Params.origin + Params.crestDir * (Params.length * 0.5f) + Params.travelDir * (crest + (Params.xiMin + Params.xiMax) * 0.5f);
            float3 ext = math.abs(Params.crestDir) * (Params.length * 0.5f + Params.sPad + 2f) + math.abs(Params.travelDir) * ((Params.xiMax - Params.xiMin) * 0.5f + 4f) + new float3(2f, 12f, 2f);
            mesh.bounds = new Bounds(c, ext * 2f);
        }

        void BuildTopology()
        {
            vertexCount = Ns * Nxi + Ns * Nv * 2;
            verts = new NativeArray<SurfVertex>(vertexCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            mesh = new Mesh { name = "SurfWave" };
            mesh.MarkDynamic();
            mesh.SetVertexBufferParams(vertexCount, k_Layout);

            int faceQuads = (Ns - 1) * (Nxi - 1);
            int lipQuads = (Ns - 1) * (Nv - 1) * 2;
            var indices = new NativeArray<uint>((faceQuads + lipQuads) * 6, Allocator.Temp);
            int k = 0;
            for (int r = 0; r < Ns - 1; r++)
            for (int c = 0; c < Nxi - 1; c++)
            {
                uint a = (uint)(r * Nxi + c), b = a + 1, cc = (uint)((r + 1) * Nxi + c), d = cc + 1;
                // a->b runs along travelDir (+Z), a->cc along crestDir (+X): (a, b, cc) is clockwise seen from above,
                // i.e. front-facing for a viewer above the water (the Storm Breakers graph switches to its underwater
                // look on back faces, so the winding matters).
                indices[k++] = a; indices[k++] = b; indices[k++] = cc;
                indices[k++] = b; indices[k++] = d; indices[k++] = cc;
            }
            int lipBase = Ns * Nxi;
            for (int side = 0; side < 2; side++)
            {
                int sb = lipBase + side * Ns * Nv;
                for (int r = 0; r < Ns - 1; r++)
                for (int c = 0; c < Nv - 1; c++)
                {
                    uint a = (uint)(sb + r * Nv + c), b = a + 1, cc = (uint)(sb + (r + 1) * Nv + c), d = cc + 1;
                    // side 0 = outer/top surface of the lip (front face outward), side 1 = underside (reversed winding)
                    if (side == 0) { indices[k++] = a; indices[k++] = b; indices[k++] = cc; indices[k++] = b; indices[k++] = d; indices[k++] = cc; }
                    else { indices[k++] = a; indices[k++] = cc; indices[k++] = b; indices[k++] = b; indices[k++] = cc; indices[k++] = d; }
                }
            }
            mesh.SetIndexBufferParams(indices.Length, IndexFormat.UInt32);
            mesh.SetIndexBufferData(indices, 0, 0, indices.Length, MeshUpdateFlags.DontValidateIndices);
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, indices.Length, MeshTopology.Triangles), MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);
            indices.Dispose();
            mf.sharedMesh = mesh;
        }

        public void Spawn(SurfSpotConfig spot, float heightScale, int id)
        {
            if (jobScheduled) { handle.Complete(); jobScheduled = false; }
            Spot = spot;
            Params = spot.BuildParams(id);
            maxTravel = spot.maxTravel;
            if (!profile.IsCreated) profile = new NativeArray<float4>(96, Allocator.Persistent);
            spot.FillProfile(profile, heightScale);
            SpawnTime = Time.timeAsDouble;
            IsAlive = true;
            mr.enabled = true;
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            WaterSurfaceComposite.Instance?.Register(this);
        }

        public void Despawn()
        {
            if (jobScheduled) { handle.Complete(); jobScheduled = false; }
            IsAlive = false;
            mr.enabled = false;
            WaterSurfaceComposite.Instance?.Unregister(this);
        }

        void Update()
        {
            if (!IsAlive) return;
            float tw = WaveTime;
            if (Params.CrestOffset(tw) > maxTravel) { Despawn(); return; }

            var amb = OceanAmbient.Instance;
            var job = new SurfWaveMeshJob
            {
                P = Params, Profile = profile, Ambient = amb != null ? amb.Params : default,
                TimeW = tw, AmbientTime = amb != null ? amb.AmbientTime : Time.time,
                Ns = Ns, Nxi = Nxi, Nv = Nv, GroundDepth = 200f, Verts = verts
            };
            handle = job.Schedule(vertexCount, 64);
            jobScheduled = true;
            JobHandle.ScheduleBatchedJobs();
        }

        void LateUpdate()
        {
            if (!jobScheduled) return;
            handle.Complete();
            jobScheduled = false;
            Upload(WaveTime);
        }

        // ---------------------------------------------------------------- gameplay sampling
        public void LocalCoords(float3 worldPos, float tw, out float s, out float xi)
        {
            float3 rel = worldPos - Params.origin;
            s = math.dot(rel, Params.crestDir);
            float d = math.dot(rel, Params.travelDir);
            xi = d - Params.CrestOffset(tw);
        }

        public bool Contains(float3 worldPos)
        {
            if (!IsAlive) return false;
            LocalCoords(worldPos, WaveTimeForSampling, out float s, out float xi);
            return s > -Params.sPad && s < Params.length + Params.sPad && xi > Params.xiMin && xi < Params.xiMax;
        }

        /// <summary>Time used for CPU sampling: fixed time inside FixedUpdate, render time otherwise.</summary>
        float WaveTimeForSampling => Time.inFixedTimeStep ? FixedWaveTime : WaveTime;

        public WaterSample Sample(float3 worldPos, float time, in OceanParams ambient, float ambientTime, float groundDepth)
        {
            float tw = (float)(time - SpawnTime);
            float3 und = new float3(worldPos.x, 0f, worldPos.z);
            float hA = OceanMath.GetHeight(in ambient, ambientTime, worldPos, ref und, out float3 def, groundDepth);
            float3 nA = OceanMath.GetNormal(in ambient, ambientTime, und, def, groundDepth, 0.3f);
            float3 vA = OceanMath.GetVelocity(in ambient, ambientTime, und, def, groundDepth, 0.1f);
            // The mesh is built on the undeformed plane and displaced by the ambient swell on the GPU (SurfWaveOcean graph):
            // evaluate the surf shape at the undeformed footprint so physics and visuals coincide.
            LocalCoords(new float3(und.x, 0f, und.z), tw, out float s, out float xi);
            SurfLocal L = SurfWaveMath.Evaluate(Params, profile, s, xi, tw);

            const float e = 0.2f;
            float hs1 = SurfWaveMath.HeightAt(Params, profile, s + e, xi, tw);
            float hs0 = SurfWaveMath.HeightAt(Params, profile, s - e, xi, tw);
            float hx1 = SurfWaveMath.HeightAt(Params, profile, s, xi + e, tw);
            float hx0 = SurfWaveMath.HeightAt(Params, profile, s, xi - e, tw);
            float3 nW = math.normalize(new float3(0f, 1f, 0f) - Params.crestDir * ((hs1 - hs0) / (2f * e)) - Params.travelDir * ((hx1 - hx0) / (2f * e)));
            float3 n = math.normalize(nW + (nA - new float3(0f, 1f, 0f)));

            WaterSample r = default;
            r.Height = hA + L.height;
            r.Normal = n;
            r.Velocity = SurfWaveMath.Velocity(Params, profile, s, xi, tw, L) + vA * 0.5f;
            r.BreakPhase = L.phase;
            r.Energy = L.energy;
            r.TravelDir = Params.travelDir;
            r.CrestDir = Params.crestDir;
            r.CrestDistance = xi - L.crestShift;
            r.WhitewaterAmount = L.whitewater;
            r.SeabedDepth = groundDepth;
            r.WaveId = Params.id;
            r.LipWidth = L.lipW * L.envelope;
            r.LipHeight = L.lipH * L.envelope;
            r.FaceWidth = L.faceWidth;
            r.WaveHeight = L.H * L.envelope;

            // peel distance: find the crest coordinate where phase == 1 (breaking front), by scanning the profile
            r.PeelDistance = PeelDistance(s, tw);

            // tube test: a point riding the face under a thrown lip (headroom >= 0.35 m), or an airborne point below the lip underside
            float x = xi - L.crestShift;
            if (SurfWaveMath.LipUndersideY(L, x, out float yLip))
            {
                float yRel = worldPos.y - hA;
                float headroom = yLip - L.height;
                bool onFace = math.abs(yRel - L.height) < 0.8f;
                if (headroom > 0.35f && (onFace || yRel < yLip))
                {
                    r.InTube = true;
                    r.TubeDepth = 1f - x / math.max(0.05f, r.LipWidth);
                }
            }
            return r;
        }

        /// <summary>Signed distance along the crest from s to the breaking front (phase crosses 1). Positive = the unbroken side is ahead in +T.</summary>
        float PeelDistance(float s, float tw)
        {
            int n = profile.Length;
            float best = float.PositiveInfinity; float bestS = s;
            for (int i = 0; i < n; i++)
            {
                float si = Params.length * i / (n - 1);
                float tb = Params.BreakTime(profile[i].x);
                float ph = SurfWaveMath.Phase(Params, tw - tb);
                float dPh = math.abs(ph - 1f);
                if (dPh < best) { best = dPh; bestS = si; }
            }
            return s - bestS;
        }

        public float CrestPositionAlongD => Params.CrestOffset(WaveTime);

        // ---------------------------------------------------------------- VFX helpers (render time)
        /// <summary>Crest coordinate of the breaking front (phase == 1) at render time.</summary>
        public float PeelS()
        {
            float tw = WaveTime;
            int n = profile.Length;
            float best = float.PositiveInfinity; float bestS = 0f;
            for (int i = 0; i < n; i++)
            {
                float si = Params.length * i / (n - 1);
                float ph = SurfWaveMath.Phase(Params, tw - Params.BreakTime(profile[i].x));
                float d = math.abs(ph - 1f);
                if (d < best) { best = d; bestS = si; }
            }
            return bestS;
        }

        /// <summary>World position of a point on the lip curve (v in 0..1) at crest coordinate s; lipAmount 0 = no lip there.</summary>
        public Vector3 LipPointWorld(float s, float v, out float lipAmount, out Vector3 dir)
        {
            float tw = WaveTime;
            SurfLocal L = SurfWaveMath.Evaluate(Params, profile, s, 0f, tw);
            SurfWaveMath.LipPoint(L, v, out float2 pt, out float2 nrm, out float th);
            lipAmount = L.lipAmount;
            float crest = Params.CrestOffset(tw);
            float3 wp = Params.origin + Params.crestDir * s + Params.travelDir * (crest + pt.x);
            dir = (Vector3)Params.travelDir;
            return (Vector3)(wp + AmbientDeform(wp) + new float3(0f, pt.y, 0f));
        }

        /// <summary>World position on the face at (s, xi) at render time.</summary>
        public Vector3 FacePointWorld(float s, float xi, out SurfLocal L)
        {
            float tw = WaveTime;
            L = SurfWaveMath.Evaluate(Params, profile, s, xi, tw);
            float crest = Params.CrestOffset(tw);
            float3 wp = Params.origin + Params.crestDir * s + Params.travelDir * (crest + xi);
            return (Vector3)(wp + AmbientDeform(wp) + new float3(0f, L.height, 0f));
        }

        /// <summary>Ambient swell displacement applied by the GPU to a mesh point built on the undeformed plane.</summary>
        float3 AmbientDeform(float3 undeformed)
        {
            var amb = OceanAmbient.Instance;
            if (amb == null || !amb.Ready) return float3.zero;
            return OceanMath.Deformation(in amb.Params, amb.AmbientTime, undeformed, amb.GroundDepth(undeformed), out _);
        }
    }
}
