using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System;

[SkyUniqueID(12345)]
[Serializable, VolumeComponentMenu("Sky/Panoramic Sky")]
public class PanoramicSkySettings : SkySettings
{
    [Tooltip("The panoramic texture to use for the sky.")]
    public TextureParameter skyTexture = new TextureParameter(null);

    public override Type GetSkyRendererType() => typeof(PanoramicSkyRenderer);

    public override int GetHashCode()
    {
        int hash = base.GetHashCode();
        unchecked
        {
            // --- FIX IS HERE ---
            // Only include the texture in the hash if it has been assigned.
            if (skyTexture != null && skyTexture.value != null)
            {
                hash = hash * 23 + skyTexture.GetHashCode();
            }
        }
        return hash;
    }
}