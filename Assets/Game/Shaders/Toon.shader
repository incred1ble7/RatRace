// Mobile-friendly toon shader for URP 17 (Unity 6).
//  * stepped (cel) lighting from the main light + additional lights (night fridge light), receives shadows
//  * tinted shadow side, soft rim light
//  * outline: inverted hull pass, constant width in pixels.
//    Models from our importers store smoothed normals in the tangent channel (no cracks on flat low-poly);
//    for Unity primitives set "Outline From Tangent" off.
// SRP Batcher compatible (all material data in UnityPerMaterial).
Shader "MouseGame/Toon"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _ShadowTint ("Shadow Tint", Color) = (0.62, 0.60, 0.78, 1)
        _Bands ("Light Bands", Range(1, 4)) = 2
        _BandSoftness ("Band Softness", Range(0.001, 0.2)) = 0.03
        _RimStrength ("Rim Light", Range(0, 1)) = 0.2
        _OutlineColor ("Outline Color", Color) = (0.10, 0.07, 0.06, 1)
        _OutlineWidth ("Outline Width (px)", Range(0, 6)) = 1.6
        [Toggle] _OutlineFromTangent ("Outline From Tangent (smoothed normals)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _ShadowTint;
            half _Bands;
            half _BandSoftness;
            half _RimStrength;
            half4 _OutlineColor;
            half _OutlineWidth;
            half _OutlineFromTangent;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        // per-level shade colour (set by ToonSceneLighting); no baked lighting needed
        half4 _ToonAmbient;

        // quantise a 0..1 light amount into bands with soft steps
        half Cel(half x)
        {
            half n = max(1.0h, _Bands);
            half v = x * n;
            half f = frac(v);
            half step = floor(v) + smoothstep(0.5h - _BandSoftness * n, 0.5h + _BandSoftness * n, f);
            return saturate(step / n);
        }
        ENDHLSL

        // ------------------------------------------------------------------ main pass
        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3  normalWS   : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half3 ToonLight(Light light, half3 n, half3 albedo)
            {
                half ndl = saturate(dot(n, light.direction));
                // bands from the angle only; falloff stays smooth so point lights make soft pools of light
                half lit = Cel(ndl * light.shadowAttenuation) * saturate(light.distanceAttenuation);
                return albedo * light.color * lit;
            }

            half CelAmount(Light light, half3 n)
            {
                return Cel(saturate(dot(n, light.direction)) * light.shadowAttenuation);
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                half3 n = i.normalWS;
                n = dot(n, n) > 1e-6h ? normalize(n) : half3(0, 1, 0);   // guard against degenerate normals
                half3 v = normalize(GetWorldSpaceViewDir(i.positionWS));

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                // shade side = albedo tinted by the level's shade colour; lit side = albedo * sun colour
                half3 shade = _ToonAmbient.a > 0 ? _ToonAmbient.rgb : half3(0.55h, 0.55h, 0.65h);
                shade *= _ShadowTint.rgb / max(0.001h, max(_ShadowTint.r, max(_ShadowTint.g, _ShadowTint.b)));
                half3 color = albedo * lerp(shade, mainLight.color, CelAmount(mainLight, n));

            #if defined(_ADDITIONAL_LIGHTS) || defined(_CLUSTER_LIGHT_LOOP)
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                uint count = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(count)
                    Light l = GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1));
                    color += ToonLight(l, n, albedo);
                LIGHT_LOOP_END
            #endif

                // rim on the lit side only, keeps silhouettes readable on a phone
                half rim = pow(1.0h - saturate(dot(n, v)), 4.0h) * saturate(dot(n, mainLight.direction) + 0.3h);
                color += albedo * mainLight.color * rim * _RimStrength;

                return half4(color, 1);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ outline (inverted hull)
        Pass
        {
            Name "ToonOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                float3 dirOS = _OutlineFromTangent > 0.5 ? v.tangentOS.xyz : v.normalOS;
                if (dot(dirOS, dirOS) < 1e-6) dirOS = v.normalOS;
                float4 cs = TransformObjectToHClip(v.positionOS.xyz);
                float3 dirWS = TransformObjectToWorldDir(normalize(dirOS));
                float2 dirCS = mul((float3x3)GetWorldToHClipMatrix(), dirWS).xy;
                float len = length(dirCS);
                if (len > 1e-5)
                {
                    // constant pixel width, independent of distance and resolution
                    float2 offset = dirCS / len * _OutlineWidth * 2.0 / _ScreenParams.xy;
                    cs.xy += offset * cs.w;
                }
                o.positionCS = cs;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return half4(_OutlineColor.rgb, 1);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ shadows
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 vert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - posWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, lightDir));
            #if UNITY_REVERSED_Z
                cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
            #else
                cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return cs;
            }

            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }

        // ------------------------------------------------------------------ depth (depth prepass / Forward+)
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 vert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                return TransformObjectToHClip(v.positionOS.xyz);
            }

            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
