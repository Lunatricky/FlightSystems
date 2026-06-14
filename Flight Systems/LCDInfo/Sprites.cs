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
        public void DrawInfoPanel(IMyTextSurface panel, int cols, Color fontColor, Color backgroundColor)
        {
            panel.ContentType = ContentType.SCRIPT;

            List<MySprite> sprites = new List<MySprite>();

            if (texts != null && texts.Count > 0)
                sprites = BuildSprites(panel, cols, fontColor, backgroundColor);

            using (var frame = panel.DrawFrame())
            {
                for (int i = 0; i < sprites.Count; i++) frame.Add(sprites[i]);
            }
        }

        // Helper: create a MySprite rectangle
        MySprite MakeRectSprite(Vector2 center, Vector2 size, Color backgroundColor)
        {
            var s = new MySprite();
            s.Type = SpriteType.TEXTURE;
            s.Data = "SquareSimple";
            s.Position = center;
            s.Size = size;
            s.Color = backgroundColor;
            s.Alignment = TextAlignment.LEFT;
            return s;
        }

        private static MySprite MakeTextSprite(Color fontColor, string text, Vector2 size, Vector2 labelPos, float scale)
        {
            MySprite s = new MySprite();
            s.Type = SpriteType.TEXT;
            s.Data = text;
            s.Position = labelPos;
            s.Size = size;
            s.Color = fontColor;
            s.RotationOrScale = scale;
            s.FontId = "DEBUG";
            s.Alignment = TextAlignment.LEFT;
            return s;
        }

        // Build sprites for a list of InfoItem
        List<MySprite> BuildSprites(IMyTextSurface panel, int cols, Color fontColor, Color backgroundColor)
        {
            Vector2 surfaceSize = panel.SurfaceSize;
            Vector2 textureSize = panel.TextureSize;

            var sprites = new List<MySprite>();
            // background
            sprites.Add(MakeRectSprite(surfaceSize * 0.5f, surfaceSize, backgroundColor));

            int rows = texts.Count;

            float margin = 1f;
            float innerW = 20 + surfaceSize.X - margin * (cols + 1);
            float innerH = surfaceSize.Y - margin * (rows + 1);
            float boxW = innerW / cols;
            float boxH = innerH / rows;


            for (int i = 0; i < rows; i++)
            {
                int col = i % cols;
                int row = i / cols;
                float x = col * (boxW + margin) + boxW * 0.5f;
                float y = row * (boxH + margin) + boxH * 0.5f;

                float posX = x / surfaceSize.X * textureSize.X;
                float posY = y / surfaceSize.Y * textureSize.Y;


                var center = new Vector2(posX, posY);
                var size = new Vector2(boxW, boxH);

                sprites.Add(MakeRectSprite(center, size, backgroundColor));

                var labelPos = new Vector2(posX - boxW * 0.38f, posY - boxH * 0.25f + cols);
                float scale;

                if (surfaceSize.X > 512) scale = 1.5f;
                else scale = 1.4f * surfaceSize.Y / 512f;

                Color color;

                if (colors[i] == null) color = fontColor;
                else color = ColorMap.GetColorFromString(colors[i]);

                sprites.Add(MakeTextSprite(color, texts[i], size, labelPos, scale));
            }

            return sprites;
        }
    }
}
