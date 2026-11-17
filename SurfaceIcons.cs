using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ThunderFeet
{
    /// <summary>
    /// The status-effect icon for each surface. The artist draws SIX icons, one per IconGroup; the text under the icon still names
    /// the exact surface. First match wins:
    ///   1. icons/&lt;IconGroup&gt;.png next to the DLL (Road, Stone, Wood, Earth, Grass, Frost)
    ///   2. a vanilla piece / item icon that fits the exact surface (hoe road, cultivator, wood, stone ...), until the art exists
    ///   3. a generated tinted disc, so the icon is never missing
    /// </summary>
    internal static class SurfaceIcons
    {
        private static readonly Dictionary<Surface, Sprite> s_cache = new Dictionary<Surface, Sprite>();
        private static readonly Dictionary<IconGroup, Sprite> s_groupPng = new Dictionary<IconGroup, Sprite>();

        internal static string IconDir => Path.Combine(Path.GetDirectoryName(Plugin.Instance.Info.Location) ?? "", "icons");

        internal static Sprite Get(Surface surface)
        {
            if (s_cache.TryGetValue(surface, out Sprite cached) && cached != null)
                return cached;

            SurfaceInfo info = Array.Find(SurfaceInfo.All, i => i.Surface == surface);
            if (info == null) return null;

            Sprite sprite = GroupPng(info.Icon);
            if (sprite == null) sprite = Vanilla(info.FallbackIcons);

            if (sprite == null)
            {
                // The game's prefab tables are not up yet: draw a disc now but try the vanilla icon again next time.
                if (ZNetScene.instance == null || ObjectDB.instance == null) return Disc(info.Tint);
                sprite = Disc(info.Tint);
            }

            s_cache[surface] = sprite;
            return sprite;
        }

        private static Sprite GroupPng(IconGroup group)
        {
            if (!s_groupPng.TryGetValue(group, out Sprite sprite))
            {
                sprite = LoadPng(Path.Combine(IconDir, group + ".png"));
                s_groupPng[group] = sprite;
            }
            return sprite;
        }

        private static Sprite LoadPng(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = Path.GetFileNameWithoutExtension(path) };
                if (!LoadImage(tex, File.ReadAllBytes(path))) return null;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[icons] could not load {path}: {e.Message}");
                return null;
            }
        }

        // Bound by exact signature (as MagicRunicMod ItemIcons / BarrkUI): the module also declares ReadOnlySpan overloads, and a direct
        // call makes the compiler consider them, which fails CS0518 on net48.
        private static readonly MethodInfo LoadImageMethod =
            typeof(ImageConversion).GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) })
            ?? typeof(ImageConversion).GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]) });

        private static bool LoadImage(Texture2D tex, byte[] bytes)
        {
            if (LoadImageMethod == null) return false;
            object[] args = LoadImageMethod.GetParameters().Length == 3 ? new object[] { tex, bytes, false } : new object[] { tex, bytes };
            return (bool)LoadImageMethod.Invoke(null, args);
        }

        private static Sprite Vanilla(string[] names)
        {
            if (names == null) return null;
            foreach (string name in names)
            {
                GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
                if (prefab == null && ObjectDB.instance != null) prefab = ObjectDB.instance.GetItemPrefab(name);
                if (prefab == null) continue;

                Piece piece = prefab.GetComponent<Piece>();
                if (piece != null && piece.m_icon != null) return piece.m_icon;

                ItemDrop item = prefab.GetComponent<ItemDrop>();
                if (item != null && item.m_itemData?.m_shared?.m_icons != null && item.m_itemData.m_shared.m_icons.Length > 0)
                    return item.m_itemData.GetIcon();
            }
            return null;
        }

        private static Sprite Disc(Color32 tint)
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "ThunderFeetDisc" };
            var px = new Color32[size * size];
            float r = size * 0.42f, c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                byte a = (byte)(Mathf.Clamp01(r - d + 0.5f) * 255f);
                bool rim = d > r - 3f;
                px[y * size + x] = rim ? new Color32(20, 20, 20, a) : new Color32(tint.r, tint.g, tint.b, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
