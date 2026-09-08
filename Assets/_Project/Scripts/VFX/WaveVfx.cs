using Biscotte.Wave;
using UnityEngine;

namespace Biscotte.VFX
{
    /// <summary>Lip spray and foam ball for one surf wave, emitted around the breaking front (spec §13, Shuriken v1).</summary>
    [RequireComponent(typeof(SurfWave))]
    public class WaveVfx : MonoBehaviour
    {
        public Material sprayMaterial;
        public Material foamMaterial;
        [Range(0f, 3f)] public float intensity = 1f;

        SurfWave wave;
        ParticleSystem spray, foam, feather;
        float sprayAcc, foamAcc, featherAcc;

        void Awake()
        {
            wave = GetComponent<SurfWave>();
            spray = ParticleFactory.Create(transform, "LipSpray", sprayMaterial, 1500, 0.35f, stretched: true, stretchSpeed: 0.08f);
            foam = ParticleFactory.Create(transform, "FoamBall", foamMaterial, 900, 0.05f);
            feather = ParticleFactory.Create(transform, "CrestFeather", sprayMaterial, 600, 0.2f);
        }

        void LateUpdate()
        {
            if (!wave.IsAlive || Camera.main == null) return;
            float tw = wave.WaveTime;
            float sp = wave.PeelS();
            var P = wave.Params;
            Vector3 D = P.travelDir; Vector3 T = P.crestDir;
            float dt = Time.deltaTime;
            float camDist = Vector3.Distance(Camera.main.transform.position, wave.LipPointWorld(sp, 0.5f, out _, out _));
            float lod = camDist < 60f ? 1f : (camDist < 160f ? 0.5f : 0.15f);

            // lip spray: droplets thrown forward from the lip tip, on the breaking section behind the front
            sprayAcc += 140f * intensity * lod * dt;
            int n = (int)sprayAcc; sprayAcc -= n;
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < n; i++)
            {
                float s = sp - Random.Range(-1.5f, 8f);
                Vector3 p = wave.LipPointWorld(s, Random.Range(0.55f, 1f), out float lipAmount, out _);
                if (lipAmount < 0.1f) continue;
                ep.position = p + Random.insideUnitSphere * 0.25f;
                ep.velocity = D * (P.celerity * Random.Range(0.5f, 1.0f)) + Vector3.up * Random.Range(1.5f, 4.5f) + T * Random.Range(-1f, 1f) + Random.insideUnitSphere * 0.8f;
                ep.startSize = Random.Range(0.25f, 0.7f);
                ep.startLifetime = Random.Range(0.5f, 1.1f);
                ep.startColor = new Color(1f, 1f, 1f, Random.Range(0.5f, 0.9f));
                spray.Emit(ep, 1);
            }

            // foam ball where the lip lands
            foamAcc += 55f * intensity * lod * dt;
            n = (int)foamAcc; foamAcc -= n;
            for (int i = 0; i < n; i++)
            {
                float s = sp - Random.Range(0.5f, 10f);
                Vector3 p = wave.LipPointWorld(s, 1f, out float lipAmount, out _);
                if (lipAmount < 0.1f) continue;
                ep.position = p + D * Random.Range(-0.3f, 1.2f) + Random.insideUnitSphere * 0.5f;
                ep.velocity = D * (P.celerity * Random.Range(0.6f, 0.95f)) + Vector3.up * Random.Range(0.5f, 2.5f) + Random.insideUnitSphere * 1.2f;
                ep.startSize = Random.Range(1.2f, 2.6f);
                ep.startLifetime = Random.Range(1.2f, 2.2f);
                ep.startColor = new Color(1f, 1f, 1f, Random.Range(0.55f, 0.85f));
                foam.Emit(ep, 1);
            }

            // feathering spray along the crest ahead of the front (offshore-wind look)
            featherAcc += 35f * intensity * lod * dt;
            n = (int)featherAcc; featherAcc -= n;
            for (int i = 0; i < n; i++)
            {
                float s = sp + Random.Range(0f, 22f);
                Vector3 p = wave.FacePointWorld(s, Random.Range(-0.5f, 1.0f), out SurfLocal L);
                if (L.phase < 0.7f || L.phase > 1.6f) continue;
                ep.position = p + Vector3.up * 0.15f;
                ep.velocity = -D * Random.Range(0.5f, 2.5f) + Vector3.up * Random.Range(1f, 3f) + Random.insideUnitSphere * 0.6f;
                ep.startSize = Random.Range(0.3f, 0.8f);
                ep.startLifetime = Random.Range(0.6f, 1.4f);
                ep.startColor = new Color(1f, 1f, 1f, Random.Range(0.25f, 0.5f));
                feather.Emit(ep, 1);
            }
        }
    }
}
