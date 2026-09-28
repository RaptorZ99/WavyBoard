#ifndef BISCOTTE_WATER_CORE_INCLUDED
#define BISCOTTE_WATER_CORE_INCLUDED

// Shared by every pass of Biscotte/Water: the ambient sea (Gerstner swell, same formulas as Scripts/Ocean/OceanSwell.cs)
// and the vertex transform of the two water meshes. The ambient ocean mesh and the surf wave mesh go through the SAME
// displacement and the SAME shading, so where the surf wave flattens out at its borders the two surfaces are one.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

#define SWELL_MAX 6

// set every frame by OceanSurface (globals)
float4 _SwellA[SWELL_MAX];   // dir.x, dir.z, k, amplitude
float4 _SwellB[SWELL_MAX];   // omega, phase, horizontal amplitude QA, vertex LOD fade distance
float _SwellCount;
float _WaterTime;

CBUFFER_START(UnityPerMaterial)
    float4 _DeepColor;
    float4 _ShallowColor;
    float4 _SSSColor;
    float4 _FoamColor;
    float4 _RippleTex_ST;
    float4 _FoamTex_ST;
    float _RippleScale;
    float _RippleStrength;
    float _RippleSpeed;
    float _RippleFar;
    float _FoamScale;
    float _FoamSharpness;
    float _Smoothness;
    float _SunSpec;
    float _SSSStrength;
    float _SSSPower;
    float _SSSAmbient;
    float _TubeDark;
    float _AmbientBias;
    float _CrestGlow;
CBUFFER_END

// Displacement of the undeformed point x0. Each component fades out with the distance to the camera once the mesh is
// too coarse to carry it (both meshes fade the same way, so they keep matching); the pixel normal keeps it much longer.
float3 SwellDisplace(float2 x0, float dist)
{
    float3 d = 0;
    [unroll]
    for (int i = 0; i < SWELL_MAX; i++)
    {
        float on = i < (int)_SwellCount ? 1.0 : 0.0;
        float4 a = _SwellA[i];
        float4 b = _SwellB[i];
        float th = a.z * dot(a.xy, x0) - b.x * _WaterTime + b.y;
        float s, c;
        sincos(th, s, c);
        float lod = on * saturate(2.0 - 2.0 * dist / max(b.w, 1.0));
        d.xz += a.xy * (b.z * c * lod);
        d.y += a.w * s * lod;
    }
    return d;
}

// Pixel normal of the swell at the undeformed point x0 (GPU Gems 1, ch. 1); `crest` returns how close to a crest
// the point is (0..1), used to let a little light through the tops of the swell.
float3 SwellNormal(float2 x0, float dist, out float crest)
{
    float3 n = float3(0, 1, 0);
    float h = 0, amp = 1e-4;
    [unroll]
    for (int i = 0; i < SWELL_MAX; i++)
    {
        float on = i < (int)_SwellCount ? 1.0 : 0.0;
        float4 a = _SwellA[i];
        float4 b = _SwellB[i];
        float th = a.z * dot(a.xy, x0) - b.x * _WaterTime + b.y;
        float s, c;
        sincos(th, s, c);
        float lod = on * saturate(1.5 - dist / max(b.w * 8.0, 1.0));
        float wa = a.z * a.w * lod;
        n.x -= a.x * wa * c;
        n.z -= a.y * wa * c;
        n.y -= a.z * b.z * s * lod;
        h += a.w * s * on;
        amp += a.w * on;
    }
    crest = saturate(h / amp);
    return normalize(n);
}

struct WaterAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
#if defined(_WAVE_MESH)
    float4 tangentOS : TANGENT;   // surface direction in which the flow coordinate U grows
    float4 color : COLOR;         // r = thin water (translucency), g = foam, b = tube occlusion, a = face (0 flat .. 1 steep wave)
    float2 uv0 : TEXCOORD0;       // flow coordinates: world metres on the flats, carried along the sheet on the wave
    float2 uv1 : TEXCOORD1;       // wave travel direction D (xz)
#endif
};

struct WaterVertexData
{
    float3 positionWS;
    float3 normalWS;
    float2 x0;       // undeformed position on the sea plane
    float dist;      // horizontal distance to the camera
};

WaterVertexData WaterVertex(WaterAttributes IN)
{
    WaterVertexData o;
    float3 p = TransformObjectToWorld(IN.positionOS.xyz);
    o.x0 = p.xz;
    o.dist = distance(p.xz, _WorldSpaceCameraPos.xz);
    p += SwellDisplace(o.x0, o.dist);
#if !defined(_WAVE_MESH)
    // the ambient sea sits a hair under the surf wave mesh, which covers it wherever a wave is (no hole, no seam)
    p.y -= _AmbientBias * (1.0 + o.dist * 0.01);
#endif
    o.positionWS = p;
    o.normalWS = normalize(TransformObjectToWorldNormal(IN.normalOS));
    return o;
}

#endif
