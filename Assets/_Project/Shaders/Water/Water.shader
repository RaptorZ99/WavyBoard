Shader "WavyBoard/Water"
{
    // One water shader for the whole sea: the ambient ocean mesh (OceanSurface) and every surf wave mesh (SurfWave,
    // keyword _WAVE_MESH). Opaque: the translucency of the lip and of the upper face is scattering light, not
    // transparency, so nothing behind the wave can ever show through it.
    Properties
    {
        [Header(Water body)]
        _DeepColor("Deep", Color) = (0.02, 0.12, 0.22, 1)
        _ShallowColor("Thin water (face, lip)", Color) = (0.05, 0.42, 0.58, 1)
        _SSSColor("Light through the water", Color) = (0.2, 0.8, 0.9, 1)
        _SSSStrength("Sun through the lip", Range(0, 8)) = 2.5
        _SSSPower("Sun through the lip: tightness", Range(1, 16)) = 4.0
        _SSSAmbient("Sky through thin water", Range(0, 3)) = 0.45
        _CrestGlow("Glow of the swell tops", Range(0, 1)) = 0.12
        _TubeDark("Inside of the barrel", Range(0, 1)) = 0.6

        [Header(Surface)]
        _Smoothness("Smoothness", Range(0.5, 1)) = 0.96
        _SunSpec("Sun glitter", Range(0, 4)) = 1.0
        [NoScaleOffset] _RippleTex("Ripples (normal map)", 2D) = "bump" {}
        _RippleScale("Ripples: tiles per metre", Float) = 0.14
        _RippleStrength("Ripples: strength", Range(0, 2)) = 0.4
        _RippleSpeed("Ripples: drift (m/s)", Float) = 0.35
        _RippleFar("Ripples: strength far away", Range(0, 1)) = 0.3

        [Header(Foam)]
        _FoamColor("Foam", Color) = (0.94, 0.97, 1.0, 1)
        [NoScaleOffset] _FoamTex("Foam masks (R lace, G dense, B breakup)", 2D) = "black" {}
        _FoamScale("Foam: tiles per metre", Float) = 0.1667
        _FoamSharpness("Foam: edge sharpness", Range(1, 12)) = 5.0

        [Header(Ambient mesh only)]
        _AmbientBias("Sink under the wave mesh (m)", Float) = 0.05

        [Toggle(_WAVE_MESH)] _WaveMesh("Surf wave mesh", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "IgnoreProjector" = "True" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _WAVE_MESH

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "WaterCore.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_RippleTex); SAMPLER(sampler_RippleTex);
            TEXTURE2D(_FoamTex); SAMPLER(sampler_FoamTex);

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 x0Dist : TEXCOORD2;     // undeformed xz, distance, fog
                float4 flowFrame : TEXCOORD3;  // flow xz, travel direction D (xz)
                float4 color : TEXCOORD4;
                float3 tangentWS : TEXCOORD5;
            };

            Varyings Vert(WaterAttributes IN)
            {
                WaterVertexData w = WaterVertex(IN);
                Varyings o;
                o.positionWS = w.positionWS;
                o.positionCS = TransformWorldToHClip(w.positionWS);
                o.normalWS = w.normalWS;
                o.x0Dist = float4(w.x0, w.dist, ComputeFogFactor(o.positionCS.z));
            #if defined(_WAVE_MESH)
                o.flowFrame = float4(IN.uv0, IN.uv1);
                o.color = IN.color;
                o.tangentWS = TransformObjectToWorldDir(IN.tangentOS.xyz);
            #else
                o.flowFrame = float4(w.x0, 1, 0);
                o.color = 0;
                o.tangentWS = float3(1, 0, 0);
            #endif
                return o;
            }

            // How much of the pixel is foam, from the foam amount carried by the mesh. The masks are thresholded, never
            // multiplied: a little foam is thin filaments of lace, a lot of foam is solid white, and in between the lace
            // thickens into dense foam with holes. No blurry white smear anywhere.
            float FoamCover(float2 flow, float amount, float dist)
            {
                if (amount <= 0.002) return 0;
                float2 uv = flow * _FoamScale;
                float4 f1 = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, uv);
                float4 f2 = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, uv * 0.37 + float2(0.31, 0.77));
                float a = saturate(amount * (0.7 + 0.6 * f2.b));
                float lace = max(f1.r, f2.r * 0.8);
                float dense = f1.g * 0.6 + f2.g * 0.4;
                float pat = lerp(lace, max(lace, dense), smoothstep(0.3, 0.8, a));
                float sharp = _FoamSharpness / (1.0 + dist * 0.02);        // softer far away: no shimmering
                return saturate((pat - (1.0 - a)) * sharp + smoothstep(0.75, 1.0, a));
            }

            half4 Frag(Varyings IN, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 posWS = IN.positionWS;
                float dist = IN.x0Dist.z;
                float3 V = GetWorldSpaceNormalizeViewDir(posWS);

                float3 nMesh = normalize(IN.normalWS);
                if (!IS_FRONT_VFACE(face, true, false)) nMesh = -nMesh;
                float crest;
                float3 nSwell = SwellNormal(IN.x0Dist.xy, dist, crest);
                float3 N = normalize(nMesh + (nSwell - float3(0, 1, 0)));

                float thin = IN.color.r;
                float foamAmt = IN.color.g;
                float ao = IN.color.b;
                float faceK = IN.color.a;

                // ripples: two scales of the same tileable normal map, drifting, in world-anchored flow coordinates
                float2 flow = IN.flowFrame.xy;
                float tt = _WaterTime * _RippleSpeed;
                float2 uvA = flow * _RippleScale + float2(0.71, 0.29) * tt * _RippleScale;
                float2 fB = float2(flow.x * 0.8 - flow.y * 0.6, flow.x * 0.6 + flow.y * 0.8);
                float2 uvB = fB * (_RippleScale * 2.63) - float2(0.23, 0.87) * tt * _RippleScale * 1.9;
                float3 rA = UnpackNormal(SAMPLE_TEXTURE2D(_RippleTex, sampler_RippleTex, uvA));
                float3 rB = UnpackNormal(SAMPLE_TEXTURE2D(_RippleTex, sampler_RippleTex, uvB));
                float2 gB = float2(rB.x * 0.8 + rB.y * 0.6, -rB.x * 0.6 + rB.y * 0.8);   // back to flow space
                float2 g = rA.xy + gB * 0.7;
                float strength = _RippleStrength * lerp(1.0, _RippleFar, saturate(dist / 350.0)) * (1.0 - 0.75 * saturate(foamAmt * 1.5));
            #if defined(_WAVE_MESH)
                float2 D = IN.flowFrame.zw;
                float2 T = float2(D.y, -D.x);
                float3 tU = IN.tangentWS - N * dot(N, IN.tangentWS);
                tU *= rsqrt(max(dot(tU, tU), 1e-6));
                float3 T3 = float3(T.x, 0, T.y);
                float3 tV = T3 - N * dot(N, T3);
                tV *= rsqrt(max(dot(tV, tV), 1e-6));
                float gU = g.x * D.x + g.y * D.y;
                float gV = g.x * T.x + g.y * T.y;
                N = normalize(N + (gU * tU + gV * tV) * strength);
            #else
                float3 tX = normalize(float3(1, 0, 0) - N * N.x);
                float3 tZ = normalize(float3(0, 0, 1) - N * N.z);
                N = normalize(N + (g.x * tX + g.y * tZ) * strength);
            #endif

                float4 shadowCoord = TransformWorldToShadowCoord(posWS);
                Light sun = GetMainLight(shadowCoord);
                float3 L = sun.direction;
                float shadow = lerp(1.0, sun.shadowAttenuation, 0.85);
                float3 sunCol = sun.color;

                float NdotV = saturate(dot(N, V));
                float NdotL = dot(N, L);
                float fresnel = 0.02 + 0.98 * pow(1.0 - NdotV, 5.0);

                // sky and scene reflection (reflection probe); rays heading into the sea reflect the horizon
                float3 R = reflect(-V, N);
                R.y = max(R.y, 0.03);
                R = normalize(R);
                float2 screenUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                float perceptualRough = 1.0 - _Smoothness;
                half3 env = GlossyEnvironmentReflection(R, posWS, perceptualRough, 1.0h, screenUV);

                // sun glitter (GGX)
                float3 Hh = normalize(L + V);
                float NdotH = saturate(dot(N, Hh));
                float rough = max(perceptualRough * perceptualRough, 0.002);
                float r2 = rough * rough;
                float dd = NdotH * NdotH * (r2 - 1.0) + 1.0;
                float ggx = r2 / (PI * dd * dd + 1e-5);
                float fSun = 0.02 + 0.98 * pow(1.0 - saturate(dot(Hh, V)), 5.0);
                float3 spec = sunCol * (ggx * fSun * saturate(NdotL) * shadow * _SunSpec * 0.25 / max(NdotV, 0.1));

                // water body: light scattered back up out of the water. Deep water returns little, thin water (the upper
                // face, the lip) returns and lets through much more, tinted turquoise
                float3 sky = SampleSH(N);
                float thinAll = saturate(thin + crest * _CrestGlow * (1.0 - faceK));
                float3 scatter = lerp(_DeepColor.rgb, _ShallowColor.rgb, thinAll);
                float3 body = scatter * (sky * 0.85 + sunCol * shadow * (0.25 + 0.75 * saturate(NdotL)) * 0.55);

                // light through the water: the sun behind the lip, and the sky behind the sheet (seen through it)
                float3 Lt = normalize(L + N * 0.35);
                float back = pow(saturate(dot(V, -Lt)), _SSSPower);
                float3 skyBehind = SampleSH(-N);
                float3 sss = _SSSColor.rgb * thinAll * (sunCol * back * _SSSStrength * lerp(0.4, 1.0, shadow)
                             + skyBehind * _SSSAmbient * thinAll);

                float occl = 1.0 - ao * _TubeDark;
                float3 water = (body + sss * lerp(1.0, 0.75, ao)) * occl;
                float3 col = lerp(water, env * lerp(1.0, 0.3, ao), fresnel) + spec * (1.0 - ao);

                // foam: a rough, lumpy white surface. Its own relief (bump from the foam masks through screen-space
                // derivatives), darker in its hollows, glowing a little when backlit
                float cover = FoamCover(flow, foamAmt, dist);
                if (cover > 0.001)
                {
                    float2 fuv = flow * _FoamScale;
                    float fh = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, fuv).g * 0.7 + SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, fuv * 2.7 + 0.37).r * 0.3;
                    float3 dpx = ddx(posWS), dpy = ddy(posWS);
                    float3 r1 = cross(dpy, N), r2 = cross(N, dpx);
                    float det = dot(dpx, r1);
                    float3 grad = (ddx(fh) * r1 + ddy(fh) * r2) / (abs(det) > 1e-8 ? det : 1e-8);
                    float3 Nf = normalize(N - grad * 0.35 * saturate(1.0 - dist / 120.0));
                    float fNdotL = saturate(dot(Nf, L));
                    float cavity = lerp(0.62, 1.0, saturate(fh * 1.4));
                    float3 foamLit = _FoamColor.rgb * cavity * (SampleSH(Nf) * 0.9 + sunCol * shadow * (fNdotL * 0.75 + 0.25) * 0.8)
                                     + _SSSColor.rgb * sunCol * back * 0.3 * (1.0 - fh);
                    foamLit *= lerp(1.0, occl, 0.6);
                    col = lerp(col, foamLit, cover);
                }

                col = MixFog(col, IN.x0Dist.w);
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma shader_feature_local _WAVE_MESH
            #include "WaterCore.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            float4 ShadowVert(WaterAttributes IN) : SV_POSITION
            {
                WaterVertexData w = WaterVertex(IN);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(w.positionWS, w.normalWS, _LightDirection));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            half4 ShadowFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma shader_feature_local _WAVE_MESH
            #include "WaterCore.hlsl"

            float4 DepthVert(WaterAttributes IN) : SV_POSITION { return TransformWorldToHClip(WaterVertex(IN).positionWS); }
            half DepthFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DNVert
            #pragma fragment DNFrag
            #pragma shader_feature_local _WAVE_MESH
            #include "WaterCore.hlsl"

            struct DNV { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            DNV DNVert(WaterAttributes IN)
            {
                WaterVertexData w = WaterVertex(IN);
                DNV o;
                o.positionCS = TransformWorldToHClip(w.positionWS);
                o.normalWS = w.normalWS;
                return o;
            }
            half4 DNFrag(DNV IN, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 n = normalize(IN.normalWS);
                if (!IS_FRONT_VFACE(face, true, false)) n = -n;
                return half4(n, 0.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
