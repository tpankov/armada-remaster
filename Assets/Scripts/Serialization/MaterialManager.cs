using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.TerrainTools;
using Unity.Mathematics;

public class MaterialManager : MonoBehaviour {
    public static MaterialManager Instance { get; private set; }
    public Dictionary<string,Material> sharedMaterials = new Dictionary<string,Material>() ;
    private Shader _flipbookShader; // Assign the array shader in Inspector or load via Resources
    

    void Awake() {
        if (Instance == null) {
            Instance = this;
            _flipbookShader = Shader.Find("Custom/HDRP/FlipbookAnimatorArray"); // Find the shader

            DontDestroyOnLoad(gameObject);
        } else {
            Destroy(gameObject);
        }

        // Create and store shared ship material
        // sharedMaterials.Add("stdhull",CreateSharedMaterial("Universal Render Pipeline/Lit"));
        // sharedMaterials.Add("lightmap",CreateSharedMaterial("Universal Render Pipeline/Unlit"));
        sharedMaterials.Add("stdhull",CreateHDRPMaterial("HDRP/Lit"));
        sharedMaterials.Add("stdhull_additive",CreateHDRPMaterial("HDRP/Lit",isAdditive:true));
        sharedMaterials.Add("lightmap",CreateHDRPMaterial("HDRP/Unlit"));
        sharedMaterials.Add("lightmap_alpha",CreateHDRPMaterial("HDRP/Unlit",isTransparent:true));
        sharedMaterials.Add("lightmap_additive",CreateHDRPMaterial("HDRP/Unlit",isAdditive:true));
        sharedMaterials.Add("lightmap_alpha_nocull",CreateHDRPMaterial("HDRP/Unlit",isTransparent:true,backfaceCulling:false));
        sharedMaterials.Add("sprite_additive",CreateHDRPMaterial("Custom/HDRP/FlipbookAnimatorArray",isAdditive:true));
        sharedMaterials.Add("sprite_alpha",CreateHDRPMaterial("Custom/HDRP/FlipbookAnimatorArray",isTransparent:true));
        sharedMaterials.Add("sprite",CreateHDRPMaterial("Custom/HDRP/FlipbookAnimatorArray",isTransparent:false,backfaceCulling:false));
        sharedMaterials.Add("nospec",sharedMaterials["stdhull"]);
        Debug.Log("MaterialManager initialized and shared materials created.");
    }

    // public void ApplyShipMaterial(string materialName, 
    //         MeshRenderer meshRenderer, 
    //         Texture2D baseTex, 
    //         Texture2D normalTex, 
    //         Texture2D emissionTex, 
    //         bool isTransparent = false,
    //         bool isAdditive = false,
    //         bool backfaceCulling = true,
    //         int materialIndex = -1,
    //         int numberOfMaterials = 1
    //         ) {
    //     if (meshRenderer == null) return;

    //     // Assign the shared material
    //     if (numberOfMaterials > 1)
    //         meshRenderer.sharedMaterials[materialIndex] = GetOrCreateMaterial(materialName, isTransparent, isAdditive, backfaceCulling);
    //     else
    //         meshRenderer.sharedMaterial = GetOrCreateMaterial(materialName, isTransparent, isAdditive, backfaceCulling);

    //     // Create a new MaterialPropertyBlock for per-instance texture overrides
    //     MaterialPropertyBlock mpb = new MaterialPropertyBlock();

    //     if (baseTex != null) mpb.SetTexture("_BaseColorMap", baseTex);
    //     if (isTransparent) {
    //         mpb.SetFloat("_AlphaCutoffEnable", 0.0f);
    //         mpb.SetColor("_BaseColor", Color.black);
    //         mpb.SetFloat("_SurfaceType", 1); // Transparent surface
    //         mpb.SetFloat("_BlendMode", 0);   // Alpha blending  
    //     }

    //     if (normalTex != null) mpb.SetTexture("_NormalMap", normalTex);
    //     if (emissionTex != null) {
    //         mpb.SetTexture("_EmissiveColorMap", emissionTex);
    //         mpb.SetColor("_EmissiveColor", Color.black * 10.0f); // HDR Emission Boost
    //     }

    //     // Apply property block to MeshRenderer
    //     if (materialIndex > -1)
    //         meshRenderer.SetPropertyBlock(mpb,materialIndex);
    //     else
    //         meshRenderer.SetPropertyBlock(mpb);

    // }


    public void ApplyMaterial(string materialName, Renderer renderer, Texture2D baseTex, Texture2D normalTex, Texture2D emissionTex,
            bool useAnimationData, EffectAnimationDataArrayBased effectAnimationData,
            bool isTransparent = false, bool isAdditive = false, bool backfaceCulling = true,
            int materialIndex = -1,
            int numberOfMaterials = 1) {
        if (renderer == null) 
        {
            Debug.LogError("Renderer is null. Cannot apply material.");
            return;
        }

        // Material name should not contain the "tag" such as "additive" or "alpha"
        Material _mat = GetOrCreateMaterial(materialName, isTransparent, isAdditive, backfaceCulling);
        if (numberOfMaterials > 1)
            renderer.sharedMaterials[materialIndex] = _mat; // Assign shared material
        else
            renderer.sharedMaterial = _mat; // Assign shared material

        if (useAnimationData) {
            // Apply animation data to the renderer
            AdvancedFlipbookControllerArrays controller = renderer.gameObject.GetComponent<AdvancedFlipbookControllerArrays>();
            if (controller == null) controller = renderer.gameObject.AddComponent<AdvancedFlipbookControllerArrays>();
            // TODO: what to do with the normalTex, emissionTex?
            controller.Configure(effectAnimationData);
        }
        else 
        {
            // If not using animation data, do a simple mpb here
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            if (baseTex != null) mpb.SetTexture("_BaseMap", baseTex);
            if (normalTex != null) mpb.SetTexture("_BumpMap", normalTex);
            if (emissionTex != null) {
                mpb.SetTexture("_EmissionMap", emissionTex);
                mpb.SetColor("_EmissionColor", Color.white * 5.0f);
            }
            renderer.SetPropertyBlock(mpb);
            
        }
        
    }

    // 
    //Material GetOrCreateMaterial(CustomSpriteFormat.MaterialType materialType)
    // {
    //     //string cacheKey = $"{texture.name}_{materialType}"; // Unique key per combo
    //     string cacheKey = $"{materialType}"; // Unique key per combo
    //     Debug.Log($"Cache key for material: {cacheKey}");
    //     if (sharedMaterials.TryGetValue(cacheKey, out Material cachedMat))
    //     {
    //         return cachedMat;
    //     }

    //     // Create new material
    //     Material newMat = new Material(_flipbookShader);
    //     newMat.name = cacheKey; // Helpful for debugging
    //     // newMat.SetTexture("_BaseMap", texture);

    //     // Configure blend mode based on type
    //     ConfigureMaterialBlendMode(newMat, materialType);

    //     sharedMaterials.Add(cacheKey, newMat);
    //     return newMat;
    // }

    


    public Material GetOrCreateMaterial(string materialName, bool isTransparent, bool isAdditive, bool backfaceCulling)
    {
        string key = materialName;
        if (isTransparent)
            key += "_alpha";
        if (isAdditive)
            key += "_additive";
        if (!backfaceCulling)
            key += "_culloff";
        Debug.LogFormat("Creating material: {0}", key);
        if (!sharedMaterials.ContainsKey(key))
            {
                if (sharedMaterials.ContainsKey(materialName))
                {
                    Debug.LogFormat("getOrCreate: {0}, {1}", materialName, sharedMaterials[materialName].shader);
                    sharedMaterials.Add(key,CreateHDRPMaterial(sharedMaterials[materialName].shader,isTransparent,isAdditive,backfaceCulling));
                }
                else
                    sharedMaterials.Add(key,CreateHDRPMaterial("HDRP/Lit",isTransparent,isAdditive,backfaceCulling));
            }
        return sharedMaterials[materialName];
    }

    void ConfigureMaterialBlendMode(Material mat, CustomSpriteFormat.MaterialType type, bool backfaceCulling = true)
    {
        mat.enableInstancing = true;

        // Surface Type: Opaque or Transparent
        mat.SetFloat("_SurfaceType", (type == CustomSpriteFormat.MaterialType.Alpha || type == CustomSpriteFormat.MaterialType.Additive) ? 1.0f : 0.0f);
       // mat.SetFloat("_BlendMode", type == CustomSpriteFormat.MaterialType.Additive ? 1.0f : 0.0f); // Additive blending
        //mat.SetFloat("_CullMode", backfaceCulling ? 2.0f : 0.0f); // 2 = Back, 0 = Off
        mat.SetFloat("_CullMode", backfaceCulling ? (int)UnityEngine.Rendering.CullMode.Back : (int)UnityEngine.Rendering.CullMode.Off);

        // Set common properties for transparency
        //mat.SetFloat("_SurfaceType", 1.0f); // 1 = Transparent
        mat.SetFloat("_ZWrite", 0.0f);      // ZWrite Off

        // HDRP blend mode setup (Simplified - adjust based on exact HDRP standard lit blend properties if needed)
        // These property names might need updating based on exact HDRP Lit shader conventions if not using legacy properties.
        switch (type)
        {
            case CustomSpriteFormat.MaterialType.Additive:
                mat.SetColor("_BaseColor", Color.black);
                mat.SetFloat("_UseEmissive", 1.0f); // Enable emissive path in shader
                mat.SetFloat("_BlendMode", 1.0f);  // Set to Additive if HDRP uses this property
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); // SrcAlpha for pre-multiplied, One for additive
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 50 ;
                mat.SetFloat("_AlphaCutoffEnable", 0.0f);
                mat.SetFloat("_SurfaceType", 1.0f); // Transparent surface
                Debug.Log("Additive blend mode set.");
                break;
        
            case CustomSpriteFormat.MaterialType.Alpha: // Standard Alpha Blending
                mat.SetColor("_BaseColor", Color.black);
                mat.SetFloat("_UseEmissive", 0.0f);
                mat.SetFloat("_BlendMode", 0.0f); // Set to Alpha if HDRP uses this property
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); // Standard alpha blend
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent  ;
                mat.SetFloat("_AlphaCutoffEnable", 0.0f);
                mat.SetFloat("_SurfaceType", 1.0f); // Transparent surface
                Debug.Log("Alpha blend mode set.");
                break;

            default:
                mat.SetColor("_BaseColor", Color.black);
                mat.SetFloat("_UseEmissive", 0.0f);
                mat.SetFloat("_BlendMode", 0.0f); // Set to Alpha if HDRP uses this property
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One); // Standard alpha blend
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                mat.SetFloat("_AlphaCutoffEnable", 1.0f);
                mat.SetFloat("_SurfaceType", 0.0f); // Opaque surface
                Debug.Log("Default blend mode set.");
                break;
        }
        // Make sure shader features dependent on blend mode are enabled/disabled if necessary
        // For example, mat.EnableKeyword("_USEEMISSIVE_ON"); / mat.DisableKeyword(...)
        if (type == CustomSpriteFormat.MaterialType.Additive) {
            mat.EnableKeyword("_USEEMISSIVE_ON");
        } else {
            mat.DisableKeyword("_USEEMISSIVE_ON");
        }

        // Set Render Queue (Transparent is 3000) - HDRP might manage this via _SurfaceType
        //mat.renderQueue = 3000;
    
        
    }
    void ConfigureMaterialBlendMode(Material mat, bool isTransparent, bool isAdditive, bool backfaceCulling) {
        // Set the blend mode based on the type
        CustomSpriteFormat.MaterialType type = isAdditive ? CustomSpriteFormat.MaterialType.Additive : 
            (isTransparent ? CustomSpriteFormat.MaterialType.Alpha : CustomSpriteFormat.MaterialType.Default);
        ConfigureMaterialBlendMode(mat, type, backfaceCulling);
    }

    Material CreateHDRPMaterial(string shaderName, bool isTransparent = false, bool isAdditive = false, bool backfaceCulling = true) {
        var shader = Shader.Find(shaderName);
        return CreateHDRPMaterial(shader, isTransparent, isAdditive, backfaceCulling);
    }
    Material CreateHDRPMaterial(Shader shader, bool isTransparent = false, bool isAdditive = false, bool backfaceCulling = true) {
        Material mat = new Material(shader);
        ConfigureMaterialBlendMode(mat, isTransparent, isAdditive, backfaceCulling);
        mat.name = shader.name + (isTransparent ? "_Alpha" : "") + (isAdditive ? "_Additive" : "") + (backfaceCulling ? "" : "_NoCull");
        return mat;
    }


    // public Material CreateSharedMaterial(string shaderName, bool isTransparent = false, bool isAdditive = false, bool backfaceCulling = true) {
    //     Material mat = new Material(Shader.Find(shaderName));
    //     mat.enableInstancing = true;

    //     // Set the blend mode
    //     if (isAdditive) {
    //         mat.SetFloat("_SurfaceType", 1); // Transparent
    //         mat.SetInt("_Blend<ode", 1);   // Additive blending
    //         mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
    //         mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
    //         mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    //     } 
    //     else if (isTransparent) {
    //         mat.SetInt("_Surface", 1); // Transparent
    //         mat.SetInt("_Blend", 0);   // Alpha blending
    //         mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
    //         mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
    //         mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    //     } 
    //     else {
    //         mat.SetInt("_Surface", 0); // Opaque
    //         mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
    //     }

    //     // Control Backface Culling
    //     mat.SetInt("_Cull", backfaceCulling ? (int)UnityEngine.Rendering.CullMode.Back : (int)UnityEngine.Rendering.CullMode.Off);

    //     return mat;
    // }

}
