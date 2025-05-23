using UnityEngine;
using System.Collections.Generic;
using CustomSpriteFormat;
using UnityEngine.Pool;
using Unity.NetCode;
using Unity.Transforms;
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

            // Initialize the pool
            GameObject parentGO;
            Queue<GameObject> objectPool;
            if (_pools.ContainsKey(poolInfo.effectId))
            {
                parentGO = _poolParents[poolInfo.effectId].gameObject;
                objectPool = _pools[poolInfo.effectId];
            }
            else
            {
                // Create a parent GameObject for organization in the hierarchy
                parentGO = new GameObject($"Pool_{poolInfo.effectId}");
                parentGO.transform.SetParent(this.transform);
                _poolParents[poolInfo.effectId] = parentGO.transform;
                objectPool = new Queue<GameObject>();
            }
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
    /// <param name="position">World or local position to spawn at.</param>
    /// <param name="rotation">World or local rotation to spawn with.</param>
    /// <param name="animationData">The pre-processed animation data for configuration.</param>
    /// <returns>The activated GameObject, or null if pool doesn't exist or is empty.</returns>
    public GameObject SpawnEffect(string effectId, Vector3 position, Quaternion rotation, GameObject parent = null)//, EffectAnimationData animationData)
    {
        // 1. Get Definition: Lookup your SpriteNodeDefinition / AnimationDefinitions based on effectLookupId
        EffectAnimationDataArrayBased controllerData; // Placeholder for animation data
        Material instanceMaterial;
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
            if (snd == null)
            {
                //Debug.LogError($"SpriteNodeDefinition for '{effectId}' not found. Cannot spawn effect.");
                return null;
            }
            controllerData = EffectAnimationDataArrayBased.CreateFromSpriteNode(snd);
            
            //EffectAnimationDataArrayBased controllerData = CreateEffectAnimationData(effectId);
            //if (controllerData != null)
            _animationData[effectId] = controllerData;
            
        }
        // 2. Get Material: Use caching function
        instanceMaterial = MaterialManager.Instance.GetOrCreateMaterial("sprite", 
            isTransparent: controllerData.materialType == MaterialType.Alpha, 
            isAdditive: controllerData.materialType == MaterialType.Additive,
            backfaceCulling: true,
            emissive: controllerData.emissiveColor != Color.clear);

        // 3. Get Pooled Object: Get a raw Quad from pool
        GameObject objToSpawn = GetFromRawPool("sprite"); // Implement pooling for raw quads
        
        if (parent != null)
        {
            objToSpawn.transform.SetParent(parent.transform); // Set parent if provided
        }
        else
        {
            objToSpawn.transform.SetParent(_poolParents["sprite"]); // Set to pool parent if no parent provided
        }
        objToSpawn.transform.localPosition = position;
        objToSpawn.transform.localRotation = rotation;

        // 4. Assign Material
        MeshRenderer rend = objToSpawn.GetComponent<MeshRenderer>();
        //if (rend == null) { Debug.LogError("Quad prefab missing Renderer!"); Destroy(objToSpawn); return null; }
        rend.sharedMaterial = instanceMaterial; // Assign the shared/cached material

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
            InitializePools(10, effectId); // Add more objects to the pool
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

    
}

