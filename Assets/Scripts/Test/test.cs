using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public class test : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public string SpriteNodeName = "ftwaspr1";
    private GameObject effect;

    private int randomIndex = 0;
    void Start()
    {
        //CustomSpriteFormat.SpriteNodeDefinition snd = SpriteAssetManager.Instance.GetSpriteNodeDefinition(SpriteNodeName);
        //EffectAnimationDataArrayBased ad = EffectAnimationDataArrayBased.CreateFromSpriteNode(snd);
        effect = EffectPoolManager.Instance.SpawnEffect(SpriteNodeName, transform.position, Quaternion.identity);
        
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
                randomIndex = 0;
            }
            Vector3 spawnPosition = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0);
            effect = EffectPoolManager.Instance.SpawnEffect(SpriteNodeName, spawnPosition, Quaternion.identity);
            // Example of how to use the EffectPoolManager to spawn an effect
        }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (effect != null)
            {
                EffectPoolManager.Instance.ReturnEffect(SpriteNodeName,effect);
                effect = null;
            }
        }
    }
}
