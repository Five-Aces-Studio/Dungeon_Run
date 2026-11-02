Shader "DungeonRun/Guille Exterior Fog"
{
    Properties
    {
        _FogColor("Fog Color", Color) = (.16,.20,.24,1)
        _FlowSpeed("Drift Speed", Float) = .65
        _Brightness("Faint ambient visibility", Range(0,1)) = .22
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _FogColor;
                float _FlowSpeed;
                float _Brightness;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 world : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(world);output.world=world.xz;
                return output;
            }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p)
            {
                float2 cell=floor(p), f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(Hash(cell),Hash(cell+float2(1,0)),f.x),
                    lerp(Hash(cell+float2(0,1)),Hash(cell+1),f.x),f.y);
            }
            float Clouds(float2 p)
            {
                return .57*Noise(p)+.28*Noise(p*2.03+17.1)+.15*Noise(p*4.11+41.7);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float t=_Time.y*_FlowSpeed;
                // Separate depths drift at different speeds; evolving distortion breaks up a sliding texture.
                float2 p=input.world*.14;
                float2 wind=float2(.19,.08)*t;
                float2 warp=float2(Clouds(p*.6+float2(t*.09,5)),Clouds(p*.7+float2(12,-t*.07)))-.5;
                float farCloud=Clouds(p*.65-wind*.55+warp*.8);
                float nearCloud=Clouds(p*float2(.65,1.35)-wind*1.7-warp*1.3+17);
                float wisps=Clouds(p*float2(1.1,2.8)-wind*2.2+warp+31);
                // Soft overlapping veils: no sharp dark trenches or raised-rock appearance.
                float farVeil=smoothstep(.2,.8,farCloud)*.55;
                float nearVeil=smoothstep(.3,.7,nearCloud)*.65;
                float thinVeil=smoothstep(.4,.7,wisps)*.25;
                float density=1-(1-farVeil)*(1-nearVeil)*(1-thinVeil);
                // Most of the exterior is almost black; only overlapping banks catch faint ambient light.
                return half4(_FogColor.rgb*_Brightness*lerp(.06,1.1,density),1);
            }
            ENDHLSL
        }
    }
}
