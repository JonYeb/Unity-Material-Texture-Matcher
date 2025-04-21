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
        GUILayout.Label("Assign Albedo Textures to Materials", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox("This tool will match material names with texture file names and assign the textures to the albedo slot.", MessageType.Info);

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

        // Create a dictionary of texture paths by name for faster lookup
        Dictionary<string, string> texturePathsByName = new Dictionary<string, string>();
        Dictionary<string, List<string>> colorVariantTextures = new Dictionary<string, List<string>>();
        
        foreach (string textureGUID in textureGUIDs)
        {
            string texturePath = AssetDatabase.GUIDToAssetPath(textureGUID);
            string textureName = Path.GetFileNameWithoutExtension(texturePath);
            
            // Store exact matches
            texturePathsByName[textureName] = texturePath;
            
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

        int matchCount = 0;
        int exactMatchCount = 0;
        int colorVariantMatchCount = 0;
        int totalCount = materialGUIDs.Length;

        // Process each material
        foreach (string materialGUID in materialGUIDs)
        {
            string materialPath = AssetDatabase.GUIDToAssetPath(materialGUID);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            
            if (material == null)
                continue;

            string materialName = Path.GetFileNameWithoutExtension(materialPath);
            bool matchFound = false;
            
            // Try to find an exact matching texture
            if (texturePathsByName.TryGetValue(materialName, out string texturePath))
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                
                if (texture != null)
                {
                    if (!dryRun)
                    {
                        // Assign the texture to the albedo slot
                        material.SetTexture("_MainTex", texture);
                        EditorUtility.SetDirty(material);
                    }
                    
                    LogMessage($"Exact match found: Material '{materialName}' with texture '{texturePath}'");
                    matchCount++;
                    exactMatchCount++;
                    matchFound = true;
                }
            }
            
            // If no exact match was found, try finding a color variant
            if (!matchFound && colorVariantTextures.TryGetValue(materialName, out List<string> variantPaths))
            {
                if (variantPaths.Count > 0)
                {
                    // Use the first color variant found
                    string colorVariantPath = variantPaths[0];
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
                        matchCount++;
                        colorVariantMatchCount++;
                        matchFound = true;
                    }
                }
            }
            
            if (!matchFound)
            {
                LogMessage($"No matching texture found for material '{materialName}'");
            }
        }

        if (!dryRun)
        {
            AssetDatabase.SaveAssets();
        }

        string modeText = dryRun ? "Preview" : "Assigned";
        LogMessage($"Completed! {modeText} {matchCount} of {totalCount} materials with albedo textures.");
        LogMessage($"Breakdown: {exactMatchCount} exact matches, {colorVariantMatchCount} color variant matches.");
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
