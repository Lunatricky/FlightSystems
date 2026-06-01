using IngameScript.Domain;
using IngameScript.Physics;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    partial class Sprites
   {
        GridContext gc;
        PhysicsContext pc;
        IniContext ic;

        List<string> items;
        public List<string> Items => items;

        public class InfoItem
        {
            public string Text;
            public string Value;
        }

        public Sprites(GridContext gc, PhysicsContext pc, IniContext ic)
        {
            this.gc = gc;
            this.pc = pc;
            this.ic = ic;
            items = new List<string>();
        }


        // Usage example
        public void DrawInfoPanel(IMyTextSurface panel, int cols)
        {

            panel.ContentType = ContentType.SCRIPT;
            var sprites = BuildInfoSprites(items, panel.SurfaceSize, cols);

            using (var frame = panel.DrawFrame())
            {
                for (int i = 0; i < sprites.Count; i++) frame.Add(sprites[i]);
            }
        }

        // Helper: create a MySprite rectangle
        MySprite MakeRectSprite(Vector2 center, Vector2 size)
        {
            var s = new MySprite();
            s.Type = SpriteType.TEXTURE;
            s.Data = "SquareSimple";
            s.Position = center;
            s.Size = size;
            s.Color = ColorMap.GetColorFromString(ic.BackgroundColor);
            s.Alignment = TextAlignment.CENTER;
            return s;
        }

        // Helper: create a MySprite text
        MySprite MakeTextSprite(string text, Vector2 pos, float scale, TextAlignment align)
        {
            var s = new MySprite();
            s.Type = SpriteType.TEXT;
            s.Data = text;
            s.Position = pos;
            s.Color = ColorMap.GetColorFromString(ic.BackgroundColor);
            s.RotationOrScale = scale;
            s.FontId = "DEBUG";
            s.Alignment = align;
            return s;
        }

        // Build sprites for a list of InfoItem
        List<MySprite> BuildInfoSprites(List<string> items, Vector2 panelSize, int cols)
        {
            var sprites = new List<MySprite>();
            if (items == null || items.Count == 0)
            {
                sprites.Add(MakeRectSprite(panelSize * 0.5f, panelSize));
                sprites.Add(MakeTextSprite("No data", panelSize * 0.5f, 1.2f, TextAlignment.CENTER));
                return sprites;
            }

            // background
            sprites.Add(MakeRectSprite(panelSize * 0.5f, panelSize));

            int n = items.Count;
            if (cols <= 0)
            {
                if (n <= 3) cols = n;
                else if (n <= 6) cols = 3;
                else cols = 4;
            }
            if (cols > n) cols = n;
            int rows = (int)Math.Ceiling((double)n / cols);

            float margin = 8f;
            float innerW = panelSize.X - margin * (cols + 1);
            float innerH = panelSize.Y - margin * (rows + 1);
            float boxW = Math.Max(20f, innerW / cols);
            float boxH = Math.Max(20f, innerH / rows);

            for (int i = 0; i < n; i++)
            {
                int col = i % cols;
                int row = i / cols;
                float x = margin + col * (boxW + margin) + boxW * 0.5f;
                float y = margin + row * (boxH + margin) + boxH * 0.5f;
                var center = new Vector2(x, y);
                var size = new Vector2(boxW, boxH);

                sprites.Add(MakeRectSprite(center, size));

                var labelPos = new Vector2(x - boxW * 0.38f + 6f, y - boxH * 0.25f);
                sprites.Add(MakeTextSprite(items[i], labelPos, 0.7f, TextAlignment.LEFT));
            }

            return sprites;
        }
    }
}
