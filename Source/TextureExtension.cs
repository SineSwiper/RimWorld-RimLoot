using UnityEngine;

namespace RimLoot {
    // Unity is frustratingly bereft of any decent utilities for Texture, so I guess we'll just create
    // them ourselves...

    public static class TextureExtension {
        public static Texture2D Clone (this Texture2D source) {
            Texture2D dest = new Texture2D(source.width, source.height);
            Graphics.CopyTexture(source, dest);
            return dest;
        }

        public static Texture2D CloneAsReadable (this Texture2D source) {
            RenderTexture renderTex = RenderTexture.GetTemporary(
                source.width, source.height, 0,
                RenderTextureFormat.Default, RenderTextureReadWrite.Linear
            );

            Graphics.Blit(source, renderTex);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTex;

            Texture2D readableTex = new Texture2D(source.width, source.height);
            readableTex.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
            readableTex.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTex);

            return readableTex;
        }

        public static void Colorize (this Texture2D tex, Color color) {
            Color[] texColors = tex.GetPixels();

            for (int y = 0; y < tex.height; y++) {
                int yw = y * tex.width;

                for (int x = 0; x < tex.width; x++) {
                    texColors[yw + x] *= color;
                }
            }

            tex.SetPixels(texColors);
        }

        // A heavily stripped down version of Eric Haines' ThreadedScale
        public static void BilinearScale (this Texture2D tex, int newWidth, int newHeight) {
            Color[] texColors = tex.GetPixels();
            Color[] newColors = new Color[newWidth * newHeight];
            float ratioX = 1.0f / ((float)newWidth  / (tex.width-1));
            float ratioY = 1.0f / ((float)newHeight / (tex.height-1));

            int w  = tex.width;
            int w2 = newWidth;
 
            for (var y = 0; y < newHeight; y++) {
                int yFloor = (int)Mathf.Floor(y * ratioY);
                int y1 = yFloor * w;
                int y2 = (yFloor+1) * w;
                int yw = y * w2;
 
                for (var x = 0; x < w2; x++) {
                    int   xFloor = (int)Mathf.Floor(x * ratioX);
                    float xLerp  = x * ratioX - xFloor;
                    newColors[yw + x] = Color.LerpUnclamped(
                        Color.LerpUnclamped(texColors[y1 + xFloor], texColors[y1 + xFloor+1], xLerp),
                        Color.LerpUnclamped(texColors[y2 + xFloor], texColors[y2 + xFloor+1], xLerp),
                        y * ratioY - yFloor
                    );
                }
            }

            tex.Resize(newWidth, newHeight);
            tex.SetPixels(newColors);
        }

        public static void AddOverlayToBLCorner (this Texture2D uiIcon, Texture2D overlayTex) {
            for (int y = 0; y < uiIcon.height; y++) {
                for (int x = 0; x < uiIcon.width; x++) {
                    if (x < overlayTex.width && y < overlayTex.height) {
                        Color iconPixel    = uiIcon    .GetPixel(x, y);
                        Color overlayPixel = overlayTex.GetPixel(x, y);
                        Color newPixel     = Color.Lerp(iconPixel, overlayPixel, overlayPixel.a);
                        uiIcon.SetPixel(x, y, newPixel);
                    }
                }
            }
        }

    }
}
