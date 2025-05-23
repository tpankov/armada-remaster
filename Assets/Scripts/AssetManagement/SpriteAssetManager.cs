using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO; // Required for File I/O
using System.Globalization; // For float parsing
using System.Text.RegularExpressions; // For parsing tuples
using CustomSpriteFormat;
using UnityEngine.UI; // For enums like AnimationType, MaterialType etc.
// NOTE: Does NOT require CustomSpriteFormat.ECS namespace


public class SpriteAssetManager : MonoBehaviour
{
    [Header("Configuration")]
    public string configFileName = "ftspecial.txt"; // Config file in StreamingAssets/DynamicAssets/sprites/

    // --- Runtime Data Storage ---
    public Dictionary<string, Sprite> loadedSprites = new Dictionary<string, Sprite>();
    public Dictionary<string, AnimationDefinition> loadedAnimations = new Dictionary<string, AnimationDefinition>();
    public Dictionary<string, SpriteNodeDefinition> loadedSpriteNodes = new Dictionary<string, SpriteNodeDefinition>();
    public Dictionary<string, Texture2D> loadedTextures = new Dictionary<string, Texture2D>(); // Cache loaded textures

    // *** ADDED: To store parsed definition including MaterialType for later lookup ***
    public Dictionary<string, ParsedSprite> parsedSpriteDefinitions = new Dictionary<string, ParsedSprite>();

    // --- Singleton Pattern ---
    private static SpriteAssetManager _instance;
    public static SpriteAssetManager Instance
    {
        get
        {
            if (_instance == null) _instance = FindFirstObjectByType<SpriteAssetManager>() ?? new GameObject("SpriteAssetManager").AddComponent<SpriteAssetManager>();
            return _instance;
        }
    }

    private bool isInitialized = false;
    private bool initializationAttempted = false;

    // --- Temporary structures for parsing before creating Sprites/storing definitions ---
    // (Using the same definitions as in the BlobAssetManager version for parsing consistency)
    public class ParsedKeyframe { public float Time; public object Value; } // Value: int, Color(float4), Vector2(float2)
    public class ParsedAnimation { 
        public string Name; 
        public AnimationType Type; 
        public int FrameCount; 
        public float Duration; 
        public InterpolationMode Interpolation; 
        public AutoKeyframeType AutoKeyframe; 
        public int AutoDimension; 
        public List<ParsedKeyframe> Keyframes = new List<ParsedKeyframe>(); // List of keyframes for this animation
        public int ReferenceSize; }
    // ParsedSprite stores data exactly as read + context, before scaling/Sprite creation
    public class ParsedSprite 
    { 
        public string Name; 
        public string SourceTextureName; 
        public Rect SourceRect; 
        public MaterialType MaterialType; 
        public string DefaultAnimationName; 
        public Vector2 Pivot; 
        public int ReferenceSize;
    }
    // SpriteNodeDefinition is already defined in SpriteData.cs


    void Awake()
    {
        if (_instance == null) { _instance = this; /* DontDestroyOnLoad(gameObject); */ }
        else if (_instance != this) { Destroy(gameObject); return; }
        Initialize();
    }

    public void Initialize()
    {
        if (isInitialized || initializationAttempted) return;
        initializationAttempted = true;

        Debug.Log($"Attempting to initialize SpriteAssetManager with config: {configFileName}");

        // --- Clear previous data ---
        ClearDictionaries(); // Clear sprites, anims, nodes, parsed defs
        // Texture cache intentionally not cleared here

        string configFilePath = Path.Combine("DynamicAssets", "sprites", configFileName);
        if (!File.Exists(configFilePath)) { Debug.LogError($"Configuration file not found at: {configFilePath}."); return; }

        string fileContent;
        try { fileContent = File.ReadAllText(configFilePath); }
        catch (Exception ex) { Debug.LogError($"Error reading configuration file '{configFilePath}': {ex.Message}"); return; }

        // --- Step 1: Parse into temporary/final structures ---
        List<ParsedSprite> tempParsedSprites = new List<ParsedSprite>(); // Temp list for processing
        List<ParsedAnimation> tempParsedAnims = new List<ParsedAnimation>(); // Temp list for processing
        // loadedSpriteNodes and parsedSpriteDefinitions are populated directly by the parser
        if (!ParseFileContent(fileContent, tempParsedSprites, tempParsedAnims, loadedSpriteNodes, parsedSpriteDefinitions))
        {
            Debug.LogError($"Failed to parse the configuration file content.");
            return;
        }
        //Debug.Log($"Parsed {tempParsedSprites.Count} sprites and {tempParsedAnims.Count} animations from '{configFileName}'.");
        //Debug.Log($"Parsed {loadedSpriteNodes.Count} sprite nodes");
        //Debug.Log($"Parsed {parsedSpriteDefinitions.Count} parsed sprite definitions");
        // print out internal fields of sprites for debugging
        //foreach (var sprite in tempParsedSprites)
        //{
            //Debug.Log($"Parsed Sprite: {sprite.Name}, Texture: {sprite.SourceTextureName}, Rect: {sprite.SourceRect}, MaterialType: {sprite.MaterialType}, ReferenceSize: {sprite.ReferenceSize}");
        //}

        // --- Step 2: Load Textures ---
        List<string> requiredTextures = GetRequiredTextureNames(tempParsedSprites);
        if (!LoadRequiredTextures(requiredTextures)) { Debug.LogError($"Failed to load required textures."); }

        // --- Step 3: Create Sprite Objects ---
        CreateSprites(tempParsedSprites); // Uses loadedTextures and populates loadedSprites

        // --- Step 4: Store Animation Definitions ---
        StoreAnimations(tempParsedAnims); // Populates loadedAnimations

        //Debug.Log($"SpriteAssetManager Initialized: Loaded {loadedAnimations.Count} animations, {loadedSpriteNodes.Count} sprite nodes from '{configFileName}'.");
        // Print out all loadedSpriteNodes and their properties
        // foreach (var node in loadedSpriteNodes)
        // {
        //     Debug.Log($"Loaded Sprite Node: {node.Key}, BaseSprite: {node.Value.BaseSpriteName}, Animation: {node.Value.AnimationName}, Size: {node.Value.Size}, Tint: {node.Value.Tint}, Billboard: {node.Value.IsBillboard}");
        // }
        
        isInitialized = true;
    }


    // --- Parsing Logic ---
    private bool ParseFileContent(string fileContent,
                                 List<ParsedSprite> tempSpriteDefs, // Temp list to gather sprites before processing
                                 List<ParsedAnimation> tempAnimDefs, // Temp list
                                 Dictionary<string, SpriteNodeDefinition> finalNodeDefs, // Populate directly
                                 Dictionary<string, ParsedSprite> finalParsedSpriteDefs) // Populate directly
    {
        // Using Regex for tuple matching
        Regex tupleRegex = new Regex(@"\(\s*([-\d\.]+)\s*,\s*([-\d\.]+)(?:\s*,\s*([-\d\.]+))?\s*\)"); // 2 or 3 values

        string[] lines = fileContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        int currentReference = 0;
        MaterialType currentMaterial = MaterialType.Default;
        ParsedAnimation currentAnimation = null; // Use the temporary parsing struct
        bool parsingKeyframes = false;
        bool success = true;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

            if (parsingKeyframes && (line.StartsWith("@") || !char.IsDigit(line[0]) && line[0] != '(')) { parsingKeyframes = false; currentAnimation = null; }

            try
            {
                string[] initialParts = line.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (initialParts.Length == 0) continue;
                string firstWord = initialParts[0].ToLower();

                if (firstWord.StartsWith("@")) // Directive processing
                {
                    string directive = firstWord;
                    string valuePart = "";
                    int equalsIndex = line.IndexOf('=');
                    if (equalsIndex != -1) 
                    {
                        valuePart = line.Substring(equalsIndex + 1).Trim();
                        directive = firstWord.Substring(0, equalsIndex).Trim(); // Directive without '='
                    }

                    switch (directive)
                    {
                        case "@include":
                            string includeFile = initialParts[1];//.Trim(); 
                            string includePath = Path.Combine("DynamicAssets", "sprites", includeFile);
                            if (File.Exists(includePath)) 
                            { 
                                string includedContent = File.ReadAllText(includePath); 
                                ParseFileContent(includedContent, tempSpriteDefs, tempAnimDefs, finalNodeDefs, finalParsedSpriteDefs);
                            } 
                            else 
                            {
                                Debug.LogWarning($"Include file '{includeFile}' not found."); 
                            } 
                            break;
                        case "@reference":
                            if (equalsIndex != -1 && int.TryParse(valuePart, out int refValue)) currentReference = refValue;
                            else if (equalsIndex != -1) Debug.LogWarning($"Could not parse @reference value '{valuePart}' on line {i + 1}");
                            parsingKeyframes = false; break;
                        case "@tmaterial":
                            if (equalsIndex != -1) { currentMaterial = valuePart.ToLower() switch { "additive" => MaterialType.Additive, "alpha" => MaterialType.Alpha, _ => MaterialType.Default }; }
                            parsingKeyframes = false; break;
                        case "@animation":
                            if (initialParts.Length > 1) {
                                string animName = initialParts[1].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)[0];
                                currentAnimation = new ParsedAnimation { 
                                    Name = animName, 
                                    ReferenceSize = currentReference, 
                                    Interpolation = InterpolationMode.Step, 
                                    Type = AnimationType.Unknown,
                                    AutoKeyframe = AutoKeyframeType.None, // Default to None
                                    }; // Initialize with defaults
                                tempAnimDefs.Add(currentAnimation); // Add to temp list
                            } else Debug.LogWarning($"Missing name for @animation on line {i + 1}");
                            parsingKeyframes = false; break;
                        case "@keyframes":
                                if (currentAnimation != null)
                                {
                                    // Check if @auto was set for this animation
                                    if (currentAnimation.AutoKeyframe != AutoKeyframeType.None)
                                    {
                                        // Generate keyframes automatically based on @auto settings
                                        // GenerateAutoKeyframes(currentAnimation); // Handled in shader now
                                        // Skip manual keyframe parsing for this animation
                                        parsingKeyframes = false;
                                        // Optional: Keep currentAnimation context active if other properties might follow?
                                        // Or set currentAnimation = null; if @keyframes always terminates the block?
                                        // Let's assume @keyframes terminates for now.
                                        //Debug.Log($"Known @auto directive {currentAnimation.AutoKeyframe}, skipping keyframe parsing.");
                                        currentAnimation = null;
                                    }
                                    else
                                    {
                                        // No @auto directive, proceed with manual keyframe parsing
                                        parsingKeyframes = true;
                                        //Debug.Log($"No @auto directive {currentAnimation.AutoKeyframe}, doing keyframe parsing.");

                                    }
                                }
                                else
                                {
                                    Debug.LogWarning($"'@keyframes' found outside of an @animation context. Line {i + 1}");
                                }
                                break; // End of @keyframes case   
                        case "@auto":
                            if (currentAnimation != null && equalsIndex != -1) 
                            { 
                                currentAnimation.AutoKeyframe = valuePart.ToLower() 
                                switch 
                                {
                                     "row" => AutoKeyframeType.Row, 
                                     "column" => AutoKeyframeType.Column,
                                     "square" => AutoKeyframeType.Grid, 
                                     _ => AutoKeyframeType.None }; 
                                }
                            else if (currentAnimation == null) Debug.LogWarning($"@auto outside @animation context line {i+1}");
                            else Debug.LogWarning($"Missing value for @auto on line {i + 1}");
                            parsingKeyframes = false; break;
                        case "@sprite_node":
                            ParseSpriteNodeLineInternal(line, finalNodeDefs, tupleRegex, i + 1); // Populate final dict directly
                            parsingKeyframes = false; break;
                        // Ignore other directives for now
                        case "@emitter": case "@end_emitter": parsingKeyframes = false; break;
                        default: parsingKeyframes = false; break;
                    }
                }
                else // Non-directive line processing
                {
                    string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parsingKeyframes && currentAnimation != null) ParseKeyframeLineInternal(line, parts, currentAnimation, tupleRegex, i + 1);
                    else if (currentAnimation != null && !parsingKeyframes && (firstWord == "draw" || firstWord == "colour" || firstWord == "color" || firstWord == "offset")) ParseAnimationPropertyLineInternal(parts, currentAnimation, i + 1);
                    else if (parts.Length >= 6 && !parsingKeyframes && currentAnimation == null) ParseSpriteDefinitionLineInternal(line, parts, currentReference, currentMaterial, tempSpriteDefs, finalParsedSpriteDefs, i + 1); // Add to temp list AND final dict
                    else if (!firstWord.StartsWith("sprite_table")) { }
                        //Debug.LogWarning($"Ignoring unrecognized line format: '{line}' on line {i + 1} - {parsingKeyframes} - {currentAnimation?.Name}");
                }
            }
            catch (Exception ex) { Debug.LogError($"Error parsing line {i + 1}: '{line}'. Exception: {ex.Message}\n{ex.StackTrace}"); success = false; }
        }
        return success;
    }

    // Helper for Sprite Definition Line (populates both lists)
    private void ParseSpriteDefinitionLineInternal(string originalLine, string[] parts, int reference, MaterialType material, List<ParsedSprite> tempSpriteDefs, Dictionary<string, ParsedSprite> finalParsedSpriteDefs, int lineNum) {
         try {
            var def = new ParsedSprite {
                Name = parts[0], SourceTextureName = parts[1],
                SourceRect = new Rect( float.Parse(parts[2], CultureInfo.InvariantCulture), float.Parse(parts[3], CultureInfo.InvariantCulture), float.Parse(parts[4], CultureInfo.InvariantCulture), float.Parse(parts[5], CultureInfo.InvariantCulture)),
                MaterialType = material, ReferenceSize = reference, Pivot = new Vector2(0.5f, 0.5f) // Add default pivot
                 // TODO: Parse pivot if format includes it
            };
            // Robust @anim= parsing
            string animDirective = "@anim="; int animIndex = originalLine.IndexOf(animDirective, StringComparison.OrdinalIgnoreCase);
            if (animIndex != -1) {
                 string remainingString = originalLine.Substring(animIndex + animDirective.Length).TrimStart();
                 string[] animParts = remainingString.Split(new[] {' ', '\t'}, 2, StringSplitOptions.RemoveEmptyEntries);
                 if (animParts.Length > 0) def.DefaultAnimationName = animParts[0];
            }
            tempSpriteDefs.Add(def); // Add to list for processing textures/sprites

            // Add to final dictionary for later lookup (e.g., by material type)
            if (!finalParsedSpriteDefs.ContainsKey(def.Name)) {
                 finalParsedSpriteDefs.Add(def.Name, def);
            } else {
                 Debug.LogWarning($"Duplicate ParsedSprite definition name '{def.Name}'. Overwriting in lookup.");
                 finalParsedSpriteDefs[def.Name] = def;
            }

         } catch (Exception ex) { Debug.LogError($"Failed parsing sprite definition line {lineNum}: '{originalLine}'. Error: {ex.Message}"); }
     }
    // Helper for Animation Property Line
    private void ParseAnimationPropertyLineInternal(string[] parts, ParsedAnimation anim, int lineNum) {
         if (parts.Length < 3) return;
         anim.Type = parts[0].ToLower() switch { "draw" => AnimationType.Draw, "colour" => AnimationType.Colour, "color" => AnimationType.Colour, "offset" => AnimationType.Offset, _ => AnimationType.Unknown };
         if (!int.TryParse(parts[1], out anim.FrameCount)) { if(anim.Type == AnimationType.Offset) int.TryParse(parts[1], out anim.AutoDimension); }
         float.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out anim.Duration);
         if (parts.Length > 3) 
         {
            if (parts[3].ToLower() == "linear") 
                anim.Interpolation = InterpolationMode.Linear; 
            else if (parts[3].ToLower() == "linearcrossfade" && anim.Type == AnimationType.Offset) 
                anim.Interpolation = InterpolationMode.LinearCrossfade; 
            else if (parts[3].ToLower() == "step") 
                anim.Interpolation = InterpolationMode.Step; 
         }
         if(anim.Type == AnimationType.Offset && anim.AutoKeyframe != AutoKeyframeType.None && anim.FrameCount == 0) anim.FrameCount = anim.AutoDimension;
      }
    // Helper for Keyframe Line
    private void ParseKeyframeLineInternal(string line, string[] parts, ParsedAnimation anim, Regex tupleRegex, int lineNum) {
        if (parts.Length < 2) return;
        if (!float.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out float time)) return;
        object value = null; string valueString = line.Substring(parts[0].Length).Trim();
        switch (anim.Type) {
            case AnimationType.Draw: if (int.TryParse(parts[1], out int index)) value = index; break;
            case AnimationType.Colour: Match match = tupleRegex.Match(valueString); if (match.Success) { try { value = new Color( float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), float.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture), float.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture), 1f);} catch {} } break;
            case AnimationType.Offset: if (parts.Length >= 3 && float.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out float x) && float.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out float y)) value = new Vector4(x/anim.ReferenceSize, y/anim.ReferenceSize, 0, 0); break;
        }
        if (value != null) anim.Keyframes.Add(new ParsedKeyframe { Time = time, Value = value });
        else Debug.LogWarning($"Could not parse keyframe value '{valueString}' for type {anim.Type} on line {lineNum}");
     }
    // Helper for Sprite Node Line (Populates final dict directly)
    private void ParseSpriteNodeLineInternal(string line, Dictionary<string, SpriteNodeDefinition> nodeDefs, Regex tupleRegex, int lineNum) {
        // ... (Logic from previous BlobAssetManager version to parse line and add to nodeDefs dictionary) ...
         string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 6) { Debug.LogWarning($"Skipping malformed @sprite_node line {lineNum}: Not enough parts."); return; }
        try {
            var nodeDef = new SpriteNodeDefinition { NodeName = parts[1], BaseSpriteName = parts[2], AnimationName = parts[3], Tint = Color.white, Size = Vector2.one };
            Match sizeMatch = tupleRegex.Match(parts[4]); if (sizeMatch.Success && sizeMatch.Groups.Count >= 3) nodeDef.Size = new Vector2( float.Parse(sizeMatch.Groups[1].Value, CultureInfo.InvariantCulture), float.Parse(sizeMatch.Groups[2].Value, CultureInfo.InvariantCulture));
            Match colorMatch = tupleRegex.Match(parts[5]); if (colorMatch.Success && colorMatch.Groups.Count >= 4) nodeDef.Tint = new Color( float.Parse(colorMatch.Groups[1].Value, CultureInfo.InvariantCulture), float.Parse(colorMatch.Groups[2].Value, CultureInfo.InvariantCulture), float.Parse(colorMatch.Groups[3].Value, CultureInfo.InvariantCulture), 1f);
            for (int j = 6; j < parts.Length; j++) if (parts[j].Equals("billboard", StringComparison.OrdinalIgnoreCase)) { nodeDef.IsBillboard = true; break; }
            if (!nodeDefs.ContainsKey(nodeDef.NodeName)) nodeDefs.Add(nodeDef.NodeName, nodeDef); else { Debug.LogWarning($"Duplicate @sprite_node name '{nodeDef.NodeName}' line {lineNum}."); nodeDefs[nodeDef.NodeName] = nodeDef; }
        } catch (Exception ex) { Debug.LogError($"Error parsing @sprite_node line {lineNum}: '{line}'. Exception: {ex.Message}"); }
     }
    // --- End Parsing Logic ---


    // --- Texture Loading ---
    private List<string> GetRequiredTextureNames(List<ParsedSprite> sprites) {
        List<string> names = new List<string>();
        foreach (var sprite in sprites) {
            if (!string.IsNullOrEmpty(sprite.SourceTextureName) && !names.Contains(sprite.SourceTextureName)) {
                names.Add(sprite.SourceTextureName);
            }
        }
        return names;
     }
    private bool LoadRequiredTextures(IEnumerable<string> textureNames) {
        bool success = true;
        foreach (string texName in textureNames) {
            if (!loadedTextures.ContainsKey(texName)) {
                string texturePath = Path.Combine("DynamicAssets", "textures", texName + ".tga");
                if (TGALoader.LoadTGA(texturePath, out Texture2D loadedTexture)) {
                    loadedTextures.Add(texName, loadedTexture);
                    //Debug.Log($"Loaded texture: {texName}.tga");
                } else 
                { 
                    Debug.LogError($"Failed to load required texture: {texName}.tga"); 
                    loadedTexture = new Texture2D(2,2); 
                    loadedTextures.Add(texName, loadedTexture);
                    success = false; }
            }
        }
        return success;
     }

    // --- Sprite Object Creation ---
    private void CreateSprites(List<ParsedSprite> spriteDefs)
    {
        loadedSprites.Clear(); // Clear only the final Sprite dictionary
        foreach (var def in spriteDefs)
        {
            if (!loadedTextures.TryGetValue(def.SourceTextureName, out Texture2D sourceTexture)) {
                Debug.LogError($"Source texture '{def.SourceTextureName}' not loaded for sprite '{def.Name}'. Skipping.", this);
                continue;
            }
            if (loadedSprites.ContainsKey(def.Name)) {
                Debug.LogWarning($"Duplicate sprite name '{def.Name}'. Overwriting.", this);
            }

            try {
                // Scale Rect based on @reference
                float refSize = def.ReferenceSize > 0 ? def.ReferenceSize : sourceTexture.width;
                if (refSize <= 0) refSize = 512;
                float scaleX = sourceTexture.width / refSize;
                float scaleY = sourceTexture.height / refSize;
                float actualX = def.SourceRect.x * scaleX;
                float actualY = def.SourceRect.y * scaleY;
                float actualWidth = def.SourceRect.width * scaleX;
                float actualHeight = def.SourceRect.height * scaleY;

                // Create Unity Rect (Y is flipped from texture coords)
                Rect spriteRect = new Rect( actualX, sourceTexture.height - actualY - actualHeight, actualWidth, actualHeight );

                 // Validate scaled Rect coordinates (add small tolerance for float inaccuracy)
                 float tolerance = 0.01f;
                 if (spriteRect.x < -tolerance || spriteRect.y < -tolerance ||
                    spriteRect.xMax > sourceTexture.width + tolerance ||
                    spriteRect.yMax > sourceTexture.height + tolerance ||
                    spriteRect.width < 0 || spriteRect.height < 0)
                {
                     Debug.LogError($"Sprite '{def.Name}' calculated Rect {spriteRect} is outside texture '{def.SourceTextureName}' ({sourceTexture.width}x{sourceTexture.height}). Raw: {def.SourceRect}, Ref: {refSize}. Skipping.", this);
                     continue;
                }

                // Create Sprite object
                Sprite newSprite = Sprite.Create( sourceTexture, spriteRect, def.Pivot, 100.0f, 0, SpriteMeshType.FullRect );

                if (newSprite != null) {
                    newSprite.name = def.Name;
                    loadedSprites[def.Name] = newSprite; // Use [] to allow overwrite
                } else { Debug.LogError($"Sprite.Create returned null for sprite '{def.Name}'.", this); }

            } catch (Exception ex) { Debug.LogError($"Error creating sprite '{def.Name}': {ex.Message}", this); }
        }
    }

    // --- Animation Definition Storage ---
    private void StoreAnimations(List<ParsedAnimation> parsedAnims)
    {
        loadedAnimations.Clear();
        foreach (var pAnim in parsedAnims)
        {
            // Convert ParsedAnimation to AnimationDefinition (structure might be identical or require mapping)
            // Assuming structure is compatible for this example
            var animDef = new AnimationDefinition {
                 name = pAnim.Name,
                 type = pAnim.Type,
                 frameCount = pAnim.FrameCount,
                 duration = pAnim.Duration,
                 interpolation = pAnim.Interpolation,
                 autoKeyframe = pAnim.AutoKeyframe, // Store parsed auto type
                 autoDimension = pAnim.AutoDimension, // Store parsed auto dimension
                 keyframes = new List<KeyframeData>(), // Convert keyframes
                 referenceSize = pAnim.ReferenceSize
            };
            // Convert keyframes (ParsedKeyframe.Value is object, KeyframeData.value is object - direct copy ok here)
            foreach(var pKey in pAnim.Keyframes) {
                 animDef.keyframes.Add(new KeyframeData { time = pKey.Time, value = pKey.Value });
            }

            if (loadedAnimations.ContainsKey(animDef.name)) { Debug.LogWarning($"Duplicate animation name '{animDef.name}'. Overwriting."); }
            loadedAnimations[animDef.name] = animDef;
        }
    }


    // --- Public Accessors ---
    public Sprite GetSprite(string spriteName) {
        if (!isInitialized && !initializationAttempted) Initialize();
        if (loadedSprites.TryGetValue(spriteName, out Sprite sprite)) return sprite;
        if (isInitialized) Debug.LogWarning($"Sprite '{spriteName}' not found.");
        return null;
     }
    public AnimationDefinition GetAnimation(string animationName) {
        if (!isInitialized && !initializationAttempted) Initialize();
        if (loadedAnimations.TryGetValue(animationName, out var animDef)) return animDef;
        if (isInitialized) Debug.LogWarning($"Animation definition '{animationName}' not found.");
        return null;
     }
    public SpriteNodeDefinition GetSpriteNodeDefinition(string nodeName) {
        if (!isInitialized && !initializationAttempted) Initialize();
        if (loadedSpriteNodes.TryGetValue(nodeName.ToLower(), out var nodeDef)) return nodeDef;
        if (isInitialized) Debug.LogWarning($"Sprite Node Definition '{nodeName}' not found.");
        return null;
     }
    // Accessor for the raw parsed sprite data (useful for getting MaterialType)
    public ParsedSprite GetParsedSpriteDefinition(string spriteName) {
        if (!isInitialized && !initializationAttempted) Initialize();
        if (parsedSpriteDefinitions.TryGetValue(spriteName, out var parsedDef)) return parsedDef;
        if (isInitialized) Debug.LogWarning($"Parsed Sprite Definition '{spriteName}' not found.");
        return null; // No warning if not found
    }


    // --- Cleanup ---
    private void ClearDictionaries() {
        loadedSprites.Clear(); // Actual Sprite objects are managed by Unity/GC
        loadedAnimations.Clear();
        loadedSpriteNodes.Clear();
        parsedSpriteDefinitions.Clear();
    }
    public void ClearCache() {
        Debug.Log("Clearing SpriteAssetManager cache...");
        ClearDictionaries();
        // Destroy loaded textures to free memory
        foreach (var kvp in loadedTextures) if (kvp.Value != null) Destroy(kvp.Value);
        loadedTextures.Clear();
        isInitialized = false;
        initializationAttempted = false;
    }
    void OnDestroy() {
        ClearDictionaries();
        foreach (var kvp in loadedTextures) if (kvp.Value != null) Destroy(kvp.Value);
        loadedTextures.Clear();
        if (_instance == this) _instance = null;
    }
}
