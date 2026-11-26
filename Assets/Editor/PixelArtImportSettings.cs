using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 像素废土素材导入规范：Point 采样 / 无 mipmap / 无压缩 / FullRect / 九宫格边框。
/// 放在 Assets/Art/PixelWasteland/ 下的任何贴图都会自动套用，避免手动设错。
/// </summary>
public class PixelArtImportSettings : AssetPostprocessor
{
    public const string Root = "Assets/Art/PixelWasteland/";

    // 九宫格边框：Vector4(左, 下, 右, 上)，单位=贴图像素
    private static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4>
    {
        { "panel_large",  new Vector4(48f, 48f, 48f, 48f) },
        { "panel_inset",  new Vector4(40f, 40f, 40f, 40f) },
        { "socket_frame", new Vector4(48f, 48f, 48f, 48f) },
        { "extra_bg",     new Vector4(24f, 24f, 24f, 24f) },
        { "btn_normal",   new Vector4(32f, 12f, 32f, 12f) },
        { "btn_hover",    new Vector4(32f, 12f, 32f, 12f) },
        { "btn_press",    new Vector4(32f, 12f, 32f, 12f) },
        { "tab_idle",     new Vector4(24f, 12f, 24f, 12f) },
        { "tab_active",   new Vector4(24f, 12f, 24f, 12f) },
        { "scroll_track", new Vector4(5f, 5f, 5f, 5f) },
        { "scroll_thumb", new Vector4(5f, 5f, 5f, 5f) },
    };

    // 需要平铺的资源
    private static readonly HashSet<string> Repeat = new HashSet<string> { "scanline", "hazard_strip" };

    private void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        if (!path.StartsWith(Root)) return;

        TextureImporter ti = assetImporter as TextureImporter;
        if (ti == null) return;

        string name = System.IO.Path.GetFileNameWithoutExtension(path);

        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100f;
        ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.alphaIsTransparency = true;
        ti.sRGBTexture = true;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.maxTextureSize = 2048;
        ti.wrapMode = Repeat.Contains(name) ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

        TextureImporterSettings s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteMeshType = SpriteMeshType.FullRect;
        s.spriteExtrude = 0;
        s.spriteGenerateFallbackPhysicsShape = false;
        s.spriteAlignment = (int)SpriteAlignment.Center;
        ti.SetTextureSettings(s);

        Vector4 border;
        if (Borders.TryGetValue(name, out border)) ti.spriteBorder = border;
    }

    [MenuItem("Tools/PixelWasteland/1 - Reimport All Art")]
    private static void ReimportAll()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { Root.TrimEnd('/') });
        foreach (string g in guids)
        {
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(g), ImportAssetOptions.ForceUpdate);
        }
        AssetDatabase.Refresh();
        Debug.Log("[PixelWasteland] 已重新导入 " + guids.Length + " 张贴图");
    }
}
