using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class PanoramicSkyRenderer : SkyRenderer
{
    private Material skyMaterial;
    private PanoramicSkySettings settings;
    private static readonly int _SkyTexture = Shader.PropertyToID("_SkyTexture");
    private static readonly int _Exposure = Shader.PropertyToID("_Exposure");
    private static readonly int _Rotation = Shader.PropertyToID("_Rotation");

    // A default constructor for robustness with Unity's systems.
    public PanoramicSkyRenderer(){}

    // The main constructor that HDRP uses to create the renderer and give it the settings.
    public PanoramicSkyRenderer(PanoramicSkySettings settings)
    {
        this.settings = settings;
    }

    public override void Build()
    {
        if (skyMaterial == null)
        {
            skyMaterial = CoreUtils.CreateEngineMaterial("Shader Graphs/PanoramicSkyShader");
        }
    }

    public override void Cleanup()
    {
        CoreUtils.Destroy(skyMaterial);
    }

    public override void RenderSky(BuiltinSkyParameters builtinParams, bool renderForCubemap, bool renderSunDisk)
    {
        // We rely on the 'settings' object that was passed into our constructor.
        // Also check that the settings object itself isn't null.
        if (settings == null || settings.skyTexture.value == null || skyMaterial == null)
            return;
        
        skyMaterial.SetFloat(_Exposure, settings.exposure.value);
        skyMaterial.SetFloat(_Rotation, settings.rotation.value);
        skyMaterial.SetTexture(_SkyTexture, settings.skyTexture.value);

        CoreUtils.DrawFullScreen(builtinParams.commandBuffer, skyMaterial, null, 0);
    }
}