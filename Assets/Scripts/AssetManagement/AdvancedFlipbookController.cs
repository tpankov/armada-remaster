using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

[RequireComponent(typeof(Renderer))]
public class AdvancedFlipbookController : MonoBehaviour
{
    // Keep public fields for things that might be SET by Configure
    // but potentially read elsewhere (though Configure is preferred)
    public float framesPerSecond = 24f;
    public FlipbookType autoFlipbookType = FlipbookType.Grid;
    public int autoTotalFrames = 16;
    public int autoFramesPerRow = 4;
    public Color autoTint = Color.white;
    public bool autoVisible = true;
    public OffsetInterpolation offsetInterpolation = OffsetInterpolation.Step;
    public TintInterpolation tintInterpolation = TintInterpolation.Step;

    // Make keyframe lists private, managed internally
    private List<OffsetKeyframe> _offsetKeyframes = new List<OffsetKeyframe>();
    private List<TintKeyframe> _tintKeyframes = new List<TintKeyframe>();
    private List<DrawKeyframe> _drawKeyframes = new List<DrawKeyframe>();

    // Private runtime variables
    private Renderer _renderer;
    private MaterialPropertyBlock _propBlock;
    private ComputeBuffer _offsetBuffer;
    private ComputeBuffer _tintBuffer;
    private ComputeBuffer _drawBuffer;
    private bool _buffersDirty = true;
    private float _timeOffset = 0.0f; // Per-instance time offset for variety
    private float _effectLifetime = -1f; // Used for auto-return
    private string _poolId = null; // Store the ID of the pool this came from
    private Coroutine _lifetimeCoroutine = null;


    // Keep Property IDs static
    private static readonly int BaseMapID = Shader.PropertyToID("_BaseMap"); // Needed if overriding via MPB (requires specific setup)
    private static readonly int OffsetKeyframesID = Shader.PropertyToID("_OffsetKeyframes");
    private static readonly int TintKeyframesID = Shader.PropertyToID("_TintKeyframes");
    private static readonly int DrawKeyframesID = Shader.PropertyToID("_DrawKeyframes");

     private static readonly int TimeOffsetID = Shader.PropertyToID("_TimeOffset");
     private static readonly int FramesPerSecondID = Shader.PropertyToID("_FramesPerSecond");
     private static readonly int AutoFlipbookTypeID = Shader.PropertyToID("_AutoFlipbookType"); // Match shader [cite: 2]
     private static readonly int AutoTotalFramesID = Shader.PropertyToID("_AutoTotalFrames"); // Match shader [cite: 2]
     private static readonly int AutoFramesPerRowID = Shader.PropertyToID("_AutoFramesPerRow"); // Match shader [cite: 2]
     private static readonly int AutoTintID = Shader.PropertyToID("_AutoTint"); // Match shader [cite: 2]
     private static readonly int AutoVisibilityID = Shader.PropertyToID("_AutoVisibility"); // Match shader [cite: 2]
     private static readonly int OffsetInterpolationID = Shader.PropertyToID("_OffsetInterpolation");
     private static readonly int TintInterpolationID = Shader.PropertyToID("_TintInterpolation");
     private static readonly int AlphaID = Shader.PropertyToID("_Alpha"); // [cite: 4]
     private static readonly int UseEmissiveID = Shader.PropertyToID("_UseEmissive"); // [cite: 3]
     private static readonly int EmissiveColorID = Shader.PropertyToID("_EmissiveColor"); // [cite: 3]
     private static readonly int EmissiveIntensityID = Shader.PropertyToID("_EmissiveIntensity"); // [cite: 4]
     private static readonly int OffsetKeyframeCountID = Shader.PropertyToID("_OffsetKeyframeCount"); // [cite: 5]
     private static readonly int TintKeyframeCountID = Shader.PropertyToID("_TintKeyframeCount"); // [cite: 5]
     private static readonly int DrawKeyframeCountID = Shader.PropertyToID("_DrawKeyframeCount"); // [cite: 6]

    // Enums match the script struct, which should align with shader properties [cite: 2, 3]
    public enum FlipbookType { Row = 0, Column = 1, Grid = 2, Unknown = 3 }
    public enum OffsetInterpolation { Step = 0, LinearScroll = 1, LinearCrossfade = 2 }
    public enum TintInterpolation { Step = 0, Linear = 1 }

    // Struct definitions remain the same [cite: 10, 12, 14]
    [System.Serializable] [StructLayout(LayoutKind.Sequential)] public struct OffsetKeyframe { public float time; public Vector4 offsetAndTiling; }
    [System.Serializable] [StructLayout(LayoutKind.Sequential)] public struct TintKeyframe { public float time; public Color color; }
    [System.Serializable] [StructLayout(LayoutKind.Sequential)] public struct DrawKeyframe { public float time; public float visibility; }


    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _propBlock = new MaterialPropertyBlock();
        // Buffers are created/updated on demand by Configure/UpdateComputeBuffer
    }

    /// <summary>
    /// Configures the controller with new animation data. Call this after spawning.
    /// </summary>
    public void Configure(EffectAnimationData data, string poolIdForReturn = null)
    {
        Debug.Log($"Configuring {gameObject.name} with new animation data.");
        this._poolId = poolIdForReturn; // Store pool ID for auto-return

        // Apply settings from data struct
        this.framesPerSecond = data.framesPerSecond;
        this.autoFlipbookType = data.autoType;
        this.autoTotalFrames = data.autoTotalFrames;
        this.autoFramesPerRow = data.autoFramesPerRow;
        this.autoTint = data.autoTint;
        this.autoVisible = data.autoVisible;
        this.offsetInterpolation = data.offsetInterpolation;
        this.tintInterpolation = data.tintInterpolation;

        // Apply overrides for MPB (more efficient than setting material directly)
        _propBlock.SetFloat(AlphaID, data.alpha);
        _propBlock.SetFloat(UseEmissiveID, data.useEmissive ? 1.0f : 0.0f);
        _propBlock.SetColor(EmissiveColorID, data.emissiveColor);
        _propBlock.SetFloat(EmissiveIntensityID, data.emissiveIntensity);
        // NOTE: Setting _BaseMap via MPB usually doesn't work without Texture Arrays
        // Base texture should be set on the Material used by the prefab pool.
        _propBlock.SetTexture(BaseMapID, data.texture); // Usually ineffective

        // Replace internal keyframe lists if none are provided
        _offsetKeyframes = data.offsetKeyframes ?? new List<OffsetKeyframe>();
        _tintKeyframes = data.tintKeyframes ?? new List<TintKeyframe>();
        _drawKeyframes = data.drawKeyframes ?? new List<DrawKeyframe>();

        // --- IMPORTANT: Mark buffers as dirty so they regenerate ---
        MarkBuffersDirty();

        // Apply initial MPB state immediately (including auto/interpolation settings)
        ApplyMaterialPropertyBlock();

        // Reset time and handle lifetime
        _timeOffset = 0;//Random.Range(-5f, 5f); // Add random offset for variety? Or set from data?
         _effectLifetime = data.lifetime;
         ResetLifetime(); // Start or stop the auto-return timer
         // Debug print a few values for sanity check
         Debug.Log($"Configured {gameObject.name} with {_offsetKeyframes.Count} offset keyframes, " +
             $"{_tintKeyframes.Count} tint keyframes, and {_drawKeyframes.Count} draw keyframes.");
    }


    // Called by Pool Manager when taking from pool (optional)
    // public void OnSpawn() { ResetLifetime(); }

    // Called by Pool Manager before returning to pool (optional)
    public void OnReturnToPool()
    {
        // Stop any running coroutines
        if (_lifetimeCoroutine != null)
        {
            StopCoroutine(_lifetimeCoroutine);
            _lifetimeCoroutine = null;
        }
         // Reset any other transient state if necessary
         transform.localScale = Vector3.one; // Reset scale if modified
         this._poolId = null; // Clear pool ID
    }


    void Update()
    {
        // Update buffers if needed (only happens once after Configure or manual mark)
        if (_buffersDirty)
        {
            UpdateComputeBuffer(ref _offsetBuffer, _offsetKeyframes, Marshal.SizeOf(typeof(OffsetKeyframe)));
            UpdateComputeBuffer(ref _tintBuffer, _tintKeyframes, Marshal.SizeOf(typeof(TintKeyframe)));
            UpdateComputeBuffer(ref _drawBuffer, _drawKeyframes, Marshal.SizeOf(typeof(DrawKeyframe)));
            _buffersDirty = false;
        }

        // Apply dynamic per-frame properties (like TimeOffset if needed)
        // We only need to call ApplyMaterialPropertyBlock IF something dynamic changes
        // OR if the buffers were just updated. Since buffers are handled above,
        // we might only need this if TimeOffset changes frame-to-frame.
        // For simplicity here, we call it always, but could optimize.
        // ApplyMaterialPropertyBlock();
    }

    void ApplyMaterialPropertyBlock()
    {
         if (_propBlock == null) _propBlock = new MaterialPropertyBlock(); // Ensure exists

        // Get current block state *only if* you need to preserve values not set below
        _renderer.GetPropertyBlock(_propBlock);
        //Debug.Log($"Applying material property block to {gameObject.name}.");

        // Set Buffers (Bind the buffer, dummy or real)
        if (_offsetBuffer != null && _offsetBuffer.IsValid()) _propBlock.SetBuffer(OffsetKeyframesID, _offsetBuffer);
        if (_tintBuffer != null && _tintBuffer.IsValid()) _propBlock.SetBuffer(TintKeyframesID, _tintBuffer);
        if (_drawBuffer != null && _drawBuffer.IsValid()) _propBlock.SetBuffer(DrawKeyframesID, _drawBuffer);

        // Set Counts (Crucially, set the *actual* list count)
        _propBlock.SetInteger(OffsetKeyframeCountID, _offsetKeyframes.Count);
        _propBlock.SetInteger(TintKeyframeCountID, _tintKeyframes.Count);
        _propBlock.SetInteger(DrawKeyframeCountID, _drawKeyframes.Count);
        Debug.Log($"Set keyframe counts: Offset: {_offsetKeyframes.Count}, Tint: {_tintKeyframes.Count}, Draw: {_drawKeyframes.Count}");

        // Set General/Auto/Interpolation properties FROM MEMBER FIELDS
        _propBlock.SetFloat(TimeOffsetID, _timeOffset); // Use the instance's time offset
        _propBlock.SetFloat(FramesPerSecondID, framesPerSecond);
        _propBlock.SetFloat(AutoFlipbookTypeID, (float)autoFlipbookType);
        _propBlock.SetInteger(AutoTotalFramesID, autoTotalFrames);
        _propBlock.SetInteger(AutoFramesPerRowID, autoFramesPerRow);
        _propBlock.SetColor(AutoTintID, autoTint);
        _propBlock.SetFloat(AutoVisibilityID, autoVisible ? 1.0f : 0.0f);
        _propBlock.SetFloat(OffsetInterpolationID, (float)offsetInterpolation);
        _propBlock.SetFloat(TintInterpolationID, (float)tintInterpolation);
        
        // Apply other properties previously set in Configure (alpha, emissive etc)
        // These were likely set once in Configure, but applying again doesn't hurt
        // (If MPB was cleared via GetPropertyBlock, you *must* re-apply them here)
        // _propBlock.SetFloat(AlphaID, ...); // Read from member field if needed
        
        // Debug log all of the properties being set (optional)
        Debug.Log($"TimeOffset: {_timeOffset}, FPS: {framesPerSecond}, AutoType: {autoFlipbookType}, " +
            $"AutoFrames: {autoTotalFrames}, AutoRow: {autoFramesPerRow}, AutoTint: {autoTint}, " +
            $"AutoVisible: {autoVisible}, OffsetInterpolation: {offsetInterpolation}, " +
            $"TintInterpolation: {tintInterpolation}");

        _renderer.SetPropertyBlock(_propBlock);
        // Debug.Log($"Applied material property block to {gameObject.name}.");
    }


    // --- Lifetime Management ---
    void ResetLifetime()
    {
        if (_lifetimeCoroutine != null)
        {
            StopCoroutine(_lifetimeCoroutine);
        }
        if (_effectLifetime > 0 && !string.IsNullOrEmpty(_poolId)) // Only start if lifetime > 0 and pool is known
        {
             _lifetimeCoroutine = StartCoroutine(LifetimeCoroutine(_effectLifetime));
        }
         else {
              _lifetimeCoroutine = null;
         }
    }

    IEnumerator LifetimeCoroutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        // Time's up, return to pool
        // Debug.Log($"Lifetime ended for {gameObject.name}. Returning to pool '{_poolId}'.");
        EffectPoolManager.Instance?.ReturnEffect(_poolId, this.gameObject);
         _lifetimeCoroutine = null;
    }


    // --- Buffer Management (Keep the dummy buffer logic) ---
     void UpdateComputeBuffer<T>(ref ComputeBuffer buffer, List<T> data, int stride) where T : struct
     {
         // ... (Keep the exact same implementation from the previous step, including dummy buffer logic) ...
        int count = (data != null) ? data.Count : 0;
        bool requiresDummy = (count == 0);
        int bufferCount = requiresDummy ? 1 : count; // Ensure buffer always has at least size 1

        // Release existing buffer if size mismatch or changing between dummy/real
        if (buffer != null && (buffer.count != bufferCount || buffer.stride != stride))
        {
            buffer.Release();
            buffer = null;
        }

        // Create new buffer if needed
        if (buffer == null && bufferCount > 0) // Create if we need one (dummy or real)
        {
             // Use Immutable if data won't change frequently after being set, otherwise Default might be okay.
            buffer = new ComputeBuffer(bufferCount, stride, ComputeBufferType.Structured, ComputeBufferMode.Immutable);
        }

        // Set data
        if (buffer != null) // Check if buffer exists (it should if bufferCount > 0)
        {
            if (requiresDummy)
            {
                T[] dummyData = new T[1];
                 // Set sensible defaults for the dummy frame
                 if (typeof(T) == typeof(TintKeyframe)) {
                     dummyData[0] = (T)(object)new TintKeyframe { time = 0, color = Color.white };
                 } else if (typeof(T) == typeof(OffsetKeyframe)) {
                      dummyData[0] = (T)(object)new OffsetKeyframe { time = 0, offsetAndTiling = new Vector4(0,0,1,1) };
                 } else if (typeof(T) == typeof(DrawKeyframe)) {
                      dummyData[0] = (T)(object)new DrawKeyframe { time = 0, visibility = 1.0f };
                 } else {
                      dummyData[0] = new T(); // Auto default
                 }
                buffer.SetData(dummyData);
            }
            else { buffer.SetData(data); }
        }
     }

    public void MarkBuffersDirty() { _buffersDirty = true; }

    void OnDestroy()
    {
        // Release buffers when the GameObject is destroyed
        _offsetBuffer?.Release(); _offsetBuffer = null;
        _tintBuffer?.Release();   _tintBuffer = null;
        _drawBuffer?.Release();   _drawBuffer = null;
        // Also stop coroutine if active
         if (_lifetimeCoroutine != null) { StopCoroutine(_lifetimeCoroutine); _lifetimeCoroutine = null;}
    }

    // Optional: Handle editor updates if needed, requires [ExecuteAlways]
     #if UNITY_EDITOR
     // void OnValidate() { MarkBuffersDirty(); }
     #endif
}