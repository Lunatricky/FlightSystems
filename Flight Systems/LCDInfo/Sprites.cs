using IngameScript.Domain;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    partial class Sprites
   {
        readonly List<string> texts = new List<string>();
        readonly List<string> colors = new List<string>();

        public void Add(string s, string c = null)
        {
            texts.Add(s);
            colors.Add(c);
        }

        // Usage example
        public void DrawInfoPanel(bool IsLG, IMyTextSurface panel, Color fontColor, Color backgroundColor)
        {
            List<MySprite> sprites = new List<MySprite>();

            if (texts != null && texts.Count > 0)
                sprites = BuildSprites(IsLG, panel, fontColor, backgroundColor);

            using (var frame = panel.DrawFrame())
            {
                for (int i = 0; i < sprites.Count; i++) frame.Add(sprites[i]);
            }
        }

        // Helper: create a MySprite rectangle
        MySprite MakeRectSprite(Vector2 center, Vector2 size, Color backgroundColor)
        {
            var s = new MySprite
            {
                Type = SpriteType.TEXTURE,
                Data = "SquareSimple",
                Position = center,
                Size = size,
                Color = backgroundColor,
                Alignment = TextAlignment.LEFT
            };
            return s;
        }

        private static MySprite MakeTextSprite(Vector2 center, Vector2 size, Color fontColor, string text, float scale)
        {
            MySprite s = new MySprite
            {
                Type = SpriteType.TEXT,
                Data = text,
                Position = center,
                Size = size,
                Color = fontColor,
                RotationOrScale = scale,
                FontId = "DEBUG",
                Alignment = TextAlignment.LEFT
            };
            return s;
        }

        // Build sprites for a list of InfoItem
        List<MySprite> BuildSprites(bool IsLG, IMyTextSurface panel, Color fontColor, Color backgroundColor)
        {
            Vector2 surfaceSize = panel.SurfaceSize;
            Vector2 textureSize = panel.TextureSize;

            // background
            var sprites = new List<MySprite>();

            sprites.Add(MakeRectSprite(new Vector2(0, 0), 2 * textureSize, fontColor));

            int rows = texts.Count;
            int margin = 5;

            for (int i = 0; i < rows; i++)
            {
                //var centerRec = new Vector2(margin, 2.5f * margin + i * surfaceSize.Y/ rows + (surfaceSize.Y - textureSize.Y) / rows / 2);
                var centerRec = new Vector2(1.5f * margin, (textureSize.Y - surfaceSize.Y) / 2 + (surfaceSize.Y / rows) / 2 + i * surfaceSize.Y / rows);
                var centerText = new Vector2(2 * margin + (textureSize.X - surfaceSize.X) / 2, 3f * margin + (textureSize.Y - surfaceSize.Y - margin) / 2 + i * (surfaceSize.Y - margin) / rows);
                var sizeRec = new Vector2(textureSize.X - 3 * margin, (surfaceSize.Y - margin) / rows - margin);
                var sizeText = new Vector2(surfaceSize.X - 3 * margin, (surfaceSize.Y) / rows);

                float scale;
                if (surfaceSize.X < 512) scale = 0.9f;
                else if (surfaceSize.Y != textureSize.Y) scale = 1.4f;
                else scale = 1.6f;

                if (colors[i] != null)
                {
                    sprites.Add(MakeRectSprite(centerRec, sizeRec, ColorMap.GetColorFromString(colors[i])));
                    sprites.Add(MakeTextSprite(centerText, sizeText, Color.Black, texts[i], scale));
                } else
                {
                    sprites.Add(MakeRectSprite(centerRec, sizeRec, backgroundColor));
                    sprites.Add(MakeTextSprite(centerText, sizeText, fontColor, texts[i], scale));
                }
            }

            return sprites;
        }
    }
}
