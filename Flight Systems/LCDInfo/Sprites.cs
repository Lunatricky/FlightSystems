using IngameScript.Domain;
using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;
using System.Text;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    class Sprites
    {
        readonly IniContext ic;
        readonly StringBuilder measure = new StringBuilder();

        readonly List<string> texts = new List<string>();
        readonly List<Color> BackgroundColors = new List<Color>();
        readonly List<Color> FontColors = new List<Color>();
        readonly List<string> texts1 = new List<string>();
        readonly List<Color> background1 = new List<Color>();
        readonly List<Color> font1 = new List<Color>();
        readonly List<string> holdText = new List<string>();
        readonly List<Color> holdBg = new List<Color>();
        readonly List<Color> holdFg = new List<Color>();
        readonly List<MySprite> scratch = new List<MySprite>(32);
        int activeStack;

        public Sprites(IniContext ic)
        {
            this.ic = ic;
        }

        public void ClearStacks()
        {
            texts.Clear();
            BackgroundColors.Clear();
            FontColors.Clear();
            texts1.Clear();
            background1.Clear();
            font1.Clear();
            activeStack = 0;
        }

        public void UseStack(int stack)
        {
            activeStack = stack <= 0 ? 0 : 1;
        }

        public void Add(string s)
        {
            AddLine(s, ic.SpriteBackgroundColor, ic.SpriteFontColor);
        }

        public void AddB(string s, Color b)
        {
            AddLine(s, b, ic.SpriteFontColor);
        }

        public void AddF(string s, Color f)
        {
            AddLine(s, ic.SpriteBackgroundColor, f);
        }

        public void Add(string s, Color b, Color f)
        {
            AddLine(s, b, f);
        }

        void AddLine(string s, Color b, Color f)
        {
            if (activeStack <= 0)
            {
                texts.Add(s);
                BackgroundColors.Add(b);
                FontColors.Add(f);
            }
            else
            {
                texts1.Add(s);
                background1.Add(b);
                font1.Add(f);
            }
        }

        public void DrawTo(List<IMyTextSurface> surfaces, int col = 1)
        {
            if (surfaces == null)
                return;

            for (int i = 0; i < surfaces.Count; i++)
            {
                IMyTextSurface panel = surfaces[i];
                if (Lcd1Display.TryDrawAsThirdColumn(this, panel))
                    continue;
                DrawInfoPanel(panel, col);
            }
        }

        public void DrawInfoPanel(IMyTextSurface panel, int col)
        {
            List<MySprite> sprites = new List<MySprite>();

            if (texts != null && texts.Count > 0)
                sprites = BuildSprites(panel, col);

            using (var frame = panel.DrawFrame())
            {
                for (int i = 0; i < sprites.Count; i++)
                    frame.Add(sprites[i]);
            }
        }

        MySprite MakeRectSprite(Vector2 center, Vector2 size, Color backgroundColor)
        {
            return new MySprite
            {
                Type = SpriteType.TEXTURE,
                Data = "SquareSimple",
                Position = center,
                Size = size,
                Color = backgroundColor,
                Alignment = TextAlignment.LEFT
            };
        }

        static MySprite MakeTextSprite(Vector2 position, Color fontColor, string text, float scale)
        {
            return new MySprite
            {
                Type = SpriteType.TEXT,
                Data = text,
                Position = position,
                Color = fontColor,
                RotationOrScale = scale,
                FontId = "DEBUG",
                Alignment = TextAlignment.LEFT
            };
        }

        List<MySprite> BuildSprites(IMyTextSurface panel, int col)
        {
            if (col < 1)
                col = 1;

            Vector2 surfaceSize = panel.SurfaceSize;
            Vector2 textureSize = panel.TextureSize;
            var sprites = new List<MySprite>();

            string panelName = panel.Name ?? "";
            bool transparent = ic.TransparentLCD && panelName.ToLower().Contains("transparent");
            if (!transparent)
                sprites.Add(MakeRectSprite(new Vector2(0, 0), 2 * textureSize, ic.SpriteMenuColor));

            int rows = texts.Count;
            float minDim = surfaceSize.X < surfaceSize.Y ? surfaceSize.X : surfaceSize.Y;
            float pad = Clamp(minDim * 0.01f, 2f, 6f);

            float originX = (textureSize.X - surfaceSize.X) * 0.5f;
            float originY = (textureSize.Y - surfaceSize.Y) * 0.5f;
            float colW = surfaceSize.X / col;
            float rowPitch = surfaceSize.Y / rows;
            float barH = rowPitch - pad;
            if (barH < 4f)
                barH = rowPitch * 0.85f;

            float barX = originX + pad;
            float barW = colW - 2f * pad;
            float textPad = pad * 1.5f;
            float textMaxW = barW - 2f * textPad;
            float textMaxH = barH * 0.7f;

            float scale = textMaxH / DebugLineHeight;
            if (scale > 1.3f)
                scale = 1.3f;
            if (scale < 0.35f)
                scale = 0.35f;

            for (int i = 0; i < rows; i++)
            {
                float centerY = originY + i * rowPitch + rowPitch * 0.5f;
                var barPos = new Vector2(barX, centerY);
                var barSize = new Vector2(barW, barH);

                sprites.Add(MakeRectSprite(barPos, barSize, BackgroundColors[i]));

                string line = texts[i] ?? "";
                float scaleLine = FitWidth(panel, line, scale, textMaxW);
                float lineH = scaleLine * DebugLineHeight;
                float barTop = centerY - barH * 0.5f;
                float extra = barH - lineH;
                if (extra < 0f)
                    extra = 0f;
                float textX = barX + textPad;
                // TEXT LEFT = top-left. Bias toward the top so small cockpit / emotion screens don't sit on the bar bottom.
                float textY = barTop + extra * 0.22f;

                sprites.Add(MakeTextSprite(new Vector2(textX, textY), FontColors[i], line, scaleLine));
            }

            return sprites;
        }

        const float DebugLineHeight = 28.8f;
        const float DebugCharWidth = 15f;

        float FitWidth(IMyTextSurface panel, string text, float scale, float maxW)
        {
            if (maxW <= 1f)
                return scale;

            Vector2 size = Measure(panel, text, scale);
            float w = size.X > 1f
                ? size.X
                : (text != null && text.Length > 0 ? text.Length : 1) * DebugCharWidth * scale;

            if (w > maxW)
                scale *= maxW / w;
            return scale;
        }

        Vector2 Measure(IMyTextSurface panel, string text, float scale)
        {
            measure.Clear();
            measure.Append(text ?? "");
            return panel.MeasureStringInPixels(measure, "DEBUG", scale);
        }

        static float Clamp(float v, float min, float max)
        {
            if (v < min)
                return min;
            if (v > max)
                return max;
            return v;
        }

        public void DrawFlightPanel(IMyTextSurface panel, bool preferSecondWhenSingle)
        {
            if (panel == null)
                return;

            int available = texts.Count > 0 && texts1.Count > 0 ? 2 : 1;
            int cols = ChooseColumns(panel, available, texts, texts1, null);
            if (cols >= 2)
            {
                PaintColumns(panel, cols, texts, BackgroundColors, FontColors, texts1, background1, font1, null, null, null);
                return;
            }

            int stack = preferSecondWhenSingle && texts1.Count > 0 ? 1 : 0;
            if (stack == 0 && texts.Count == 0 && texts1.Count > 0)
                stack = 1;
            DrawSingle(panel, stack);
        }

        public bool TryDrawThird(IMyTextSurface panel, Sprites menu)
        {
            if (panel == null || menu == null)
                return false;
            if (texts.Count == 0 || texts1.Count == 0 || menu.texts.Count == 0)
                return false;

            int cols = ChooseColumns(panel, 3, texts, texts1, menu.texts);
            if (cols < 3)
                return false;

            PaintColumns(
                panel, 3,
                texts, BackgroundColors, FontColors,
                texts1, background1, font1,
                menu.texts, menu.BackgroundColors, menu.FontColors);
            return true;
        }

        void DrawSingle(IMyTextSurface panel, int stack)
        {
            if (stack == 1)
                ExchangeSecond();
            DrawInfoPanel(panel, 1);
            if (stack == 1)
                ExchangeSecond();
        }

        void ExchangeSecond()
        {
            ExchangeText(texts, texts1, holdText);
            ExchangeColor(BackgroundColors, background1, holdBg);
            ExchangeColor(FontColors, font1, holdFg);
        }

        static void ExchangeText(List<string> a, List<string> b, List<string> hold)
        {
            hold.Clear();
            for (int i = 0; i < a.Count; i++)
                hold.Add(a[i]);
            a.Clear();
            for (int i = 0; i < b.Count; i++)
                a.Add(b[i]);
            b.Clear();
            for (int i = 0; i < hold.Count; i++)
                b.Add(hold[i]);
        }

        static void ExchangeColor(List<Color> a, List<Color> b, List<Color> hold)
        {
            hold.Clear();
            for (int i = 0; i < a.Count; i++)
                hold.Add(a[i]);
            a.Clear();
            for (int i = 0; i < b.Count; i++)
                a.Add(b[i]);
            b.Clear();
            for (int i = 0; i < hold.Count; i++)
                b.Add(hold[i]);
        }

        int ChooseColumns(IMyTextSurface panel, int available, List<string> a, List<string> b, List<string> c)
        {
            if (available < 1)
                available = 1;
            if (available > 3)
                available = 3;

            int tall = 1;
            if (a != null && a.Count > tall)
                tall = a.Count;
            if (available >= 2 && b != null && b.Count > tall)
                tall = b.Count;
            if (available >= 3 && c != null && c.Count > tall)
                tall = c.Count;

            Vector2 visible = panel.SurfaceSize;
            float pad = PanelPad(visible);
            float scale = ScaleForRows(visible.Y, tall, pad);
            float longest = 0f;
            if (a != null)
                longest = MaxWidth(panel, a, scale);
            if (available >= 2 && b != null)
            {
                float w = MaxWidth(panel, b, scale);
                if (w > longest)
                    longest = w;
            }
            if (available >= 3 && c != null)
            {
                float w = MaxWidth(panel, c, scale);
                if (w > longest)
                    longest = w;
            }

            float needW = longest + Chrome(pad);
            int cols = 1;
            if (available >= 2 && visible.X >= 2f * needW)
                cols = 2;
            if (available >= 3 && visible.X >= 3f * needW)
                cols = 3;
            if (cols > available)
                cols = available;
            while (cols > 1 && needW * cols > visible.X)
                cols--;
            return cols;
        }

        float MaxWidth(IMyTextSurface panel, List<string> lines, float scale)
        {
            float max = 0f;
            for (int i = 0; i < lines.Count; i++)
            {
                float w = WidthOf(panel, lines[i], scale);
                if (w > max)
                    max = w;
            }
            return max;
        }

        float WidthOf(IMyTextSurface panel, string text, float scale)
        {
            Vector2 size = Measure(panel, text, scale);
            if (size.X > 1f)
                return size.X;
            int len = string.IsNullOrEmpty(text) ? 1 : text.Length;
            return len * DebugCharWidth * scale;
        }

        static float PanelPad(Vector2 surfaceSize)
        {
            float minDim = surfaceSize.X < surfaceSize.Y ? surfaceSize.X : surfaceSize.Y;
            return Clamp(minDim * 0.01f, 2f, 6f);
        }

        static float Chrome(float pad)
        {
            float textPad = pad * 1.5f;
            return 2f * pad + 2f * textPad;
        }

        static float ScaleForRows(float surfaceY, int rows, float pad)
        {
            if (rows < 1)
                rows = 1;
            if (surfaceY < 1f)
                surfaceY = 1f;
            float rowPitch = surfaceY / rows;
            float barH = rowPitch - pad;
            if (barH < 4f)
                barH = rowPitch * 0.85f;
            float scale = barH * 0.7f / DebugLineHeight;
            if (scale > 1.3f)
                scale = 1.3f;
            if (scale < 0.35f)
                scale = 0.35f;
            return scale;
        }

        void PaintColumns(
            IMyTextSurface panel, int cols,
            List<string> a, List<Color> aB, List<Color> aF,
            List<string> b, List<Color> bB, List<Color> bF,
            List<string> c, List<Color> cB, List<Color> cF)
        {
            scratch.Clear();
            Vector2 surfaceSize = panel.SurfaceSize;
            Vector2 textureSize = panel.TextureSize;
            string panelName = panel.Name ?? "";
            bool transparent = ic.TransparentLCD && panelName.ToLower().Contains("transparent");
            if (!transparent)
                scratch.Add(MakeRectSprite(new Vector2(0, 0), 2 * textureSize, ic.SpriteMenuColor));

            int tall = 1;
            if (a != null && a.Count > tall)
                tall = a.Count;
            if (cols >= 2 && b != null && b.Count > tall)
                tall = b.Count;
            if (cols >= 3 && c != null && c.Count > tall)
                tall = c.Count;

            float pad = PanelPad(surfaceSize);
            float scale = ScaleForRows(surfaceSize.Y, tall, pad);

            if (a != null && a.Count > 0)
                PaintStack(panel, a, aB, aF, 0, cols, scale);
            if (cols >= 2 && b != null && b.Count > 0)
                PaintStack(panel, b, bB, bF, 1, cols, scale);
            if (cols >= 3 && c != null && c.Count > 0)
                PaintStack(panel, c, cB, cF, 2, cols, scale);

            using (var frame = panel.DrawFrame())
            {
                for (int i = 0; i < scratch.Count; i++)
                    frame.Add(scratch[i]);
            }
        }

        void PaintStack(IMyTextSurface panel, List<string> lines, List<Color> backs, List<Color> fonts, int col, int cols, float scale)
        {
            int rows = lines.Count;
            if (rows < 1 || cols < 1)
                return;

            Vector2 surfaceSize = panel.SurfaceSize;
            Vector2 textureSize = panel.TextureSize;
            float pad = PanelPad(surfaceSize);
            float originX = (textureSize.X - surfaceSize.X) * 0.5f;
            float originY = (textureSize.Y - surfaceSize.Y) * 0.5f;
            float colW = surfaceSize.X / cols;
            float rowPitch = surfaceSize.Y / rows;
            float barH = rowPitch - pad;
            if (barH < 4f)
                barH = rowPitch * 0.85f;

            float barX = originX + col * colW + pad;
            float barW = colW - 2f * pad;
            float textPad = pad * 1.5f;
            float textMaxW = barW - 2f * textPad;
            if (textMaxW < 1f)
                textMaxW = 1f;

            for (int i = 0; i < rows; i++)
            {
                float centerY = originY + i * rowPitch + rowPitch * 0.5f;
                Color back = i < backs.Count ? backs[i] : ic.SpriteBackgroundColor;
                scratch.Add(MakeRectSprite(new Vector2(barX, centerY), new Vector2(barW, barH), back));

                string line = lines[i] ?? "";
                float scaleLine = FitWidth(panel, line, scale, textMaxW);
                if (cols == 1)
                    line = CutIfNeeded(panel, line, ref scaleLine, textMaxW);

                float lineH = scaleLine * DebugLineHeight;
                float barTop = centerY - barH * 0.5f;
                float extra = barH - lineH;
                if (extra < 0f)
                    extra = 0f;
                float textX = barX + textPad;
                float textY = barTop + extra * 0.22f;
                Color font = i < fonts.Count ? fonts[i] : ic.SpriteFontColor;
                scratch.Add(MakeTextSprite(new Vector2(textX, textY), font, line, scaleLine));
            }
        }

        string CutIfNeeded(IMyTextSurface panel, string line, ref float scaleLine, float textMaxW)
        {
            if (string.IsNullOrEmpty(line))
                return line;

            float width = WidthOf(panel, line, scaleLine);
            if (width <= textMaxW + 0.5f)
                return line;

            string cut = line;
            while (cut.Length > 2)
            {
                cut = cut.Substring(0, cut.Length - 1);
                string shown = cut.Length > 2 ? cut.Substring(0, cut.Length - 2) + ".." : cut;
                float fitted = FitWidth(panel, shown, scaleLine, textMaxW);
                if (WidthOf(panel, shown, fitted) <= textMaxW + 0.5f)
                {
                    scaleLine = fitted;
                    return shown;
                }
            }
            return line;
        }
    }
}
