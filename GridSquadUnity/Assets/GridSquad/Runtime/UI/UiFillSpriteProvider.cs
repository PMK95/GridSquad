using UnityEngine;
using UnityEngine.UI;

namespace GridSquad
{
    internal static class UiFillSpriteProvider
    {
        private static Sprite runtimeFillSprite;

        public static Sprite GetSprite(Image preferredSource = null)
        {
            if (preferredSource != null && preferredSource.sprite != null)
                return preferredSource.sprite;
            if (runtimeFillSprite != null)
                return runtimeFillSprite;

            runtimeFillSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            if (runtimeFillSprite != null)
                return runtimeFillSprite;

            Texture2D texture = new(1, 1, TextureFormat.RGBA32, false)
            {
                name = "런타임 UI Fill 텍스처"
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            runtimeFillSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            runtimeFillSprite.name = "런타임 UI Fill 스프라이트";
            return runtimeFillSprite;
        }
    }
}
