using UnityEngine;

namespace Biscotte.VFX
{
    /// <summary>Creates world-space particle systems driven by manual emission (EmitParams).</summary>
    public static class ParticleFactory
    {
        public static ParticleSystem Create(Transform parent, string name, Material material, int maxParticles, float gravity, bool stretched = false, float stretchSpeed = 0.15f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;
            main.startSpeed = 0f;
            main.startLifetime = 1f;
            main.startSize = 1f;
            main.gravityModifier = gravity;
            main.playOnAwake = false;
            main.loop = true;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);
            var sol = ps.sizeOverLifetime; sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.4f, 1f), new Keyframe(1f, 1.4f)));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material;
            r.renderMode = stretched ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            r.velocityScale = stretched ? stretchSpeed : 0f;
            r.lengthScale = stretched ? 1.2f : 1f;
            r.sortMode = ParticleSystemSortMode.Distance;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }
    }
}
