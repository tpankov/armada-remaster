using System.Linq;
using System.Collections.Generic;
using System.IO; // Required for File I/O
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Threading.Tasks;

public class test : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public string SpriteNodeName = "ftwaspr1";
    private GameObject effect;

    private int randomIndex = 0;
    async Task Start()
    {
        //CustomSpriteFormat.SpriteNodeDefinition snd = SpriteAssetManager.Instance.GetSpriteNodeDefinition(SpriteNodeName);
        //EffectAnimationDataArrayBased ad = EffectAnimationDataArrayBased.CreateFromSpriteNode(snd);
        //effect = EffectPoolManager.Instance.SpawnEffect(SpriteNodeName, transform.position, Quaternion.identity);
        GameObject shipObject = new GameObject("fedbat2");
        string configPath = Path.Combine("DynamicAssets","addon","fedbat2.odf");
        StarshipBase ship = shipObject.AddComponent<StarshipBase>();
        ship.LoadFromConfig(configPath);
        await AddressablePrefabSaver.SaveModelAsAddressable(shipObject, "fedbat2");
        Debug.Log("Prefab saved as addressable: fedbat2");
    }

    // Update is called once per frame  
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            //CustomSpriteFormat.SpriteNodeDefinition snd = SpriteAssetManager.Instance.GetSpriteNodeDefinition(SpriteNodeName);
            //EffectAnimationData ad = EffectAnimationData.CreateFromSpriteNode(snd);
            // Debug print all of the EffectAnimationData properties
            //Debug.Log($"{snd.NodeName} - EffectAnimationData: {ad.autoTotalFrames}, {ad.autoType}, {ad.autoVisible}, {ad.emissiveColor}, {ad.emissiveIntensity}");
            string toSpawn = SpriteAssetManager.Instance.loadedSpriteNodes.ElementAt(randomIndex).Key;
            randomIndex++;
            if (randomIndex >= SpriteAssetManager.Instance.loadedSpriteNodes.Count)
            {
                randomIndex = 6;
                Debug.Log("Resetting random index to 0");
            }
            Vector3 spawnPosition = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0);
            effect = EffectPoolManager.Instance.SpawnEffect(toSpawn, spawnPosition, Quaternion.identity);
            // Example of how to use the EffectPoolManager to spawn an effect
        }
        if (Input.GetKeyDown(KeyCode.Return))
        {
            AsyncOperationHandle<GameObject> loadHandle = Addressables.LoadAssetAsync<GameObject>("fedbat2");
            loadHandle.Completed += (handle) =>
            {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    GameObject prefab = handle.Result;
                    Vector3 spawnPosition = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f));
                    // random orientation
                    Quaternion spawnRotation = Quaternion.Euler(Random.Range(0f, 10f), Random.Range(0f, 360f), Random.Range(0f, 360f));  
                    
                    prefab.transform.position = spawnPosition;
                    prefab.transform.rotation = spawnRotation;
                    // Use the prefab as needed
                    Debug.Log($"Loaded prefab: {prefab.name}");
                }
                else
                {
                    Debug.LogError("Failed to load prefab.");
                }
            };
        }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (effect != null)
            {
                string toSpawn = SpriteAssetManager.Instance.loadedSpriteNodes.ElementAt(randomIndex).Key;
                EffectPoolManager.Instance.ReturnEffect(toSpawn,effect);
                effect = null;
            }
        }
    }
}
