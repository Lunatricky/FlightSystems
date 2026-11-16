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
        // Settings pages copy into flightMenu. A shared surface keeps that stack as column 3
        // whenever the panel is wide enough, including the idle list while the menu is closed.
        static List<IMyTextSurface> flight1;
        static List<IMyTextSurface> flight2;
        static List<IMyTextSurface> flightSettings;
        static bool flightSettingsOpen;
        static Sprites flightHud1;
        static Sprites flightHud2;
        static Sprites flightMenu;
        static readonly List<IMyTextSurface> painted = new List<IMyTextSurface>();

        readonly IniContext ic;
        readonly StringBuilder measure = new StringBuilder();
        readonly List<MySprite> spriteBuf = new List<MySprite>();
        readonly Sprites[] shown = new Sprites[3];

        readonly List<string> texts = new List<string>();
        readonly List<Color> BackgroundColors = new List<Color>();
        readonly List<Color> FontColors = new List<Color>();

        public Sprites(IniContext ic)
        {
            this.ic = ic;
        }

        public void Clear()
        {
            texts.Clear();
            BackgroundColors.Clear();
            FontColors.Clear();
        }

        public void CopyFrom(Sprites src)
        {
            Clear();
            if (src == null)
                return;

            for (int i = 0; i < src.texts.Count; i++)
            {
                texts.Add(src.texts[i]);
                BackgroundColors.Add(src.BackgroundColors[i]);
                FontColors.Add(src.FontColors[i]);
            }
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

        public static void BindFlight(
            List<IMyTextSurface> lcds1,
            List<IMyTextSurface> lcds2,
            List<IMyTextSurface> settings,
            bool settingsOpen,
            Sprites hud1,
            Sprites hud2,
            Sprites menu)
        {
            flight1 = lcds1;
            flight2 = lcds2;
            flightSettings = settings;
            flightSettingsOpen = settingsOpen;
            flightHud1 = hud1;
            flightHud2 = hud2;
            flightMenu = menu;
        }

        public static void PaintHud()
        {
            if (flightHud1 == null || flightHud2 == null)
                return;

            flightHud1.PaintAllHud();
        }

        public void DrawTo(List<IMyTextSurface> surfaces)
        {
            if (surfaces == null)
                return;

            if (flightMenu != null && flightMenu != this)
                flightMenu.CopyFrom(this);

            for (int i = 0; i < surfaces.Count; i++)
            {
                IMyTextSurface panel = surfaces[i];
                if (panel == null)
                    continue;
                if (TryDrawAsThirdColumn(panel))
                    continue;
                DrawInfoPanel(panel);
            }
        }

        void PaintAllHud()
        {
            painted.Clear();
            PaintHudList(flight1);
            PaintHudList(flight2);
        }

        void PaintHudList(List<IMyTextSurface> list)
        {
            if (list == null)
                return;

            for (int i = 0; i < list.Count; i++)
            {
                IMyTextSurface panel = list[i];
                if (panel == null)
                    continue;
                if (Contains(painted, panel))
                    continue;

                painted.Add(panel);

                bool onSettings = Contains(flightSettings, panel);
                bool wantSettings = onSettings && flightMenu != null && flightMenu.LineCount > 0;
                int cols = CountColumns(panel, flightHud1, flightHud2, wantSettings ? flightMenu : null, wantSettings);
                bool showSettings = wantSettings && cols >= 3;

                // Not wide enough for three columns: the open menu keeps the whole panel.
                if (onSettings && flightSettingsOpen && !showSettings)
                    continue;

                bool in1 = Contains(flight1, panel);
                PaintPanel(panel, cols, showSettings, in1, flightHud1, flightHud2, flightMenu);
            }
        }

        bool TryDrawAsThirdColumn(IMyTextSurface panel)
        {
            if (flightHud1 == null || flightHud2 == null)
                return false;
            if (!Contains(flight1, panel) && !Contains(flight2, panel))
                return false;

            int cols = CountColumns(panel, flightHud1, flightHud2, this, true);
            if (cols < 3)
                return false;

            PaintPanel(panel, 3, true, true, flightHud1, flightHud2, this);
            return true;
        }

        void DrawInfoPanel(IMyTextSurface panel)
        {
            if (LineCount < 1)
            {
                Blank(panel);
                return;
            }

            shown[0] = this;
            shown[1] = null;
            shown[2] = null;
            PaintShown(panel, 1);
        }

        void PaintPanel(IMyTextSurface panel, int cols, bool showSettings, bool preferFirst, Sprites s1, Sprites s2, Sprites settings)
        {
            int n = FillShown(cols, showSettings, preferFirst, s1, s2, settings);
            if (n < 1)
            {
                Blank(panel);
                return;
            }

            PaintShown(panel, n);
        }

        int FillShown(int cols, bool showSettings, bool preferFirst, Sprites s1, Sprites s2, Sprites settings)
        {
            shown[0] = null;
            shown[1] = null;
            shown[2] = null;

            int n = 0;
            if (cols >= 2)
            {
                if (s1 != null && s1.LineCount > 0)
                    shown[n++] = s1;
                if (s2 != null && s2.LineCount > 0)
                    shown[n++] = s2;
                if (cols >= 3 && showSettings && settings != null && settings.LineCount > 0)
                    shown[n++] = settings;
                return n;
            }

            if (preferFirst && s1 != null && s1.LineCount > 0)
                shown[n++] = s1;
            else if (s2 != null && s2.LineCount > 0)
                shown[n++] = s2;
            else if (s1 != null && s1.LineCount > 0)
                shown[n++] = s1;
            else if (settings != null && settings.LineCount > 0)
                shown[n++] = settings;
            return n;
        }

        void Blank(IMyTextSurface panel)
        {
            panel.ContentType = ContentType.SCRIPT;
            panel.Script = "";
            spriteBuf.Clear();
            Flush(panel);
        }

        void PaintShown(IMyTextSurface panel, int cols)
        {
            panel.ContentType = ContentType.SCRIPT;
            panel.Script = "";
            spriteBuf.Clear();

            Vector2 surfaceSize = panel.SurfaceSize;
            Vector2 textureSize = panel.TextureSize;

            string panelName = panel.Name ?? "";
            bool transparent = ic.TransparentLCD && panelName.ToLower().Contains("transparent");
            if (!transparent)
                spriteBuf.Add(MakeRectSprite(new Vector2(0, 0), 2 * textureSize, ic.SpriteMenuColor));

            if (cols < 1)
                cols = 1;
            if (surfaceSize.X < 1f || surfaceSize.Y < 1f)
            {
                Flush(panel);
                return;
            }

            float pad = Pad(surfaceSize);
            float textPad = pad * 1.5f;
            float originX = (textureSize.X - surfaceSize.X) * 0.5f;
            float originY = (textureSize.Y - surfaceSize.Y) * 0.5f;
            float colW = surfaceSize.X / cols;

            float baseScale = 1.3f;
            for (int s = 0; s < cols; s++)
            {
                Sprites stack = shown[s];
                if (stack == null || stack.LineCount < 1)
                    continue;
                float hs = HeightScale(surfaceSize.Y, stack.LineCount, pad);
                if (hs < baseScale)
                    baseScale = hs;
            }

            for (int s = 0; s < cols; s++)
            {
                Sprites stack = shown[s];
                if (stack == null)
                    continue;

                int rows = stack.LineCount;
                if (rows < 1)
                    continue;

                float rowPitch = surfaceSize.Y / rows;
                float barH = rowPitch - pad;
                if (barH < 4f)
                    barH = rowPitch * 0.85f;

                float barX = originX + s * colW + pad;
                float barW = colW - 2f * pad;
                if (barW < 1f)
                    barW = 1f;

                float textMaxW = barW - 2f * textPad;
                if (textMaxW < 1f)
                    textMaxW = 1f;

                for (int i = 0; i < rows; i++)
                {
                    float centerY = originY + i * rowPitch + rowPitch * 0.5f;
                    spriteBuf.Add(MakeRectSprite(new Vector2(barX, centerY), new Vector2(barW, barH), stack.BackgroundColors[i]));

                    string line = stack.texts[i] ?? "";
                    float scaleLine = FitWidth(panel, line, baseScale, textMaxW);
                    if (scaleLine < DebugMinScale)
                    {
                        line = Truncate(panel, line, DebugMinScale, textMaxW);
                        scaleLine = DebugMinScale;
                    }

                    float lineH = scaleLine * DebugLineHeight;
                    float barTop = centerY - barH * 0.5f;
                    float extra = barH - lineH;
                    if (extra < 0f)
                        extra = 0f;

                    // TEXT LEFT is the top-left. Bias toward the top so short cockpit screens don't sit on the bar bottom.
                    float textX = barX + textPad;
                    float textY = barTop + extra * 0.22f;
                    spriteBuf.Add(MakeTextSprite(new Vector2(textX, textY), stack.FontColors[i], line, scaleLine));
                }
            }

            Flush(panel);
        }

        void Flush(IMyTextSurface panel)
        {
            using (var frame = panel.DrawFrame())
            {
                for (int i = 0; i < spriteBuf.Count; i++)
                    frame.Add(spriteBuf[i]);
            }
        }

        int CountColumns(IMyTextSurface panel, Sprites s1, Sprites s2, Sprites settings, bool settingsOnThis)
        {
            Vector2 surfaceSize = panel.SurfaceSize;
            if (surfaceSize.X < 1f)
                return 1;

            bool has1 = s1 != null && s1.LineCount > 0;
            bool has2 = s2 != null && s2.LineCount > 0;
            bool showSettings = settingsOnThis && settings != null && settings.LineCount > 0;
            int stackCount = 0;
            if (has1)
                stackCount++;
            if (has2)
                stackCount++;
            if (showSettings)
                stackCount++;
            if (stackCount <= 1)
                return 1;

            float pad = Pad(surfaceSize);
            float textPad = pad * 1.5f;
            float chrome = 2f * pad + 2f * textPad;

            float measureScale = 1.3f;
            if (has1)
            {
                float hs = HeightScale(surfaceSize.Y, s1.LineCount, pad);
                if (hs < measureScale)
                    measureScale = hs;
            }
            if (has2)
            {
                float hs = HeightScale(surfaceSize.Y, s2.LineCount, pad);
                if (hs < measureScale)
                    measureScale = hs;
            }
            if (showSettings)
            {
                float hs = HeightScale(surfaceSize.Y, settings.LineCount, pad);
                if (hs < measureScale)
                    measureScale = hs;
            }

            float longest = 0f;
            if (has1)
                longest = Max(longest, Longest(panel, s1, measureScale));
            if (has2)
                longest = Max(longest, Longest(panel, s2, measureScale));
            if (showSettings)
                longest = Max(longest, Longest(panel, settings, measureScale));

            // Insets around the line are part of the column, otherwise the text runs into the next stack.
            float needW = longest + chrome;
            if (needW < 1f)
                needW = 1f;

            int cols = 1;
            if (stackCount >= 2 && surfaceSize.X >= 2f * needW)
                cols = 2;
            if (stackCount >= 3 && showSettings && surfaceSize.X >= 3f * needW)
                cols = 3;
            if (cols > stackCount)
                cols = stackCount;

            while (cols > 1 && !LinesFit(panel, cols, measureScale, chrome, s1, s2, settings, showSettings))
                cols--;

            return cols;
        }

        bool LinesFit(IMyTextSurface panel, int cols, float scale, float chrome, Sprites s1, Sprites s2, Sprites settings, bool showSettings)
        {
            float textMaxW = panel.SurfaceSize.X / cols - chrome;
            if (textMaxW < 1f)
                return false;

            if (!StackFits(panel, s1, scale, textMaxW))
                return false;
            if (cols >= 2 && !StackFits(panel, s2, scale, textMaxW))
                return false;
            if (cols >= 3 && showSettings && !StackFits(panel, settings, scale, textMaxW))
                return false;
            return true;
        }

        bool StackFits(IMyTextSurface panel, Sprites stack, float scale, float maxW)
        {
            if (stack == null)
                return true;

            for (int i = 0; i < stack.LineCount; i++)
            {
                if (FitWidth(panel, stack.texts[i], scale, maxW) < DebugMinScale)
                    return false;
            }
            return true;
        }

        float Longest(IMyTextSurface panel, Sprites stack, float scale)
        {
            float max = 0f;
            for (int i = 0; i < stack.LineCount; i++)
            {
                float w = WidthOf(panel, stack.texts[i], scale);
                if (w > max)
                    max = w;
            }
            return max;
        }

        int LineCount
        {
            get { return texts.Count; }
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

        const float DebugLineHeight = 28.8f;
        const float DebugCharWidth = 15f;
        const float DebugMinScale = 0.35f;

        float FitWidth(IMyTextSurface panel, string text, float scale, float maxW)
        {
            if (maxW <= 1f)
                return scale;

            float w = WidthOf(panel, text, scale);
            if (w > maxW)
                scale *= maxW / w;
            return scale;
        }

        float WidthOf(IMyTextSurface panel, string text, float scale)
        {
            Vector2 size = Measure(panel, text, scale);
            if (size.X > 1f)
                return size.X;

            int n = text != null && text.Length > 0 ? text.Length : 1;
            return n * DebugCharWidth * scale;
        }

        string Truncate(IMyTextSurface panel, string line, float scale, float maxW)
        {
            if (string.IsNullOrEmpty(line))
                return "";

            float w = WidthOf(panel, line, scale);
            if (w <= maxW)
                return line;

            int keep = (int)(line.Length * (maxW / w)) - 2;
            if (keep < 1)
                keep = 1;
            if (keep > line.Length)
                keep = line.Length;

            while (keep > 1)
            {
                string cut = line.Substring(0, keep) + "..";
                if (WidthOf(panel, cut, scale) <= maxW)
                    return cut;
                keep--;
            }

            return "..";
        }

        Vector2 Measure(IMyTextSurface panel, string text, float scale)
        {
            measure.Clear();
            measure.Append(text ?? "");
            return panel.MeasureStringInPixels(measure, "DEBUG", scale);
        }

        static float HeightScale(float surfaceY, int rows, float pad)
        {
            if (rows < 1)
                rows = 1;

            float rowPitch = surfaceY / rows;
            float barH = rowPitch - pad;
            if (barH < 4f)
                barH = rowPitch * 0.85f;

            float scale = (barH * 0.7f) / DebugLineHeight;
            if (scale > 1.3f)
                scale = 1.3f;
            if (scale < DebugMinScale)
                scale = DebugMinScale;
            return scale;
        }

        static float Pad(Vector2 surfaceSize)
        {
            float minDim = surfaceSize.X < surfaceSize.Y ? surfaceSize.X : surfaceSize.Y;
            return Clamp(minDim * 0.01f, 2f, 6f);
        }

        static float Max(float a, float b)
        {
            return a > b ? a : b;
        }

        static float Clamp(float v, float min, float max)
        {
            if (v < min)
                return min;
            if (v > max)
                return max;
            return v;
        }

        static bool Contains(List<IMyTextSurface> list, IMyTextSurface panel)
        {
            if (list == null || panel == null)
                return false;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == panel)
                    return true;
            }
            return false;
        }
    }
}
