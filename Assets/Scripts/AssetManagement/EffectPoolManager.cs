using UnityEngine;
using System.Collections.Generic;
using CustomSpriteFormat;
using Unity.Mathematics; // Assuming SpriteData.cs is in this namespace

public class EffectPoolManager : MonoBehaviour
{
    [System.Serializable]
    public class PoolInfo
    {
        public string effectId; // Unique identifier (e.g., "PhotonTorpedo", "LaserHit_TypeA")
        public GameObject prefab; // Prefab of the quad with MeshRenderer & AdvancedFlipbookController
        public int initialSize = 20;
    }

    public List<PoolInfo> poolsToInitialize;

    // The master dictionary storing all pools. Key is effectId.
    private Dictionary<string, Queue<GameObject>> _pools = new Dictionary<string, Queue<GameObject>>();
    // Optional: Store parent transforms for organization
    private Dictionary<string, Transform> _poolParents = new Dictionary<string, Transform>();

    // Singleton pattern for easy access (optional but common)
    public static EffectPoolManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            InitializePools();
        }
    }

    void InitializePools()
    {
        foreach (PoolInfo poolInfo in poolsToInitialize)
        {
            if (poolInfo.prefab == null || string.IsNullOrEmpty(poolInfo.effectId))
            {
                Debug.LogError($"PoolInfo for '{poolInfo.effectId}' is invalid (missing prefab or ID). Skipping.");
                continue;
            }

            // Create a parent GameObject for organization in the hierarchy
            GameObject parentGO = new GameObject($"Pool_{poolInfo.effectId}");
            parentGO.transform.SetParent(this.transform);
            _poolParents[poolInfo.effectId] = parentGO.transform;

            Queue<GameObject> objectPool = new Queue<GameObject>();
            for (int i = 0; i < poolInfo.initialSize; i++)
            {
                GameObject obj = Instantiate(poolInfo.prefab, parentGO.transform);
                obj.SetActive(false); // Start inactive
                objectPool.Enqueue(obj);
            }
            _pools[poolInfo.effectId] = objectPool;
            Debug.Log($"Initialized pool '{poolInfo.effectId}' with {poolInfo.initialSize} objects.");
        }
    }

    /// <summary>
    /// Spawns an effect from the pool.
    /// </summary>
    /// <param name="effectId">Matches the PoolInfo effectId.</param>
    /// <param name="position">World position to spawn at.</param>
    /// <param name="rotation">World rotation to spawn with.</param>
    /// <param name="animationData">The pre-processed animation data for configuration.</param>
    /// <returns>The activated GameObject, or null if pool doesn't exist or is empty.</returns>
    public GameObject SpawnEffect(string effectId, Vector3 position, Quaternion rotation, EffectAnimationData animationData)
    {
        if (!_pools.ContainsKey(effectId))
        {
            Debug.LogWarning($"Pool for effect ID '{effectId}' does not exist.");
            return null;
        }

        Queue<GameObject> pool = _pools[effectId];
        GameObject objToSpawn = null;

        if (pool.Count > 0)
        {
            objToSpawn = pool.Dequeue();
        }
        else
        {
            // Optional: Instantiate a new one if pool is empty (can cause spikes)
            Debug.LogWarning($"Pool '{effectId}' empty. Instantiating new object.");
            Transform parent = _poolParents.ContainsKey(effectId) ? _poolParents[effectId] : this.transform;
            // Find the original prefab to instantiate
             PoolInfo info = poolsToInitialize.Find(p => p.effectId == effectId);
             if (info?.prefab != null) {
                 objToSpawn = Instantiate(info.prefab, parent);
             } else {
                  Debug.LogError($"Cannot instantiate for empty pool '{effectId}', prefab info not found.");
                  return null; // Cannot instantiate
             }

        }

        // Configure the spawned object
        objToSpawn.transform.position = position;
        objToSpawn.transform.rotation = rotation;
        objToSpawn.SetActive(true);

        // Get the controller script and configure it
        AdvancedFlipbookController controller = objToSpawn.GetComponent<AdvancedFlipbookController>();
        if (controller != null)
        {
            controller.Configure(animationData); // Call a configuration method (see step 3)
            // Optionally, trigger something on the controller: controller.OnSpawn();
        }
        else
        {
            Debug.LogError($"Prefab for pool '{effectId}' is missing AdvancedFlipbookController component!", objToSpawn);
        }

        return objToSpawn;
    }

    /// <summary>
    /// Returns an active effect object back to its pool.
    /// </summary>
    /// <param name="effectId">The ID of the pool the object belongs to.</param>
    /// <param name="objToReturn">The GameObject instance to return.</param>
     public void ReturnEffect(string effectId, GameObject objToReturn)
    {
        if (objToReturn == null) return;

        if (!_pools.ContainsKey(effectId))
        {
            Debug.LogWarning($"Trying to return object to non-existent pool '{effectId}'. Destroying object instead.", objToReturn);
            Destroy(objToReturn);
            return;
        }

         // Optional: Call a reset method on the controller
         AdvancedFlipbookController controller = objToReturn.GetComponent<AdvancedFlipbookController>();
         controller?.OnReturnToPool(); // See step 3

        objToReturn.SetActive(false);
        // Reset parent if desired (helps keep hierarchy clean)
        if(_poolParents.TryGetValue(effectId, out Transform parent)) {
             objToReturn.transform.SetParent(parent);
        }

        _pools[effectId].Enqueue(objToReturn);
    }
}


/// <summary>
/// Helper struct to hold processed data ready for AdvancedFlipbookController.
/// You would create instances of this from your SpriteData definitions.
/// </summary>
public struct EffectAnimationData
{
    // Material properties (can be set via MPB if not texture)
    public float framesPerSecond;
    public Color tint; // Base tint (can be overridden by tint track)
    public float alpha; // Global alpha
    public bool useEmissive;
    public Color emissiveColor;
    public float emissiveIntensity;
    public Texture2D texture; // Main texture (if not using sprite sheet)

    // Fallback settings (if keyframes are empty)
    public AdvancedFlipbookController.FlipbookType autoType;
    public int autoTotalFrames;
    public int autoFramesPerRow;
    public Color autoTint;
    public bool autoVisible;

    // Interpolation modes
    public AdvancedFlipbookController.OffsetInterpolation offsetInterpolation;
    public AdvancedFlipbookController.TintInterpolation tintInterpolation;

    // Processed Keyframe Data
    public List<AdvancedFlipbookController.OffsetKeyframe> offsetKeyframes;
    public List<AdvancedFlipbookController.TintKeyframe> tintKeyframes;
    public List<AdvancedFlipbookController.DrawKeyframe> drawKeyframes;

    // Optional: Lifetime for auto-return
    public float lifetime; // If <= 0, doesn't auto-return

    public static EffectAnimationData CreateFromSpriteNode(SpriteNodeDefinition spriteNode)
    {
        SpriteAssetManager spriteAssetManager = SpriteAssetManager.Instance;
        if (spriteAssetManager == null)
        {
            Debug.LogErrorFormat($"SpriteAssetManager instance not found. Cannot create EffectAnimationData.{spriteNode.AnimationName}");
            return default;
        }
        EffectAnimationData data = new EffectAnimationData();
        AnimationDefinition animDef = spriteAssetManager.GetAnimation(spriteNode.AnimationName); // Ensure not null
        // Set default values from spriteNode or other sources
        if (animDef == null)
        {
            // use default values if no animation definition found
            data.framesPerSecond = 0.0f; // Example default FPS
            data.tint = Color.white; // Example tint from node
            data.alpha = 1.0f; // Example alpha
            data.autoType = AdvancedFlipbookController.FlipbookType.Row; // Example type
            data.autoTotalFrames = 1; // Example total frames
            data.autoFramesPerRow = 1; // Example frames per row
            data.autoTint = Color.white; // Example auto tint
            data.autoVisible = true; // Example visibility
        }
        else
        {
            Debug.LogFormat($"AnimationDefinition for {spriteNode.BaseSpriteName} found. Creating EffectAnimationData {animDef.name}");
            data.framesPerSecond = animDef.frameCount/animDef.duration; // Example default FPS
            data.tint = spriteNode.Tint; // Example tint from node
            data.alpha = 1.0f; // Example alpha
            data.autoType = animDef.autoKeyframe;//AdvancedFlipbookController.FlipbookType.Grid; // Example type
            data.autoTotalFrames = animDef.frameCount; // Example total frames
            data.autoFramesPerRow = animDef.autoKeyframe == AdvancedFlipbookController.FlipbookType.Grid ? (int)math.sqrt(animDef.frameCount) : animDef.frameCount; // Example frames per row
            data.autoTint = Color.white; // Example auto tint
            data.autoVisible = true; // Example visibility
            if (animDef.type == AnimationType.Draw)
            {
                data.autoVisible = true;
                data.drawKeyframes = ConvertDrawKeyframes(animDef);
            }
            else if (animDef.type == AnimationType.Colour)
            {
                data.tintKeyframes = ConvertTintKeyframes(animDef); 
                data.tintInterpolation = animDef.interpolation == AdvancedFlipbookController.OffsetInterpolation.Step ? AdvancedFlipbookController.TintInterpolation.Step : AdvancedFlipbookController.TintInterpolation.Linear;
            }
            else if (animDef.type == AnimationType.Offset)
            {
                data.offsetKeyframes = ConvertOffsetKeyframes(animDef); 
                data.offsetInterpolation = animDef.interpolation;
            }
            
        }
        
        // Texture animation
        string animName = spriteAssetManager.GetParsedSpriteDefinition(spriteNode.BaseSpriteName).DefaultAnimationName;
        animDef = spriteAssetManager.GetAnimation(animName); // Ensure not null
        if (animDef == null)
        {
            Debug.LogErrorFormat($"AnimationDefinition for {spriteNode.BaseSpriteName} not found. Cannot create EffectAnimationData {animName}");
        }
        else
        {
            if (animDef.type == AnimationType.Draw)
            {
                data.autoVisible = true;
                data.drawKeyframes = ConvertDrawKeyframes(animDef);
            }
            else if (animDef.type == AnimationType.Colour)
            {
                data.tintKeyframes = ConvertTintKeyframes(animDef); 
                data.tintInterpolation = animDef.interpolation == AdvancedFlipbookController.OffsetInterpolation.Step ? AdvancedFlipbookController.TintInterpolation.Step : AdvancedFlipbookController.TintInterpolation.Linear;
            }
            else if (animDef.type == AnimationType.Offset)
            {
                data.offsetKeyframes = ConvertOffsetKeyframes(animDef); 
                data.offsetInterpolation = animDef.interpolation;
            } 
            data.autoType = animDef.autoKeyframe; // Example
            data.autoTotalFrames = animDef.frameCount; // Example
            data.autoFramesPerRow = animDef.autoKeyframe == AdvancedFlipbookController.FlipbookType.Grid ? (int)math.sqrt(animDef.frameCount) : animDef.frameCount ; // Example
            data.autoTint = Color.white; // Example TODO: Set from animDef if needed
            data.autoVisible = true; // Example TODO: Set from animDef if needed
            
        }

        // Set the actual texture.
        Sprite spr = spriteAssetManager.GetSprite(spriteNode.BaseSpriteName);
        data.texture = spr.texture; // Get the texture from the sprite asset manager
        data.useEmissive = spriteAssetManager.GetParsedSpriteDefinition(spriteNode.BaseSpriteName).MaterialType == MaterialType.Additive; // Example emissive setting
        data.emissiveColor = Color.white; // Example emissive color
        data.emissiveIntensity = 6.0f; // Example emissive intensity
        return data;
    }
     // Constructor or factory method to convert from SpriteData
     public static EffectAnimationData CreateFrom(AnimationDefinition offsetAnim, AnimationDefinition tintAnim, AnimationDefinition drawAnim, /* other params like SpriteNodeDefinition for defaults */ float defaultFps)
     {
         EffectAnimationData data = new EffectAnimationData();
         data.framesPerSecond = offsetAnim.frameCount/offsetAnim.duration; // Example default
         // Set auto values from SpriteNodeDefinition or defaults
         data.autoType = AdvancedFlipbookController.FlipbookType.Grid; // Example
         data.autoTotalFrames = 16; // Example
         data.autoFramesPerRow = 4; // Example
         data.autoTint = Color.white; // Example
         data.autoVisible = true; // Example

         // --- Conversion Logic ---
         // This is where you translate AnimationDefinition into the controller's format
         data.offsetKeyframes = ConvertOffsetKeyframes(offsetAnim);
         data.tintKeyframes = ConvertTintKeyframes(tintAnim);
         data.drawKeyframes = ConvertDrawKeyframes(drawAnim);

         // Determine interpolation modes based on AnimationDefinition
         data.offsetInterpolation = offsetAnim.interpolation;//ConvertInterpolationMode_Offset(offsetAnim?.interpolation ?? InterpolationMode.Step);
         data.tintInterpolation = tintAnim.interpolation == AdvancedFlipbookController.OffsetInterpolation.Step ? AdvancedFlipbookController.TintInterpolation.Step : AdvancedFlipbookController.TintInterpolation.Linear;

         // Set other properties like lifetime, base tint, alpha etc.
         data.tint = Color.white; // Example
         data.alpha = 1.0f; // Example
         data.lifetime = offsetAnim?.duration ?? tintAnim?.duration ?? drawAnim?.duration ?? 2.0f; // Estimate lifetime

         return data;
     }

    // --- TODO: Implement these conversion functions ---
     private static List<AdvancedFlipbookController.OffsetKeyframe> ConvertOffsetKeyframes(AnimationDefinition animDef) {
         var list = new List<AdvancedFlipbookController.OffsetKeyframe>();
         if (animDef == null || animDef.type != AnimationType.Offset) return list; // Return empty if null or wrong type

         // Example: Assuming animDef.keyframes[i].value is already Vector4(offsetX, offsetY, tileX, tileY)
         // You'll need to calculate this based on your SpriteDefinition sourceRect etc.
         foreach (var kf in animDef.keyframes) {
              // This is pseudo-code - you need the actual calculation based on your source data format
              // Vector4 offsetAndTiling = CalculateOffsetTilingFromSpriteIndex(kf.IntValue, spriteDefinition);
              Vector4 offsetAndTiling = Vector4.zero; // Placeholder! Needs real calculation.
              if (kf.value is Vector4 v4) offsetAndTiling = v4; // Use if value is already correct Vector4

             list.Add(new AdvancedFlipbookController.OffsetKeyframe { time = kf.time, offsetAndTiling = offsetAndTiling });
         }
         list.Sort((a, b) => a.time.CompareTo(b.time)); // Ensure sorted
         return list;
     }

     private static List<AdvancedFlipbookController.TintKeyframe> ConvertTintKeyframes(AnimationDefinition animDef) {
          var list = new List<AdvancedFlipbookController.TintKeyframe>();
         if (animDef == null || animDef.type != AnimationType.Colour) return list;

         foreach (var kf in animDef.keyframes) {
             list.Add(new AdvancedFlipbookController.TintKeyframe { time = kf.time, color = kf.ColorValue });
         }
          list.Sort((a, b) => a.time.CompareTo(b.time));
         return list;
     }

      private static List<AdvancedFlipbookController.DrawKeyframe> ConvertDrawKeyframes(AnimationDefinition animDef) {
          // Draw animation likely controls OFFSET frames in the original design.
          // If Draw means Visibility in the new shader, convert accordingly.
          // If Draw means switching Sprites (which implies Offset changes), handle in ConvertOffsetKeyframes.
          // This example assumes Draw means Visibility:
          var list = new List<AdvancedFlipbookController.DrawKeyframe>();
         if (animDef == null || animDef.type != AnimationType.Draw) return list; // Adjust type check if needed

         foreach (var kf in animDef.keyframes) {
             // Assuming kf.IntValue > 0 means visible? Adjust logic as needed.
             list.Add(new AdvancedFlipbookController.DrawKeyframe { time = kf.time, visibility = (kf.IntValue > 0) ? 1.0f : 0.0f });
         }
         list.Sort((a, b) => a.time.CompareTo(b.time));
         return list;
     }

    //   private static AdvancedFlipbookController.OffsetInterpolation ConvertInterpolationMode_Offset(InterpolationMode mode) {
    //      switch (mode) {
    //          case InterpolationMode.Linear: return AdvancedFlipbookController.OffsetInterpolation.LinearScroll; // Map Linear to Scroll? Or Crossfade? Decide based on desired effect.
    //          case InterpolationMode.LinearCrossfade: return AdvancedFlipbookController.OffsetInterpolation.LinearCrossfade;
    //          case InterpolationMode.Step:
    //          default: return AdvancedFlipbookController.OffsetInterpolation.Step;
    //      }
    //  }
    //   private static AdvancedFlipbookController.TintInterpolation ConvertInterpolationMode_Tint(InterpolationMode mode) {
    //     return (mode == InterpolationMode.Linear || mode == InterpolationMode.LinearCrossfade) // Treat Crossfade as Linear for Tint
    //         ? AdvancedFlipbookController.TintInterpolation.Linear
    //         : AdvancedFlipbookController.TintInterpolation.Step;
    // }


}