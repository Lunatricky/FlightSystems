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
                sprites = BuildSprites(panel.SurfaceSize, cols, fontColor, backgroundColor);

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
            s.Alignment = TextAlignment.CENTER;
            return s;
        }

        private static MySprite MakeTextSprite(Color fontColor, string text, Vector2 size, Vector2 labelPos, float scale)
        {
            MySprite sprite = new MySprite();
            sprite.Type = SpriteType.TEXT;
            sprite.Data = text;
            sprite.Position = labelPos;
            sprite.Size = size;
            sprite.Color = fontColor;
            sprite.RotationOrScale = scale;
            sprite.FontId = "DEBUG";
            sprite.Alignment = TextAlignment.CENTER;
            return sprite;
        }

        // Build sprites for a list of InfoItem
        List<MySprite> BuildSprites(Vector2 panelSize, int cols, Color fontColor, Color backgroundColor)
        {            
            var sprites = new List<MySprite>();
            // background
            sprites.Add(MakeRectSprite(panelSize * 0.5f, panelSize, backgroundColor));

            int rows = texts.Count;

            float margin = 1f;
            float innerW = panelSize.X - margin * (cols + 1);
            float innerH = panelSize.Y - margin * (rows + 1);
            float boxW = innerW / cols;
            float boxH = innerH / rows;

            float incrmentY = 0f;
            if (panelSize.X == 512f) incrmentY = 70f;

            for (int i = 0; i < rows; i++)
            {
                int col = i % cols;
                int row = i / cols;
                float x = margin + col * (boxW + margin) + boxW * 0.5f;
                float y = margin + row * (boxH + margin) + boxH * 0.5f;
                var center = new Vector2(x, y + cols * incrmentY);
                var size = new Vector2(boxW, boxH);

                sprites.Add(MakeRectSprite(center, size, backgroundColor));

                var labelPos = new Vector2(x - boxW * 0.38f, y - boxH * 0.25f + cols * incrmentY);
                float scale = 1.9f - cols;

                Color color;

                if (colors[i] == null) color = fontColor;
                else color = ColorMap.GetColorFromString(colors[i]);

                sprites.Add(MakeTextSprite(color, texts[i], size, labelPos, scale));
            }

            return sprites;
        }
    }
}
