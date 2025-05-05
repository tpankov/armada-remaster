using NUnit.Framework.Constraints;
using UnityEngine;

public class test : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public string SpriteNodeName = "ftwaspr1";
    private GameObject effect;
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
            effect = EffectPoolManager.Instance.SpawnEffect(SpriteNodeName, transform.position, Quaternion.identity);
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
