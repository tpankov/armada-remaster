Shader "Shader Graphs/PanoramicSkyShader"
{
    Properties
    {
        _ProjSkyTexture("Sky Texture", 2D) = "white" {}
        _Exposure("Exposure", Float) = 1.0
        _Rotation("Rotation", Float) = 0.0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "HighDefinitionRenderPipeline" }
        Pass
        {
            ZWrite Off
            Cull Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            TEXTURE2D(_ProjSkyTexture);
            SAMPLER(sampler_ProjSkyTexture);
            float _Exposure;
            float _Rotation;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDir : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.viewDir = GetWorldSpaceViewDir(output.positionCS);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 viewDir = normalize(input.viewDir);

                // Apply rotation
                float rotationRad = _Rotation * PI / 180.0;
                float2x2 rotationMatrix = float2x2(cos(rotationRad), -sin(rotationRad), sin(rotationRad), cos(rotationRad));
                viewDir.xz = mul(rotationMatrix, viewDir.xz);

                // Equirectangular projection
                float2 uv = float2(atan2(viewDir.x, viewDir.z) / (2.0 * PI) + 0.5, acos(viewDir.y) / PI);
                
                float4 color = SAMPLE_TEXTURE2D(_ProjSkyTexture, sampler_ProjSkyTexture, uv);
                color.rgb *= _Exposure;

                return color;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/HDRP/Unlit"
}