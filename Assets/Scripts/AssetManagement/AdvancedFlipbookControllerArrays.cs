
using UnityEngine;
using System.Collections.Generic;
using CustomSpriteFormat;
using Unity.Mathematics;
// using Unity.VisualScripting;
// using TMPro.EditorUtilities;

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
    //private bool _isConfigured = false;

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
    // private static readonly int AutoFramesPerRowID = Shader.PropertyToID("_AutoFramesPerRow");
    // private static readonly int AutoFramesPerColID = Shader.PropertyToID("_AutoFramesPerColumn");
    private static readonly int OffsetxID = Shader.PropertyToID("_offsetX");
    private static readonly int OffsetyID = Shader.PropertyToID("_offsetY");
    private static readonly int TilingxID = Shader.PropertyToID("_tileX");
    private static readonly int TilingyID = Shader.PropertyToID("_tileY");
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

    private static readonly int NormalTextureID = Shader.PropertyToID("_BumpMap");
    private static readonly int EmissiveTextureID = Shader.PropertyToID("_EmissiveMap"); // Emissive texture property ID

    //private static readonly int SrcBlendID = Shader.PropertyToID("_SrcBlend");
    //private static readonly int DstBlendID = Shader.PropertyToID("_DstBlend");

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
        //  _propBlock.SetInteger(AutoFramesPerRowID, data.autoFramesPerRow);
        //  _propBlock.SetInteger(AutoFramesPerColID, data.autoFramesPerCol);
        _propBlock.SetFloat(OffsetxID, data.Offsetx); // Set the offset x value
        _propBlock.SetFloat(OffsetyID, data.Offsety); // Set the offset y value
        _propBlock.SetFloat(TilingxID, data.Tilingx); // Set the tiling x value
        _propBlock.SetFloat(TilingyID, data.Tilingy); // Set the tiling y value

        // Set rendering params (optional overrides per instance)
        _propBlock.SetFloat(AlphaID, data.alpha);
        _propBlock.SetFloat(UseEmissiveID, data.useEmissive ? 1.0f : 0.0f);
        _propBlock.SetColor(EmissiveColorID, data.emissiveColor);
        _propBlock.SetFloat(EmissiveIntensityID, data.emissiveIntensity);
        //_propBlock.SetFloat(SrcBlendID, data.materialType == MaterialType.Additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        //_propBlock.SetFloat(DstBlendID, data.materialType == MaterialType.Additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        // Essential for HDRP transparency

        if (data.texture != null) // Check if texture is not null
        {
            _propBlock.SetTexture(TextureID, data.texture); // Set the texture
        }
        else
        {
            Debug.LogError("Texture is null. Cannot set texture property.");
        }
        if (data.normalTexture != null) _propBlock.SetTexture(NormalTextureID, data.normalTexture); // Set the normal texture
        if (data.emissiveTexture != null) _propBlock.SetTexture(EmissiveTextureID, data.emissiveTexture); // Set the emissive texture


        _renderer.SetPropertyBlock(_propBlock);
        //_isConfigured = true;
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
         //_isConfigured = false;
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
    //  public int autoFramesPerRow; // Layout info needed by shader
    //  public int autoFramesPerCol; // Layout info needed by shader
    public float Offsetx;
    public float Offsety;
    public float Tilingx;
    public float Tilingy;

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

    public MaterialType materialType;
    public Texture texture; // The actual texture to use
    public Texture normalTexture; // The normal texture to use
    public Texture emissiveTexture; // The emissive texture to use

    public static void setDataFromAnim(AnimationDefinition animDef, SpriteAssetManager.ParsedSprite spriteDef,  ref EffectAnimationDataArrayBased data)
    {
        // Set data from AnimationDefinition to EffectAnimationDataArrayBased
        // Set values from spriteNode or other sources
        if (animDef == null)
        {
            // use values if no animation definition found
            data.autoColFPS = 0.0f;
            data.autoRowFPS = 0.0f; 
            data.Offsetx = 0;
            data.Offsety = 0;
            data.Tilingx = 1;
            data.Tilingy = 1; 
            //data.autoFramesPerCol = 1;
            data.alpha = 1.0f; // Example alpha
        }
        else
        {
            data.Offsetx = spriteDef.SourceRect.xMin / spriteDef.ReferenceSize;
            data.Offsety = spriteDef.SourceRect.yMin / spriteDef.ReferenceSize;
            data.Tilingx = spriteDef.SourceRect.width / spriteDef.ReferenceSize;
            data.Tilingy = spriteDef.SourceRect.height / spriteDef.ReferenceSize;
            if (animDef.type == AnimationType.Draw)
            {
                data.drawDuration = animDef.duration;
                data.drawFrameVisibilities = animDef.keyframes.ConvertAll(kf => (float)kf.IntValue); // Convert to visibility
                data.drawFrameTimes = animDef.keyframes.ConvertAll(kf => kf.time); // Convert to time
                Debug.Log($"Draw: {data.drawDuration}, {data.drawFrameVisibilities.Count} frames.");
            }
            else if (animDef.type == AnimationType.Colour)
            {
                data.tintDuration = animDef.duration;
                data.tintFrameColors = animDef.keyframes.ConvertAll(kf => kf.ColorValue); // Convert to Color
                data.tintFrameTimes = animDef.keyframes.ConvertAll(kf => kf.time); // Convert to time
                data.tintInterpolation = (float)animDef.interpolation;
                Debug.Log($"Tint: {data.tintDuration}, {data.tintFrameColors.Count} frames.");
            }
            else if (animDef.type == AnimationType.Offset)
            {
                data.offsetDuration = animDef.duration;
                data.offsetInterpolation = (float)animDef.interpolation;

                if (animDef.autoKeyframe ==  AutoKeyframeType.Row)
                {
                    data.autoRowTotalFrames = animDef.frameCount; 
                    data.autoRowFPS = animDef.frameCount / animDef.duration;
                    //data.autoFramesPerRow = (int)math.ceil(spriteDef.ReferenceSize / spriteDef.SourceRect.width);
                    data.useAutoRow = true; 
                    //Debug.Log($"AutoRow: {data.autoRowTotalFrames}, {data.autoFramesPerRow}, {data.autoRowFPS}, {data.useAutoRow}.");
                }
                else if (animDef.autoKeyframe == AutoKeyframeType.Column)
                {
                    data.autoColTotalFrames = animDef.frameCount; 
                    data.autoColFPS = animDef.frameCount / animDef.duration; 
                    //data.autoFramesPerCol = (int)math.ceil(spriteDef.ReferenceSize / spriteDef.SourceRect.height);
                    data.useAutoColumn = true; 
                    //Debug.Log($"AutoColumn: {data.autoColTotalFrames}, {data.autoFramesPerCol}, {data.autoColFPS}, {data.useAutoColumn}.");
                }
                else if (animDef.autoKeyframe == AutoKeyframeType.Grid)
                {
                    data.autoRowTotalFrames = (int)math.sqrt(animDef.frameCount);
                    data.autoColTotalFrames = (int)math.sqrt(animDef.frameCount);
                    //data.autoFramesPerRow = (int)math.ceil(spriteDef.ReferenceSize / spriteDef.SourceRect.width); 
                    //data.autoFramesPerCol = (int)math.ceil(spriteDef.ReferenceSize / spriteDef.SourceRect.height); 
                    data.autoRowFPS = animDef.frameCount / animDef.duration;
                    data.autoColFPS = animDef.frameCount / animDef.duration / data.autoColTotalFrames;
                    data.useAutoRow = true; 
                    data.useAutoColumn = true;
                   // Debug.Log($"AutoGrid: {data.autoRowTotalFrames}, {data.autoColTotalFrames}, {data.autoFramesPerRow}, {data.autoFramesPerCol}, {data.autoRowFPS}, {data.autoColFPS}");
                }
                else
                {
                    data.autoRowFPS = 0.0f; 
                    data.autoColFPS = 0.0f; 
                    data.useAutoRow = false; 
                    data.useAutoColumn = false; 
                    data.offsetFrameData = animDef.keyframes.ConvertAll(kf => (Vector4)kf.value); // Convert to Vector4
                    data.offsetFrameTimes = animDef.keyframes.ConvertAll(kf => kf.time); // Convert to time
                    Debug.Log($"Custom Offset: {data.offsetDuration}, {data.offsetFrameData.Count} frames.");
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
        if (animDef == null)
        {
            Debug.LogErrorFormat($"AnimationDefinition for {spriteNode.AnimationName} not found. Cannot create EffectAnimationData.{spriteNode.AnimationName}");
            return default;
        }
        Sprite spr = spriteAssetManager.GetSprite(spriteNode.BaseSpriteName);
        if (spr == null)
        {
            Debug.LogErrorFormat($"Sprite {spriteNode.BaseSpriteName} not found.");
            return default;
        }
        SpriteAssetManager.ParsedSprite spriteDef = spriteAssetManager.GetParsedSpriteDefinition(spriteNode.BaseSpriteName);
        if (spriteDef == null)
        {
            Debug.LogErrorFormat($"ParsedSpriteDefinition for {spriteNode.BaseSpriteName} not found.");
            return default;
        }

        Debug.LogFormat($"AnimationDefinition for {spriteNode.NodeName} found. Creating EffectAnimationData {animDef.name}");
        data.tintDuration = animDef.frameCount/animDef.duration;
        data.useEmissive = spriteAssetManager.GetParsedSpriteDefinition(spriteNode.BaseSpriteName).MaterialType == MaterialType.Additive; // Example emissive setting
        data.materialType = spriteAssetManager.GetParsedSpriteDefinition(spriteNode.BaseSpriteName).MaterialType; // Example material type
        data.emissiveIntensity = 6.0f; // Example emissive intensity
        data.emissiveColor = spriteNode.Tint; 
        data.alpha = 1.0f; 
        setDataFromAnim(animDef, spriteDef, ref data); // Set data from AnimationDefinition
        
        // Texture animation
        string animName = spriteAssetManager.GetParsedSpriteDefinition(spriteNode.BaseSpriteName).DefaultAnimationName;
        if (animName == null || animName == "const")
        {
            // do nothing;
        }
        else
        {
            animDef = spriteAssetManager.GetAnimation(animName); // Ensure not null
            Debug.LogFormat($"Texture AnimationDefinition for {spriteNode.BaseSpriteName} found. Creating EffectAnimationData {animDef.name}");
            setDataFromAnim(animDef, spriteDef, ref data); // Set data from AnimationDefinition
        }
        // print some values from data
        Debug.LogFormat($"EffectAnimationData {spriteNode.NodeName} - offsetDuration: {data.offsetDuration}, tintDuration: {data.tintDuration}, drawDuration: {data.drawDuration}");
        // Set the actual texture.
        data.texture = spr.texture; // Get the texture from the sprite asset manager
        return data;
    }
}
