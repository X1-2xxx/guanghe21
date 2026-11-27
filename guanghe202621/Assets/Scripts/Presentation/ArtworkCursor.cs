using UnityEngine;

namespace GamePresentation
{
    /// <summary>使用美术指针；原始 Sprite 和场景中的美术引用保持可用。</summary>
    public sealed class ArtworkCursor : MonoBehaviour
    {
        static ArtworkCursor instance;
        GameCursorSettings settings;
        Texture2D cursor;
        Vector2 hotspot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (instance || !Resources.Load<GameCursorSettings>("UI/GameCursorSettings")) return;
            new GameObject("游戏鼠标指针").AddComponent<ArtworkCursor>();
        }

        void Awake()
        {
            if (instance && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            settings = Resources.Load<GameCursorSettings>("UI/GameCursorSettings");
            if (!settings || !settings.artwork) return;
            cursor = CreateTexture(settings.artwork, settings.pixelSize, settings.trimTransparentPadding);
            hotspot = new Vector2(Mathf.Clamp(settings.hotspot.x, 0, cursor.width - 1), Mathf.Clamp(settings.hotspot.y, 0, cursor.height - 1));
            Apply();
        }

        void OnEnable() { if (cursor) Apply(); }
        void OnApplicationFocus(bool focused) { if (focused && cursor) Apply(); }
        void Apply() { Cursor.SetCursor(cursor, hotspot, CursorMode.Auto); Cursor.visible = true; }
        void OnDisable() { if (instance == this) Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); }
        void OnDestroy()
        {
            if (instance != this) return;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            if (cursor) Destroy(cursor);
            instance = null;
        }

        internal static Texture2D CreateTexture(Texture2D source, int longestSide, bool trim)
        {
            var pixels = source.GetPixels32();
            int left = 0, bottom = 0, right = source.width - 1, top = source.height - 1;
            if (trim)
            {
                left = source.width; bottom = source.height; right = top = -1;
                for (int y = 0; y < source.height; y++)
                for (int x = 0; x < source.width; x++)
                {
                    if (pixels[y * source.width + x].a == 0) continue;
                    left = Mathf.Min(left, x); right = Mathf.Max(right, x);
                    bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
                }
                if (right < left) { left = bottom = 0; right = source.width - 1; top = source.height - 1; }
            }
            int width = right - left + 1, height = top - bottom + 1;
            float scale = Mathf.Clamp(longestSide, 16, 128) / (float)Mathf.Max(width, height);
            int outputWidth = Mathf.Max(1, Mathf.RoundToInt(width * scale)), outputHeight = Mathf.Max(1, Mathf.RoundToInt(height * scale));
            var texture = new Texture2D(outputWidth, outputHeight, TextureFormat.RGBA32, false) { name = "Art 鼠标（运行时指针）", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var output = new Color32[outputWidth * outputHeight];
            for (int y = 0; y < outputHeight; y++)
            for (int x = 0; x < outputWidth; x++)
            {
                float sx = Mathf.Clamp(left + (x + .5f) * width / outputWidth - .5f, left, right);
                float sy = Mathf.Clamp(bottom + (y + .5f) * height / outputHeight - .5f, bottom, top);
                int x0 = Mathf.FloorToInt(sx), y0 = Mathf.FloorToInt(sy), x1 = Mathf.Min(x0 + 1, right), y1 = Mathf.Min(y0 + 1, top);
                Color a = Color.Lerp(pixels[y0 * source.width + x0], pixels[y0 * source.width + x1], sx - x0);
                Color b = Color.Lerp(pixels[y1 * source.width + x0], pixels[y1 * source.width + x1], sx - x0);
                output[y * outputWidth + x] = Color.Lerp(a, b, sy - y0);
            }
            texture.SetPixels32(output);
            texture.Apply(false, false);
            return texture;
        }
    }
}
