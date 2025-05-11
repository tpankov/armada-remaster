Shader "Custom/HDRP/FlipbookAnimatorArray"
{
    Properties
    {
        [Header(Rendering)]
        _BaseMap("Flipbook Texture", 2D) = "white" {}
        [Toggle] _UseEmissive("Use Emissive (Additive) Blending", Float) = 0
        _EmissiveColor("Emissive Color", Color) = (1, 1, 1, 1)
        _EmissiveIntensity("Emissive Intensity", Range(0, 10)) = 1.0
        _Alpha("Global Alpha", Range(0, 1)) = 1.0
        _billboardMode("Billboard Mode", Float) = 0 // 0: Non-Billboarded, 1: Billboarded

         [Header(Volumetric Depth Effect)]
        _DepthFadeStartDistance("Depth Fade Start (Soft Particle)", Float) = 0.1 // Start fading when this close to a surface
        _DepthFadeEndDistance("Depth Fade End (Soft Particle)", Float) = 1.0   // Fully faded (or opaque) by this distance from surface
        _UseVolumetricDensity("Use Volumetric Density", Float) = 0.0 // Toggle for fog-like density
        _VolumeMaxDepth("Volume Max Depth for Full Density", Float) = 10.0 // Depth at which fog reaches max configured density
        _VolumeDensityScale("Volume Density Scale", Range(0, 5)) = 1.0   // Multiplier for the volumetric effect

        [Header(Auto Animation)]
        _UseAutoRow("Use Auto Row Anim", Float) = 0 // 0 or 1
        _AutoRowTotalFrames("Auto Row Total Frames", Int) = 16
        _AutoRowFPS("Auto Row FPS", Float) = 24
        _UseAutoColumn("Use Auto Column Anim", Float) = 0 // 0 or 1
        _AutoColTotalFrames("Auto Column Total Frames", Int) = 16
        _AutoColFPS("Auto Column FPS", Float) = 24
        _offsetX("Offset X", Range(0, 1)) = 0.0 // Offset for auto animation
        _offsetY("Offset Y", Range(0, 1)) = 0.0 // Offset for auto animation
        _tileX("Tile X", Range(0, 1)) = 0.0625 // 1/16 for 16 frames in row
        _tileY("Tile Y", Range(0, 1)) = 0.0625 // 1/16 for 16 frames in column
        //_AutoFramesPerRow("Layout: Frames Per Row", Int) = 4 // Needed for tiling calc
        //_AutoFramesPerColumn("Layout: Frames Per Column", Int) = 4 // Needed for tiling calc

        [Header(Custom Animation Set via MPB)]
        // These are placeholders for MPB, default values don't matter much
        _InstanceStartTime("Instance Start Time", Float) = 0.0

        _OffsetFrameCount("Offset Frame Count", Int) = 0
        _OffsetDuration("Offset Duration", Float) = 1.0
        _OffsetInterpolation("Offset Interpolation", Float) = 0 // 0:Step, 1:Scroll, 2:Crossfade

        _TintFrameCount("Tint Frame Count", Int) = 0
        _TintDuration("Tint Duration", Float) = 1.0
        _TintInterpolation("Tint Interpolation", Float) = 0 // 0:Step, 1:Linear

        _DrawFrameCount("Draw Frame Count", Int) = 0
        _DrawDuration("Draw Duration", Float) = 1.0

        // Draw is always Step

        // HDRP specific properties (Keep defaults)
        [HideInInspector] _RenderQueueType("Render Queue Type", Float) = 5 // Transparent
        [HideInInspector] [ToggleUI] _AddPrecomputedVelocity("Add Precomputed Velocity", Float) = 0.0
        [HideInInspector] _SurfaceType("Surface Type", Float) = 1.0 // Transparent
        [HideInInspector] _BlendMode("Blend Mode", Float) = 0.0 // Alpha
        _SrcBlend("Source Blend", Float) = 1.0 // One
        _DstBlend("Destination Blend", Float) = 1.0 // Alpha
        [HideInInspector] _ZWrite("ZWrite", Float) = 0.0 // Off

        [ToggleUI] _EnableInstancing("Enable Instancing", Float) = 1.0 // IMPORTANT
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

            // Define max frames for arrays
            #define MAX_FRAMES 32 // Choose a limit (e.g., 32, 64)

            // Features for blending (optional but good practice)
            #pragma shader_feature_local _USEEMISSIVE_ON
            #pragma multi_compile_instancing

            // Includes
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl"

            // --- Uniform Variables ---
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            // Rendering
            float _Alpha;
            float4 _EmissiveColor;
            float _EmissiveIntensity;

            // Auto Anim
            float _UseAutoRow;
            int _AutoRowTotalFrames;
            float _AutoRowFPS;
            float _UseAutoColumn;
            int _AutoColTotalFrames;
            float _AutoColFPS;
            float _offsetX;
            float _offsetY;
            float _tileX;
            float _tileY;

            // Custom Anim Track Data (Set via MPB)
            float _InstanceStartTime;

            int _OffsetFrameCount;
            float _OffsetDuration;
            float _OffsetInterpolation; // 0:Step, 1:Scroll, 2:Crossfade
            float _OffsetFrameTimes[MAX_FRAMES];
            float4 _OffsetFrameData[MAX_FRAMES]; // xy=Offset, zw=Tiling

            int _TintFrameCount;
            float _TintDuration;
            float _TintInterpolation; // 0:Step, 1:Linear
            float _TintFrameTimes[MAX_FRAMES];
            float4 _TintFrameColors[MAX_FRAMES]; // rgba

            int _DrawFrameCount;
            float _DrawDuration;
            float _DrawFrameTimes[MAX_FRAMES];
            float _DrawFrameVisibilities[MAX_FRAMES]; // 0 or 1

            // Volumetric Depth Effect
            float _billboardMode; // 0: Non-Billboarded, 1: Billboarded
            float _DepthFadeStartDistance;
            float _DepthFadeEndDistance;
            float _UseVolumetricDensity;
            float _VolumeMaxDepth;
            float _VolumeDensityScale;

            // Helper Structs (same as before)
            struct FrameIndicesInfo { int index0; int index1; float t; };

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 baseUV       : TEXCOORD0; // Original model UV
                float4 instanceColor: COLOR;     // Per-instance color
                float4 frameTint    : TEXCOORD1; // Calculated tint
                float visibility    : TEXCOORD2; // Calculated visibility
                // Data for Offset Interpolation
                float4 uvData0      : TEXCOORD3; // Step/Scroll: Final OT | Crossfade: OT Frame 0
                float4 uvData1      : TEXCOORD4; // Crossfade: OT Frame 1
                float crossfadeLerp : TEXCOORD5; // Crossfade interpolation factor (0 if not crossfading)
                float viewSpaceZ    : TEXCOORD6; // View Space Z
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // --- Helper Functions ---

            // Finds indices and interpolation factor 't' for a given track time within uniform arrays
            // Assumes times_array is sorted
            FrameIndicesInfo FindIndices(const float times_array[MAX_FRAMES], int count, float track_time)
            {
                FrameIndicesInfo info;
                info.index0 = 0; info.index1 = 0; info.t = 0.0f;
                if (count <= 0) return info; // No keyframes

                // Find the last frame whose time <= track_time
                // (Loop capped by MAX_FRAMES implicitly)
                for (int i = 0; i < count; ++i) {
                    if (times_array[i] <= track_time) {
                        info.index0 = i;
                    } else {
                        break;
                    }
                }
                info.index1 = min(info.index0 + 1, count - 1); // Clamp next index

                // Calculate interpolation factor 't'
                if (info.index0 != info.index1) {
                    float time0 = times_array[info.index0];
                    float time1 = times_array[info.index1];
                    info.t = saturate((track_time - time0) / max(time1 - time0, 1e-6f));
                } else { // At or beyond the last keyframe, or only one keyframe
                    info.t = (count > 0 && times_array[info.index0] <= track_time) ? 1.0f : 0.0f;
                     if(count == 1) info.t = 1.0f;
                }
                return info;
            }

            // --- Vertex Shader ---
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 viewPos = float3(0,0,0); // Initialize view position

                // --- Calculate Billboarded World Position ---
                if (_billboardMode == 1) // 1: Billboarded, 0: Non-Billboarded
                {
                    
                    // 1. Get object's world position (its pivot)
                    float3 objectWorldPos = TransformObjectToWorld(float3(0,0,0)); // Assumes quad pivot is at its center

                    // 2. Get camera's right and up vectors in world space
                    // UNITY_MATRIX_V is the view matrix (world to camera/view space)
                    // We need the inverse to get camera orientation in world space.
                    // More directly, can use _WorldSpaceCameraPos and construct vectors.
                    float3 camRightWS = normalize(UNITY_MATRIX_V[0].xyz); // X-axis of camera in world space
                    float3 camUpWS    = normalize(UNITY_MATRIX_V[1].xyz); // Y-axis of camera in world space
                    // Note: For some setups or if issues arise with handedness/axes,
                    // you might need GetCameraRight() / GetCameraUp() from HDRP's ShaderVariablesFunctions.hlsl
                    // or reconstruct from _WorldSpaceCameraPos and a global up vector if strict alignment is needed.
                    // For basic spherical, using UNITY_MATRIX_V axes is common.

                    // 3. Calculate vertex offset from object pivot in camera space, then apply to object world pos
                    // input.positionOS.xy contains the local offsets of the quad's vertices from its pivot.
                    // (e.g., for a unit quad centered at origin, they are -0.5 to 0.5)
                    float3 vertexOffset = input.positionOS.x * camRightWS + input.positionOS.y * camUpWS;

                    // 4. Final world position of the vertex
                    float3 billboardedWorldPos = objectWorldPos + vertexOffset;

                    // 5. Transform to clip space
                    output.positionCS = TransformWorldToHClip(billboardedWorldPos);
                    viewPos = TransformWorldToView(billboardedWorldPos);
                }
                else // Non-billboarded (default)
                {
                    // For non-billboarded, just transform to clip space directly
                    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                    viewPos = TransformWorldToView(input.positionOS.xyz); // View space position of the vertex
                }

                output.viewSpaceZ = viewPos.z; // This is negative for objects in front of camera

                output.baseUV = input.uv;
                output.instanceColor = input.color;
                output.crossfadeLerp = 0.0f; // Default: not crossfading

                float instanceTime = _Time.y - _InstanceStartTime;

                // --- 1. Calculate Offset/Tiling (Custom overrides Auto) ---
                float4 finalOffsetTiling = float4(_offsetX,1-_offsetY-_tileY,_tileX,_tileY); // Default Identity
                float4 offsetTilingNext = float4(_offsetX,1-_offsetY-_tileY,_tileX,_tileY); // For crossfade


                if (_OffsetFrameCount > 0)
                {
                    float trackTime = fmod(instanceTime, max(_OffsetDuration, 1e-6f));
                    FrameIndicesInfo info = FindIndices(_OffsetFrameTimes, _OffsetFrameCount, trackTime);
                    finalOffsetTiling = _OffsetFrameData[info.index0];
                    finalOffsetTiling.y = 1 - finalOffsetTiling.w - _tileY; // Flip Y for UVs
                    finalOffsetTiling.zw = float2(_tileX, _tileY); // Tiling ZW
                    if (_OffsetInterpolation > 0.5) // Linear Scroll or Crossfade
                    {
                        offsetTilingNext = _OffsetFrameData[info.index1];
                        offsetTilingNext.y = 1 - offsetTilingNext.w - _tileY; // Flip Y for UVs
                        offsetTilingNext.zw = float2(_tileX, _tileY);
                         if (_OffsetInterpolation > 1.5) // Crossfade (== 2)
                         {
                             // Pass both sets of OT data, fragment shader does sampling/lerp
                             output.crossfadeLerp = info.t;
                         }
                         else // Linear Scroll (== 1)
                         {
                             // Interpolate offset XY only, use tiling ZW from frame 0
                             float2 interpolatedOffset = lerp(finalOffsetTiling.xy, offsetTilingNext.xy, info.t);
                             finalOffsetTiling = float4(interpolatedOffset, finalOffsetTiling.zw);
                         }
                    }
                     // else Step (== 0), just use finalOffsetTiling as is
                }
                else if (_UseAutoRow > 0.5 || _UseAutoColumn > 0.5) // Auto Animation (if no custom offset)
                {
                    // Calculate base tiling based on layout counts
                    float rowStep = 1/((float)_AutoRowTotalFrames); // Step size for row animation
                    float colStep = 1/((float)_AutoColTotalFrames); // Step size for column animation
                    //float tileSizeX = ; //(_AutoFramesPerRow > 0) ? (1.0 / _AutoFramesPerRow) : 1.0;
                    //float tileSizeY = _tileY; //(_AutoFramesPerColumn > 0) ? (1.0 / _AutoFramesPerColumn) : 1.0;
                    finalOffsetTiling = float4(_offsetX, 1 - _offsetY - _tileY, _tileX, _tileY); // Base tile
                    offsetTilingNext = float4(_offsetX, 1 - _offsetY - _tileY, _tileX, _tileY);
                    float rowOffset = 0;
                    float colOffset = 0;
                    float rowOffsetNext = 0; // For crossfade path
                    float colOffsetNext = 0; // For crossfade path
                    float rowFrame = 0;

                    // Calculate Row animation contribution
                    if (_UseAutoRow > 0.5 && _AutoRowTotalFrames > 0 && _AutoRowFPS > 0) {
                        float rowTime = instanceTime; // Use instance time directly for independent FPS
                        rowFrame = fmod(rowTime * _AutoRowFPS, (float)_AutoRowTotalFrames);
                        if (_OffsetInterpolation < 0.5) // Step (== 0)
                        {
                            rowOffset = floor(rowFrame) * rowStep; // Assumes rows run down vertically
                        }
                        else if (_OffsetInterpolation < 1.5) // Linear Scroll
                        {
                            rowOffset = rowFrame * rowStep; // Assumes rows run down vertically
                        }
                        else if (_OffsetInterpolation < 2.5) // Crossfade (== 2)
                        {
                            rowOffset = floor(rowFrame) * rowStep; 
                            rowOffsetNext = rowOffset + rowStep; // Next frame offset for crossfade
                            output.crossfadeLerp = fmod(rowFrame, 1.0); // Use fractional part for crossfade
                        }
                    }

                     // Calculate Column animation contribution
                    if (_UseAutoColumn > 0.5 && _AutoColTotalFrames > 0 && _AutoColFPS > 0) {
                        float colTime = instanceTime; // Use instance time directly for independent FPS
                        float colFrame = fmod(colTime * _AutoColFPS, (float)_AutoColTotalFrames);
                        if (_OffsetInterpolation < 0.5) // Step (== 0)
                        {
                            colOffset = floor(colFrame) * colStep; // Assumes columns run across horizontally
                        }
                        else if (_OffsetInterpolation < 1.5) // Linear Scroll
                        {
                            colOffset = colFrame * colStep; // Assumes columns run across horizontally
                        }
                        else if (_OffsetInterpolation < 2.5) // Crossfade (== 2)
                        {
                            colOffset = floor(colFrame) * colStep; 
                            if (_UseAutoRow > 0.5)
                            {
                                colOffsetNext = colOffset + colStep * floor(rowOffsetNext); // Next frame offset for crossfade
                            }
                            else
                            {
                                // Assumes columns run across horizontally                            
                                colOffsetNext = colOffset + colStep; // Next frame offset for crossfade
                                output.crossfadeLerp = fmod(colFrame, 1.0); // Use fractional part for crossfade
                            }
                        }
                    }
                    finalOffsetTiling.y -= colOffset;
                    finalOffsetTiling.x += rowOffset;
                    offsetTilingNext.y -= colOffsetNext; // Needed for crossfade path
                    offsetTilingNext.x += rowOffsetNext; // Needed for crossfade path
                }
                 // Else: No custom offset, no auto -> use default float4(0,0,1,1)

                output.uvData0 = finalOffsetTiling;
                output.uvData1 = offsetTilingNext; // Needed for crossfade path

                // --- 2. Calculate Tint ---
                output.frameTint = float4(1,1,1,1); // Default white
                if (_TintFrameCount > 0)
                {
                    float trackTime = fmod(instanceTime, max(_TintDuration, 1e-6f));
                    FrameIndicesInfo info = FindIndices(_TintFrameTimes, _TintFrameCount, trackTime);
                    float4 tint0 = _TintFrameColors[info.index0];

                    if (_TintInterpolation > 0.5) // Linear (== 1)
                    {
                        float4 tint1 = _TintFrameColors[info.index1];
                        output.frameTint = lerp(tint0, tint1, info.t);
                    }
                    else // Step (== 0)
                    {
                        output.frameTint = tint0;
                    }
                }
                 // Else: No custom tint -> use default white

                // --- 3. Calculate Visibility ---
                output.visibility = 1.0; // Default visible
                if (_DrawFrameCount > 0)
                {
                    float trackTime = fmod(instanceTime, max(_DrawDuration, 1e-6f));
                    FrameIndicesInfo info = FindIndices(_DrawFrameTimes, _DrawFrameCount, trackTime);
                    // Visibility is always stepped
                    output.visibility = _DrawFrameVisibilities[info.index0];
                }
                // Else: No custom visibility -> use default visible

                return output;
            }

            // --- Fragment Shader ---
            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Visibility Check
                clip(input.visibility > 0 ? 1 : -1);

                 // --- Depth Calculations ---
                // Screen UVs for sampling depth texture
                float2 screenUV = input.positionCS.xy / input.positionCS.w;
                screenUV = screenUV * 0.5 + 0.5;
                #if UNITY_UV_STARTS_AT_TOP // Required for DirectX
                    screenUV.y = 1.0 - screenUV.y;
                #endif

                // Sample scene depth (opaque objects behind this fragment)
                float sceneRawDepth = SampleCameraDepth(screenUV); // HDRP function
                // Linearize scene depth to view space (distance from camera plane)
                float sceneViewZ = LinearEyeDepth(sceneRawDepth, _ZBufferParams); // _ZBufferParams is built-in

                // Fragment's own view space depth (distance from camera plane)
                // input.viewSpaceZ is negative, make it positive distance
                float fragmentViewZ = -input.viewSpaceZ;

                // --- Calculate Depth-Based Alpha Modifier ---
                float depthAlphaMod = 1.0;

                // 1. Soft Particle Intersection Fade
                float depthDifference = sceneViewZ - fragmentViewZ; // Positive if fragment is in front
                float softFade = saturate((depthDifference - _DepthFadeStartDistance) / max(1e-6f, _DepthFadeEndDistance - _DepthFadeStartDistance));
                // softFade = 0 when fragment is at or behind _DepthFadeStartDistance from surface
                // softFade = 1 when fragment is at or beyond _DepthFadeEndDistance from surface

                depthAlphaMod *= softFade;

                // 2. Volumetric Density (Fog-like)
                if (_UseVolumetricDensity > 0.5)
                {
                    // Option A: Density based on how much "fog volume" is between fragment and background surface
                    float volumeThickness = saturate(depthDifference / max(1e-6f, _VolumeMaxDepth));

                    // Option B: Density based on distance from camera (more traditional fog)
                    // float viewDistanceFade = saturate(fragmentViewZ / max(1e-6f, _VolumeMaxDepth));
                    // viewDistanceFade = 1.0 - viewDistanceFade; // Closer is denser, or vice-versa

                    depthAlphaMod *= volumeThickness * _VolumeDensityScale; // Using Option A
                }

                // Calculate Final UVs and Sample Texture
                float4 texColor;
                if (input.crossfadeLerp > 0) // Check if crossfading active
                {
                    // Cross-fade requires two texture samples
                    float2 uv0 = input.baseUV * input.uvData0.zw + input.uvData0.xy;
                    float4 color0 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv0);

                    float2 uv1 = input.baseUV * input.uvData1.zw + input.uvData1.xy;
                    float4 color1 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv1);

                    texColor = lerp(color0, color1, input.crossfadeLerp);
                }
                else
                {
                    // Step or LinearScroll: Only need one sample using uvData0
                    float2 uv = input.baseUV * input.uvData0.zw + input.uvData0.xy;
                    texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                }

                // Apply Colors and Alpha
                float4 finalColor = texColor;
                finalColor *= input.instanceColor; // 1. Instance Color
                finalColor *= input.frameTint;     // 2. Frame Tint

                #if defined(_USEEMISSIVE_ON)        // 3. Emissive
                    finalColor.rgb *= _EmissiveColor.rgb * _EmissiveIntensity;
                #endif

                // Combine original alpha with depth-based modifier
                finalColor.a *= _Alpha; // Global alpha from properties
                finalColor.a *= depthAlphaMod; // Apply depth-based alpha
                finalColor.a = saturate(finalColor.a); // Clamp final alpha

                return finalColor;
            }
            ENDHLSL
        }
    }
    //CustomEditor "UnityEditor.Rendering.HighDefinition.HDLitGUI" // Keep default editor for HDRP
}