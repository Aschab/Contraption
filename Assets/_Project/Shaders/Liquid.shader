// Liquid inside a container. Clipped at a world-space fill height with a wobbling surface; back faces
// render as the liquid surface. Refraction bends the scene behind it (needs the URP opaque texture).
Shader "Contraption/Liquid"
{
    Properties
    {
        _BaseColor("Color", Color) = (0.3, 0.9, 0.8, 1)
        _FillLevel("Fill level (world Y)", Float) = 0
        _Wobble("Wobble", Float) = 0.02
        _WobbleSpeed("Wobble speed", Float) = 3
        _Refraction("Refraction", Range(0, 1)) = 0.3
        _Opacity("Opacity", Range(0, 1)) = 0.6
        _Emission("Emission", Float) = 0.25
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _FillLevel;
                float _Wobble;
                float _WobbleSpeed;
                half _Refraction;
                half _Opacity;
                half _Emission;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 Frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float t = _Time.y * _WobbleSpeed;
                float level = _FillLevel + _Wobble * (sin(i.positionWS.x * 7.0 + t) + sin(i.positionWS.z * 5.0 + t * 1.37));
                clip(level - i.positionWS.y);

                bool front = IS_FRONT_VFACE(face, true, false);
                float3 n = front ? normalize(i.normalWS) : float3(0, 1, 0);
                float3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                Light light = GetMainLight();
                half3 lit = _BaseColor.rgb * (saturate(dot(n, light.direction)) * 0.55 + 0.45);

                if (!front)
                    return half4(_BaseColor.rgb * (1.2 + _Emission) + _Refraction * 0.15, 1);

                float2 uv = GetNormalizedScreenSpaceUV(i.positionCS);
                float3 nVS = TransformWorldToViewDir(n);
                float2 shimmer = float2(sin(t * 0.7 + i.positionWS.y * 9.0), cos(t * 0.6 + i.positionWS.x * 8.0)) * 0.004;
                half3 behind = SampleSceneColor(uv + (nVS.xy * 0.08 + shimmer) * _Refraction);

                half fresnel = pow(1.0 - saturate(dot(n, viewDir)), 3.0);
                half3 h = normalize(light.direction + viewDir);
                half spec = pow(saturate(dot(n, h)), lerp(8.0, 96.0, _Refraction)) * (0.15 + _Refraction * 2.5);
                half opacity = lerp(_Opacity, _Opacity * 0.55, _Refraction);
                half3 col = lerp(behind * _BaseColor.rgb * 1.3, lit, opacity);
                col += spec * light.color + fresnel * (0.1 + _Refraction * 0.6) + _BaseColor.rgb * _Emission;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
