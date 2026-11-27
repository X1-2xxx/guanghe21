using UnityEditor;
using UnityEngine;

/// <summary>Restores baked-white background holes on two supplied sheets without changing their source art or sprite IDs.</summary>
internal sealed class ArtworkAlphaRepairPostprocessor : AssetPostprocessor
{
    internal const string PitPath = "Assets/Tiles/郊外/04.png";
    internal const string DoorPath = "Assets/Tiles/旅馆/门与招牌/旅馆门.png";
    private bool IsTarget => assetPath == PitPath || assetPath == DoorPath;
    public override uint GetVersion() => 1;

    private void OnPreprocessTexture()
    {
        if (!IsTarget) return;
        var importer = (TextureImporter)assetImporter;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        var settings = importer.GetDefaultPlatformTextureSettings();
        settings.format = TextureImporterFormat.RGBA32;
        settings.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SetPlatformTextureSettings(settings);
        var standalone = importer.GetPlatformTextureSettings("Standalone");
        if (standalone.overridden)
        {
            standalone.format = TextureImporterFormat.RGBA32;
            standalone.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(standalone);
        }
    }

    private void OnPostprocessTexture(Texture2D texture)
    {
        if (!IsTarget) return;
        bool pit = assetPath == PitPath;
        int width = texture.width, height = texture.height;
        if ((pit && (width != 480 || height != 480)) || (!pit && (width != 1035 || height != 1757)))
        {
            Debug.LogWarning("透明修复区域需按新版素材尺寸重新核对：" + assetPath);
            return;
        }
        var pixels = texture.GetPixels32();
        int count = 0;
        // Coordinates are measured from the top-left of the artist's original PNG.
        int xMin = pit ? 0 : 650, xMax = pit ? width : 688;
        int yMin = pit ? 0 : 700, yMax = pit ? 32 : 1221;
        for (int topY = yMin; topY < yMax; topY++)
        for (int x = xMin; x < xMax; x++)
        {
            int index = (height - 1 - topY) * width + x;
            var pixel = pixels[index];
            // These precise background regions contain no painted white highlights.
            if (pixel.a == 0 || pixel.r < 235 || pixel.g < 235 || pixel.b < 235) continue;
            // Dark transparent RGB also avoids a white fringe with bilinear sampling.
            pixels[index] = new Color32(66, 11, 5, 0);
            count++;
        }
        if (count == 0) return;
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
    }
}
