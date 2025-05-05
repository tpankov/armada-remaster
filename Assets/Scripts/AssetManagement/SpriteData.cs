using UnityEngine;
using System.Collections.Generic;

namespace CustomSpriteFormat
{
    // Enum to represent material types mentioned in the file
    public enum MaterialType
    {
        Default,
        Additive,
        Alpha
    }

    // Holds definition parsed from a texture line
    public class SpriteDefinition
    {
        public string name;
        public string sourceTextureName; // Filename from the config (e.g., "FTwaspr1")
        public Rect sourceRect; // Pixel coordinates within the source texture
        public MaterialType materialType = MaterialType.Default;
        public string defaultAnimationName = null; // From @anim=...
        public Vector2 pivot = new Vector2(0.5f, 0.5f); // Default pivot
        public int referenceSize = 0; // Store the @reference value active when defined
    }

    // Enum for different animation types
    public enum AnimationType
    {
        Unknown,
        Draw,   // Index/Sprite switching
        Colour, // Color tweening
        Offset  // Texture UV offset/tiling/scrolling
    }

    // Enum for interpolation modes
    public enum InterpolationMode
    {
        Step,
        Linear,
        LinearCrossfade
    }

     // Enum for automatic keyframe generation types (parsed but generation logic not implemented here)
    public enum AutoKeyframeType
    {
        None,
        Row,
        Column,
        Grid
    }

    // Represents a single keyframe in an animation
    public struct KeyframeData
    {
        public float time;
        public object value; // Can be int (draw), Color (colour), Vector2 (offset)

        // Convenience getters
        public int IntValue => (value is int i) ? i : 0;
        public float FloatValue => (value is float f) ? f : 0f; // Default to 0 if not float
        public Color ColorValue => (value is Color c) ? c : Color.clear; // Default to clear if not Color
        public Vector2 Vector2Value => (value is Vector2 v) ? v : Vector2.zero;
    }

    // Holds definition parsed from an @animation block
    public class AnimationDefinition
    {
        public string name;
        public AnimationType type = AnimationType.Unknown; // Type of animation (draw, colour, offset)
        public int frameCount; // Frame count specified in definition (e.g., draw 3 ...)
        public float duration;
        public InterpolationMode interpolation; // Interpolation mode (step, linear, linear crossfade)
        public AutoKeyframeType autoKeyframe; // If @auto is used (e.g., row, column, grid)
        public int autoDimension = 0; // Dimension for @auto (e.g., offset 64 ...)
        public List<KeyframeData> keyframes = new List<KeyframeData>();
        public int referenceSize = 0; // Store the @reference value active when defined (for offset animations)
    }

    public class SpriteNodeDefinition
    {
        public string NodeName { get; set; } // Name of the node itself (e.g., "ftwaspr1")
        public string BaseSpriteName { get; set; } // Name of the sprite definition to use (e.g., "ftspark1")
        public string AnimationName { get; set; } = "const"; // Name of animation definition ("const" if none)
        public Vector2 Size { get; set; } = Vector2.one; // Size from tuple (e.g., (7,7))
        public Color Tint { get; set; } = Color.white; // Tint from tuple (e.g., (1,1,1))
        public bool IsBillboard { get; set; } = false; // From "billboard" tag
    }
}