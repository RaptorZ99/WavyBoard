using WavyBoard.Ocean;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace WavyBoard.Wave
{
    /// <summary>
    /// One breaking wave. The mesh is a grid of cross-sections (<see cref="WaveProfile"/>) rebuilt every frame by two
    /// Burst jobs; the same profile code answers gameplay queries analytically (<see cref="Sample"/>), so the board
    /// rides exactly the water that is drawn. The mesh sits on the undeformed sea plane: the water shader adds the
    /// ambient swell to it exactly as it does to the open ocean, so the two are one surface at the wave's borders.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [DefaultExecutionOrder(-100)]
    public class SurfWave : MonoBehaviour, IWaterProbe
    {
        [Header("Mesh resolution")]
        [Tooltip("Cross-sections along the crest")] public int Ns = 320;
        const int kSectionSamples = 384;

        public SurfWaveParams Params;
        public bool IsAlive { get; private set; }
        public double SpawnTime { get; private set; }
        public float WaveTime => (float)(Time.timeAsDouble - SpawnTime);
        public SurfSpotConfig Spot { get; private set; }

        NativeArray<float4> sections;
        NativeArray<float2> keys, vmap, scratch, sampleA, sampleB, sampleC;
        NativeArray<float3> pos;
        NativeArray<float4> col;
        NativeArray<float2> flow;
        NativeArray<SurfVertex> verts;
        NativeArray<float3> probePts;
        NativeArray<WaterProbe> probeRes;
        NativeArray<float2> probeScratch;
        NativeArray<WaterSample> sampleOut;
        public const int ProbeCapacity = 64;
        static readonly int footVertex = WaveProfile.VertexOfControl(WaveProfile.Foot);
        Mesh mesh;
        MeshFilter mf;
        MeshRenderer mr;
        JobHandle handle;
        bool jobScheduled;
        int nu, vertexCount;

        static readonly VertexAttributeDescriptor[] k_Layout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 2),
        };

        // ------------------------------------------------------------------ gameplay-facing landmarks
        /// <summary>Where a rider has to be to take this wave: just seaward of the peak, a moment before it pitches.</summary>
        public Vector3 AimPoint
        {
            get
            {
                var s = Spot;
                float lineupS = s != null ? s.lineupS : 6f, back = s != null ? s.lineupBack : 4f;
                return (Vector3)(Params.origin + Params.crestDir * lineupS + Params.travelDir * (Params.CrestOffset(Params.firstBreakTime) - back));
            }
        }

        /// <summary>Seconds until the wave starts to stand up. Until then it is a uniform round swell and where it will
        /// break can still move without anything visible changing.</summary>
        public float SwellLead => Params.ShoalEnd - Params.shoalDuration - WaveTime;

        /// <summary>World position of the crest line along D right now (dot with the travel direction).</summary>
        public float CrestAlongD => math.dot(Params.origin, Params.travelDir) + Params.CrestOffset(WaveTime);

        /// <summary>
        /// How much this wave owns a world point inside its footprint: 0 on its crest line, 1 around the foot of its face
        /// (or the end of its back slope), more out on its flats. Where two footprints overlap, the water belongs to the
        /// wave the point is nearest to, in units of that wave's own size — never simply to the newest one (a rider on
        /// the face of a wave must not fall through to the flat in front of the one behind).
        /// </summary>
        public bool Ownership(float3 worldPos, out float score)
        {
            score = float.MaxValue;
            if (!IsAlive) return false;
            LocalCoords(worldPos, (float)(SamplingTime - SpawnTime), out float s, out float xi);
            if (s <= Params.sMin || s >= Params.sMax || xi <= Params.xiMin || xi >= Params.xiMax) return false;
            float H = math.max(0.5f, Params.height);
            score = xi >= 0f ? xi / (3.5f * H + 4f) : -xi / (1.6f * H + 4f);
            return true;
        }

        // ------------------------------------------------------------------ lifecycle
        void Awake()
        {
            EnsureBuffers();
            if (!IsAlive) mr.enabled = false;
        }

        void OnEnable() { EnsureBuffers(); }

        // native buffers do not survive a domain reload (the mesh does): release them whenever the component goes away
        void OnDisable() { ReleaseBuffers(); }

        void EnsureBuffers()
        {
            if (keys.IsCreated && mesh != null && mr != null) return;
            mf = GetComponent<MeshFilter>();
            mr = GetComponent<MeshRenderer>();
            if (keys.IsCreated && mesh != null) return;
            ReleaseBuffers();
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;
            nu = WaveProfile.VertexCount;
            vertexCount = Ns * nu;
            keys = WaveProfile.CreateKeys(Allocator.Persistent);
            vmap = WaveProfile.CreateVertexMap(Allocator.Persistent);
            sections = new NativeArray<float4>(kSectionSamples, Allocator.Persistent);
            scratch = new NativeArray<float2>(vertexCount, Allocator.Persistent);
            sampleA = new NativeArray<float2>(nu, Allocator.Persistent);
            sampleB = new NativeArray<float2>(nu, Allocator.Persistent);
            sampleC = new NativeArray<float2>(nu, Allocator.Persistent);
            pos = new NativeArray<float3>(vertexCount, Allocator.Persistent);
            col = new NativeArray<float4>(vertexCount, Allocator.Persistent);
            flow = new NativeArray<float2>(vertexCount, Allocator.Persistent);
            verts = new NativeArray<SurfVertex>(vertexCount, Allocator.Persistent);
            probePts = new NativeArray<float3>(ProbeCapacity, Allocator.Persistent);
            probeRes = new NativeArray<WaterProbe>(ProbeCapacity, Allocator.Persistent);
            probeScratch = new NativeArray<float2>(nu, Allocator.Persistent);
            sampleOut = new NativeArray<WaterSample>(1, Allocator.Persistent);
            BuildTopology();
        }

        void OnDestroy()
        {
            ReleaseBuffers();
            if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        }

        void ReleaseBuffers()
        {
            if (jobScheduled) handle.Complete();
            jobScheduled = false;
            if (keys.IsCreated) keys.Dispose();
            if (vmap.IsCreated) vmap.Dispose();
            if (sections.IsCreated) sections.Dispose();
            if (scratch.IsCreated) scratch.Dispose();
            if (sampleA.IsCreated) sampleA.Dispose();
            if (sampleB.IsCreated) sampleB.Dispose();
            if (sampleC.IsCreated) sampleC.Dispose();
            if (pos.IsCreated) pos.Dispose();
            if (col.IsCreated) col.Dispose();
            if (flow.IsCreated) flow.Dispose();
            if (verts.IsCreated) verts.Dispose();
            if (probePts.IsCreated) probePts.Dispose();
            if (probeRes.IsCreated) probeRes.Dispose();
            if (probeScratch.IsCreated) probeScratch.Dispose();
            if (sampleOut.IsCreated) sampleOut.Dispose();
        }

        void BuildTopology()
        {
            if (mesh == null) mesh = new Mesh { name = "SurfWave", hideFlags = HideFlags.DontSave };
            mesh.Clear();
            mesh.MarkDynamic();
            mesh.SetVertexBufferParams(vertexCount, k_Layout);
            int quads = (Ns - 1) * (nu - 1);
            var indices = new NativeArray<uint>(quads * 6, Allocator.Temp);
            int k = 0;
            for (int r = 0; r < Ns - 1; r++)
            for (int c = 0; c < nu - 1; c++)
            {
                // a -> b runs along the section (toward the sea on the flats), a -> cc along the crest (+T):
                // (a, cc, b) is clockwise seen from the air side
                uint a = (uint)(r * nu + c), b = a + 1, cc = (uint)((r + 1) * nu + c), d = cc + 1;
                indices[k++] = a; indices[k++] = cc; indices[k++] = b;
                indices[k++] = cc; indices[k++] = d; indices[k++] = b;
            }
            mesh.SetIndexBufferParams(indices.Length, IndexFormat.UInt32);
            mesh.SetIndexBufferData(indices, 0, 0, indices.Length, MeshUpdateFlags.DontValidateIndices);
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, indices.Length), MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);
            indices.Dispose();
            mf.sharedMesh = mesh;
        }

        /// <summary>Spawns a wave aimed at a rider at <paramref name="aim"/>: its peak pitches just in front of him.</summary>
        public void Spawn(SurfSpotConfig spot, float heightScale, int id, Vector3 aim, float celerityScale = -1f, float extraLead = 0f)
        {
            EnsureBuffers();
            if (jobScheduled) { handle.Complete(); jobScheduled = false; }
            Spot = spot;
            Params = spot.BuildParams(id, heightScale, aim, celerityScale, extraLead);
            spot.FillSections(sections, Params);
            SpawnTime = Time.timeAsDouble;
            IsAlive = true;
            mr.enabled = true;
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            WaterSurfaceComposite.Instance?.Register(this);
        }

        /// <summary>Editor/preview helper: builds the mesh synchronously at wave time tw (no Play mode needed).</summary>
        public void BuildNow(SurfSpotConfig spot, float tw, float heightScale, Vector3 aim, int id = 999)
        {
            EnsureBuffers();
            if (jobScheduled) { handle.Complete(); jobScheduled = false; }
            Spot = spot;
            Params = spot.BuildParams(id, heightScale, aim);
            spot.FillSections(sections, Params);
            SpawnTime = Time.timeAsDouble - tw;
            IsAlive = true;
            mr.enabled = true;
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Schedule(tw).Complete();
            Upload(tw);
        }

        /// <summary>
        /// Moves where the wave will break without moving the water: dS metres along the crest (the whole round swell
        /// slides sideways) and dD metres along its travel (it simply breaks dD / c later). Only meant for a wave that
        /// is still a uniform swell (<see cref="SwellLead"/> &gt; 0).
        /// </summary>
        public void Retarget(float dS, float dD)
        {
            Params.origin += Params.crestDir * dS;
            float dt = dD / math.max(0.5f, Params.celerity);
            Params.firstBreakTime += dt;
            Params.endTime += dt;
        }

        /// <summary>Fades the wave out over the next <paramref name="fade"/> seconds, at least the 6 s of the amplitude
        /// envelope (<see cref="SurfWaveMath.Row"/>) so it never pops; never extends its life.</summary>
        public void Retire(float fade)
        {
            float end = WaveTime + math.max(6f, fade);
            if (end < Params.endTime) Params.endTime = end;
        }

        public void Despawn()
        {
            if (jobScheduled) { handle.Complete(); jobScheduled = false; }
            IsAlive = false;
            mr.enabled = false;
            WaterSurfaceComposite.Instance?.Unregister(this);
        }

        JobHandle Schedule(float tw)
        {
            var rows = new SurfWaveRowJob
            {
                P = Params, Sections = sections, Keys = keys, VMap = vmap, TimeW = tw, Ns = Ns, Nu = nu,
                Scratch = scratch, Pos = pos, Col = col, Flow = flow,
            };
            var vtx = new SurfWaveVertexJob
            {
                Pos = pos, Col = col, Flow = flow, TravelXZ = Params.travelDir.xz, Ns = Ns, Nu = nu, Verts = verts,
            };
            var h = rows.Schedule(Ns, 4);
            return vtx.Schedule(vertexCount, 256, h);
        }

        void Update()
        {
            if (!IsAlive) return;
            float tw = WaveTime;
            if (tw > Params.endTime) { Despawn(); return; }
            handle = Schedule(tw);
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

        void Upload(float tw)
        {
            mesh.SetVertexBufferData(verts, 0, 0, vertexCount, 0,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);
            var P = Params;
            float crest = P.CrestOffset(tw);
            float sMid = 0.5f * (P.sMin + P.sMax), xMid = 0.5f * (P.xiMin + P.xiMax);
            float3 c = P.origin + P.crestDir * sMid + P.travelDir * (crest + xMid) + new float3(0f, P.height * 0.6f, 0f);
            float3 ext = math.abs(P.crestDir) * (0.5f * (P.sMax - P.sMin) + 1f) + math.abs(P.travelDir) * (0.5f * (P.xiMax - P.xiMin) + 1f)
                         + new float3(1f, P.height * 1.3f + 2f, 1f);
            mesh.bounds = new Bounds(c, ext * 2f);
        }

        // ------------------------------------------------------------------ gameplay sampling
        public void LocalCoords(float3 worldPos, float tw, out float s, out float xi)
        {
            float3 rel = worldPos - Params.origin;
            s = math.dot(rel, Params.crestDir);
            xi = math.dot(rel, Params.travelDir) - Params.CrestOffset(tw);
        }

        /// <summary>Time used for CPU sampling: fixed time inside FixedUpdate, render time otherwise.</summary>
        double SamplingTime => Time.inFixedTimeStep ? Time.fixedTimeAsDouble : Time.timeAsDouble;

        public bool Contains(float3 worldPos)
        {
            if (!IsAlive) return false;
            LocalCoords(worldPos, (float)(SamplingTime - SpawnTime), out float s, out float xi);
            return s > Params.sMin && s < Params.sMax && xi > Params.xiMin && xi < Params.xiMax;
        }

        /// <summary>Evaluates the cross-section at s into buf and returns its inputs and landmarks.</summary>
        WaveProfile.Landmarks EvalRow(float s, float tw, NativeArray<float2> buf, out WaveProfile.RowInput ri)
        {
            ri = SurfWaveMath.Row(Params, sections, s, tw, out _);
            var slice = new NativeSlice<float2>(buf);
            WaveProfile.Section(keys, vmap, ri, slice);
            return WaveProfile.Find(slice);
        }

        /// <summary>
        /// Everything gameplay needs about the water at a world point (see <see cref="SurfWaveSampleJob"/>, which holds
        /// the evaluation): Burst, run synchronously, main thread only.
        /// </summary>
        public WaterSample Sample(float3 worldPos, double time)
        {
            EnsureBuffers();
            var ocean = OceanSurface.Instance;
            new SurfWaveSampleJob
            {
                P = Params, Sections = sections, Keys = keys, VMap = vmap,
                Swell = ocean != null ? ocean.Params : default, SwellT = ocean != null ? ocean.SwellTime(time) : 0f,
                TimeW = (float)(time - SpawnTime), Point = worldPos, FootVertex = footVertex,
                Out = sampleOut, A = sampleA, B = sampleB, C = sampleC,
            }.Run();
            return sampleOut[0];
        }

        // ------------------------------------------------------------------ camera queries (main thread, any time)

        /// <summary>The inside of the barrel at one crest coordinate: where a camera can sit under the lip.</summary>
        public struct TubeSlice
        {
            public float s;          // crest coordinate
            public float xWall;      // back wall of the tube (xi, m)
            public float xTip;       // lip tip (xi, m)
            public float x;          // the chosen point across the cavity (xi, m)
            public float yFloor;     // face under it (m above the ambient sea)
            public float yRoof;      // lip underside above it (m above the ambient sea)
            public float Headroom => yRoof - yFloor;
        }

        /// <summary>Crest coordinate, distance from the crest line along D and ambient sea height at a world point.</summary>
        public void WaveCoords(float3 worldPos, double time, out float s, out float xi, out float ambientHeight)
        {
            var ocean = OceanSurface.Instance;
            float2 x0 = worldPos.xz;
            ambientHeight = 0f;
            if (ocean != null)
            {
                float tS = ocean.SwellTime(time);
                x0 = OceanSwell.Undeform(ocean.Params, worldPos.xz, tS);
                ambientHeight = OceanSwell.Displacement(ocean.Params, x0, tS).y;
            }
            LocalCoords(new float3(x0.x, 0f, x0.y), (float)(time - SpawnTime), out s, out xi);
        }

        /// <summary>True when the world point is inside this wave's water (under the face, inside the lip, under the
        /// sea next to it). The inside of the tube is air.</summary>
        public bool IsInsideWater(float3 worldPos, double time) => ProbeOne(worldPos, time).inside;

        /// <summary>One water probe at a world point (Burst).</summary>
        public WaterProbe ProbeOne(float3 worldPos, double time)
        {
            EnsureBuffers();
            probePts[0] = worldPos;
            RunProbe(probePts, probeRes, 1, time);
            return probeRes[0];
        }

        public int Capacity => ProbeCapacity;
        public NativeArray<float3> ProbePoints { get { EnsureBuffers(); return probePts; } }
        public NativeArray<WaterProbe> ProbeResults { get { EnsureBuffers(); return probeRes; } }
        public void Probe(int count, double time) => RunProbe(probePts, probeRes, count, time);

        /// <summary>Runs a batch of probes against this wave, synchronously, in Burst.</summary>
        public void RunProbe(NativeArray<float3> points, NativeArray<WaterProbe> results, int count, double time)
        {
            var ocean = OceanSurface.Instance;
            new SurfWaveProbeJob
            {
                P = Params, Sections = sections, Keys = keys, VMap = vmap,
                Swell = ocean != null ? ocean.Params : default, SwellT = ocean != null ? ocean.SwellTime(time) : 0f,
                TimeW = (float)(time - SpawnTime), Count = math.min(count, math.min(points.Length, results.Length)),
                Points = points, Results = results, Scratch = probeScratch,
            }.Run();
        }

        /// <summary>
        /// The open barrel at crest coordinate s, if there is one: a thrown lip with at least <paramref name="minHeadroom"/>
        /// metres under it. The chosen point sits across the cavity toward <paramref name="preferX"/> (the rider's line),
        /// kept clear of the back wall and of the lip curtain.
        /// </summary>
        public bool TryTubeSlice(float s, float preferX, double time, float minHeadroom, out TubeSlice t)
        {
            t = default;
            t.s = s;
            if (s <= Params.sMin || s >= Params.sMax) return false;
            var L = EvalRow(s, (float)(time - SpawnTime), sampleB, out _);
            if (!L.curl || L.xTip < L.xRef + 1f) return false;
            var slice = new NativeSlice<float2>(sampleB);
            float margin = 0.2f * (L.xTip - L.xRef);
            float x = math.clamp(preferX, L.xRef + margin, L.xTip - margin);
            float yF = WaveProfile.HeightAt(slice, L, x, out bool roof, out float yR, out _);
            if (!roof || yR - yF < minHeadroom) return false;
            t.xWall = L.xRef; t.xTip = L.xTip; t.x = x; t.yFloor = yF; t.yRoof = yR;
            return true;
        }

        /// <summary>World position of a point given in wave coordinates (s, xi, height above the ambient sea).</summary>
        public Vector3 WaveToWorld(float s, float xi, float y, double time) => ToWorld(s, xi, y, (float)(time - SpawnTime));

        // ------------------------------------------------------------------ peel and VFX helpers (render time)
        /// <summary>Crest coordinate where the curl is pitching right now.</summary>
        public float PeelS() => PeelS(WaveTime);

        public float PeelS(float tw) => SurfWaveMath.PeelS(Params, sections, tw);

        /// <summary>Stage of the crest at s: time since it started to pitch (s, scaled), and how much it crumbles.</summary>
        public float LocalTau(float s, out float lipless)
        {
            var ri = SurfWaveMath.Row(Params, sections, s, WaveTime, out _);
            lipless = ri.lipless;
            return ri.tau;
        }

        /// <summary>World position of a point of the section at s, given by its control-space coordinate (0 front flat ..
        /// 14 back flat; 8 = lip tip, 10 = top of the lip). Includes the ambient swell.</summary>
        public Vector3 SectionPointWorld(float s, float controlCoord, out WaveProfile.RowInput ri, out WaveProfile.Landmarks L)
        {
            float tw = WaveTime;
            L = EvalRow(s, tw, sampleA, out ri);
            int best = 0; float bd = float.MaxValue;
            for (int j = 0; j < nu; j++)
            {
                float2 m = vmap[j];
                float d = math.abs(m.x + m.y - controlCoord);
                if (d < bd) { bd = d; best = j; }
            }
            float2 p = sampleA[best];
            return ToWorld(s, p.x, p.y, tw);
        }

        /// <summary>World position of the face (the floor a rider stands on) at (s, xi).</summary>
        public Vector3 FacePointWorld(float s, float xi)
        {
            float tw = WaveTime;
            var L = EvalRow(s, tw, sampleA, out _);
            float y = WaveProfile.HeightAt(new NativeSlice<float2>(sampleA), L, xi, out _, out _, out _);
            return ToWorld(s, xi, y, tw);
        }

        Vector3 ToWorld(float s, float x, float y, float tw)
        {
            float3 wp = Params.origin + Params.crestDir * s + Params.travelDir * (Params.CrestOffset(tw) + x);
            var ocean = OceanSurface.Instance;
            float3 d = ocean != null ? OceanSwell.Displacement(ocean.Params, wp.xz, ocean.RenderTime) : float3.zero;
            return (Vector3)(wp + d + new float3(0f, y, 0f));
        }

        /// <summary>Min / max / mean of the vertex attributes (thin, foam, ao, face) and the height range, for diagnostics.</summary>
        public string VertexStats()
        {
            if (!verts.IsCreated) return "no verts";
            float4 mn = new float4(float.MaxValue), mx = new float4(float.MinValue), sum = float4.zero;
            float ymin = float.MaxValue, ymax = float.MinValue;
            int bad = 0;
            for (int i = 0; i < vertexCount; i++)
            {
                var v = verts[i];
                if (!math.all(math.isfinite(v.pos)) || !math.all(math.isfinite(v.nrm))) { bad++; continue; }
                mn = math.min(mn, v.col); mx = math.max(mx, v.col); sum += v.col;
                ymin = math.min(ymin, v.pos.y); ymax = math.max(ymax, v.pos.y);
            }
            return $"verts {vertexCount} (non-finite {bad}) | col min {mn} max {mx} mean {sum / vertexCount} | y [{ymin:0.00}, {ymax:0.00}] | peelS {PeelS():0.0}";
        }
    }
}
