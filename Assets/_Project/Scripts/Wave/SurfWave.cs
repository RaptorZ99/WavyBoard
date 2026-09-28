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
    public class SurfWave : MonoBehaviour
    {
        [Header("Mesh resolution")]
        [Tooltip("Cross-sections along the crest")] public int Ns = 320;
        const int kSectionSamples = 384;

        public SurfWaveParams Params;
        public bool IsAlive { get; private set; }
        public double SpawnTime { get; private set; }
        public float WaveTime => (float)(Time.timeAsDouble - SpawnTime);
        public float FixedWaveTime => (float)(Time.fixedTimeAsDouble - SpawnTime);
        public SurfSpotConfig Spot { get; private set; }

        NativeArray<float4> sections;
        NativeArray<float2> keys, vmap, scratch, sampleA, sampleB, sampleC;
        NativeArray<float3> pos;
        NativeArray<float4> col;
        NativeArray<float2> flow;
        NativeArray<SurfVertex> verts;
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

        public float CrestPositionAlongD => Params.CrestOffset(WaveTime);

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
        public void Spawn(SurfSpotConfig spot, float heightScale, int id, Vector3 aim)
        {
            EnsureBuffers();
            if (jobScheduled) { handle.Complete(); jobScheduled = false; }
            Spot = spot;
            Params = spot.BuildParams(id, heightScale, aim);
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

        static float BreakPhase(in WaveProfile.RowInput ri)
        {
            float tau = ri.tau;
            float pre = 0.25f + 0.6f * ri.shoal;
            float phase;
            if (tau < WaveProfile.TPre) phase = pre;
            else if (tau < 0f) phase = math.lerp(pre, 1f, (tau - WaveProfile.TPre) / -WaveProfile.TPre);
            else if (tau < WaveProfile.TBarrel) phase = 1f + tau / WaveProfile.TBarrel;
            else if (tau < WaveProfile.TBarrel2) phase = 2f + 0.15f * (tau - WaveProfile.TBarrel) / (WaveProfile.TBarrel2 - WaveProfile.TBarrel);
            else if (tau < WaveProfile.TMound) phase = 2.15f + 0.85f * (tau - WaveProfile.TBarrel2) / (WaveProfile.TMound - WaveProfile.TBarrel2);
            else phase = 3f;
            if (ri.lipless > 0f && tau >= 0f) phase = math.lerp(phase, 1f + 2f * math.saturate(tau / WaveProfile.TCrumbleMound), ri.lipless);
            return phase;
        }

        public WaterSample Sample(float3 worldPos, double time)
        {
            var ocean = OceanSurface.Instance;
            float tS = ocean != null ? ocean.SwellTime(time) : 0f;
            SwellParams swell = ocean != null ? ocean.Params : default;
            OceanSwell.Sample(swell, worldPos.xz, tS, out float hA, out float3 nA, out float3 vA, out float2 x0);

            float tw = (float)(time - SpawnTime);
            LocalCoords(new float3(x0.x, 0f, x0.y), tw, out float s, out float xi);
            var slice = new NativeSlice<float2>(sampleA);
            var L = EvalRow(s, tw, sampleA, out var ri);
            float y = WaveProfile.HeightAt(slice, L, xi, out bool hasRoof, out float roofY, out float hit);

            // normal from the analytic surface: across the section on this row, along the crest on two more rows
            const float e = 0.25f;
            float yx1 = WaveProfile.HeightAt(slice, L, xi + e, out _, out _, out _);
            float yx0 = WaveProfile.HeightAt(slice, L, xi - e, out _, out _, out _);
            var L1 = EvalRow(s + e, tw, sampleB, out _);
            var L0 = EvalRow(s - e, tw, sampleC, out _);
            float ys1 = WaveProfile.HeightAt(new NativeSlice<float2>(sampleB), L1, xi, out _, out _, out _);
            float ys0 = WaveProfile.HeightAt(new NativeSlice<float2>(sampleC), L0, xi, out _, out _, out _);
            float3 D = Params.travelDir, T = Params.crestDir;
            float3 nW = math.normalize(new float3(0f, 1f, 0f) - T * ((ys1 - ys0) / (2f * e)) - D * ((yx1 - yx0) / (2f * e)));
            float3 n = math.normalize(nW + (nA - new float3(0f, 1f, 0f)));

            int hi = math.clamp((int)hit, 0, nu - 1);
            float2 m = vmap[hi];
            float4 attr = SurfWaveMath.Attributes(m.x + m.y, xi, y, ri, L, Params.celerity);
            float phase = BreakPhase(ri);
            float cd = xi - L.xRef;
            float faceWidth = math.max(1f, sampleA[WaveProfile.VertexOfControl(WaveProfile.Foot)].x - L.xRef);
            float waveH = math.max(0f, L.yTop);

            WaterSample r = default;
            r.Height = hA + y;
            r.Normal = n;
            r.BreakPhase = phase;
            r.TravelDir = D;
            r.CrestDir = T;
            r.CrestDistance = cd;
            r.FaceWidth = faceWidth;
            r.WaveHeight = waveH;
            r.WhitewaterAmount = phase >= 2f || ri.lipless > 0.5f ? attr.y : 0f;
            r.SeabedDepth = 200f;
            r.WaveId = Params.id;
            r.PeelDistance = s - PeelS(tw);
            float pocket = math.saturate(1f - math.abs(cd - 0.35f * faceWidth) / (0.8f * faceWidth));
            r.Energy = pocket * math.smoothstep(0.35f, 1f, math.min(phase, 1f)) * (1f - math.smoothstep(2.2f, 2.8f, phase))
                       * math.saturate(waveH / 0.8f);
            if (L.curl && L.xTip > L.xRef + 0.3f)
            {
                r.LipWidth = L.xTip - L.xRef;
                r.LipHeight = math.max(0f, L.yTop - L.yTip);
            }

            // push of the moving water: the face carries a rider along D; the whitewater shoves him
            float hNorm = waveH > 0.05f ? math.saturate(y / waveH) : 0f;
            float push = phase >= 2.3f ? Params.celerity * 0.85f : Params.celerity * (0.55f + 0.45f * hNorm) * math.smoothstep(0.15f, 0.9f, phase);
            const float dt = 0.05f;
            var Lf = EvalRow(s, tw + dt, sampleB, out _);
            float yFuture = WaveProfile.HeightAt(new NativeSlice<float2>(sampleB), Lf, xi - Params.celerity * dt, out _, out _, out _);
            r.Velocity = D * push + new float3(0f, (yFuture - y) / dt, 0f) + vA * 0.5f;

            // the tube: riding the face under the thrown lip with some headroom, or flying under it
            if (hasRoof)
            {
                r.HasLipRoof = true;
                r.LipRoofY = hA + roofY;
                float yRel = worldPos.y - hA;
                float headroom = roofY - y;
                bool onFace = math.abs(yRel - y) < 0.8f;
                if (headroom > 0.35f && (onFace || yRel < roofY))
                {
                    r.InTube = true;
                    r.TubeDepth = math.saturate(1f - cd / math.max(0.05f, r.LipWidth));
                }
            }
            return r;
        }

        // ------------------------------------------------------------------ peel and VFX helpers (render time)
        /// <summary>Crest coordinate where the curl is pitching right now.</summary>
        public float PeelS() => PeelS(WaveTime);

        public float PeelS(float tw)
        {
            float d = tw - Params.firstBreakTime;
            if (d <= 0f) return 0f;
            int n = sections.Length;
            for (int i = 1; i < n; i++)
            {
                float s1 = math.lerp(Params.sMin, Params.sMax, (float)i / (n - 1));
                if (s1 <= 0f) continue;
                if (sections[i].x >= d)
                {
                    float s0 = math.lerp(Params.sMin, Params.sMax, (float)(i - 1) / (n - 1));
                    float d0 = sections[i - 1].x, d1 = sections[i].x;
                    return math.max(0f, math.lerp(s0, s1, math.saturate((d - d0) / math.max(1e-4f, d1 - d0))));
                }
            }
            return Params.sMax;
        }

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
