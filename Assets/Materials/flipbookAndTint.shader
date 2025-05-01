Shader "Custom/HDRP/AsyncAnimatedFlipbookSpriteEnhanced" // MODIFIED: Name
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
        [HideInInspector] _CustomOffsets("Custom Offsets", Vector) = (0, 0, 1, 1) // xy=offset, zw=tiling // [cite: 25, 26]
        [HideInInspector] _CustomFrameData("Custom Frame Data", Vector) = (1, 1, 1, 1) // NEW: rgb=tint, a=visibility (0=invisible, 1=visible)

        // Blend settings
        [Toggle] _UseEmissive("Use Emissive (Additive) Blending", Float) = 0
        _EmissiveColor("Emissive Color", Color) = (1, 1, 1, 1)
        _EmissiveIntensity("Emissive Intensity", Range(0, 10)) = 1.0
        _Alpha("Global Alpha", Range(0, 1)) = 1.0 // MODIFIED: Renamed for clarity

        [ToggleUI] _EnableInstancing("Enable Instancing", Float) = 1.0

        // HDRP specific properties (Assuming defaults are okay) // [cite: 4]
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
        Tags { "RenderPipeline"="HDRenderPipeline" "RenderType"="Transparent" "Queue"="Transparent" }// // [cite: 5]

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="ForwardOnly" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite] //// [cite: 6]
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local _FLIPBOOKTYPE_ROW _FLIPBOOKTYPE_COLUMN _FLIPBOOKTYPE_GRID _FLIPBOOKTYPE_CUSTOM // // [cite: 7]
            #pragma shader_feature_local _USEEMISSIVE_ON // [cite: 7]
            #pragma multi_compile_instancing // [cite: 7]

            // HDRP includes // [cite: 8]
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0; // [cite: 9]
                float4 color : COLOR; // Instance color (can be used for global tint per instance) // [cite: 9]
                UNITY_VERTEX_INPUT_INSTANCE_ID // [cite: 9]
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0; // [cite: 11]
                float4 color : COLOR; // Pass instance color // [cite: 11]
                 // NEW: Pass frame-specific data
                float4 frameTintAndVisibility : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO // [cite: 11]
            };

            TEXTURE2D(_BaseMap); // [cite: 12]
            SAMPLER(sampler_BaseMap); // [cite: 12]

            uint _TotalFrames; // [cite: 12]
            uint _FramesPerRow; // [cite: 12]
            uint _FramesPerColumn; // [cite: 12]
            float _FramesPerSecond; // [cite: 12]
            float _TimeOffset; // [cite: 12]
            float4 _EmissiveColor; // [cite: 12]
            float _EmissiveIntensity; // [cite: 12]
            float _Alpha; // Global Alpha // [cite: 12]

            // Custom frame data arrays
            uint _CustomOffsetCount; // [cite: 13]
            float4 _CustomOffsets[32]; // xy=offset, zw=tiling // [cite: 13]
            float4 _CustomFrameData[32]; // NEW: rgb=tint, a=visibility // [cite: 13]

            // Structure to hold calculated frame data
            struct FrameInfo {
                float2 uvOffset;
                float2 uvTiling;
                float4 tintAndVisibility; // rgb=tint, a=visibility
            };

            // MODIFIED: Function to get all frame info
            FrameInfo GetCurrentFrameInfo(float time)
            {
                FrameInfo info;
                info.uvOffset = float2(0, 0);
                info.uvTiling = float2(1, 1);
                info.tintAndVisibility = float4(1, 1, 1, 1); // Default: white tint, visible

                float adjustedTime = time + _TimeOffset; // [cite: 16]
                float frameTime = 1.0 / max(0.001, _FramesPerSecond); // Avoid division by zero // [cite: 17]
                uint currentFrame = fmod(adjustedTime / frameTime, (float)_TotalFrames); // [cite: 18]

                #if defined(_FLIPBOOKTYPE_ROW)
                    info.uvTiling.x = 1.0 / _TotalFrames; // [cite: 19]
                    info.uvOffset.x = (float)currentFrame / _TotalFrames; // [cite: 20]
                #elif defined(_FLIPBOOKTYPE_COLUMN)
                    info.uvTiling.y = 1.0 / _TotalFrames; // [cite: 20]
                    info.uvOffset.y = (float)currentFrame / _TotalFrames; // [cite: 21]
                #elif defined(_FLIPBOOKTYPE_GRID)
                    info.uvTiling.x = 1.0 / _FramesPerRow; // [cite: 21]
                    info.uvTiling.y = 1.0 / _FramesPerColumn; // [cite: 22]
                    uint row = currentFrame / _FramesPerRow; // [cite: 22]
                    uint col = currentFrame % _FramesPerRow; // [cite: 22]
                    info.uvOffset.x = (float)col / _FramesPerRow; // [cite: 23]
                    info.uvOffset.y = (float)row / _FramesPerColumn; // [cite: 23]
                #elif defined(_FLIPBOOKTYPE_CUSTOM)
                    if (_CustomOffsetCount > 0) {
                        uint frameIndex = min(currentFrame, _CustomOffsetCount - 1); // [cite: 25]
                        // Fetch offset/tiling
                        info.uvOffset = _CustomOffsets[frameIndex].xy; // [cite: 25]
                        info.uvTiling = _CustomOffsets[frameIndex].zw; // [cite: 26]
                        // NEW: Fetch tint/visibility
                        info.tintAndVisibility = _CustomFrameData[frameIndex];
                    }
                #endif

                return info;
            }


            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input); // [cite: 15]
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output); // [cite: 15]

                // NEW: Calculate frame data in vertex shader once
                FrameInfo frameInfo = GetCurrentFrameInfo(_Time.y);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // MODIFIED: Apply tiling/offset to UVs here
                output.uv = input.uv * frameInfo.uvTiling + frameInfo.uvOffset;
                output.color = input.color; // Pass instance color
                output.frameTintAndVisibility = frameInfo.tintAndVisibility; // NEW: Pass frame data to fragment

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // NEW: Check visibility first
                clip(input.frameTintAndVisibility.a > 0 ? 1 : -1); // Discard fragment if visibility is <= 0

                // MODIFIED: UVs are already calculated in vert
                float2 finalUV = input.uv;

                // Sample the texture
                float4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, finalUV); // [cite: 28]

                // Apply instance color (optional global tint per object) // [cite: 29]
                color *= input.color;

                // NEW: Apply frame-specific tint
                color.rgb *= input.frameTintAndVisibility.rgb;

                #if defined(_USEEMISSIVE_ON)
                    // Apply emissive effect // [cite: 30]
                    color.rgb *= _EmissiveColor.rgb * _EmissiveIntensity; // [cite: 30]
                #endif

                // Apply global alpha
                color.a *= _Alpha; // [cite: 31]

                return color; // [cite: 32]
            }
            ENDHLSL
        }
    }
    CustomEditor "UnityEditor.Rendering.HighDefinition.HDLitGUI" // Keep default editor for HDRP
}