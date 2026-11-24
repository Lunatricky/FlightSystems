using IngameScript.Domain;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    class Sprites
    {
        // A surface in the menu list and in LCD 1 or LCD 2 stays in both.
        // Closed, that panel is the flight HUD. Open, the menu fills the whole panel.
        // Menu-only surfaces still get the idle flight-command list.
        static List<IMyTextSurface> flight1;
        static List<IMyTextSurface> flight2;
        static List<IMyTextSurface> flightSettings;
        static bool flightSettingsOpen;
        static Sprites flightHud1;
        static Sprites flightHud2;
        static Sprites flightMenu;
        static readonly List<IMyTextSurface> painted = new List<IMyTextSurface>();
        static readonly StringBuilder textOut = new StringBuilder();
        static IMyTextSurface markedPanel;
        static int spriteTicks;
        static bool leadEmptySprite;
        static bool leadArmed;

        readonly IniContext ic;
        readonly StringBuilder measure = new StringBuilder();
        readonly List<MySprite> spriteBuf = new List<MySprite>();
        readonly Sprites[] shown = new Sprites[3];

        readonly List<string> texts = new List<string>();
        readonly List<string> boxB = new List<string>();
        readonly List<string> boxC = new List<string>();
        readonly List<float> bar = new List<float>();
        readonly List<Color> BackgroundColors = new List<Color>();
        readonly List<Color> FontColors = new List<Color>();
        int followLine = -1;

        public Sprites(IniContext ic)
        {
            this.ic = ic;
        }

        public void Clear()
        {
            texts.Clear();
            boxB.Clear();
            boxC.Clear();
            bar.Clear();
            BackgroundColors.Clear();
            FontColors.Clear();
            followLine = -1;
        }

        public void CopyFrom(Sprites src)
        {
            Clear();
            if (src == null)
                return;

            for (int i = 0; i < src.texts.Count; i++)
            {
                texts.Add(src.texts[i]);
                boxB.Add(src.boxB[i]);
                boxC.Add(src.boxC[i]);
                bar.Add(src.bar[i]);
                BackgroundColors.Add(src.BackgroundColors[i]);
                FontColors.Add(src.FontColors[i]);
            }
            followLine = src.followLine;
        }

        void Put(string a, string b, string c, Color back, Color font)
        {
            texts.Add(a);
            boxB.Add(b);
            boxC.Add(c);
            bar.Add(-1f);
            BackgroundColors.Add(back);
            FontColors.Add(font);
        }

        public void AddBar(double percent, Color color)
        {
            if (double.IsNaN(percent) || percent < 0)
                percent = 0;
            if (percent > 100)
                percent = 100;
            texts.Add("");
            boxB.Add(null);
            boxC.Add(null);
            bar.Add((float)percent / 100f);
            BackgroundColors.Add(color);
            FontColors.Add(ic.SpriteFontColor);
        }

        public void Add(string s)
        {
            Put(s, null, null, ic.SpriteBackgroundColor, ic.SpriteFontColor);
        }

        public void AddB(string s, Color b)
        {
            Put(s, null, null, b, ic.SpriteFontColor);
        }

        public void AddF(string s, Color f)
        {
            Put(s, null, null, ic.SpriteBackgroundColor, f);
        }

        public void Add(string s, Color b, Color f)
        {
            Put(s, null, null, b, f);
        }

        public void Add2(string a, string b)
        {
            Put(a, b, null, ic.SpriteBackgroundColor, ic.SpriteFontColor);
        }

        public void Add2B(string a, string b, Color back)
        {
            Put(a, b, null, back, ic.SpriteFontColor);
        }

        public void Add3(string a, string b, string c)
        {
            Put(a, b, c, ic.SpriteBackgroundColor, ic.SpriteFontColor);
        }

        // Line index that must stay on screen. Title is 0. The last line stays put.
        public void KeepVisible(int line)
        {
            followLine = line;
        }

        public static void BindFlight(
            List<IMyTextSurface> lcds1,
            List<IMyTextSurface> lcds2,
            List<IMyTextSurface> menu,
            bool menuOpen,
            Sprites hud1,
            Sprites hud2,
            Sprites menuHud)
        {
            flight1 = lcds1;
            flight2 = lcds2;
            flightSettings = menu;
            flightSettingsOpen = menuOpen;
            flightHud1 = hud1;
            flightHud2 = hud2;
            flightMenu = menuHud;
        }

        public static void PaintHud()
        {
            if (flightHud1 == null || flightHud2 == null)
                return;

            flightHud1.PaintAllHud();
        }

        public void DrawTo(List<IMyTextSurface> surfaces)
        {
            DrawSurfaces(surfaces, false);
        }

        public void DrawIdleTo(List<IMyTextSurface> surfaces)
        {
            DrawSurfaces(surfaces, true);
        }

        void DrawSurfaces(List<IMyTextSurface> surfaces, bool idleList)
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

                // Same panel as LCD 1 or LCD 2. Idle and a closed menu do not paint it.
                if (SharedWithFlight(panel))
                {
                    if (idleList || !flightSettingsOpen)
                        continue;
                    DrawInfoPanel(panel);
                    continue;
                }

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
                if (panel == markedPanel)
                    continue;
                if (Contains(painted, panel))
                    continue;

                painted.Add(panel);

                PaintFlightHud(panel);
            }
        }

        // Both sprites share a surface only when it is in both LCD lists and wide enough.
        void PaintFlightHud(IMyTextSurface panel)
        {
            bool onSettings = Contains(flightSettings, panel);
            if (onSettings && flightSettingsOpen)
                return;

            bool in1 = Contains(flight1, panel);
            bool in2 = Contains(flight2, panel);
            Sprites left = in1 ? flightHud1 : null;
            Sprites right = in2 ? flightHud2 : null;
            int cols = CountColumns(panel, left, right, null, false);
            // A shared panel does not fall back to the idle list when the HUD stacks are empty.
            Sprites settings = onSettings ? null : flightMenu;
            PaintPanel(panel, cols, false, in1, left, right, settings);
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

        public static void SetMarked(IMyTextSurface panel)
        {
            markedPanel = panel;
        }

        // A flight surface gets its HUD back. A menu surface is left for the menu draw.
        public static void RestoreUnmarked(IMyTextSurface panel)
        {
            if (panel == null || panel == markedPanel || flightHud1 == null)
                return;
            if (!Contains(flight1, panel) && !Contains(flight2, panel))
                return;
            flightHud1.PaintOneHud(panel);
        }

        void PaintOneHud(IMyTextSurface panel)
        {
            if (panel == null || panel == markedPanel)
                return;
            PaintFlightHud(panel);
        }

        public void DrawPlainMarker(IMyTextSurface panel)
        {
            if (panel == null)
                return;

            if (ic.TextPanels)
            {
                WriteMarkerText(panel);
                return;
            }

            panel.ContentType = ContentType.SCRIPT;
            panel.Script = "";
            spriteBuf.Clear();

            Vector2 surfaceSize = panel.SurfaceSize;
            Vector2 textureSize = panel.TextureSize;
            if (surfaceSize.X < 1f || surfaceSize.Y < 1f)
            {
                Flush(panel);
                return;
            }

            // One sprite covers the visible area. The sentence stays at a cockpit-readable size.
            float centerX = (textureSize.X - surfaceSize.X) * 0.5f + surfaceSize.X * 0.5f;
            float centerY = (textureSize.Y - surfaceSize.Y) * 0.5f + surfaceSize.Y * 0.5f;
            spriteBuf.Add(MakeRectSprite(new Vector2(centerX, centerY), surfaceSize, ic.SpriteBackgroundColor));

            float pad = Pad(surfaceSize);
            float textMaxW = surfaceSize.X - 2f * pad;
            if (textMaxW < 1f)
                textMaxW = 1f;

            float scale = 1.3f;
            float floor = scale * MenuFitFloor;
            string line = SelectThisScreen;
            string line2 = null;
            if (TextWidth(line, scale) > textMaxW)
            {
                line = SelectLineTop;
                line2 = SelectLineBottom;
                float w1 = TextWidth(line, scale);
                float w2 = TextWidth(line2, scale);
                float widest = w1 > w2 ? w1 : w2;
                if (widest > textMaxW && widest > 1f)
                {
                    float fit = scale * textMaxW / widest;
                    if (fit < floor)
                        fit = floor;
                    scale = fit;
                }
            }

            float lineH = scale * DebugLineHeight;
            float blockH = line2 == null ? lineH : lineH * 2f;
            float top = centerY - blockH * 0.5f;
            float wLine = TextWidth(line, scale);
            spriteBuf.Add(MakeTextSprite(new Vector2(centerX - wLine * 0.5f, top), ic.SpriteFontColor, line, scale));
            if (line2 != null)
            {
                float wLine2 = TextWidth(line2, scale);
                spriteBuf.Add(MakeTextSprite(new Vector2(centerX - wLine2 * 0.5f, top + lineH), ic.SpriteFontColor, line2, scale));
            }
            Flush(panel);
        }

        void Blank(IMyTextSurface panel)
        {
            if (ic.TextPanels)
            {
                PrepareText(panel, TextAlignment.LEFT);
                textOut.Clear();
                panel.WriteText(textOut);
                return;
            }

            panel.ContentType = ContentType.SCRIPT;
            panel.Script = "";
            spriteBuf.Clear();
            Flush(panel);
        }

        void PaintShown(IMyTextSurface panel, int cols)
        {
            if (ic.TextPanels)
            {
                WriteStackedText(panel, cols);
                return;
            }

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
                float hs = HeightScale(surfaceSize.Y, MenuScaleRows(stack), pad);
                if (hs < baseScale)
                    baseScale = hs;
            }

            for (int s = 0; s < cols; s++)
            {
                Sprites stack = shown[s];
                if (stack == null)
                    continue;

                int lineCount = stack.LineCount;
                if (lineCount < 1)
                    continue;

                int pitchRows = MenuScaleRows(stack);
                float rowPitch = surfaceSize.Y / pitchRows;
                float barH = rowPitch - pad;
                if (barH < 4f)
                    barH = rowPitch * 0.85f;

                int rows = lineCount;
                // Each menu surface shows as many rows as fit at this pitch. The scale stays put.
                if (IsMenu(stack))
                {
                    float glyphBar = baseScale * DebugLineHeight / 0.7f;
                    if (barH > glyphBar)
                    {
                        barH = glyphBar;
                        rowPitch = barH + pad;
                    }
                    int fit = (int)(surfaceSize.Y / rowPitch + 0.01f);
                    if (fit < 1)
                        fit = 1;
                    if (rows > fit)
                        rows = fit;
                }
                else if (rows > pitchRows)
                    rows = pitchRows;

                float barX = originX + s * colW + pad;
                float barW = colW - 2f * pad;
                if (barW < 1f)
                    barW = 1f;

                // One scale for the column. It fits BoxChars in the narrowest box and ignores the live text.
                int boxes = 1;
                for (int i = 0; i < lineCount; i++)
                {
                    string rawB = stack.boxB[i];
                    string rawC = stack.boxC[i];
                    int n = rawB == null ? 1 : rawC == null ? 2 : 3;
                    if (n > boxes)
                        boxes = n;
                }
                float scaleGap = boxes < 2 ? 0f : pad < 2f ? 2f : pad;
                float narrow = (barW - scaleGap * (boxes - 1)) / boxes;
                if (narrow < 1f)
                    narrow = 1f;
                float narrowText = narrow - 2f * textPad;
                if (narrowText < 1f)
                    narrowText = 1f;
                float scaleLine = narrowText / (BoxChars * DebugCharWidth);
                if (scaleLine > baseScale)
                    scaleLine = baseScale;
                if (scaleLine < DebugMinScale)
                    scaleLine = DebugMinScale;

                bool window = IsMenu(stack) && stack.followLine >= 0 && stack.LineCount > rows && rows >= 3;
                int winStart = 1;
                if (window)
                {
                    int middle = rows - 2;
                    int sel = stack.followLine;
                    if (sel < 1)
                        sel = 1;
                    if (sel > stack.LineCount - 2)
                        sel = stack.LineCount - 2;
                    winStart = sel - middle / 2;
                    if (winStart < 1)
                        winStart = 1;
                    int maxStart = stack.LineCount - 1 - middle;
                    if (winStart > maxStart)
                        winStart = maxStart;
                    if (winStart < 1)
                        winStart = 1;
                }

                for (int i = 0; i < rows; i++)
                {
                    int src = i;
                    if (window)
                    {
                        if (i == 0)
                            src = 0;
                        else if (i == rows - 1)
                            src = stack.LineCount - 1;
                        else
                            src = winStart + i - 1;
                    }

                    float centerY = originY + i * rowPitch + rowPitch * 0.5f;
                    float fill = stack.bar[src];
                    if (fill >= 0f)
                    {
                        spriteBuf.Add(MakeRectSprite(new Vector2(barX, centerY), new Vector2(barW, barH), ic.SpriteBackgroundColor));
                        if (fill > 0.004f)
                            spriteBuf.Add(MakeRectSprite(new Vector2(barX, centerY), new Vector2(barW * fill, barH), stack.BackgroundColors[src]));
                        continue;
                    }
                    string rawB = stack.boxB[src];
                    string rawC = stack.boxC[src];
                    int n = rawB == null ? 1 : rawC == null ? 2 : 3;
                    float gap = n < 2 ? 0f : pad < 2f ? 2f : pad;
                    float boxW = (barW - gap * (n - 1)) / n;
                    if (boxW < 1f)
                        boxW = 1f;
                    // LCD 2 pairs: the right value is the long one. Left keeps 40% of the row.
                    bool lcd2Pair = n == 2 && stack == flightHud2;
                    float pair = barW - gap;
                    float leftW = lcd2Pair ? pair * Lcd2Left : boxW;
                    if (leftW < 1f)
                        leftW = 1f;
                    float x = barX;
                    for (int b = 0; b < n; b++)
                    {
                        string part = b == 0 ? stack.texts[src] ?? "" : b == 1 ? rawB : rawC;
                        if (part == null)
                            part = "";
                        float w = boxW;
                        if (lcd2Pair)
                            w = b == 0 ? leftW : pair - leftW;
                        if (w < 1f)
                            w = 1f;
                        spriteBuf.Add(MakeRectSprite(new Vector2(x, centerY), new Vector2(w, barH), stack.BackgroundColors[src]));
                        float cellMax = w - 2f * textPad;
                        if (cellMax < 1f)
                            cellMax = 1f;
                        int fitChars = (int)(cellMax / (DebugCharWidth * scaleLine));
                        if (fitChars < 1)
                            fitChars = 1;
                        part = Truncate(part, fitChars);

                        float lineH = scaleLine * DebugLineHeight;
                        float barTop = centerY - barH * 0.5f;
                        float extra = barH - lineH;
                        if (extra < 0f)
                            extra = 0f;

                        // TEXT LEFT is the top-left. Bias toward the top so short cockpit screens don't sit on the bar bottom.
                        float textY = barTop + extra * 0.22f;
                        spriteBuf.Add(MakeTextSprite(new Vector2(x + textPad, textY), stack.FontColors[src], part, scaleLine));
                        x += w + gap;
                    }
                }
            }

            Flush(panel);
        }

        // Update1 is 60 Hz. Hold the flag until a sprite frame is drawn, because that tick may not paint.
        public static void NoteSpriteTick()
        {
            if (leadArmed)
            {
                leadEmptySprite = false;
                leadArmed = false;
            }
            if (leadEmptySprite)
                return;

            spriteTicks++;
            if (spriteTicks >= 180)
            {
                spriteTicks = 0;
                leadEmptySprite = true;
            }
        }

        void PrepareText(IMyTextSurface panel, TextAlignment align)
        {
            if (panel.ContentType != ContentType.TEXT_AND_IMAGE)
                panel.ContentType = ContentType.TEXT_AND_IMAGE;
            if (panel.Alignment != align)
                panel.Alignment = align;

            string name = panel.Name;
            bool transparent = ic.TransparentLCD && name != null && name.IndexOf("transparent", StringComparison.OrdinalIgnoreCase) >= 0;
            panel.BackgroundColor = transparent ? Color.Black : ic.LcdBackgroundColor;
            panel.FontColor = ic.LcdFontColor;
        }

        void WriteStackedText(IMyTextSurface panel, int cols)
        {
            PrepareText(panel, TextAlignment.LEFT);
            textOut.Clear();
            if (cols < 1)
                cols = 1;
            if (cols > shown.Length)
                cols = shown.Length;

            bool any = false;
            for (int s = 0; s < cols; s++)
            {
                Sprites stack = shown[s];
                if (stack == null)
                    continue;

                for (int i = 0; i < stack.texts.Count; i++)
                {
                    if (any)
                        textOut.Append('\n');
                    any = true;
                    string line = stack.texts[i];
                    string mid = stack.boxB[i];
                    string end = stack.boxC[i];
                    bool mark = IsMenu(stack) && stack.followLine == i;
                    if (mark)
                        textOut.Append(TextMark);
                    if (mid == null)
                    {
                        if (line != null)
                            textOut.Append(line);
                    }
                    else
                    {
                        AppendField(textOut, line, 10);
                        if (end != null)
                        {
                            AppendField(textOut, mid, 10);
                            textOut.Append(end ?? "");
                        }
                        else
                            textOut.Append(mid);
                    }
                    if (mark)
                        textOut.Append(TextMark);
                }
            }

            panel.WriteText(textOut);
        }

        static void AppendField(StringBuilder sb, string text, int width)
        {
            if (text == null)
                text = "";
            sb.Append(text);
            for (int i = text.Length; i < width; i++)
                sb.Append(' ');
        }

        void WriteMarkerText(IMyTextSurface panel)
        {
            PrepareText(panel, TextAlignment.CENTER);

            string font = panel.Font;
            if (string.IsNullOrEmpty(font))
                font = "DEBUG";
            measure.Clear();
            measure.Append('A');
            Vector2 size = panel.MeasureStringInPixels(measure, font, panel.FontSize);
            float lineH = size.Y;
            if (lineH < 1f)
                lineH = panel.FontSize * DebugLineHeight;
            if (lineH < 1f)
                lineH = DebugLineHeight;

            int fit = (int)(panel.SurfaceSize.Y / lineH);
            if (fit < 1)
                fit = 1;
            int pad = (fit - 1) / 2;

            textOut.Clear();
            for (int i = 0; i < pad; i++)
                textOut.Append('\n');
            textOut.Append(SelectThisScreen);
            panel.WriteText(textOut);
        }

        void Flush(IMyTextSurface panel)
        {
            using (var frame = panel.DrawFrame())
            {
                if (leadEmptySprite)
                {
                    frame.Add(new MySprite());
                    leadArmed = true;
                }
                for (int i = 0; i < spriteBuf.Count; i++)
                    frame.Add(spriteBuf[i]);
            }
        }

        int CountColumns(IMyTextSurface panel, Sprites s1, Sprites s2, Sprites menu, bool menuOnThis)
        {
            Vector2 surfaceSize = panel.SurfaceSize;
            if (surfaceSize.X < 1f)
                return 1;

            bool has1 = s1 != null && s1.LineCount > 0;
            bool has2 = s2 != null && s2.LineCount > 0;
            bool showMenu = menuOnThis && menu != null && menu.LineCount > 0;
            int stackCount = 0;
            if (has1)
                stackCount++;
            if (has2)
                stackCount++;
            if (showMenu)
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
            if (showMenu)
            {
                float hs = HeightScale(surfaceSize.Y, menu.LineCount, pad);
                if (hs < measureScale)
                    measureScale = hs;
            }

            // Column width stays on the longer budget so the bigger text does not force a second column.
            float longest = ColumnChars * DebugCharWidth * measureScale;
            float needW = longest + chrome;
            if (needW < 1f)
                needW = 1f;

            int cols = 1;
            if (stackCount >= 2 && surfaceSize.X >= 2f * needW)
                cols = 2;
            if (stackCount >= 3 && showMenu && surfaceSize.X >= 3f * needW)
                cols = 3;
            if (cols > stackCount)
                cols = stackCount;

            while (cols > 1 && surfaceSize.X / cols - chrome < longest)
                cols--;

            return cols;
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
        // Drawn size fits this many chars in the narrowest box. Half of the old 22, so the text is about twice as tall.
        const int BoxChars = 10;
        const int ColumnChars = 22;
        const float Lcd2Left = 0.5f;
        // Home, Flight, and the other short pages stay on their own line count.
        // A shorter page uses the 7-row bars. A longer list does not shrink past 10.
        const int MenuBarRows = 7;
        const int MenuCapRows = 10;
        const float MenuFitFloor = 0.8f;
        const string TextMark = "-";
        const string SelectThisScreen = "Press space to select this screen";
        const string SelectLineTop = "Press space to";
        const string SelectLineBottom = "select this screen";

        static bool IsMenu(Sprites stack)
        {
            return stack != flightHud1 && stack != flightHud2;
        }

        static int MenuScaleRows(Sprites stack)
        {
            int rows = stack.LineCount;
            if (!IsMenu(stack))
                return rows;
            if (rows < MenuBarRows)
                return MenuBarRows;
            if (rows > MenuCapRows)
                return MenuCapRows;
            return rows;
        }

        static float TextWidth(string text, float scale)
        {
            int n = 1;
            if (text != null && text.Length > 0)
                n = text.Length;
            return n * DebugCharWidth * scale;
        }

        static string Truncate(string line, int maxChars)
        {
            if (string.IsNullOrEmpty(line) || line.Length <= maxChars)
                return line;
            if (maxChars < 3)
                return line.Substring(0, maxChars);
            return line.Substring(0, maxChars - 2) + "..";
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

        static float Clamp(float v, float min, float max)
        {
            if (v < min)
                return min;
            if (v > max)
                return max;
            return v;
        }

        static bool SharedWithFlight(IMyTextSurface panel)
        {
            if (!Contains(flightSettings, panel))
                return false;
            return Contains(flight1, panel) || Contains(flight2, panel);
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
