using WavyBoard.Rider;
using UnityEngine;

namespace WavyBoard.VFX
{
    /// <summary>Rail spray, wake and splashes for the rider (Shuriken, manual emission).</summary>
    public class RiderVfx : MonoBehaviour
    {
        public RiderController rider;
        public Material sprayMaterial;
        public Material foamMaterial;

        ParticleSystem spray, wake, splash;
        float sprayAcc, wakeAcc;

        void Awake()
        {
            if (rider == null) rider = GetComponent<RiderController>();
            spray = ParticleFactory.Create(transform, "RailSpray", sprayMaterial, 800, 0.6f, stretched: true, stretchSpeed: 0.06f);
            wake = ParticleFactory.Create(transform, "Wake", foamMaterial, 500, 0f);
            splash = ParticleFactory.Create(transform, "Splash", sprayMaterial, 400, 0.8f, stretched: true, stretchSpeed: 0.05f);
        }

        void OnEnable() { if (rider != null) { rider.OnEvent += OnEvent; rider.OnLanded += OnLanded; } }
        void OnDisable() { if (rider != null) { rider.OnEvent -= OnEvent; rider.OnLanded -= OnLanded; } }

        void OnLanded(float impact, bool clean) => Splash(clean ? 40 : 55, 0.9f + Mathf.Clamp01(impact / 10f) * 0.6f);

        void LateUpdate()
        {
            if (rider == null) return;
            float dt = Time.deltaTime;
            Vector3 pos = rider.RenderPosition;
            Vector3 F = rider.BoardForward, R = rider.BoardRight;
            var ep = new ParticleSystem.EmitParams();

            if (rider.State == RiderState.Ride)
            {
                float speed = rider.Speed;
                float lean = Mathf.Abs(rider.Lean);
                float rate = Mathf.Clamp01((speed - 3f) / 8f) * (0.4f + 1.6f * lean + rider.RailSlip * 0.3f) * 120f;
                sprayAcc += rate * dt;
                int n = (int)sprayAcc; sprayAcc -= n;
                float side = rider.Lean >= 0f ? 1f : -1f;
                for (int i = 0; i < n; i++)
                {
                    Vector3 rail = pos + R * (side * 0.28f) + F * Random.Range(-0.5f, 0.3f) + Vector3.up * 0.05f;
                    ep.position = rail;
                    ep.velocity = -F * Random.Range(1f, 3f) + R * (side * Random.Range(1.5f, 4f) * (0.5f + lean)) + Vector3.up * Random.Range(1f, 3f + 2f * lean) + Random.insideUnitSphere * 0.7f;
                    ep.startSize = Random.Range(0.15f, 0.45f);
                    ep.startLifetime = Random.Range(0.4f, 0.9f);
                    ep.startColor = new Color(1f, 1f, 1f, Random.Range(0.4f, 0.8f));
                    spray.Emit(ep, 1);
                }
                wakeAcc += Mathf.Clamp01((speed - 2f) / 6f) * 25f * dt;
                n = (int)wakeAcc; wakeAcc -= n;
                for (int i = 0; i < n; i++)
                {
                    ep.position = pos - F * 0.6f + R * Random.Range(-0.3f, 0.3f) + Vector3.up * 0.03f;
                    ep.velocity = rider.Velocity * 0.15f;
                    ep.startSize = Random.Range(0.35f, 0.8f);
                    ep.startLifetime = Random.Range(0.6f, 1.2f);
                    ep.startColor = new Color(1f, 1f, 1f, Random.Range(0.18f, 0.35f));
                    wake.Emit(ep, 1);
                }
            }
        }

        void OnEvent(string e)
        {
            int count = 0; float power = 1f;
            if (e.StartsWith("Wipeout")) { count = 70; power = 1.6f; }
            else if (e == "Envol") { count = 30; power = 1f; }
            else if (e == "Take-off" || e == "Duck dive") { count = 25; power = 0.9f; }
            Splash(count, power);
        }

        void Splash(int count, float power)
        {
            if (count == 0) return;
            var ep = new ParticleSystem.EmitParams();
            Vector3 pos = rider.RenderPosition;
            for (int i = 0; i < count; i++)
            {
                ep.position = pos + Random.insideUnitSphere * 0.5f;
                ep.velocity = Vector3.up * Random.Range(2f, 5f) * power + Random.insideUnitSphere * 2.5f * power;
                ep.startSize = Random.Range(0.2f, 0.6f) * power;
                ep.startLifetime = Random.Range(0.5f, 1.1f);
                ep.startColor = new Color(1f, 1f, 1f, Random.Range(0.5f, 0.9f));
                splash.Emit(ep, 1);
            }
        }
    }
}
