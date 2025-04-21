using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class MaterialTextureAssigner : EditorWindow
{
    private string textureFolder = "Assets/Textures";
    private string materialFolder = "Assets/Materials";
    private bool includeSubfolders = true;
    private bool dryRun = false;
    private Vector2 scrollPosition;
    private List<string> logMessages = new List<string>();

    [MenuItem("Tools/Material Texture Assigner")]
    public static void ShowWindow()
    {
        GetWindow<MaterialTextureAssigner>("Material Texture Assigner");
    }

    private void OnGUI()
    {
        GUILayout.Label("Assign Textures to Materials", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox("This tool will match material names with texture file names and assign the textures to the appropriate slots (albedo and normal).", MessageType.Info);

        EditorGUILayout.Space();
        
        materialFolder = EditorGUILayout.TextField("Materials Folder", materialFolder);
        if (GUILayout.Button("Browse Materials Folder"))
        {
            string selectedPath = EditorUtility.OpenFolderPanel("Select Materials Folder", materialFolder, "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                materialFolder = GetRelativePath(selectedPath);
            }
        }

        textureFolder = EditorGUILayout.TextField("Textures Folder", textureFolder);
        if (GUILayout.Button("Browse Textures Folder"))
        {
            string selectedPath = EditorUtility.OpenFolderPanel("Select Textures Folder", textureFolder, "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                textureFolder = GetRelativePath(selectedPath);
            }
        }

        includeSubfolders = EditorGUILayout.Toggle("Include Subfolders", includeSubfolders);
        dryRun = EditorGUILayout.Toggle("Dry Run (Preview Only)", dryRun);

        EditorGUILayout.Space();

        if (GUILayout.Button("Assign Textures"))
        {
            AssignTextures();
        }

        EditorGUILayout.Space();

        if (logMessages.Count > 0)
        {
            EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));
            foreach (string message in logMessages)
            {
                EditorGUILayout.HelpBox(message, MessageType.Info);
            }
            EditorGUILayout.EndScrollView();
            
            if (GUILayout.Button("Clear Log"))
            {
                logMessages.Clear();
            }
        }
    }

    private void AssignTextures()
    {
        if (!AssetDatabase.IsValidFolder(materialFolder))
        {
            LogMessage($"Error: Materials folder '{materialFolder}' does not exist!");
            return;
        }

        if (!AssetDatabase.IsValidFolder(textureFolder))
        {
            LogMessage($"Error: Textures folder '{textureFolder}' does not exist!");
            return;
        }

        // Get all materials
        string[] materialGUIDs = AssetDatabase.FindAssets("t:Material", new[] { materialFolder });
        if (materialGUIDs.Length == 0)
        {
            LogMessage("No materials found in the specified folder.");
            return;
        }

        // Get all textures
        string[] textureGUIDs = AssetDatabase.FindAssets("t:Texture2D", includeSubfolders ? new[] { textureFolder } : new[] { textureFolder });
        if (textureGUIDs.Length == 0)
        {
            LogMessage("No textures found in the specified folder.");
            return;
        }

        // Create dictionaries for different texture types
        Dictionary<string, string> exactMatches = new Dictionary<string, string>();
        Dictionary<string, List<string>> colorVariantTextures = new Dictionary<string, List<string>>();
        Dictionary<string, List<string>> normalMapTextures = new Dictionary<string, List<string>>();
        
        foreach (string textureGUID in textureGUIDs)
        {
            string texturePath = AssetDatabase.GUIDToAssetPath(textureGUID);
            string textureName = Path.GetFileNameWithoutExtension(texturePath);
            
            // Store exact matches
            exactMatches[textureName] = texturePath;
            
            // Check if this is a normal map texture
            if (textureName.Contains("_normal"))
            {
                // Extract the base name (everything before "_normal")
                string baseName = Regex.Replace(textureName, "_normal.*$", "");
                
                if (!normalMapTextures.ContainsKey(baseName))
                {
                    normalMapTextures[baseName] = new List<string>();
                }
                
                normalMapTextures[baseName].Add(texturePath);
                continue;  // Skip further processing for normal maps
            }
            
            // Store color variant matches (materialName_color_XYZ)
            Match match = Regex.Match(textureName, @"^(.+)_color_.*$");
            if (match.Success)
            {
                string baseName = match.Groups[1].Value;
                
                if (!colorVariantTextures.ContainsKey(baseName))
                {
                    colorVariantTextures[baseName] = new List<string>();
                }
                
                colorVariantTextures[baseName].Add(texturePath);
            }
        }

        int albedoMatchCount = 0;
        int exactAlbedoMatchCount = 0;
        int colorVariantMatchCount = 0;
        int normalMapMatchCount = 0;
        int totalCount = materialGUIDs.Length;

        // Process each material
        foreach (string materialGUID in materialGUIDs)
        {
            string materialPath = AssetDatabase.GUIDToAssetPath(materialGUID);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            
            if (material == null)
                continue;

            string materialName = Path.GetFileNameWithoutExtension(materialPath);
            bool albedoMatchFound = false;
            bool normalMapMatchFound = false;
            
            // Try to find an exact matching albedo texture
            if (exactMatches.TryGetValue(materialName, out string albedoTexturePath))
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoTexturePath);
                
                if (texture != null)
                {
                    if (!dryRun)
                    {
                        // Assign the texture to the albedo slot
                        material.SetTexture("_MainTex", texture);
                        EditorUtility.SetDirty(material);
                    }
                    
                    LogMessage($"Exact albedo match found: Material '{materialName}' with texture '{albedoTexturePath}'");
                    albedoMatchCount++;
                    exactAlbedoMatchCount++;
                    albedoMatchFound = true;
                }
            }
            
            // If no exact albedo match was found, try finding a color variant
            if (!albedoMatchFound && colorVariantTextures.TryGetValue(materialName, out List<string> colorVariantPaths))
            {
                if (colorVariantPaths.Count > 0)
                {
                    // Use the first color variant found
                    string colorVariantPath = colorVariantPaths[0];
                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(colorVariantPath);
                    
                    if (texture != null)
                    {
                        if (!dryRun)
                        {
                            // Assign the texture to the albedo slot
                            material.SetTexture("_MainTex", texture);
                            EditorUtility.SetDirty(material);
                        }
                        
                        LogMessage($"Color variant match found: Material '{materialName}' with texture '{colorVariantPath}'");
                        albedoMatchCount++;
                        colorVariantMatchCount++;
                        albedoMatchFound = true;
                    }
                }
            }
            
            // Try to find a normal map texture
            if (normalMapTextures.TryGetValue(materialName, out List<string> normalMapPaths))
            {
                if (normalMapPaths.Count > 0)
                {
                    // Use the first normal map found
                    string normalMapPath = normalMapPaths[0];
                    Texture2D normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(normalMapPath);
                    
                    if (normalMap != null)
                    {
                        if (!dryRun)
                        {
                            // Set texture to be a normal map if it's not already
                            TextureImporter importer = AssetImporter.GetAtPath(normalMapPath) as TextureImporter;
                            if (importer != null && !importer.isReadable)
                            {
                                // Only modify import settings if necessary
                                if (importer.textureType != TextureImporterType.NormalMap)
                                {
                                    importer.textureType = TextureImporterType.NormalMap;
                                    importer.SaveAndReimport();
                                    // Reload the normal map after reimporting
                                    normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(normalMapPath);
                                }
                            }

                            // Assign the texture to the normal map slot
                            material.SetTexture("_BumpMap", normalMap);
                            
                            // Enable normal map if the shader supports it
                            if (material.HasProperty("_NormalMap"))
                            {
                                material.SetFloat("_NormalMap", 1.0f);
                            }
                            else if (material.HasProperty("_BumpScale"))
                            {
                                material.SetFloat("_BumpScale", 1.0f);
                            }

                            EditorUtility.SetDirty(material);
                        }
                        
                        LogMessage($"Normal map found: Material '{materialName}' with normal map '{normalMapPath}'");
                        normalMapMatchCount++;
                        normalMapMatchFound = true;
                    }
                }
            }
            
            if (!albedoMatchFound && !normalMapMatchFound)
            {
                LogMessage($"No matching textures found for material '{materialName}'");
            }
            else if (!albedoMatchFound)
            {
                LogMessage($"No albedo texture found for material '{materialName}'");
            }
            else if (!normalMapMatchFound)
            {
                LogMessage($"No normal map found for material '{materialName}'");
            }
        }

        if (!dryRun)
        {
            AssetDatabase.SaveAssets();
        }

        string modeText = dryRun ? "Preview" : "Assigned";
        LogMessage($"Completed! {modeText} {albedoMatchCount + normalMapMatchCount} textures for {totalCount} materials.");
        LogMessage($"Breakdown: {exactAlbedoMatchCount} exact albedo matches, {colorVariantMatchCount} color variant matches, {normalMapMatchCount} normal map matches.");
    }

    private string GetRelativePath(string absolutePath)
    {
        if (string.IsNullOrEmpty(absolutePath))
            return string.Empty;

        string projectPath = Application.dataPath;
        projectPath = projectPath.Substring(0, projectPath.Length - 6); // Remove "Assets"
        
        if (absolutePath.StartsWith(projectPath))
        {
            return absolutePath.Substring(projectPath.Length);
        }
        
        return absolutePath;
    }

    private void LogMessage(string message)
    {
        logMessages.Add(message);
        Debug.Log(message);
    }
}
