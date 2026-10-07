Shader "DungeonRun/Maze/FogVolume"
{
    Properties
    {
        _FogColor ("Deep fog", Color) = (0.010,0.018,0.025,1)
        _CloudColor ("Cloud highlights", Color) = (0.11,0.16,0.19,1)
        _Coverage ("Coverage: 0 revealed, 1 hidden", Range(0,1)) = 1
        _Density ("Optical density", Range(1,6)) = 2.5
        _CloudContrast ("Cloud contrast", Range(0.5,3)) = 1.6
        _NoiseScale ("World noise scale", Float) = 0.65
        _DriftSpeed ("Drift speed", Range(0,0.2)) = 0.025
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "FogOfWar"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Front
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _FogColor;
                half4 _CloudColor;
                float _Coverage;
                float _Density;
                float _CloudContrast;
                float _NoiseScale;
                float _DriftSpeed;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i+float2(1,0)),f.x),
                            lerp(Hash(i+float2(0,1)), Hash(i+float2(1,1)),f.x),f.y);
            }
            Varyings Vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float coverage = saturate(_Coverage);

                if (coverage <= 0.0001)
                    return half4(0, 0, 0, 0);

                float3 rayOriginWS = _WorldSpaceCameraPos;
                float3 rayDirectionWS =
                    normalize(input.positionWS - rayOriginWS);

                // Convertir rayo al espacio del cubo.
                float3 rayOriginOS = TransformWorldToObject(rayOriginWS);
                float3 rayDirectionOS = mul(
                    (float3x3)unity_WorldToObject,
                    rayDirectionWS
                );

                float3 directionSign = float3(
                    rayDirectionOS.x >= 0 ? 1 : -1,
                    rayDirectionOS.y >= 0 ? 1 : -1,
                    rayDirectionOS.z >= 0 ? 1 : -1
                );

                float3 inverseDirection =
                    directionSign / max(abs(rayDirectionOS), 0.000001);

                float3 t0 = (-0.5 - rayOriginOS) * inverseDirection;
                float3 t1 = ( 0.5 - rayOriginOS) * inverseDirection;

                float3 nearDistances = min(t0, t1);
                float3 farDistances = max(t0, t1);

                float entryDistance = max(
                    max(nearDistances.x, nearDistances.y),
                    nearDistances.z
                );

                float exitDistance = min(
                    min(farDistances.x, farDistances.y),
                    farDistances.z
                );

                entryDistance = max(entryDistance, 0.0);

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(screenUV);

                #if !UNITY_REVERSED_Z
                    rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif

                float3 scenePositionWS = ComputeWorldSpacePosition(
                    screenUV,
                    rawDepth,
                    UNITY_MATRIX_I_VP
                );

                float sceneDistance = dot(
                    scenePositionWS - rayOriginWS,
                    rayDirectionWS
                );

                exitDistance = min(exitDistance, sceneDistance);

                if (exitDistance <= entryDistance)
                    return half4(0, 0, 0, 0);

                const int sampleCount = 8;

                float stepLength =
                    (exitDistance - entryDistance) / sampleCount;

                float3 accumulatedColor = float3(0, 0, 0);
                float accumulatedAlpha = 0.0;
                float time = _Time.y * _DriftSpeed;

                [unroll]
                for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                {
                    float distanceAlongRay =
                        entryDistance + (sampleIndex + 0.5) * stepLength;

                    float3 positionWS =
                        rayOriginWS + rayDirectionWS * distanceAlongRay;

                    float2 p =
                        positionWS.xz * max(_NoiseScale, 0.001)
                        + positionWS.y * float2(0.37, 0.61);

                    float noise =
                        Noise(p + float2(time, time * 0.35)) * 0.7
                        + Noise(p * 2.03 + float2(-time * 0.4, time)) * 0.3;

                    float clouds = smoothstep(0.2, 0.85, noise);
                    clouds = saturate(
                        (clouds - 0.5) * _CloudContrast + 0.5
                    );

                    float density =
                        max(_Density, 0.0)
                        * coverage
                        * lerp(0.65, 1.15, clouds);

                    float sampleAlpha = 1.0 - exp(-density * stepLength);
                    float3 sampleColor =
                        lerp(_FogColor.rgb, _CloudColor.rgb, clouds);

                    float contribution =
                        (1.0 - accumulatedAlpha) * sampleAlpha;

                    accumulatedColor += sampleColor * contribution;
                    accumulatedAlpha += contribution;
                }

                return half4(accumulatedColor, accumulatedAlpha);
            }
            ENDHLSL
        }
    }
}
