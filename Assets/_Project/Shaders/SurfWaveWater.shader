Shader "Biscotte/SurfWaveWater"
{
    Properties
    {
        _ShallowColor("Shallow Color", Color) = (0.06, 0.52, 0.58, 1)
        _DeepColor("Deep Color", Color) = (0.012, 0.085, 0.16, 1)
        _FoamColor("Foam Color", Color) = (0.93, 0.97, 1.0, 1)
        _FoamTex("Foam Texture", 2D) = "white" {}
        _FoamTiling("Foam Tiling (1/m)", Float) = 0.22
        _FoamAmount("Foam Amount", Range(0, 3)) = 1.3
        _NormalMap("Ripple Normals", 2D) = "bump" {}
        _NormalTiling("Ripple Tiling (1/m)", Float) = 0.3
        _NormalStrength("Ripple Strength", Range(0, 2)) = 0.55
        _RippleSpeed("Ripple Speed", Float) = 0.5
        _Smoothness("Smoothness", Range(0, 1)) = 0.93
        _SSSColor("SSS Color", Color) = (0.12, 0.85, 0.65, 1)
        _SSSStrength("SSS Strength", Range(0, 6)) = 1.6
        _SSSPower("SSS Power", Range(1, 16)) = 4
        _TubeDarkening("Tube Darkening", Range(0, 1)) = 0.55
        _ShallowByHeight("Shallow By Thin Water", Range(0, 1)) = 0.7
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" "IgnoreProjector" = "True" }
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

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _FoamColor;
                float4 _FoamTex_ST;
                float _FoamTiling;
                float _FoamAmount;
                float4 _NormalMap_ST;
                float _NormalTiling;
                float _NormalStrength;
                float _RippleSpeed;
                float _Smoothness;
                float4 _SSSColor;
                float _SSSStrength;
                float _SSSPower;
                float _TubeDarkening;
                float _ShallowByHeight;
            CBUFFER_END

            TEXTURE2D(_FoamTex); SAMPLER(sampler_FoamTex);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 color : TEXCOORD2;
                float4 uv : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord : TEXCOORD5;
                #endif
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = vpi.positionCS;
                OUT.positionWS = vpi.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.color = IN.color;
                OUT.uv = float4(IN.uv0, IN.uv1);
                OUT.fogFactor = ComputeFogFactor(vpi.positionCS.z);
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                OUT.shadowCoord = GetShadowCoord(vpi);
                #endif
                return OUT;
            }

            half4 Frag(Varyings IN, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 n = normalize(IN.normalWS);
                if (!IS_FRONT_VFACE(face, true, false)) n = -n;

                float t = _Time.y * _RippleSpeed;
                float2 wuv = IN.positionWS.xz * _NormalTiling;
                float3 n1 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, wuv + float2(t * 0.07, t * 0.03)));
                float3 n2 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, wuv * 2.7 - float2(t * 0.05, t * 0.09)));
                float foamMask = saturate(IN.color.g * _FoamAmount);
                float2 nxy = (n1.xy + n2.xy) * 0.5 * _NormalStrength * (1.0 - 0.7 * foamMask);
                n = normalize(n + float3(nxy.x, 0.0, nxy.y));

                float3 viewDir = GetWorldSpaceNormalizeViewDir(IN.positionWS);

                float thin = IN.uv.w;                       // sss thickness proxy from the wave math
                float shallowK = saturate(thin * _ShallowByHeight + IN.color.b * 0.35);
                float3 water = lerp(_DeepColor.rgb, _ShallowColor.rgb, shallowK);
                float foamTex = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, IN.positionWS.xz * _FoamTiling + float2(t * 0.02, 0.0)).r;
                float foam = saturate(foamMask * (0.4 + 0.9 * foamTex));
                float3 albedo = lerp(water, _FoamColor.rgb, foam);
                albedo *= 1.0 - _TubeDarkening * IN.color.b;

                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.positionCS = IN.positionCS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = viewDir;
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                inputData.shadowCoord = IN.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                #else
                inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif
                inputData.fogCoord = IN.fogFactor;
                inputData.bakedGI = SampleSH(n);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData sd = (SurfaceData)0;
                sd.albedo = albedo;
                sd.metallic = 0.0;
                sd.specular = 0.0;
                sd.smoothness = lerp(_Smoothness, 0.2, foam);
                sd.normalTS = float3(0, 0, 1);
                sd.occlusion = 1.0;
                sd.alpha = 1.0;

                Light mainLight = GetMainLight();
                float3 H = normalize(mainLight.direction + n * 0.3);
                float sss = pow(saturate(dot(viewDir, -H)), _SSSPower) * _SSSStrength * thin;
                sd.emission = _SSSColor.rgb * sss * mainLight.color * (1.0 - foam * 0.8);

                half4 col = UniversalFragmentPBR(inputData, sd);
                col.rgb = MixFog(col.rgb, IN.fogFactor);
                col.a = 1.0;
                return col;
            }
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; };
            V DepthVert(A IN) { V OUT; OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz); return OUT; }
            half DepthFrag(V IN) : SV_TARGET { return 0; }
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            V DNVert(A IN) { V OUT; OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz); OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS); return OUT; }
            half4 DNFrag(V IN) : SV_TARGET { return half4(normalize(IN.normalWS), 0.0); }
            ENDHLSL
        }
    }
    FallBack Off
}
