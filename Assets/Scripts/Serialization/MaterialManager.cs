using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.TerrainTools;
using Unity.Mathematics;

public class ShipMaterialManager : MonoBehaviour {
    public static ShipMaterialManager Instance { get; private set; }
    public Dictionary<string,Material> sharedMaterials = new Dictionary<string,Material>() ;

    void Awake() {
        if (Instance == null) {
            Instance = this;
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
        sharedMaterials.Add("nospec",sharedMaterials["stdhull"]);
    }

    public void ApplyShipMaterial(string materialName, 
            MeshRenderer meshRenderer, 
            Texture2D baseTex, 
            Texture2D normalTex, 
            Texture2D emissionTex, 
            bool isTransparent = false,
            bool isAdditive = false,
            bool backfaceCulling = true,
            int materialIndex = -1,
            int numberOfMaterials = 1
            ) {
        if (meshRenderer == null) return;

        // Assign the shared material
        if (numberOfMaterials > 1)
            meshRenderer.sharedMaterials[materialIndex] = GetOrCreateMaterial(materialName, isTransparent, isAdditive, backfaceCulling);
        else
            meshRenderer.sharedMaterial = GetOrCreateMaterial(materialName, isTransparent, isAdditive, backfaceCulling);

        // Create a new MaterialPropertyBlock for per-instance texture overrides
        MaterialPropertyBlock mpb = new MaterialPropertyBlock();

        if (baseTex != null) mpb.SetTexture("_BaseColorMap", baseTex);
        if (isTransparent) {
            mpb.SetFloat("_AlphaCutoffEnable", 0.0f);
            mpb.SetColor("_BaseColor", Color.black);
            mpb.SetFloat("_SurfaceType", 1); // Transparent surface
            mpb.SetFloat("_BlendMode", 0);   // Alpha blending  
        }

        if (normalTex != null) mpb.SetTexture("_NormalMap", normalTex);
        if (emissionTex != null) {
            mpb.SetTexture("_EmissiveColorMap", emissionTex);
            mpb.SetColor("_EmissiveColor", Color.black * 10.0f); // HDR Emission Boost
        }

        // Apply property block to MeshRenderer
        if (materialIndex > -1)
            meshRenderer.SetPropertyBlock(mpb,materialIndex);
        else
            meshRenderer.SetPropertyBlock(mpb);

    }


    public void ApplyShipMaterial(string materialName, Renderer renderer, Texture2D baseTex, Texture2D normalTex, Texture2D emissionTex,
            bool isTransparent = false, bool isAdditive = false, bool backfaceCulling = true) {
        if (renderer == null) return;

        renderer.sharedMaterial = GetOrCreateMaterial(materialName, isTransparent, isAdditive, backfaceCulling); // Assign shared material

        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        if (baseTex != null) mpb.SetTexture("_BaseMap", baseTex);
        if (normalTex != null) mpb.SetTexture("_BumpMap", normalTex);
        if (emissionTex != null) {
            mpb.SetTexture("_EmissionMap", emissionTex);
            mpb.SetColor("_EmissionColor", Color.white * 5.0f);
        }
        renderer.SetPropertyBlock(mpb);
    }

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
                    Debug.LogFormat("getorcreate: {0}, {1}", materialName, sharedMaterials[materialName].shader);
                    sharedMaterials.Add(key,CreateHDRPMaterial(sharedMaterials[materialName].shader,isTransparent,isAdditive,backfaceCulling));
                }
                else
                    sharedMaterials.Add(key,CreateHDRPMaterial("HDRP/Lit",isTransparent,isAdditive,backfaceCulling));
            }
        return sharedMaterials[materialName];
    }
    Material CreateHDRPMaterial(string shaderName, bool isTransparent = false, bool isAdditive = false, bool backfaceCulling = true) {
        var shader = Shader.Find(shaderName);
        return CreateHDRPMaterial(shader, isTransparent, isAdditive, backfaceCulling);
    }
    Material CreateHDRPMaterial(Shader shader, bool isTransparent = false, bool isAdditive = false, bool backfaceCulling = true) {
        Material mat = new Material(shader);
        mat.enableInstancing = true;

        // Surface Type: Opaque or Transparent
        mat.SetFloat("_SurfaceType", isTransparent ? 1.0f : 0.0f);
        mat.SetFloat("_BlendMode", isAdditive ? 1.0f : 0.0f); // Additive blending
        mat.SetFloat("_CullMode", backfaceCulling ? 2.0f : 0.0f); // 2 = Back, 0 = Off

        // Additive rendering requires alpha blending
        if (isTransparent) {
            mat.renderQueue = isAdditive 
                ? (int)UnityEngine.Rendering.RenderQueue.Transparent + 50 
                : (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetFloat("_AlphaCutoffEnable", 0.0f);
        } else {
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
        }

        return mat;
    }

    // public Material CreateSharedMaterial(string shaderName, bool isTransparent = false, bool isAdditive = false, bool backfaceCulling = true) {
    //     Material mat = new Material(Shader.Find(shaderName));
    //     mat.enableInstancing = true;

    //     // Set the blend mode
    //     if (isAdditive) {
    //         mat.SetInt("_Surface", 1); // Transparent
    //         mat.SetInt("_Blend", 1);   // Additive blending
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
