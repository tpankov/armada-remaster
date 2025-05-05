
using UnityEngine;
using System.Collections.Generic;
using CustomSpriteFormat;
using Unity.Mathematics;
using Unity.VisualScripting;
using TMPro.EditorUtilities;

[RequireComponent(typeof(Renderer))]
public class AdvancedFlipbookControllerArrays : MonoBehaviour
{
    // --- Constants ---
    private const int MAX_FRAMES = 32; // MUST MATCH SHADER #define

    // --- Cached Data (Set by Configure) ---
    // Note: Making these public allows setting via Inspector for testing,
    // but Configure is the intended method.
    public float instanceStartTime;

    public float offsetDuration = 1f;
    public float offsetInterpolation = 0f; // 0:Step, 1:Scroll, 2:Crossfade
    public List<float> offsetFrameTimes = new List<float>();
    public List<Vector4> offsetFrameData = new List<Vector4>(); // xy=Offset, zw=Tiling

    public float tintDuration = 1f;
    public float tintInterpolation = 0f; // 0:Step, 1:Linear
    public List<float> tintFrameTimes = new List<float>();
    public List<Color> tintFrameColors = new List<Color>();

    public float drawDuration = 1f;
    public List<float> drawFrameTimes = new List<float>();
    public List<float> drawFrameVisibilities = new List<float>(); // 0 or 1

    // --- Private ---
    private MeshRenderer _renderer;
    private MaterialPropertyBlock _propBlock;
    private bool _isConfigured = false;

    // Pre-sized arrays for sending data via MPB
    private float[] _offsetTimeArray = new float[MAX_FRAMES];
    private Vector4[] _offsetDataArray = new Vector4[MAX_FRAMES];
    private float[] _tintTimeArray = new float[MAX_FRAMES];
    private Vector4[] _tintColorArray = new Vector4[MAX_FRAMES]; // MPB needs Vector4 for color
    private float[] _drawTimeArray = new float[MAX_FRAMES];
    private float[] _drawVisArray = new float[MAX_FRAMES];


    // --- Shader Property IDs ---
    private static readonly int InstanceStartTimeID = Shader.PropertyToID("_InstanceStartTime");
    private static readonly int OffsetFrameCountID = Shader.PropertyToID("_OffsetFrameCount");
    private static readonly int OffsetDurationID = Shader.PropertyToID("_OffsetDuration");
    private static readonly int OffsetInterpolationID = Shader.PropertyToID("_OffsetInterpolation");
    private static readonly int OffsetFrameTimesID = Shader.PropertyToID("_OffsetFrameTimes");
    private static readonly int OffsetFrameDataID = Shader.PropertyToID("_OffsetFrameData");
    // ... (IDs for Tint and Draw tracks) ...
    private static readonly int TintFrameCountID = Shader.PropertyToID("_TintFrameCount");
    private static readonly int TintDurationID = Shader.PropertyToID("_TintDuration");
    private static readonly int TintInterpolationID = Shader.PropertyToID("_TintInterpolation");
    private static readonly int TintFrameTimesID = Shader.PropertyToID("_TintFrameTimes");
    private static readonly int TintFrameColorsID = Shader.PropertyToID("_TintFrameColors"); // Expects Vector4[]
    private static readonly int DrawFrameCountID = Shader.PropertyToID("_DrawFrameCount");
    private static readonly int DrawDurationID = Shader.PropertyToID("_DrawDuration");
    private static readonly int DrawFrameTimesID = Shader.PropertyToID("_DrawFrameTimes");
    private static readonly int DrawFrameVisibilitiesID = Shader.PropertyToID("_DrawFrameVisibilities");
    // ... (IDs for Auto properties if you need to set them dynamically, e.g., _AutoFramesPerRow) ...
    private static readonly int AutoFramesPerRowID = Shader.PropertyToID("_AutoFramesPerRow");
    private static readonly int AutoFramesPerColID = Shader.PropertyToID("_AutoFramesPerColumn");
     private static readonly int UseAutoRowID = Shader.PropertyToID("_UseAutoRow");
     private static readonly int AutoRowTotalFramesID = Shader.PropertyToID("_AutoRowTotalFrames");
     private static readonly int AutoRowFPSID = Shader.PropertyToID("_AutoRowFPS");
     private static readonly int UseAutoColumnID = Shader.PropertyToID("_UseAutoColumn");
     private static readonly int AutoColTotalFramesID = Shader.PropertyToID("_AutoColTotalFrames");
     private static readonly int AutoColFPSID = Shader.PropertyToID("_AutoColFPS");
     // ... (IDs for Alpha, Emissive etc if overriding per instance) ...
      private static readonly int AlphaID = Shader.PropertyToID("_Alpha");
      private static readonly int UseEmissiveID = Shader.PropertyToID("_UseEmissive");
      private static readonly int EmissiveColorID = Shader.PropertyToID("_EmissiveColor");
      private static readonly int EmissiveIntensityID = Shader.PropertyToID("_EmissiveIntensity");
      private static readonly int TextureID = Shader.PropertyToID("_BaseMap"); // Texture property ID

    void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
        _propBlock = new MaterialPropertyBlock();
    }

    // Call this from your Pool Manager after getting an instance
    public void Configure(EffectAnimationDataArrayBased data) // Use a new data struct
    {
        instanceStartTime = Time.time; // Set start time for animation sync

        // Copy data from struct (perform validation/clamping)
        offsetDuration = Mathf.Max(data.offsetDuration, 1e-6f);
        offsetInterpolation = data.offsetInterpolation;
        CopyToList(data.offsetFrameTimes, offsetFrameTimes, MAX_FRAMES);
        CopyToList(data.offsetFrameData, offsetFrameData, MAX_FRAMES);

        tintDuration = Mathf.Max(data.tintDuration, 1e-6f);
        tintInterpolation = data.tintInterpolation;
        CopyToList(data.tintFrameTimes, tintFrameTimes, MAX_FRAMES);
        CopyToList(data.tintFrameColors, tintFrameColors, MAX_FRAMES); // Color list

        drawDuration = Mathf.Max(data.drawDuration, 1e-6f);
        CopyToList(data.drawFrameTimes, drawFrameTimes, MAX_FRAMES);
        CopyToList(data.drawFrameVisibilities, drawFrameVisibilities, MAX_FRAMES);

        // Prepare arrays for MPB
        PrepareArray(_offsetTimeArray, offsetFrameTimes);
        PrepareArray(_offsetDataArray, offsetFrameData);
        PrepareArray(_tintTimeArray, tintFrameTimes);
        PrepareColorArray(_tintColorArray, tintFrameColors); // Special handling for Color -> Vector4
        PrepareArray(_drawTimeArray, drawFrameTimes);
        PrepareArray(_drawVisArray, drawFrameVisibilities);

        // Apply properties ONCE via MaterialPropertyBlock
        _renderer.GetPropertyBlock(_propBlock); // Start fresh or preserve other MPB props

        _propBlock.SetFloat(InstanceStartTimeID, instanceStartTime);

        _propBlock.SetInteger(OffsetFrameCountID, offsetFrameTimes.Count);
        _propBlock.SetFloat(OffsetDurationID, offsetDuration);
        _propBlock.SetFloat(OffsetInterpolationID, offsetInterpolation);
        if (offsetFrameTimes.Count > 0) {
             _propBlock.SetFloatArray(OffsetFrameTimesID, _offsetTimeArray);
             _propBlock.SetVectorArray(OffsetFrameDataID, _offsetDataArray);
        }
         // ... (Set Tint track arrays/properties) ...
         _propBlock.SetInteger(TintFrameCountID, tintFrameTimes.Count);
         _propBlock.SetFloat(TintDurationID, tintDuration);
         _propBlock.SetFloat(TintInterpolationID, tintInterpolation);
         if (tintFrameTimes.Count > 0) {
             _propBlock.SetFloatArray(TintFrameTimesID, _tintTimeArray);
             _propBlock.SetVectorArray(TintFrameColorsID, _tintColorArray); // Send Vector4 array
         }
         // ... (Set Draw track arrays/properties) ...
          _propBlock.SetInteger(DrawFrameCountID, drawFrameTimes.Count);
          _propBlock.SetFloat(DrawDurationID, drawDuration);
          if (drawFrameTimes.Count > 0) {
             _propBlock.SetFloatArray(DrawFrameTimesID, _drawTimeArray);
             _propBlock.SetFloatArray(DrawFrameVisibilitiesID, _drawVisArray);
          }

         // Set Auto params (assuming they come from EffectAnimationDataArrayBased)
         _propBlock.SetFloat(UseAutoRowID, data.useAutoRow ? 1.0f : 0.0f);
         _propBlock.SetInteger(AutoRowTotalFramesID, data.autoRowTotalFrames);
         _propBlock.SetFloat(AutoRowFPSID, data.autoRowFPS);
         _propBlock.SetFloat(UseAutoColumnID, data.useAutoColumn ? 1.0f : 0.0f);
         _propBlock.SetInteger(AutoColTotalFramesID, data.autoColTotalFrames);
         _propBlock.SetFloat(AutoColFPSID, data.autoColFPS);
         _propBlock.SetInteger(AutoFramesPerRowID, data.autoFramesPerRow);
         _propBlock.SetInteger(AutoFramesPerColID, data.autoFramesPerCol);

         // Set rendering params (optional overrides per instance)
         _propBlock.SetFloat(AlphaID, data.alpha);
         _propBlock.SetFloat(UseEmissiveID, data.useEmissive ? 1.0f : 0.0f);
         _propBlock.SetColor(EmissiveColorID, data.emissiveColor);
         _propBlock.SetFloat(EmissiveIntensityID, data.emissiveIntensity);

        _propBlock.SetTexture(TextureID, data.texture); // Set the texture

        _renderer.SetPropertyBlock(_propBlock);
        _isConfigured = true;
    }

     // Helper to copy list data to fixed-size arrays, padding if needed
    private void CopyToList<T>(List<T> source, List<T> dest, int maxCount)
    {
        dest.Clear();
        if (source != null) {
             int count = Mathf.Min(source.Count, maxCount);
             for(int i = 0; i < count; i++) {
                 dest.Add(source[i]);
             }
        }
    }

     private void PrepareArray<T>(T[] destArray, List<T> sourceList) where T : struct
     {
         int count = Mathf.Min(sourceList.Count, destArray.Length);
         for (int i = 0; i < count; i++) { destArray[i] = sourceList[i]; }
         // Optional: Zero out remaining elements in destArray if necessary
         // for (int i = count; i < destArray.Length; i++) { destArray[i] = default(T); }
     }

     private void PrepareColorArray(Vector4[] destArray, List<Color> sourceList)
     {
         int count = Mathf.Min(sourceList.Count, destArray.Length);
         for (int i = 0; i < count; i++) { destArray[i] = sourceList[i]; } // Implicit conversion
         // Optional: Zero out remaining elements
         // for (int i = count; i < destArray.Length; i++) { destArray[i] = Vector4.zero; }
     }

    // Optional: OnEnable could re-apply MPB if needed, but Configure should handle initial setup
    // void OnEnable() { if (_isConfigured) { /* Re-apply MPB if state gets lost? */ } }

     // Optional: Reset state when returned to pool
     public void OnReturnToPool() {
         _isConfigured = false;
         // Reset lists if needed, though Configure should overwrite them
         offsetFrameTimes.Clear();
         offsetFrameData.Clear();
         // etc.
     }
}

// Define a new struct to hold the data needed by the array-based controller
public struct EffectAnimationDataArrayBased
{
    // Auto anim settings
     public bool useAutoRow;
     public int autoRowTotalFrames;
     public float autoRowFPS;
     public bool useAutoColumn;
     public int autoColTotalFrames;
     public float autoColFPS;
     public int autoFramesPerRow; // Layout info needed by shader
     public int autoFramesPerCol; // Layout info needed by shader

    // Custom anim track data (use Lists for flexibility before passing to controller)
    public float offsetDuration;
    public float offsetInterpolation; // 0,1,2
    public List<float> offsetFrameTimes;
    public List<Vector4> offsetFrameData; // xy=Offset, zw=Tiling

    public float tintDuration;
    public float tintInterpolation; // 0,1
    public List<float> tintFrameTimes;
    public List<Color> tintFrameColors;

    public float drawDuration;
    public List<float> drawFrameTimes;
    public List<float> drawFrameVisibilities; // 0 or 1

    // Other per-instance overrides
    public float alpha;
    public bool useEmissive;
    public Color emissiveColor;
    public float emissiveIntensity;

    public Texture texture; // The actual texture to use

    public static void setDataFromAnim(AnimationDefinition animDef, SpriteAssetManager.ParsedSprite spriteDef,  ref EffectAnimationDataArrayBased data)
    {
        // Set data from AnimationDefinition to EffectAnimationDataArrayBased
        // Set values from spriteNode or other sources
        if (animDef == null)
        {
            // use values if no animation definition found
            data.autoColFPS = 0.0f;
            data.autoRowFPS = 0.0f; 
            data.autoFramesPerRow = 1; 
            data.autoFramesPerCol = 1;
            data.alpha = 1.0f; // Example alpha
        }
        else
        {
            
            if (animDef.type == AnimationType.Draw)
            {
                data.drawDuration = animDef.duration;
                data.drawFrameVisibilities = animDef.keyframes.ConvertAll(kf => kf.FloatValue); // Convert to visibility
                data.drawFrameTimes = animDef.keyframes.ConvertAll(kf => kf.time); // Convert to time
            }
            else if (animDef.type == AnimationType.Colour)
            {
                data.tintDuration = animDef.duration;
                data.tintFrameColors = animDef.keyframes.ConvertAll(kf => kf.ColorValue); // Convert to Color
                data.tintFrameTimes = animDef.keyframes.ConvertAll(kf => kf.time); // Convert to time
                data.tintInterpolation = (float)animDef.interpolation;
            }
            else if (animDef.type == AnimationType.Offset)
            {
                data.offsetDuration = animDef.duration;
                data.offsetInterpolation = (float)animDef.interpolation;

                if (animDef.autoKeyframe ==  AutoKeyframeType.Row)
                {
                    data.autoRowTotalFrames = animDef.frameCount; 
                    data.autoRowFPS = animDef.frameCount / animDef.duration;
                    data.autoFramesPerRow = (int)math.ceil(spriteDef.ReferenceSize / spriteDef.SourceRect.width);
                    data.useAutoRow = true; 
                }
                if (animDef.autoKeyframe == AutoKeyframeType.Column)
                {
                    data.autoColTotalFrames = animDef.frameCount; 
                    data.autoColFPS = animDef.frameCount / animDef.duration; 
                    data.autoFramesPerCol = (int)math.ceil(spriteDef.ReferenceSize / spriteDef.SourceRect.height);
                    data.useAutoColumn = true; 
                }
                else if (animDef.autoKeyframe == AutoKeyframeType.Grid)
                {
                    data.autoRowTotalFrames = (int)math.sqrt(animDef.frameCount);
                    data.autoColTotalFrames = (int)math.sqrt(animDef.frameCount);
                    data.autoFramesPerRow = (int)math.ceil(spriteDef.ReferenceSize / spriteDef.SourceRect.width); 
                    data.autoFramesPerCol = (int)math.ceil(spriteDef.ReferenceSize / spriteDef.SourceRect.height); 
                    data.autoRowFPS = animDef.frameCount / animDef.duration;
                    data.autoColFPS = animDef.frameCount / animDef.duration / data.autoColTotalFrames;
                    data.useAutoRow = true; 
                    data.useAutoColumn = true;
                }
                else
                {
                    data.autoRowFPS = 0.0f; 
                    data.autoColFPS = 0.0f; 
                    data.autoFramesPerRow = 1; 
                    data.autoFramesPerCol = 1;
                    data.useAutoRow = false; 
                    data.useAutoColumn = false; 
                    data.offsetFrameData = animDef.keyframes.ConvertAll(kf => (Vector4)kf.value); // Convert to Vector4
                    data.offsetFrameTimes = animDef.keyframes.ConvertAll(kf => kf.time); // Convert to time
                }
                
            }
            
        }
    }
    
    // public float lifetime; // Add if needed for pool auto-return
    public static EffectAnimationDataArrayBased CreateFromSpriteNode(SpriteNodeDefinition spriteNode)
    {
        SpriteAssetManager spriteAssetManager = SpriteAssetManager.Instance;
        if (spriteAssetManager == null)
        {
            Debug.LogErrorFormat($"SpriteAssetManager instance not found. Cannot create EffectAnimationData.{spriteNode.AnimationName}");
            return default;
        }
        EffectAnimationDataArrayBased data = new EffectAnimationDataArrayBased();
        AnimationDefinition animDef = spriteAssetManager.GetAnimation(spriteNode.AnimationName); // Ensure not null
        Sprite spr = spriteAssetManager.GetSprite(spriteNode.BaseSpriteName);
        SpriteAssetManager.ParsedSprite spriteDef = spriteAssetManager.GetParsedSpriteDefinition(spriteNode.BaseSpriteName);

        Debug.LogFormat($"AnimationDefinition for {spriteNode.NodeName} found. Creating EffectAnimationData {animDef.name}");
        data.tintDuration = animDef.frameCount/animDef.duration;
        data.useEmissive = spriteAssetManager.GetParsedSpriteDefinition(spriteNode.BaseSpriteName).MaterialType == MaterialType.Additive; // Example emissive setting
        data.emissiveIntensity = 6.0f; // Example emissive intensity
        data.emissiveColor = spriteNode.Tint; 
        data.alpha = 1.0f; 
        setDataFromAnim(animDef, spriteDef, ref data); // Set data from AnimationDefinition
        
        // Texture animation
        string animName = spriteAssetManager.GetParsedSpriteDefinition(spriteNode.BaseSpriteName).DefaultAnimationName;
        animDef = spriteAssetManager.GetAnimation(animName); // Ensure not null
        Debug.LogFormat($"Texture AnimationDefinition for {spriteNode.BaseSpriteName} found. Creating EffectAnimationData {animDef.name}");
        setDataFromAnim(animDef, spriteDef, ref data); // Set data from AnimationDefinition

        // print some values from data
        Debug.LogFormat($"EffectAnimationData {spriteNode.NodeName} - offsetDuration: {data.offsetDuration}, tintDuration: {data.tintDuration}, drawDuration: {data.drawDuration}");
        // Set the actual texture.
        data.texture = spr.texture; // Get the texture from the sprite asset manager
        //data.useEmissive = spriteAssetManager.GetParsedSpriteDefinition(spriteNode.BaseSpriteName).MaterialType == MaterialType.Additive; // Example emissive setting
        //data.emissiveColor = Color.white; // Example emissive color
        return data;
    }
}
    //  // Constructor or factory method to convert from SpriteData
    //  public static EffectAnimationData CreateFrom(AnimationDefinition offsetAnim, AnimationDefinition tintAnim, AnimationDefinition drawAnim, /* other params like SpriteNodeDefinition for defaults */ float defaultFps)
    //  {
    //      EffectAnimationData data = new EffectAnimationData();
    //      data.framesPerSecond = offsetAnim.frameCount/offsetAnim.duration; // Example default
    //      // Set auto values from SpriteNodeDefinition or defaults
    //      data.autoType = AdvancedFlipbookController.FlipbookType.Grid; // Example
    //      data.autoTotalFrames = 16; // Example
    //      data.autoFramesPerRow = 4; // Example
    //      data.autoTint = Color.white; // Example
    //      data.autoVisible = true; // Example

    //      // --- Conversion Logic ---
    //      // This is where you translate AnimationDefinition into the controller's format
    //      data.offsetKeyframes = ConvertOffsetKeyframes(offsetAnim);
    //      data.tintKeyframes = ConvertTintKeyframes(tintAnim);
    //      data.drawKeyframes = ConvertDrawKeyframes(drawAnim);

    //      // Determine interpolation modes based on AnimationDefinition
    //      data.offsetInterpolation = offsetAnim.interpolation;//ConvertInterpolationMode_Offset(offsetAnim?.interpolation ?? InterpolationMode.Step);
    //      data.tintInterpolation = tintAnim.interpolation == AdvancedFlipbookController.OffsetInterpolation.Step ? AdvancedFlipbookController.TintInterpolation.Step : AdvancedFlipbookController.TintInterpolation.Linear;

    //      // Set other properties like lifetime, base tint, alpha etc.
    //      data.tint = Color.white; // Example
    //      data.alpha = 1.0f; // Example
    //      data.lifetime = offsetAnim?.duration ?? tintAnim?.duration ?? drawAnim?.duration ?? 2.0f; // Estimate lifetime

    //      return data;
    //  }

    // --- TODO: Implement these conversion functions ---
//      private static List<AdvancedFlipbookController.OffsetKeyframe> ConvertOffsetKeyframes(AnimationDefinition animDef) {
//          var list = new List<AdvancedFlipbookController.OffsetKeyframe>();
//          if (animDef == null || animDef.type != AnimationType.Offset) return list; // Return empty if null or wrong type

//          // Example: Assuming animDef.keyframes[i].value is already Vector4(offsetX, offsetY, tileX, tileY)
//          // You'll need to calculate this based on your SpriteDefinition sourceRect etc.
//          foreach (var kf in animDef.keyframes) {
//               // This is pseudo-code - you need the actual calculation based on your source data format
//               // Vector4 offsetAndTiling = CalculateOffsetTilingFromSpriteIndex(kf.IntValue, spriteDefinition);
//               Vector4 offsetAndTiling = Vector4.zero; // Placeholder! Needs real calculation.
//               if (kf.value is Vector4 v4) offsetAndTiling = v4; // Use if value is already correct Vector4

//              list.Add(new AdvancedFlipbookController.OffsetKeyframe { time = kf.time, offsetAndTiling = offsetAndTiling });
//          }
//          list.Sort((a, b) => a.time.CompareTo(b.time)); // Ensure sorted
//          return list;
//      }

//      private static List<AdvancedFlipbookController.TintKeyframe> ConvertTintKeyframes(AnimationDefinition animDef) {
//           var list = new List<AdvancedFlipbookController.TintKeyframe>();
//          if (animDef == null || animDef.type != AnimationType.Colour) return list;

//          foreach (var kf in animDef.keyframes) {
//              list.Add(new AdvancedFlipbookController.TintKeyframe { time = kf.time, color = kf.ColorValue });
//          }
//           list.Sort((a, b) => a.time.CompareTo(b.time));
//          return list;
//      }

//       private static List<AdvancedFlipbookController.DrawKeyframe> ConvertDrawKeyframes(AnimationDefinition animDef) {
//           // Draw animation likely controls OFFSET frames in the original design.
//           // If Draw means Visibility in the new shader, convert accordingly.
//           // If Draw means switching Sprites (which implies Offset changes), handle in ConvertOffsetKeyframes.
//           // This example assumes Draw means Visibility:
//           var list = new List<AdvancedFlipbookController.DrawKeyframe>();
//          if (animDef == null || animDef.type != AnimationType.Draw) return list; // Adjust type check if needed

//          foreach (var kf in animDef.keyframes) {
//              // Assuming kf.IntValue > 0 means visible? Adjust logic as needed.
//              list.Add(new AdvancedFlipbookController.DrawKeyframe { time = kf.time, visibility = (kf.IntValue > 0) ? 1.0f : 0.0f });
//          }
//          list.Sort((a, b) => a.time.CompareTo(b.time));
//          return list;
//      }

//     //   private static AdvancedFlipbookController.OffsetInterpolation ConvertInterpolationMode_Offset(InterpolationMode mode) {
//     //      switch (mode) {
//     //          case InterpolationMode.Linear: return AdvancedFlipbookController.OffsetInterpolation.LinearScroll; // Map Linear to Scroll? Or Crossfade? Decide based on desired effect.
//     //          case InterpolationMode.LinearCrossfade: return AdvancedFlipbookController.OffsetInterpolation.LinearCrossfade;
//     //          case InterpolationMode.Step:
//     //          default: return AdvancedFlipbookController.OffsetInterpolation.Step;
//     //      }
//     //  }
//     //   private static AdvancedFlipbookController.TintInterpolation ConvertInterpolationMode_Tint(InterpolationMode mode) {
//     //     return (mode == InterpolationMode.Linear || mode == InterpolationMode.LinearCrossfade) // Treat Crossfade as Linear for Tint
//     //         ? AdvancedFlipbookController.TintInterpolation.Linear
//     //         : AdvancedFlipbookController.TintInterpolation.Step;
//     // }

// }