using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.TerrainTools;
using Unity.Mathematics;
using UnityEditor.Rendering;

public class MaterialManager : MonoBehaviour {
    public static MaterialManager Instance { get; private set; }
    public List<Material> definedMaterials = new List<Material>();
    public Dictionary<string, Material> sharedMaterials = new Dictionary<string, Material>();
    private Shader _flipbookShader; // Assign the array shader in Inspector or load via Resources
    private MaterialPropertyBlock mpb;
    void Awake() {
        if (Instance == null) {
            Instance = this;
            _flipbookShader = Shader.Find("Custom/HDRP/FlipbookAnimatorArray"); // Find the shader
            mpb = new MaterialPropertyBlock();
            DontDestroyOnLoad(gameObject);
        } else {
            Destroy(gameObject);
        }

        // Create and store shared ship material
        // sharedMaterials.Add("stdhull",CreateSharedMaterial("Universal Render Pipeline/Lit"));
        // sharedMaterials.Add("lightmap",CreateSharedMaterial("Universal Render Pipeline/Unlit"));
        //sharedMaterials.Add("stdhull",CreateHDRPMaterial("HDRP/Lit"));
        for (int i = 0; i < definedMaterials.Count; i++)
        {
            if (definedMaterials[i] != null)
            {
                sharedMaterials.Add(definedMaterials[i].name, definedMaterials[i]);
            }
        }

        //sharedMaterials.Add("stdhull_additive",CreateHDRPMaterial("HDRP/Lit",isAdditive:true));
        //sharedMaterials.Add("stdhull_alpha",CreateHDRPMaterial("HDRP/Lit",isTransparent:true));
        //sharedMaterials.Add("lightmap",CreateHDRPMaterial("HDRP/Lit", emissive:true));
        //sharedMaterials.Add("lightmap_alpha",CreateHDRPMaterial("HDRP/Unlit",isTransparent:true));
        //sharedMaterials.Add("lightmap_additive",CreateHDRPMaterial("HDRP/Unlit",isAdditive:true));
        //sharedMaterials.Add("lightmap_alpha_nocull",CreateHDRPMaterial("HDRP/Unlit",isTransparent:true,backfaceCulling:false));
        // sharedMaterials.Add("sprite_additive",CreateHDRPMaterial("Custom/HDRP/FlipbookAnimatorArray",isAdditive:true, backfaceCulling:false));
        // sharedMaterials.Add("sprite_alpha",CreateHDRPMaterial("Custom/HDRP/FlipbookAnimatorArray",isTransparent:true, backfaceCulling:false));
        // sharedMaterials.Add("sprite",CreateHDRPMaterial("Custom/HDRP/FlipbookAnimatorArray",isTransparent:false,backfaceCulling:false));
        // sharedMaterials.Add("nospec",sharedMaterials["stdhull"]);
        Debug.Log("MaterialManager initialized and shared materials created.");
    }

    public void ApplySODMaterial(Renderer renderer, Texture2D baseTex, Texture2D normalTex, Texture2D emissionTex,
            bool useAnimationData, EffectAnimationDataArrayBased effectAnimationData,
            // bool isTransparent = false, bool isAdditive = false, bool backfaceCulling = true,
            int materialIndex = -1,
            bool lit_material = true)
            //int numberOfMaterials = 1,
            //bool emissive = false) {
    
    {
        // Apply the material property block
        if (useAnimationData)
        {
            // Apply animation data to the renderer
            AdvancedFlipbookControllerArrays controller = renderer.gameObject.GetComponent<AdvancedFlipbookControllerArrays>();
            if (controller == null) controller = renderer.gameObject.AddComponent<AdvancedFlipbookControllerArrays>();
            // TODO: what to do with the normalTex, emissionTex?
            controller.Configure(effectAnimationData);
        }
        else
        {
            // If not using animation data, do a simple mpb here
            if (materialIndex > -1) renderer.GetPropertyBlock(mpb, materialIndex); else renderer.GetPropertyBlock(mpb);
            if (lit_material)
            {

                if (baseTex != null)
                        mpb.SetTexture("_BaseColorMap", baseTex);
                    else
                    {
                        //mpb.SetTexture("_BaseColorMap", null);
                        mpb.SetColor("_BaseColor", Color.clear);
                    }
                if (normalTex != null)
                    mpb.SetTexture("_NormalMap", normalTex);
                else
                    mpb.SetTexture("_NormalMap", new Texture2D(2,2));
                if (emissionTex != null)
                {
                    //mpb.SetFloat("_UseEmissive", 1.0f);
                    //mpb.SetColor("_EmissiveColor", Color.white * 25.0f);
                    mpb.SetTexture("_EmissiveColorMap", emissionTex);
                    Debug.Log("Emissive map set to: " + emissionTex.name);
                }
                else
                {
                    Texture2D _et = new Texture2D(2, 2);
                    _et.SetPixels(new Color[4] { Color.clear, Color.clear, Color.clear, Color.clear });
                    _et.Apply();
                    mpb.SetTexture("_EmissiveColorMap", _et);
                }
            }
            else
            {
                if (baseTex != null)
                {
                    mpb.SetTexture("_UnlitColorMap", baseTex);
                    mpb.SetColor("_UnlitColor", Color.grey*0.5f);
                    mpb.SetTexture("_EmissiveColorMap", baseTex);
                }
                else
                    mpb.SetColor("_UnlitColor", Color.clear);
                //if (normalTex != null) mpb.SetTexture("_NormalMap", normalTex);
            }
            if (materialIndex > -1) renderer.SetPropertyBlock(mpb, materialIndex); else renderer.SetPropertyBlock(mpb);
            Debug.LogFormat("Applied material properties to {0}.{1} with baseTex: {2},",
                renderer.name, materialIndex, baseTex != null ? baseTex.name : "null");
        }
    }

    public void ApplyMaterial(string materialName, Renderer renderer, Texture2D baseTex, Texture2D normalTex, Texture2D emissionTex,
            bool useAnimationData, EffectAnimationDataArrayBased effectAnimationData,
            bool isTransparent = false, bool isAdditive = false, bool backfaceCulling = true,
            int materialIndex = -1,
            int numberOfMaterials = 1,
            bool emissive = false)
    {
        if (renderer == null)
        {
            Debug.LogError("Renderer is null. Cannot apply material.");
            return;
        }

        // Check if the materials already applied; if not, create and assign them
        // Material name should not contain the "tag" such as "additive" or "alpha"
        Material _mat = null;
        if (renderer.sharedMaterials.Length == 0 && renderer.materials.Length == 0 &&
            renderer.sharedMaterial == null && renderer.material == null)
        {
            _mat = GetOrCreateMaterial(materialName, isTransparent, isAdditive, backfaceCulling, emissive);
            if (numberOfMaterials > 1)
            {
                Debug.LogFormat("Assigning shared material {0} to renderer {1} at index {2}, reqst: {3}", _mat.name, renderer.name, materialIndex, materialName);
                renderer.sharedMaterial = _mat; // Assign shared material
            }
            else
            {
                Debug.LogFormat("Assigning material {0} to renderer {1} at index {2}, reqst: {3}", _mat.name, renderer.name, materialIndex, materialName);
            }
        }

        // Apply the material property block
        if (useAnimationData)
        {
            // Apply animation data to the renderer
            AdvancedFlipbookControllerArrays controller = renderer.gameObject.GetComponent<AdvancedFlipbookControllerArrays>();
            if (controller == null) controller = renderer.gameObject.AddComponent<AdvancedFlipbookControllerArrays>();
            // TODO: what to do with the normalTex, emissionTex?
            controller.Configure(effectAnimationData);
        }
        else
        {
            // If not using animation data, do a simple mpb here
            if (materialIndex > -1) renderer.GetPropertyBlock(mpb, materialIndex); else renderer.GetPropertyBlock(mpb);
            if (baseTex != null)
                mpb.SetTexture("_BaseColorMap", baseTex);
            else
                mpb.SetColor("_BaseColor", Color.clear);
            if (normalTex != null) mpb.SetTexture("_NormalMap", normalTex);
            if (emissionTex != null)
            {
                //mpb.SetFloat("_UseEmissive", 1.0f);
                //mpb.SetColor("_EmissiveColor", Color.white * 25.0f);
                mpb.SetTexture("_EmissiveColorMap", emissionTex);
                Debug.Log("Emissive map set to: " + emissionTex.name);
            }
            if (materialIndex > -1) renderer.SetPropertyBlock(mpb, materialIndex); else renderer.SetPropertyBlock(mpb);
            Debug.LogFormat("Applied material properties to {0} / {1} with baseTex: {2},",
                renderer.name, _mat?.name ?? "n/a", baseTex != null ? baseTex.name : "null");
        }
    }


    public Material GetOrCreateMaterial(string materialName, bool isTransparent, bool isAdditive, bool backfaceCulling, bool emissive)
    {

        // string key = materialName;
        // if (isTransparent)
        //     key += "_alpha";
        // if (isAdditive)
        //     key += "_additive";
        // if (!backfaceCulling)
        //     key += "_culloff";
        // if (!sharedMaterials.ContainsKey(key))
        // {
        //     Debug.LogFormat("Creating material: {0}", key);
        //     if (sharedMaterials.ContainsKey(materialName))
        //     {
        //         Debug.LogFormat("getOrCreate: {0}, {1}", materialName, sharedMaterials[materialName].shader);
        //         //sharedMaterials.Add(key,CreateHDRPMaterial(sharedMaterials[materialName].shader,isTransparent,isAdditive,backfaceCulling,emissive));
        //     }
        //     else { }
        //         //sharedMaterials.Add(key,CreateHDRPMaterial("HDRP/Lit",isTransparent,isAdditive,backfaceCulling,emissive));
        // }
        // else
        // {
        //     Debug.LogFormat("Using existing material: {0}", key);
        // }
        if (!sharedMaterials.ContainsKey(materialName))
        {
            Debug.LogErrorFormat("Material {0} not found in shared materials. Creating new material.", materialName);
            return null;
        }
        return sharedMaterials[materialName];
    }

    // void ConfigureMaterialBlendMode(ref Material mat, CustomSpriteFormat.MaterialType type, bool backfaceCulling = true, bool emissive = false)
    // {
    //     mat.enableInstancing = true;

    //     // Surface Type: Opaque or Transparent
    //     //mat.SetFloat("_SurfaceType", (type == CustomSpriteFormat.MaterialType.Alpha || type == CustomSpriteFormat.MaterialType.Additive) ? 1.0f : 0.0f);
    //    // mat.SetFloat("_BlendMode", type == CustomSpriteFormat.MaterialType.Additive ? 1.0f : 0.0f); // Additive blending
    //     //mat.SetFloat("_CullMode", backfaceCulling ? 2.0f : 0.0f); // 2 = Back, 0 = Off
    //     mat.SetFloat("_CullMode", backfaceCulling ? (int)UnityEngine.Rendering.CullMode.Back : (int)UnityEngine.Rendering.CullMode.Off);

    //     // Set the emissive color if applicable
    //     if (emissive)
    //     {
    //         mat.SetFloat("_UseEmissive", 1.0f);
    //         mat.SetColor("_EmissiveColor", Color.white * 2500.0f); // Set emissive color to white
    //         mat.EnableKeyword("_EMISSIVE_COLOR_MAP");
    //     }
    //     else
    //     {
    //         mat.SetFloat("_UseEmissive", 0.0f);
    //         mat.SetColor("_EmissiveColor", Color.black); // Set emissive color to black
    //     }
    //     // Set common properties for transparency
    //     //mat.SetFloat("_SurfaceType", 1.0f); // 1 = Transparent
    //     //return;

    //     // HDRP blend mode setup (Simplified - adjust based on exact HDRP standard lit blend properties if needed)
    //     // These property names might need updating based on exact HDRP Lit shader conventions if not using legacy properties.
    //     switch (type)
    //     {
    //         case CustomSpriteFormat.MaterialType.Additive:
    //             mat.SetColor("_BaseColor", Color.white);
    //             //mat.SetFloat("_UseEmissive", 1.0f); // Enable emissive path in shader
    //             //mat.SetColor("_EmissiveColor", Color.white*3000f); // Set emissive color to white
    //             //mat.SetFloat("_BlendMode", 1.0f);  // Set to Additive if HDRP uses this property
    //             mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); // SrcAlpha for pre-multiplied, One for additive
    //             mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
    //             //mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 50 ;
    //             //mat.SetFloat("_AlphaCutoffEnable", 0.0f);
    //             mat.SetFloat("_SurfaceType", 1.0f); // Transparent surface
    //             mat.SetFloat("_ZWrite", 0.0f);      // ZWrite Off

    //             Debug.Log("Additive blend mode set.");
    //             break;

    //         case CustomSpriteFormat.MaterialType.Alpha: // Standard Alpha Blending
    //             mat.SetColor("_BaseColor", Color.white);
    //             //mat.SetFloat("_UseEmissive", 0.0f);
    //             //mat.SetFloat("_BlendMode", 0.0f); // Set to Alpha if HDRP uses this property
    //             mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); // Standard alpha blend
    //             mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
    //             mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    //             //mat.SetFloat("_AlphaCutoffEnable", 0.0f);
    //             mat.SetFloat("_SurfaceType", 1.0f); // Transparent surface
    //             Debug.Log("Alpha blend mode set.");
    //             break;

    //         default:
    //             mat.SetColor("_BaseColor", Color.white);
    //             //mat.SetFloat("_UseEmissive", 0.0f);
    //             //mat.SetFloat("_BlendMode", 0.0f); // Set to Alpha if HDRP uses this property
    //             mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One); // Standard alpha blend
    //             mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
    //             mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
    //             mat.SetFloat("_AlphaCutoffEnable", 1.0f);
    //             mat.SetFloat("_SurfaceType", 0.0f); // Opaque surface
    //             Debug.Log("Default blend mode set.");
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
    //     //mat.renderQueue = 3000;
    // }
    
    // void ConfigureMaterialBlendMode(ref Material mat, bool isTransparent, bool isAdditive, bool backfaceCulling, bool emissive = false) {
    //     // Set the blend mode based on the type
    //     CustomSpriteFormat.MaterialType type = isAdditive ? CustomSpriteFormat.MaterialType.Additive : 
    //         (isTransparent ? CustomSpriteFormat.MaterialType.Alpha : CustomSpriteFormat.MaterialType.Default);
    //     ConfigureMaterialBlendMode(ref mat, type, backfaceCulling, emissive);
    // }

    // Material CreateHDRPMaterial(string shaderName, bool isTransparent = false, bool isAdditive = false, bool backfaceCulling = true, bool emissive = false) {
    //     var shader = Shader.Find(shaderName);
    //     return CreateHDRPMaterial(shader, isTransparent, isAdditive, backfaceCulling, emissive);
    // }
    // Material CreateHDRPMaterial(Shader shader, bool isTransparent = false, bool isAdditive = false, bool backfaceCulling = true, bool emissive = false) {
    //     Material mat = new Material(shader);
    //     //ConfigureMaterialBlendMode(ref mat, isTransparent, isAdditive, backfaceCulling, emissive);
    //     mat.enableInstancing = true;
    //     mat.name = shader.name + (isTransparent ? "_Alpha" : "") + (isAdditive ? "_Additive" : "") + (backfaceCulling ? "" : "_NoCull") + (emissive ? "_Emissive" : "");
    //     return mat;
    // }

    // Update the emissive color based on the intensity and LDR color
    // This is necessary because HDRP does not set emissive intensity directly in the shader
    public static void UpdateEmissiveColorFromIntensityAndEmissiveColorLDR(Material material)
    {
        const string kEmissiveColorLDR = "_EmissiveColorLDR";
        const string kEmissiveColor = "_EmissiveColor";
        const string kEmissiveIntensity = "_EmissiveIntensity";

        if (material.HasProperty(kEmissiveColorLDR) && material.HasProperty(kEmissiveIntensity) && material.HasProperty(kEmissiveColor))
        {
            // Important: The color picker for kEmissiveColorLDR is LDR and in sRGB color space but Unity don't perform any color space conversion in the color
            // picker BUT only when sending the color data to the shader... So as we are doing our own calculation here in C#, we must do the conversion ourselves.
            Color emissiveColorLDR = material.GetColor(kEmissiveColorLDR);
            Color emissiveColorLDRLinear = new Color(Mathf.GammaToLinearSpace(emissiveColorLDR.r), Mathf.GammaToLinearSpace(emissiveColorLDR.g), Mathf.GammaToLinearSpace(emissiveColorLDR.b));
            material.SetColor(kEmissiveColor, emissiveColorLDRLinear * material.GetFloat(kEmissiveIntensity));
        }
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
