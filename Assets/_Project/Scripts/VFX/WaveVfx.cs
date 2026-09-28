using WavyBoard.Wave;
using UnityEngine;

namespace WavyBoard.VFX
{
    /// <summary>
    /// The white water that geometry cannot carry, emitted along the curl: spray blown back off the crest as it
    /// stands up (offshore wind), droplets thrown ahead of the lip, and the explosion where the lip lands on the flat
    /// water in front of the tube. Everything is placed on the live cross-sections of the wave, so it sits exactly on
    /// the water that is drawn.
    /// </summary>
    [RequireComponent(typeof(SurfWave))]
    public class WaveVfx : MonoBehaviour
    {
        public Material sprayMaterial;
        public Material foamMaterial;
        [Range(0f, 3f)] public float intensity = 1f;

        SurfWave wave;
        ParticleSystem spray, burst, feather;
        float sprayAcc, burstAcc, featherAcc;

        void Awake()
        {
            wave = GetComponent<SurfWave>();
            spray = ParticleFactory.Create(transform, "LipSpray", sprayMaterial, 1600, 0.45f, stretched: true, stretchSpeed: 0.07f);
            burst = ParticleFactory.Create(transform, "LipImpact", foamMaterial, 1400, 0.12f);
            feather = ParticleFactory.Create(transform, "CrestFeather", sprayMaterial, 900, 0.08f);
        }

        void LateUpdate()
        {
            if (!wave.IsAlive || Camera.main == null) return;
            var P = wave.Params;
            float sp = wave.PeelS();
            Vector3 D = P.travelDir, T = P.crestDir;
            float dt = Time.deltaTime;
            float size = Mathf.Clamp(P.height / 3.4f, 0.5f, 1.6f);
            Vector3 cam = Camera.main.transform.position;
            float camDist = Vector3.Distance(cam, wave.FacePointWorld(sp, 0f));
            float lod = camDist < 70f ? 1f : (camDist < 180f ? 0.45f : 0.12f);
            float k = intensity * lod * size;
            var ep = new ParticleSystem.EmitParams();

            // feathering: a veil of spray torn off the top of the crest by the wind, just ahead of the curl
            featherAcc += 70f * k * dt;
            int n = (int)featherAcc; featherAcc -= n;
            for (int i = 0; i < n; i++)
            {
                float s = sp + Random.Range(-4f, 18f);
                float tau = wave.LocalTau(s, out float lipless);
                if (tau < -1.8f || tau > 1.6f || lipless > 0.5f) continue;
                Vector3 p = wave.SectionPointWorld(s, Random.Range(9.6f, 10.8f), out var ri, out _);
                ep.position = p + Vector3.up * 0.1f + T * Random.Range(-0.5f, 0.5f);
                ep.velocity = -D * Random.Range(2f, 5.5f) + Vector3.up * Random.Range(1.5f, 4f) + Random.insideUnitSphere * 0.8f;
                ep.startSize = Random.Range(0.5f, 1.4f) * size;
                ep.startLifetime = Random.Range(0.9f, 1.8f);
                ep.startColor = new Color(1f, 1f, 1f, Random.Range(0.2f, 0.42f));
                feather.Emit(ep, 1);
            }

            // spray thrown ahead of the lip while it pitches and while the tube is open
            sprayAcc += 150f * k * dt;
            n = (int)sprayAcc; sprayAcc -= n;
            for (int i = 0; i < n; i++)
            {
                float s = sp - Random.Range(0f, 26f);
                float tau = wave.LocalTau(s, out float lipless);
                if (tau < 0.2f || tau > 4.2f || lipless > 0.5f) continue;
                Vector3 p = wave.SectionPointWorld(s, Random.Range(8.3f, 9.6f), out _, out _);
                ep.position = p + Random.insideUnitSphere * 0.3f;
                ep.velocity = D * (P.celerity * Random.Range(0.55f, 1.05f)) + Vector3.up * Random.Range(-0.5f, 2.5f) + T * Random.Range(-0.8f, 0.8f)
                              + Random.insideUnitSphere * 0.9f;
                ep.startSize = Random.Range(0.3f, 0.8f) * size;
                ep.startLifetime = Random.Range(0.5f, 1.0f);
                ep.startColor = new Color(1f, 1f, 1f, Random.Range(0.45f, 0.85f));
                spray.Emit(ep, 1);
            }

            // the explosion where the lip lands: foam and spray thrown up in front of the tube
            burstAcc += 110f * k * dt;
            n = (int)burstAcc; burstAcc -= n;
            for (int i = 0; i < n; i++)
            {
                float s = sp - Random.Range(2f, 34f);
                float tau = wave.LocalTau(s, out float lipless);
                if (tau < 1.3f || tau > 5.2f || lipless > 0.6f) continue;
                Vector3 p = wave.SectionPointWorld(s, Random.Range(7.9f, 8.6f), out _, out var L);
                float energy = Mathf.Clamp01((5.2f - tau) / 3f);
                ep.position = p + D * Random.Range(0f, 1.2f) + Random.insideUnitSphere * 0.6f;
                ep.velocity = D * (P.celerity * Random.Range(0.5f, 0.9f)) + Vector3.up * Random.Range(1.5f, 6.5f) * energy + Random.insideUnitSphere * 1.6f;
                ep.startSize = Random.Range(1.0f, 2.4f) * size;
                ep.startLifetime = Random.Range(1.1f, 2.2f);
                ep.startColor = new Color(1f, 1f, 1f, Random.Range(0.35f, 0.7f) * (0.4f + 0.6f * energy));
                burst.Emit(ep, 1);
            }
        }
    }
}
