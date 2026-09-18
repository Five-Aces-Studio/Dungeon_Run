Shader "DungeonRun/SH_DungeonRun_SelectiveOutlineMask"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "VisibleSelectedSurface"
            ZWrite Off ZTest LEqual Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _SurfaceId;
            float4 _Region;
            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionOS : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.positionOS = input.positionOS;
                return output;
            }
            float4 Frag(Varyings input) : SV_Target
            {
                float eligible = (_Region.z < .5 || input.positionOS.x < _Region.x) &&
                    (_Region.w < .5 || input.positionOS.y < _Region.y);
                // Identity survives region cutoffs; only true visible-surface edges may form a contour.
                return float4(_SurfaceId, eligible, 0, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
