//using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
//using UnityEditor;
//using UnityEngine.Rendering.Universal;
//using Unity.VisualScripting;
//using NUnit.Framework;

public class SODLoader
{
    //public string sodFilePath = "DynamicAssets/sod/fedbat2.SOD"; // Change to your actual file path
    public string textureFolderPath = "DynamicAssets/textures/"; // Folder where textures are stored

    private Dictionary<string, GameObject> nodeObjects = new Dictionary<string, GameObject>();
    private static Dictionary<string, Material> materials = new Dictionary<string, Material>();

    // private void Start()
    // {

    // }
    private class Pair<T, U>
    {
        public Pair() {
        }

        public Pair(T first, U second) {
            this.First = first;
            this.Second = second;
        }

        public T First { get; set; }
        public U Second { get; set; }
    };
    private struct SODmaterial {
        public Color baseColor;
        public Color diffuseColor;
        public Color specularColor;
        public float specularPower;
        public bool selfIllumination;

    }

    private GameObject configureNode(string nodeName, string parentName, Matrix4x4 localTransform)
    {
        GameObject nodeObject = new GameObject(nodeName);
        if (parentName != null && parentName.Length > 0 && nodeObjects.ContainsKey(parentName))
        {
            nodeObject.transform.parent = nodeObjects[parentName].transform;
        }
        else
        {
            nodeObject.transform.parent = null;
        }
        nodeObject.transform.localPosition = localTransform.GetColumn(3);
        nodeObject.transform.localRotation = localTransform.rotation.normalized;
        //Quaternion.LookRotation(localTransform.GetColumn(2), localTransform.GetColumn(1));
        nodeObject.transform.localScale = new Vector3(localTransform.GetColumn(0).magnitude, localTransform.GetColumn(1).magnitude, localTransform.GetColumn(2).magnitude);
        //Debug.LogFormat("scale: {0} {1} {2}", nodeObject.transform.localScale.x, nodeObject.transform.localScale.y, nodeObject.transform.localScale.z);
        nodeObjects.Add(nodeName, nodeObject);
        return nodeObject;
    }
    // {
    //     GameObject nodeObject = new GameObject(nodeName);
    //     nodeObject.transform.localPosition = localTransform.GetPosition();  
    //     nodeObject.transform.SetLocalPositionAndRotation(localTransform.GetPosition(),localTransform.rotation);

    //     if (!string.IsNullOrEmpty(parentName) && nodeObjects.ContainsKey(parentName))
    //     {
    //         nodeObject.transform.parent = nodeObjects[parentName].transform;
    //     }
    //     else if (go != null)
    //     {
    //         nodeObject.transform.parent = go.transform;
    //     }

    //     nodeObjects[nodeName] = nodeObject;
    //}
    public void LoadSOD(string filePath, GameObject go = null, Dictionary<string, string> legacyLightmaps = null)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError("SOD file not found: " + filePath);
            return;
        }
        
        // Supplied object will be root of the hierarchy
        if (go != null)
        {
            nodeObjects[go.name] = go;
        }

        using (BinaryReader reader = new BinaryReader(File.Open(filePath, FileMode.Open)))
        {
            // Read Header
            string header = new string(reader.ReadChars(10)); // "Storm3D_SW"
            float version = reader.ReadSingle();

            if (header.Trim() != "Storm3D_SW" || version < 1.73f || version > 1.91f)
            {
                Debug.LogFormat("Invalid SOD file version: {0} {1}", header, version);
                return;
            }
            Debug.LogFormat("SOD file version: {0} {1}", header, version);


            // Load Materials
            ushort materialCount = reader.ReadUInt16();
            Dictionary<string, SODmaterial> SODmaterials = new Dictionary<string, SODmaterial>();
            for (int i = 0; i < materialCount; i++)
            {
                string materialName = ReadIdentifier(reader);
                Debug.Log("Material Name: " + materialName);
                // if (MaterialManager.Instance.sharedMaterials.ContainsKey(materialName))
                // {
                //     Debug.LogWarning("Material already loaded: " + materialName);
                //     //continue;
                // }
                Color ambient = ReadColor(reader);
                Color diffuse = ReadColor(reader);
                Color specular = ReadColor(reader);
                float specularPower = reader.ReadSingle();
                byte lightingModel = reader.ReadByte();
                byte selfIllumination = 0;
                if (version > 1.801)
                {
                    selfIllumination = reader.ReadByte();
                }
                SODmaterial sm = new SODmaterial();
                sm.baseColor = ambient;
                sm.diffuseColor = diffuse;
                sm.specularColor = specular;
                sm.specularPower = specularPower;
                sm.selfIllumination = selfIllumination == 1;
                SODmaterials.Add(materialName, sm);
            }

            // Load Nodes
            ushort nodeCount = reader.ReadUInt16();
            Debug.LogFormat("Node Count: {0}", nodeCount);
            for (int n = 0; n < nodeCount; n++)
            {
                ushort nodeType = reader.ReadUInt16();
                string nodeName = ReadIdentifier(reader);
                string parentName = ReadIdentifier(reader);
                Matrix4x4 localTransform = ReadMatrix(reader);

                //Debug.LogFormat("{0} transofm: det:{1}, scale: {2}", nodeName, localTransform.determinant, localTransform.lossyScale);
                // print out the Matrix4x4
                // Debug.LogFormat("Matrix: {0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10} {11} {12} {13} {14} {15}",
                //     localTransform[0, 0], localTransform[0, 1], localTransform[0, 2], localTransform[0, 3],
                //     localTransform[1, 0], localTransform[1, 1], localTransform[1, 2], localTransform[1, 3],
                //     localTransform[2, 0], localTransform[2, 1], localTransform[2, 2], localTransform[2, 3],
                //     localTransform[3, 0], localTransform[3, 1], localTransform[3, 2], localTransform[3, 3]);

                // Skip root node if go was supplied
                if (go != null)
                {
                    if (nodeName == "root" || nodeName == "scene_root")
                    {
                        // Skip the root node
                        continue;
                    }
                }
                // Make a new game object for the node
                GameObject nodeObject = null; ;
                if (nodeType != 3)
                    nodeObject = configureNode(nodeName, (parentName == "root" || parentName == "scene_root") ? (go != null ? go.name : parentName) : parentName, localTransform);

                Debug.LogFormat("Node Name: {0}:{1}", nodeName, nodeType);
                if (nodeType == 1) // MESH
                {
                    string textureMaterial = ReadIdentifier(reader); // alpha, opaque, etc
                    ulong dummy0 = 0;
                    ushort dummy2 = 0;
                    ulong bump = 0;
                    string bumpTexPath = "";
                    string borgTexPath = "";
                    bool isLightmap = false;
                    if (version > 1.9101)
                    {
                        dummy0 = reader.ReadUInt32();
                        bump = reader.ReadUInt32();
                    }
                    string texture = ReadIdentifier(reader);
                    isLightmap = texture.EndsWith("b");

                    if (version == 1.91)
                    {
                        dummy2 = reader.ReadUInt16();
                    }
                    if (version > 1.9101)
                    {
                        dummy2 = reader.ReadUInt16();
                        dummy2 = reader.ReadUInt16();
                        if (bump == 2)
                        {
                            bumpTexPath = ReadIdentifier(reader);
                            dummy2 = reader.ReadUInt16();
                            dummy2 = reader.ReadUInt16();
                        }
                        borgTexPath = ReadIdentifier(reader);
                        dummy2 = reader.ReadUInt16();
                    }

                    ushort vertexCount = reader.ReadUInt16();
                    ushort texCoordCount = reader.ReadUInt16();
                    ushort groupCount = reader.ReadUInt16();
                    //Debug.LogFormat("Vertex Count: {0} TexCoord Count: {1} groups {2}", vertexCount, texCoordCount, groupCount);
                    Vector3[] vertices = new Vector3[vertexCount];
                    for (int v = 0; v < vertexCount; v++)
                        vertices[v] = ReadVector3(reader, true);
                    //Debug.LogFormat("Vertex: {0} {1} {2}", vertices[vertexCount-1][0], vertices[vertexCount-1][1], vertices[vertexCount-1][2]);
                    Vector2[] uvs = new Vector2[texCoordCount];
                    for (int t = 0; t < texCoordCount; t++)
                        uvs[t] = ReadVector2(reader);

                    List<lightingGroup> lightingGroups = new List<lightingGroup>();
                    List<int> triangles = new List<int>(); // v1 v2 v3
                    //List<int> texuvs = new List<int>(); // t1 t2 t3

                    int[] dummy6 = new int[6];
                    for (int g = 0; g < groupCount; g++)
                    {
                        ushort numFaces = reader.ReadUInt16();
                        string lightingMaterial = ReadIdentifier(reader);
                        Debug.LogFormat("Material: {0} {1}", textureMaterial, lightingMaterial);
                        if (SODmaterials.ContainsKey(lightingMaterial))
                        {
                            SODmaterial lm = SODmaterials[lightingMaterial];
                            if (lm.baseColor.maxColorComponent > 0.5) // emissive
                            {
                                // Lhis is a lightmap
                                // legacy lightmaps were a separate mesh. Check in odf whether to keep this or apply as emissive map.
                                bool _added = false;
                                // Go through all keys in the legacyLightmaps dictionary and check if regex matches nodeName
                                if (legacyLightmaps != null)
                                {
                                    foreach (KeyValuePair<string, string> kvp in legacyLightmaps)
                                    {
                                        if (Regex.IsMatch(nodeName, kvp.Key) || Regex.IsMatch(nodeName + $"_{g}", kvp.Key))
                                        {
                                            Debug.LogFormat("Legacy Emissive: {0} {1}", lightingMaterial, lm.baseColor);
                                            if (legacyLightmaps[kvp.Key].Length > 0)
                                                lightingMaterial = legacyLightmaps[kvp.Key];
                                            if (lightingMaterial == "lightmap")
                                            {
                                                // Our hardcoded lightmap material is called "lmap"
                                                lightingMaterial = "lmap";
                                            }
                                            lightingGroups.Add(new lightingGroup(numFaces, lightingMaterial, legacyEemissive: true));
                                            _added = true;
                                            break;
                                        }
                                    }
                                }
                                if (!_added)
                                {
                                    Debug.LogFormat("Skipping Legacy Emissive: {0} {1}", lightingMaterial, lm.baseColor);
                                    lightingGroups.Add(new lightingGroup(0, "lightmap_skip", legacyEemissive: true));
                                }
                            }
                            else // non-emissive
                            {
                                lightingGroups.Add(new lightingGroup(numFaces, lightingMaterial));
                            }
                        }
                        else // hopefully this lightingMaterial is defined elsewhere
                        {
                            Debug.LogWarning("Material not found in SOD, this is unusual: " + lightingMaterial);
                            lightingGroups.Add(new lightingGroup(numFaces, lightingMaterial));
                        }

                        // read the faces
                        for (int f = 0; f < numFaces; f++)
                        {
                            for (int t = 0; t < 6; t++)
                            {
                                dummy6[t] = reader.ReadUInt16();
                            }

                            // swap the order of the vertices to match Unity's left-handed coordinate system
                            triangles.Add(dummy6[0]);
                            lightingGroups[g].triangles.Add(dummy6[0]);
                            //texuvs.Add(dummy6[1]);
                            lightingGroups[g].uvs.Add(dummy6[1]);


                            triangles.Add(dummy6[2]);
                            lightingGroups[g].triangles.Add(dummy6[2]);
                            //texuvs.Add(dummy6[3]);
                            lightingGroups[g].uvs.Add(dummy6[3]);

                            triangles.Add(dummy6[4]);
                            lightingGroups[g].triangles.Add(dummy6[4]);
                            //texuvs.Add(dummy6[5]);
                            lightingGroups[g].uvs.Add(dummy6[5]);
                        }
                    }
                    ushort cull = (ushort)reader.ReadByte();
                    ushort dummy3 = reader.ReadUInt16();

                    // Temporary mesh to capture normals
                    Mesh mesh = new Mesh();
                    mesh.vertices = vertices;
                    mesh.triangles = triangles.ToArray();
                    mesh.RecalculateNormals();

                    // Expand vertices and uvs to match each other
                    // Each real vertex must have a unique uv
                    Dictionary<int, int> vertexMap = new Dictionary<int, int>();
                    Dictionary<int, int> uvMap = new Dictionary<int, int>();
                    Dictionary<Pair<int, int>, int> vertexUVMap = new Dictionary<Pair<int, int>, int>();
                    List<Vector3> newVertices = new List<Vector3>();
                    List<Vector2> newUVs = new List<Vector2>();
                    List<int> newTriangles = new List<int>();
                    List<Vector3> newNormals = new List<Vector3>();
                    int numVerticesCurrent = -1;
                    for (int g = 0; g < lightingGroups.Count; g++)
                    {
                        for (int t = 0; t < lightingGroups[g].triangles.Count; t++)
                        {
                            //int i = g*lightingGroups.Count + t;
                            int vertexIndex = lightingGroups[g].triangles[t];
                            int uvIndex = lightingGroups[g].uvs[t];
                            Pair<int, int> key = new Pair<int, int>(vertexIndex, uvIndex);
                            if (!vertexUVMap.ContainsKey(key))
                            {
                                numVerticesCurrent++;
                                vertexUVMap.Add(key, numVerticesCurrent);
                                newVertices.Add(vertices[vertexIndex]);
                                newNormals.Add(mesh.normals[vertexIndex]);
                                vertexMap[vertexIndex] = numVerticesCurrent;
                                newUVs.Add(uvs[uvIndex]);
                                uvMap[uvIndex] = numVerticesCurrent;
                            }
                            newTriangles.Add(vertexUVMap[key]);
                            lightingGroups[g].triangles[t] = vertexUVMap[key];
                            lightingGroups[g].uvs[t] = vertexUVMap[key];
                        }
                    }

                    // Prepare the mesh
                    mesh.Clear();
                    mesh.vertices = newVertices.ToArray();
                    mesh.uv = newUVs.ToArray();
                    mesh.normals = newNormals.ToArray();
                    mesh.subMeshCount = lightingGroups.Count;
                    int totalFaces = 0;
                    for (int g = 0; g < lightingGroups.Count; g++)
                    {
                        totalFaces += lightingGroups[g].numFaces;
                        if (lightingGroups[g].numFaces == 0)
                            continue;
                        mesh.SetTriangles(lightingGroups[g].triangles.ToArray(), g);
                    }
                    mesh.RecalculateBounds();

                    // Skip if no faces
                    if (totalFaces == 0)
                    {
                        Debug.LogWarning("No faces in mesh: " + nodeName);
                        continue;
                    }

                    // Add the mesh
                    MeshFilter meshFilter = nodeObject.AddComponent<MeshFilter>();
                    meshFilter.mesh = mesh;

                    MeshRenderer meshRenderer = nodeObject.AddComponent<MeshRenderer>();
                    Texture2D baseTexture = null;
                    if (texture != null)
                    {
                        baseTexture = LoadTexture(textureFolderPath, texture + ".tga");
                    }
                    Texture2D bumpTexture = null;
                    if (bumpTexPath != null)
                    {
                        if (version > 1.9101)
                        {
                            bumpTexture = LoadTexture(textureFolderPath, bumpTexPath + ".tga");
                        }
                        else
                        {
                            bumpTexture = LoadTexture(textureFolderPath, texture + "_bump.tga");
                        }
                    }
                    Texture2D emissionTexture = null;
                    emissionTexture = LoadTexture(textureFolderPath, texture + "_glow.tga");

                    // Multi-lighting group means multiple submeshes, each with its own material
                    if (lightingGroups.Count > 1)
                    {
                        meshRenderer.sharedMaterials = new Material[lightingGroups.Count];
                    }

                    // Apply the base texture to each submesh
                    System.Collections.Generic.List<Material> _materials = new System.Collections.Generic.List<Material>();
                    for (int g = 0; g < lightingGroups.Count; g++)
                    {
                        // get the material name, or default to stdhull

                        string materialName = lightingGroups[g].lightingMaterial.Length > 0 ? lightingGroups[g].lightingMaterial : "stdhull";
                        if (emissionTexture != null)
                        {
                            materialName = materialName + "_glow";
                        }
                        // Material mat = MaterialManager.Instance.GetOrCreateMaterial(
                        //     materialName,
                        //     textureMaterial == "alpha" || isLightmap || textureMaterial == "wormhole",
                        //     textureMaterial == "additive",
                        //     cull == 1,
                        //     emissive: lightingGroups[g].legacyEemissive || SODmaterials[materialName].selfIllumination || emissionTexture != null
                        //     );
                        if (!MaterialManager.Instance.sharedMaterials.ContainsKey(materialName))
                        {
                            Debug.LogErrorFormat("Material {0} not found in shared materials. Creating new material.", materialName);
                        }
                        else
                        {
                            _materials.Add(MaterialManager.Instance.sharedMaterials[materialName]);
                        }
                    }
                    meshRenderer.SetSharedMaterials(_materials);

                    // Add a different material for lighting groups 
                    for (int g = 0; g < lightingGroups.Count; g++)
                    {
                        // // Apply to submesh; this just does a mpb for each submesh
                        MaterialManager.Instance.ApplySODMaterial(
                            renderer: meshRenderer,
                            baseTex: baseTexture,
                            normalTex: bumpTexture,
                            emissionTex: emissionTexture,
                            useAnimationData: false,
                            effectAnimationData: new EffectAnimationDataArrayBased(),
                            materialIndex: g);
                        // MaterialManager.Instance.ApplyMaterial(
                        //     materialName: (lightingGroups[g].lightingMaterial == "lmap" || textureMaterial == "alpha") ? "stdhull_alpha" : "stdhull",
                        //     renderer: meshRenderer,
                        //     baseTex: lightingGroups[g].lightingMaterial == "lmap" ? null : baseTexture,
                        //     normalTex: bumpTexture,
                        //     emissionTex: lightingGroups[g].lightingMaterial == "lmap" ? baseTexture : emissionTexture,
                        //     useAnimationData: false,
                        //     effectAnimationData: new EffectAnimationDataArrayBased(),
                        //     isTransparent: textureMaterial == "alpha" || isLightmap || textureMaterial == "wormhole",
                        //     isAdditive: textureMaterial == "additive",
                        //     backfaceCulling: cull == 1,
                        //     materialIndex: g,
                        //     numberOfMaterials: lightingGroups.Count,
                        //     emissive: lightingGroups[g].legacyEemissive || SODmaterials[lightingGroups[g].lightingMaterial].selfIllumination || emissionTexture != null
                        // );
                    }
                }
                else if (nodeType == 12)
                {
                    string emitter = ReadIdentifier(reader);
                    //Debug.Log("Emitter: " + emitter);
                }
                else if (nodeType == 0)
                {
                    //string hardpoint = ReadIdentifier(reader);
                    //Debug.Log("Point: " + nodeName);
                }
                else if (nodeType == 3)
                {
                    // Node name might end with an underscore followed by one or two digits; we want to remove that
                    EffectPoolManager.Instance.SpawnEffect(Regex.Replace(nodeName, "_\\d+$", ""), localTransform.GetPosition(), localTransform.rotation.normalized, go);
                }


            }

            // Load Texture Offset Animations (Section 5)
            //LoadTextureAnimations(reader);
        }
    }

    private struct lightingGroup
    {
        public ushort numFaces;
        public string lightingMaterial;
        public List<int> triangles;
        public List<int> uvs;
        public bool legacyEemissive;
        public lightingGroup(ushort numFaces, string lightingMaterial, bool legacyEemissive = false)
        {
            this.numFaces = numFaces;
            this.lightingMaterial = lightingMaterial;
            this.triangles = new List<int>();
            this.uvs = new List<int>();
            this.legacyEemissive = legacyEemissive;
        }
    };


    private Texture2D LoadTexture(string textureFolderPath, string textureFile)
    {
        // Load texture
        string texturePath = textureFolderPath + textureFile;
        Texture2D texture = LoadTexture(texturePath);
        return texture;
    }




    // Modify shader based on texture material keyword
    // switch (materialName.ToLower())
    // {
    //     case "additive":
    //         mat.SetFloat("_Mode", 3);
    //         mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
    //         mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
    //         mat.SetInt("_ZWrite", 0);
    //         mat.DisableKeyword("_ALPHATEST_ON");
    //         mat.DisableKeyword("_ALPHABLEND_ON");
    //         mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
    //         mat.renderQueue = 3000;
    //         break;

    //     case "translucent":
    //         mat.SetFloat("_Mode", 2);
    //         mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
    //         mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
    //         mat.SetInt("_ZWrite", 0);
    //         mat.DisableKeyword("_ALPHATEST_ON");
    //         mat.EnableKeyword("_ALPHABLEND_ON");
    //         mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
    //         mat.renderQueue = 3000;
    //         break;

    //     case "wireframe":
    //         mat.shader = Shader.Find("Unlit/Color");
    //         break;
    // }

    //materials[materialName] = mat;
    //return null;


    private Texture2D LoadTexture(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogWarning("Texture file not found: " + path);
            return null;
        }

        if (path.EndsWith(".tga"))
        {
            Texture2D tex = new Texture2D(2, 2);
            TGALoader.LoadTGA(path, out tex);
            return tex;
        }
        else
        {
            byte[] fileData = File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(fileData);
            return tex;
        }
    }



    private void LoadTextureAnimations(BinaryReader reader)
    {
        ushort animCount = reader.ReadUInt16();
        for (int i = 0; i < animCount; i++)
        {
            byte type = reader.ReadByte();
            string nodeName = ReadIdentifier(reader);
            string animName = ReadIdentifier(reader);
            float playbackOffset = reader.ReadSingle();

            if (!nodeObjects.ContainsKey(nodeName))
            {
                Debug.LogWarning("Animation node not found: " + nodeName);
                continue;
            }

            GameObject nodeObject = nodeObjects[nodeName];
            MeshRenderer renderer = nodeObject.GetComponent<MeshRenderer>();
            if (renderer == null || renderer.material == null) continue;

            Material mat = renderer.material;
            //StartCoroutine(AnimateTextureOffset(mat, playbackOffset));
        }
    }

    // private System.Collections.IEnumerator AnimateTextureOffset(Material mat, float playbackOffset)
    // {
    //     float speed = 1f / playbackOffset;
    //     while (true)
    //     {
    //         float offset = Time.time * speed;
    //         mat.mainTextureOffset = new Vector2(offset % 1, 0);
    //         yield return null;
    //     }
    // }

    private string ReadIdentifier(BinaryReader reader) => new string(reader.ReadChars(reader.ReadUInt16())).TrimEnd('\0');
    private Color ReadColor(BinaryReader reader) => new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    private Vector3 ReadVector3(BinaryReader reader) => new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    private Vector3 ReadVector3(BinaryReader reader, bool toLeftHanded) => new Vector3(toLeftHanded ? -reader.ReadSingle() : reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    private Vector2 ReadVector2(BinaryReader reader) => new Vector2(reader.ReadSingle(), -reader.ReadSingle());
    private Matrix4x4 ReadMatrix(BinaryReader reader)
    {
        return new Matrix4x4(
            new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), 0),
            new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), 0),
            new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), 0),
            new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), 1)
        );
    }
    // => new Matrix4x4(ReadVector3(reader), ReadVector3(reader), ReadVector3(reader), ReadVector3(reader));

}
