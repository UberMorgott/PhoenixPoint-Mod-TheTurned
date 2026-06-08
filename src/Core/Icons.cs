using PhoenixPoint.Common.UI;
using System;
using System.IO;
using UnityEngine;

namespace TheTurned.Core
{
    /// <summary>
    /// Sprite loader mirroring the Officer mod's Helper.CreateSpriteFromImageFile. Resolves
    /// Assets\Textures from the mod's entry directory. Gracefully returns null (and leaves any cloned
    /// icon intact) when no file is supplied or present.
    /// </summary>
    internal static class Icons
    {
        private static string TexturesDirectory
        {
            get
            {
                string modDir = TheTurnedMain.Main?.Instance?.Entry?.Directory;
                return string.IsNullOrEmpty(modDir) ? null : Path.Combine(modDir, "Assets", "Textures");
            }
        }

        /// <summary>
        /// Load a sprite from <c>Assets\Textures\<paramref name="imageFileName"/></c>. Returns null if the
        /// name is empty, the directory is unknown, or the file is missing.
        /// </summary>
        internal static Sprite CreateSpriteFromImageFile(string imageFileName, int width = 128, int height = 128,
            TextureFormat textureFormat = TextureFormat.RGBA32, bool mipChain = true)
        {
            if (string.IsNullOrEmpty(imageFileName))
            {
                return null;
            }
            try
            {
                string dir = TexturesDirectory;
                if (string.IsNullOrEmpty(dir))
                {
                    return null;
                }
                string filePath = Path.Combine(dir, imageFileName);
                if (!File.Exists(filePath))
                {
                    TheTurnedMain.Main?.Logger?.LogInfo($"[TheTurned] Icon file not found (skipping): {filePath}");
                    return null;
                }
                byte[] data = File.ReadAllBytes(filePath);
                Texture2D texture = new Texture2D(width, height, textureFormat, mipChain);
                return ImageConversion.LoadImage(texture, data)
                    ? Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0f, 0f))
                    : null;
            }
            catch (Exception e)
            {
                TheTurnedMain.Main?.Logger?.LogInfo($"[TheTurned] Icon load failed: {e}");
                return null;
            }
        }

        /// <summary>
        /// Set both Small and Large icons on a ViewElementDef from a texture file, if it exists.
        /// No-op when the file is missing so the cloned (Sniper) icon is preserved.
        /// </summary>
        internal static void TrySetSpecIcon(ViewElementDef ved, string imageFileName)
        {
            if (ved == null || string.IsNullOrEmpty(imageFileName))
            {
                return;
            }
            Sprite sprite = CreateSpriteFromImageFile(imageFileName);
            if (sprite != null)
            {
                ved.SmallIcon = sprite;
                ved.LargeIcon = sprite;
            }
        }

        /// <summary>
        /// Set both Small and Large icons on an ability ViewElementDef from a texture file, if it exists.
        /// (TacticalAbilityViewElementDef derives from ViewElementDef.) No-op when the file is missing.
        /// </summary>
        internal static void TrySetAbilityIcon(ViewElementDef ved, string imageFileName)
        {
            TrySetSpecIcon(ved, imageFileName);
        }
    }
}
