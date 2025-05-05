using UnityEngine;
using System.Collections.Generic;
using CustomSpriteFormat;
//using Unity.Mathematics;
//using NUnit.Framework; // Assuming SpriteData.cs is in this namespace

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
    private Dictionary<string, Queue<GameObject>> _pools = new Dictionary<string, Queue<GameObject>>(); // Store pooled GameObjects
    private Dictionary<string, EffectAnimationDataArrayBased> _animationData = new Dictionary<string, EffectAnimationDataArrayBased>(); // Store animation data for each effectId
    
    // Optional: Store parent transforms for organization
    private Dictionary<string, Transform> _poolParents = new Dictionary<string, Transform>();

    // Singleton pattern for easy access (optional but common)
    public static EffectPoolManager Instance { get; private set; }

    //private Dictionary<string, Material> _materialCache = new Dictionary<string, Material>();
    //private Shader _flipbookShader; // Assign the array shader in Inspector or load via Resources


    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            //_flipbookShader = Shader.Find("Custom/HDRP/FlipbookAnimatorArray"); // Find the shader
            InitializePools();

        }
    }

    void InitializePools(int addPool = 0, string effectId = null)
    {
        foreach (PoolInfo poolInfo in poolsToInitialize)
        {
            if (effectId != null && poolInfo.effectId != effectId) continue; // Filter by effectId if specified
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
            if (addPool == 0)
                addPool = poolInfo.initialSize; // Default to initial size if not specified
            for (int i = 0; i < addPool; i++)
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
    public GameObject SpawnEffect(string effectId, Vector3 position, Quaternion rotation)//, EffectAnimationData animationData)
    {
        // 1. Get Definition: Lookup your SpriteNodeDefinition / AnimationDefinitions based on effectLookupId
    // Example: SpriteNodeDefinition nodeDef = LookupNodeDefinition(effectLookupId);
    // Example: AnimationDefinition[] animDefs = LookupAnimationDefinitions(nodeDef.AnimationName); // Find all relevant anims
    // Example: SpriteDefinition spriteDef = LookupSpriteDefinition(nodeDef.BaseSpriteName);
    EffectAnimationDataArrayBased controllerData; // Placeholder for animation data
    if (_animationData.ContainsKey(effectId))
    {
        // Use cached animation data if available
        controllerData = _animationData[effectId];
        Debug.Log($"Using cached animation data for effectId: {effectId}");
    }
    else
    {
        // If not cached, create and cache it
        SpriteNodeDefinition snd = SpriteAssetManager.Instance.GetSpriteNodeDefinition(effectId);
        controllerData = EffectAnimationDataArrayBased.CreateFromSpriteNode(snd);
        //EffectAnimationDataArrayBased controllerData = CreateEffectAnimationData(effectId);
        //if (controllerData != null)
        _animationData[effectId] = controllerData;
    }

    // Debug print the animation data for verification
    Debug.Log($"EffectAnimationData for {effectId}: {controllerData.autoRowFPS}, {controllerData.autoRowTotalFrames}, {controllerData.drawFrameVisibilities}, {controllerData.offsetFrameData.Count}, {controllerData.useAutoRow}");
    // --- TODO: Replace Placeholders with your actual data lookup ---
    //SpriteNodeDefinition nodeDef = new SpriteNodeDefinition(); // Placeholder
    //SpriteDefinition spriteDef = new SpriteDefinition(); // Placeholder
    //List<AnimationDefinition> animDefs = new List<AnimationDefinition>(); // Placeholder
    //Texture2D effectTexture = null; // Placeholder: Load texture based on spriteDef.sourceTextureName
    // --- End Placeholder ---


    //if (effectTexture == null) { Debug.LogError("Texture not found!"); return null; }

    // 2. Get Material: Use caching function
    //Material instanceMaterial = GetOrCreateMaterial(effectTexture, spriteDef.materialType);

    // 3. Get Pooled Object: Get a raw Quad from pool
    GameObject objToSpawn = GetFromRawPool("sprite"); // Implement pooling for raw quads
    //GameObject objToSpawn = Instantiate(quadPrefab); // Simple instantiate for now
    objToSpawn.transform.position = position;
    objToSpawn.transform.rotation = rotation;

    // 4. Assign Material
    //Renderer rend = objToSpawn.GetComponent<Renderer>();
    //if (rend == null) { Debug.LogError("Quad prefab missing Renderer!"); Destroy(objToSpawn); return null; }
    //rend.material = instanceMaterial; // Assign the shared/cached material

    // 5. Prepare Animation Data for Controller
    //EffectAnimationDataArrayBased controllerData = ConvertDefinitionsToControllerData(nodeDef, spriteDef, animDefs);

    // 6. Configure Controller
    AdvancedFlipbookControllerArrays controller = objToSpawn.GetComponent<AdvancedFlipbookControllerArrays>();
    if (controller == null) controller = objToSpawn.AddComponent<AdvancedFlipbookControllerArrays>(); // Add if missing
    controller.Configure(controllerData);

    objToSpawn.SetActive(true);
    return objToSpawn;
}

    /// <summary>
    /// Gets a GameObject from the pool. If the pool is empty, it creates a new instance.   
    /// /// </summary>
    /// <param name="effectId">The ID of the pool to get from.</param>  
    /// <returns>The GameObject instance from the pool.</returns>
    private GameObject GetFromRawPool(string effectId)
    {
        if (!_pools.ContainsKey(effectId))
        {
            Debug.LogWarning($"Pool '{effectId}' does not exist. Returning null.");
            return null;
        }

        Queue<GameObject> pool = _pools[effectId];
        if (pool.Count > 0)
        {
            return pool.Dequeue();
        }
        else
        {
            // Optional: Expand the pool if needed
            InitializePools(5, effectId); // Add more objects to the pool
            return pool.Dequeue();
        }
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
         AdvancedFlipbookControllerArrays controller = objToReturn.GetComponent<AdvancedFlipbookControllerArrays>();
         controller?.OnReturnToPool(); // See step 3

        objToReturn.SetActive(false);
        // Reset parent if desired (helps keep hierarchy clean)
        if(_poolParents.TryGetValue(effectId, out Transform parent)) {
             objToReturn.transform.SetParent(parent);
        }

        _pools[effectId].Enqueue(objToReturn);
    }

    // Function to get or create a material based on texture and blend mode
    // Material GetOrCreateMaterial(Texture2D texture, CustomSpriteFormat.MaterialType materialType)
    // {
    //     string cacheKey = $"{texture.name}_{materialType}"; // Unique key per combo

    //     if (_materialCache.TryGetValue(cacheKey, out Material cachedMat))
    //     {
    //         return cachedMat;
    //     }

    //     // Create new material
    //     Material newMat = new Material(_flipbookShader);
    //     newMat.name = cacheKey; // Helpful for debugging
    //     newMat.SetTexture("_BaseMap", texture);

    //     // Configure blend mode based on type
    //     ConfigureMaterialBlendMode(newMat, materialType);

    //     _materialCache.Add(cacheKey, newMat);
    //     return newMat;
    // }

    // void ConfigureMaterialBlendMode(Material mat, CustomSpriteFormat.MaterialType type)
    // {
    //     // Set common properties for transparency
    //     mat.SetFloat("_SurfaceType", 1.0f); // 1 = Transparent
    //     mat.SetFloat("_ZWrite", 0.0f);      // ZWrite Off

    //     // HDRP blend mode setup (Simplified - adjust based on exact HDRP standard lit blend properties if needed)
    //     // These property names might need updating based on exact HDRP Lit shader conventions if not using legacy properties.
    //     // Assuming _SrcBlend, _DstBlend control it here based on previous shader.
    //     switch (type)
    //     {
    //         case CustomSpriteFormat.MaterialType.Additive:
    //             mat.SetFloat("_UseEmissive", 1.0f); // Enable emissive path in shader
    //             mat.SetFloat("_BlendMode", 1.0f);  // Set to Additive if HDRP uses this property
    //             mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One); // SrcAlpha for pre-multiplied, One for additive
    //             mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
    //             break;

    //         case CustomSpriteFormat.MaterialType.Alpha: // Standard Alpha Blending
    //         default:
    //             mat.SetFloat("_UseEmissive", 0.0f);
    //             mat.SetFloat("_BlendMode", 0.0f); // Set to Alpha if HDRP uses this property
    //             mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); // Standard alpha blend
    //             mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
    //             break;
    //     }
    //     // Make sure shader features dependent on blend mode are enabled/disabled if necessary
    //     // For example, mat.EnableKeyword("_USEEMISSIVE_ON"); / mat.DisableKeyword(...)
    //     if (type == CustomSpriteFormat.MaterialType.Additive) {
    //         mat.EnableKeyword("_USEEMISSIVE_ON");
    //     } else {
    //         mat.DisableKeyword("_USEEMISSIVE_ON");
    //     }

    //     // Set Render Queue (Transparent is 3000) - HDRP might manage this via _SurfaceType
    //     mat.renderQueue = 3000;
    // }

}

