Shader "Custom/HDRP/Debug"
{
    Properties
    {
        _BaseMap("Flipbook Texture", 2D) = "white" {}

        // General Animation Settings
        _FramesPerSecond("Frames Per Second", Float) = 24
        _TimeOffset("Time Offset", Float) = 0.0 // Global time offset for all tracks

        // --- Fallback Settings (Used if keyframe counts are 0) ---
        [Header(Fallback Settings)]
        [KeywordEnum(Row, Column, Grid)] _FallbackFlipbookType("Fallback Flipbook Type", Float) = 2
        _FallbackTotalFrames("Fallback Total Frames", Int) = 16
        _FallbackFramesPerRow("Fallback Frames Per Row", Int) = 4
        _FallbackFramesPerColumn("Fallback Frames Per Column", Int) = 4
        _FallbackTint("Fallback Tint", Color) = (1,1,1,1)
        _FallbackVisibility("Fallback Visibility", Range(0, 1)) = 1.0

        // --- Interpolation Settings ---
        [Header(Interpolation)]
        [KeywordEnum(Step, LinearScroll, LinearCrossfade)] _OffsetInterpolation("Offset Interpolation", Float) = 0 // 0:Step, 1:Scroll, 2:Crossfade
        [KeywordEnum(Step, Linear)] _TintInterpolation("Tint Interpolation", Float) = 0 // 0:Step, 1:Linear
        // Draw/Visibility is always Stepped

        // --- Blend Settings ---
        [Header(Blending)]
        [Toggle] _UseEmissive("Use Emissive (Additive) Blending", Float) = 0
        _EmissiveColor("Emissive Color", Color) = (1, 1, 1, 1)
        _EmissiveIntensity("Emissive Intensity", Range(0, 10)) = 1.0
        _Alpha("Global Alpha", Range(0, 1)) = 1.0

        [ToggleUI] _EnableInstancing("Enable Instancing", Float) = 1.0

        // HDRP specific properties
        [HideInInspector] _RenderQueueType("Render Queue Type", Float) = 5 // Transparent
        [HideInInspector] [ToggleUI] _AddPrecomputedVelocity("Add Precomputed Velocity", Float) = 0.0
        [HideInInspector] _SurfaceType("Surface Type", Float) = 1.0 // Transparent
        [HideInInspector] _BlendMode("Blend Mode", Float) = 0.0 // Alpha
        [HideInInspector] _SrcBlend("Source Blend", Float) = 1.0 // One
        [HideInInspector] _DstBlend("Destination Blend", Float) = 10.0 // OneMinusSrcAlpha
        [HideInInspector] _ZWrite("ZWrite", Float) = 0.0 // Off

        // --- Keyframe Counts (Set via MaterialPropertyBlock) ---
        [HideInInspector] _OffsetKeyframeCount("Offset Keyframe Count", Int) = 0
        [HideInInspector] _TintKeyframeCount("Tint Keyframe Count", Int) = 0
        [HideInInspector] _DrawKeyframeCount("Draw Keyframe Count", Int) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="HDRenderPipeline" "RenderType"="Transparent" "Queue"="Transparent" }

        Pass
        {
            Name "ForwardLit_Debug" // Changed name slightly
            Tags { "LightMode"="ForwardOnly" } // Keep minimal tags

            // DEBUG: Force simple state
            Blend Off          // Disable blending
            ZWrite On          // Write to depth buffer
            ZTest Always       // Ignore depth testing
            Cull Back          // Keep basic backface culling

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert_minimal
            #pragma fragment frag_minimal

            // No shader features needed for this minimal test
            #pragma multi_compile_instancing // Keep instancing

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            // #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl" // Probably not needed

            // Minimal Structures
            struct Attributes_Minimal
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings_Minimal
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // --- Minimal Vertex Shader ---
            Varyings_Minimal vert_minimal(Attributes_Minimal input)
            {
                Varyings_Minimal output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Bypass ALL calculations, just transform position
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

                // DEBUG: Check for invalid vertex positions
                if (!all(isfinite(output.positionCS))) {
                   // This check might be tricky to visualize directly from vertex shader
                   // but if NaN/Inf happens here, rasterizer fails.
                   // Outputting a fixed position if error occurs *might* help, but often fails anyway.
                   // output.positionCS = float4(0,0,0,1); // Doesn't guarantee visibility
                }


                return output;
            }

            // --- Minimal Fragment Shader ---
            float4 frag_minimal(Varyings_Minimal input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Bypass ALL calculations, textures, clipping, blending logic.
                // Return a bright, solid, fully opaque color.
                return float4(1.0, 0.0, 1.0, 1.0); // Magenta, Opaque
            }
            ENDHLSL
        } // End Pass
    }
    CustomEditor "UnityEditor.Rendering.HighDefinition.HDLitGUI" // Keep default editor for HDRP
}
