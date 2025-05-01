Shader "Custom/HDRP/AsyncAnimatedFlipbookSprite"
{
    Properties
    {
        _BaseMap("Flipbook Texture", 2D) = "white" {}
        
        // Flipbook animation settings
        [KeywordEnum(Row, Column, Grid, Custom)] _FlipbookType("Flipbook Type", Float) = 2
        _TotalFrames("Total Frames", int) = 16
        _FramesPerRow("Frames Per Row", int) = 4
        _FramesPerColumn("Frames Per Column", int) = 4
        _FramesPerSecond("Frames Per Second", Float) = 24
        _TimeOffset("Time Offset", Float) = 0.0
        
        // Custom keyframe settings
        [HideInInspector] _CustomOffsetCount("Custom Offset Count", int) = 0
        [HideInInspector] _CustomOffsets("Custom Offsets", Vector) = (0, 0, 1, 1) // Placeholder for custom offsets
        
        // Blend settings
        [Toggle] _UseEmissive("Use Emissive (Additive) Blending", Float) = 0
        _EmissiveColor("Emissive Color", Color) = (1, 1, 1, 1)
        _EmissiveIntensity("Emissive Intensity", Range(0, 10)) = 1.0
        _Alpha("Alpha", Range(0, 1)) = 1.0
        
        [ToggleUI] _EnableInstancing("Enable Instancing", Float) = 1.0

        // HDRP specific properties
        [HideInInspector] _RenderQueueType("Render Queue Type", Float) = 5 // Transparent
        [HideInInspector] [ToggleUI] _AddPrecomputedVelocity("Add Precomputed Velocity", Float) = 0.0
        [HideInInspector] _SurfaceType("Surface Type", Float) = 1.0 // Transparent
        [HideInInspector] _BlendMode("Blend Mode", Float) = 0.0 // Alpha
        [HideInInspector] _SrcBlend("Source Blend", Float) = 1.0 // One
        [HideInInspector] _DstBlend("Destination Blend", Float) = 10.0 // OneMinusSrcAlpha
        [HideInInspector] _ZWrite("ZWrite", Float) = 0.0 // Off
    }
    
    SubShader
    {
        Tags { "RenderPipeline"="HDRenderPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="ForwardOnly" }
            
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest LEqual
            Cull Back
            
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma shader_feature_local _FLIPBOOKTYPE_ROW _FLIPBOOKTYPE_COLUMN _FLIPBOOKTYPE_GRID _FLIPBOOKTYPE_CUSTOM
            #pragma shader_feature_local _USEEMISSIVE_ON
            #pragma multi_compile_instancing
            
            // HDRP specific includes
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;  // Instance color (optional)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            
            uint _TotalFrames;
            uint _FramesPerRow;
            uint _FramesPerColumn;
            float _FramesPerSecond;
            float _TimeOffset;
            float4 _EmissiveColor;
            float _EmissiveIntensity;
            float _Alpha;
            
            // Custom offsets buffer
            uint _CustomOffsetCount;
            float4 _CustomOffsets[32]; // Support up to 32 keyframes
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            
            float2 CalculateFlipbookUV(float2 originalUV, float time)
            {
                // Use adjusted time with offset
                float adjustedTime = time + _TimeOffset;
                
                // Calculate current frame based on time and framerate
                float frameTime = 1.0 / _FramesPerSecond;
                uint currentFrame = fmod(adjustedTime / frameTime, _TotalFrames);
                
                float2 offset = float2(0, 0);
                float2 tiling = float2(1, 1);
                
                #if defined(_FLIPBOOKTYPE_ROW)
                    // All frames in a single row
                    tiling.x = 1.0 / _TotalFrames;
                    offset.x = (float)currentFrame / _TotalFrames;
                    
                #elif defined(_FLIPBOOKTYPE_COLUMN)
                    // All frames in a single column
                    tiling.y = 1.0 / _TotalFrames;
                    offset.y = (float)currentFrame / _TotalFrames;
                    
                #elif defined(_FLIPBOOKTYPE_GRID)
                    // Frames in a grid
                    tiling.x = 1.0 / _FramesPerRow;
                    tiling.y = 1.0 / _FramesPerColumn;
                    
                    uint row = currentFrame / _FramesPerRow;
                    uint col = currentFrame % _FramesPerRow;
                    
                    offset.x = (float)col / _FramesPerRow;
                    offset.y = (float)row / _FramesPerColumn;
                    
                #elif defined(_FLIPBOOKTYPE_CUSTOM)
                    // Custom keyframe animation
                    if (_CustomOffsetCount > 0) {
                        // Find the correct keyframe
                        uint frameIndex = min(currentFrame, _CustomOffsetCount - 1);
                        
                        // Each keyframe stores: xy = offset, zw = tiling
                        offset = _CustomOffsets[frameIndex].xy;
                        tiling = _CustomOffsets[frameIndex].zw;
                    }
                #endif
                
                // Apply tiling and offset
                return originalUV * tiling + offset;
            }
            
            float4 frag(Varyings input) : SV_Target
            {
                // Calculate flipbook UVs
                float2 flipbookUV = CalculateFlipbookUV(input.uv, _Time.y);
                
                // Sample the texture
                float4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, flipbookUV);
                
                // Apply vertex color (useful for instance variation)
                color *= input.color;
                
                #if defined(_USEEMISSIVE_ON)
                    // Apply emissive effect for additive-like blending
                    color.rgb *= _EmissiveColor.rgb * _EmissiveIntensity;
                #endif
                
                // Apply alpha
                color.a *= _Alpha;
                
                return color;
            }
            ENDHLSL
        }
    }
    CustomEditor "UnityEditor.Rendering.HighDefinition.HDLitGUI"
}