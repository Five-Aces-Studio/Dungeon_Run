Shader "DungeonRun/Maze/FogOfWar"
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
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
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
                float2 p = input.positionWS.xz * max(_NoiseScale,0.001);
                float t = _Time.y * _DriftSpeed;
                // World-space distortion keeps cloud movement continuous across cells.
                float warp = Noise(p * 0.55 + float2(-t * 0.3, t * 0.2));
                p += (warp - 0.5) * 1.4;
                float n = Noise(p+float2(t,t*0.35))*0.6
                        + Noise(p*2.03+float2(-t*0.4,t))*0.28
                        + Noise(p*4.07+float2(t*0.2,-t*0.5))*0.12;
                float clouds = smoothstep(0.2,0.85,n);
                clouds = saturate((clouds - 0.5) * _CloudContrast + 0.5);
                // Noise only changes color: hidden cells never have accidental holes.
                // half3 color = lerp(_FogColor.rgb,_CloudColor.rgb,clouds);
                // // Preserve exact endpoints: discovered = transparent, unknown = opaque.
                // float alpha = 1.0 - pow(1.0 - saturate(_Coverage), max(_Density, 1.0));
                // return half4(color,alpha);
                half3 color = lerp(_FogColor.rgb, _CloudColor.rgb, clouds);

                // Controla la disipación al descubrir una habitación.
                float coverageAlpha = 1.0 - pow(
                    1.0 - saturate(_Coverage),
                    max(_Density, 1.0)
                );

                // Las nubes tapan entre el 90 % y el 97 % del fondo.
                // Hay pequeñas variaciones, pero nunca agujeros transparentes.
                float cloudOpacity = lerp(0.90, 0.97, clouds);

                return half4(color, coverageAlpha * cloudOpacity);
            }
            ENDHLSL
        }
    }
}
