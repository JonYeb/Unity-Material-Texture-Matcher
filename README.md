# Unity Material Texture Matcher
This script creates a convenient editor window in Unity that will match your materials with their corresponding textures based on matching names. Here's how to use it:
## Usage and installation
1. Copy this script into your Unity project in an "Editor" folder
2. Access the tool via the Unity menu: Tools > Material Texture Assigner
3. Set the paths to your materials and textures folders
4. Choose whether to include subfolders for texture search
5. Optionally enable "Dry Run" to preview changes without applying them
6. Click "Assign Textures" to run the process

### The script will:

- Find all materials in your specified materials folder
- Search for textures with matching filenames in the textures folder
- Assign matching textures to the albedo slot (_MainTex) of each material
- Provide a detailed log of what was matched and what wasn't
