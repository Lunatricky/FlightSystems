using IngameScript.Domain;
using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    partial class Sprites
   {
        IniContext ic;

        readonly List<string> texts = new List<string>();
        readonly List<Color> BackgroundColors = new List<Color>();
        readonly List<Color> FontColors = new List<Color>();
        
        public Sprites(IniContext ic)
        {
            this.ic = ic;
        }

        public void Add(string s)
        {
            texts.Add(s);
            BackgroundColors.Add(ic.SpriteBackgroundColor);
            FontColors.Add(ic.SpriteFontColor);
        }

        public void AddB(string s, Color b)
        {
            texts.Add(s);
            BackgroundColors.Add(b);
            FontColors.Add(ic.SpriteFontColor);
        }

        public void AddF(string s, Color f)
        {
            texts.Add(s);
            BackgroundColors.Add(ic.SpriteBackgroundColor);
            FontColors.Add(f);
        }

        public void Add(string s, Color b, Color f)
        {
            texts.Add(s);
            BackgroundColors.Add(b);
            FontColors.Add(f);
        }

        public void DrawInfoPanel(IMyTextSurface panel, int col)
        {
            List<MySprite> sprites = new List<MySprite>();

            if (texts != null && texts.Count > 0)
                sprites = BuildSprites(panel, col);

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
        List<MySprite> BuildSprites(IMyTextSurface panel, int col)
        {
            Vector2 surfaceSize = panel.SurfaceSize;
            Vector2 textureSize = panel.TextureSize;

            // background
            var sprites = new List<MySprite>();

            sprites.Add(MakeRectSprite(new Vector2(0, 0), 2 * textureSize, ic.SpriteMenuColor));

            int rows = texts.Count;
            int margin = 5;

            for (int i = 0; i < rows; i++)
            {
                //Centers
                var centerRec = new Vector2(
                    1.5f * margin / col, 
                    (textureSize.Y - surfaceSize.Y) / 2 + (surfaceSize.Y / rows) / 2 + i * surfaceSize.Y / rows
                    );

                var centerText = new Vector2(
                    3 * margin + (textureSize.X - surfaceSize.X) / 2 / col,
                    (textureSize.Y - surfaceSize.Y) / 2 + i * (surfaceSize.Y) / rows
                    );

                //Sizes
                var sizeRec = new Vector2(textureSize.X / col - 3 * margin, (surfaceSize.Y - margin) / rows - margin);
                var sizeText = new Vector2(surfaceSize.X / col - 3 * margin, (surfaceSize.Y) / rows);

                float scale;
                if (surfaceSize.X < 512) scale = 0.9f;
                else if (surfaceSize.Y != textureSize.Y) scale = 1.4f;
                else scale = 1.6f;

                if (6 / rows < 1) scale = scale * 6 / rows;

                if (ic.TransparentLCD && panel.Name.ToLower().Contains("transparent"))
                {
                    sprites.Add(MakeRectSprite(centerRec, sizeRec, Color.Black));
                    sprites.Add(MakeTextSprite(centerText, sizeText, FontColors[i], texts[i], scale));
                }
                else
                {
                    sprites.Add(MakeRectSprite(centerRec, sizeRec, BackgroundColors[i]));
                    sprites.Add(MakeTextSprite(centerText, sizeText, FontColors[i], texts[i], scale));
                }
            }

            return sprites;
        }
    }
}
