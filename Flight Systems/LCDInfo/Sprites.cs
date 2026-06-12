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
        IniContext ic;
        public List<TextSprite> TextList => textList;
        List<TextSprite> textList;

        public struct TextSprite {public string Text; public Color BackgroundColor; public Color FontColor;}

        public Sprites(IniContext ic)
        {
            this.ic = ic;
            textList = new List<TextSprite>();
        }

        public void Add(string text)
        {
            textList.Add(new TextSprite { Text = text, BackgroundColor = ColorMap.GetColorFromString(ic.BackgroundColor), FontColor = ColorMap.GetColorFromString(ic.FontColor) });
        }

        public void Add(string text, Color backgroundColor, Color fontColor)
        {
            textList.Add(new TextSprite { Text = text, BackgroundColor = backgroundColor, FontColor = fontColor });
        }

        // Usage example
        public void DrawInfoPanel(IMyTextSurface panel, int cols)
        {
            panel.ContentType = ContentType.SCRIPT;

            List<MySprite> sprites = new List<MySprite>();

            if (textList != null && textList.Count > 0)
                sprites = BuildSprites(textList, panel.SurfaceSize, cols);

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

        // Helper: create a MySprite text
        MySprite MakeTextSprite(string text, Vector2 pos, float scale, TextAlignment align, Color fontColor)
        {
            MySprite s = new MySprite();
            s.Type = SpriteType.TEXT;
            s.Data = text;
            s.Position = pos;
            s.Color = fontColor;
            s.RotationOrScale = scale;
            s.FontId = "DEBUG";
            s.Alignment = align;
            return s;
        }

        // Build sprites for a list of InfoItem
        List<MySprite> BuildSprites(List<TextSprite> items, Vector2 panelSize, int cols)
        {
            Color backgroundColor = ColorMap.GetColorFromString(ic.BackgroundColor);
            Color fontColor = ColorMap.GetColorFromString(ic.FontColor);
            
            var sprites = new List<MySprite>();
            if (items == null || items.Count == 0)
            {
                sprites.Add(MakeRectSprite(panelSize * 0.5f, panelSize, backgroundColor));
                sprites.Add(MakeTextSprite("No data", panelSize * 0.5f, 1.2f, TextAlignment.CENTER, fontColor));
                return sprites;
            }

            // background
            sprites.Add(MakeRectSprite(panelSize * 0.5f, panelSize, backgroundColor));

            int n = items.Count;
            if (cols <= 0)
            {
                if (n <= 3) cols = n;
                else if (n <= 6) cols = 3;
                else cols = 4;
            }
            if (cols > n) cols = n;
            int rows = (int)Math.Ceiling((double)n / cols);

            float margin = 1f;
            float innerW = panelSize.X - margin * (cols + 1);
            float innerH = panelSize.Y - margin * (rows + 1);
            float boxW = Math.Max(2f, innerW / cols);
            float boxH = Math.Max(2f, innerH / rows);

            float incrmentY = 0f; 
            if (panelSize.X == 512f) incrmentY = 70f;

            int i = 0;
            foreach (var item in items)
            {
                int col = i % cols;
                int row = i / cols;
                float x = margin + col * (boxW + margin) + boxW * 0.5f - 25f;
                float y = margin + row * (boxH + margin) + boxH * 0.5f;
                var center = new Vector2(x, y + cols * incrmentY);
                var size = new Vector2(boxW, boxH);

                if (item.BackgroundColor != null) backgroundColor = item.BackgroundColor;
                if (item.FontColor != null) fontColor = item.FontColor;

                sprites.Add(MakeRectSprite(center, size, backgroundColor));

                var labelPos = new Vector2(x - boxW * 0.38f, y - boxH * 0.25f + cols * incrmentY);
                float scale = 1.9f - cols;
                sprites.Add(MakeTextSprite(item.Text, labelPos, scale, TextAlignment.LEFT, fontColor));

                i++;
            }

            return sprites;
        }
    }
}
