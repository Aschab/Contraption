// Stylized "painted metal" surface for machine pieces (concept art Share/t1–t4): soft wrapped light,
// procedural grime in object space, lighter worn chamfers, gentle rim. Meshes need no UVs.
// Shadow/depth passes come from URP Lit, so SSAO and shadows keep working.
Shader "Contraption/Painted"
{
    Properties
    {
        _BaseColor("Color", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor("Emission", Color) = (0, 0, 0, 1)
        _Smoothness("Smoothness", Range(0, 1)) = 0.3
        _Metallic("Metallic", Range(0, 1)) = 0
        _Grime("Grime", Range(0, 1)) = 0.6
        _Wear("Edge wear", Range(0, 1)) = 0.6
        [HideInInspector] _BaseMap("Base map", 2D) = "white" {}
        [HideInInspector] _Cutoff("Cutoff", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _EmissionColor;
                half _Smoothness;
                half _Metallic;
                half _Grime;
                half _Wear;
                half _Cutoff;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float3 normalOS : TEXCOORD3;
            };

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Noise(float3 x)
            {
                float3 i = floor(x), f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x), lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x), lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            half Wrap(half3 n, half3 l) { return saturate((dot(n, l) + 0.4h) / 1.4h); }

            Varyings Vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionOS = v.positionOS.xyz;
                o.normalOS = v.normalOS;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 nOS = normalize(i.normalOS);
                half3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);

                // Grime: two octaves of value noise; chamfers (diagonal normals on box-like parts) read as worn, lighter paint.
                float noise = Noise(i.positionOS * 3.0) * 0.65 + Noise(i.positionOS * 11.0) * 0.35;
                half3 albedo = _BaseColor.rgb * lerp(1.0h, 0.84h + 0.28h * (half)noise, _Grime);
                float3 a = abs(nOS);
                half bevel = smoothstep(0.12, 0.26, 1.0 - max(a.x, max(a.y, a.z)));
                albedo = lerp(albedo, albedo * 1.22h + 0.035h, bevel * _Wear * (0.55h + 0.45h * (half)noise));

                AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(screenUV);
                Light main = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 light = main.color * Wrap(n, main.direction) * main.shadowAttenuation * ao.directAmbientOcclusion;
                half3 h = normalize(main.direction + viewDir);
                half3 specColor = lerp(half3(0.04h, 0.04h, 0.04h), albedo, _Metallic);
                half3 spec = specColor * pow(saturate(dot(n, h)), exp2(9.0h * _Smoothness + 1.0h)) * (0.4h + 2.0h * _Smoothness) * main.color * main.shadowAttenuation;

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = viewDir;
                inputData.normalizedScreenSpaceUV = screenUV;
            #if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint d = 0; d < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); d++)
                {
                    Light dl = GetAdditionalLight(d, i.positionWS);
                    light += dl.color * Wrap(n, dl.direction) * dl.distanceAttenuation * dl.shadowAttenuation;
                }
            #endif
                uint count = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(count)
                    Light al = GetAdditionalLight(lightIndex, i.positionWS);
                    light += al.color * Wrap(n, al.direction) * al.distanceAttenuation * al.shadowAttenuation;
                LIGHT_LOOP_END

                half3 ambient = SampleSH(n) * ao.indirectAmbientOcclusion;
                half rim = pow(1.0h - saturate(dot(n, viewDir)), 4.0h) * 0.15h;
                half3 color = albedo * (1.0h - _Metallic * 0.4h) * (light + ambient) + spec + rim * albedo + _EmissionColor.rgb;
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
        UsePass "Universal Render Pipeline/Lit/DEPTHONLY"
        UsePass "Universal Render Pipeline/Lit/DEPTHNORMALS"
    }
    FallBack "Universal Render Pipeline/Lit"
}
