using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // C#6 / Space Engineers compatible dynamic sprite generator

        // Simple container (no Tuple)
        public class InfoItem
        {
            public string Label;
            public string Value;
            public InfoItem(string label, string value) { Label = label; Value = value; }
        }

        // Helper: create a MySprite rectangle
        MySprite MakeRectSprite(Vector2 center, Vector2 size, Color color)
        {
            var s = new MySprite();
            s.Type = SpriteType.TEXTURE;
            s.Data = "SquareSimple";
            s.Position = center;
            s.Size = size;
            s.Color = color;
            s.Alignment = TextAlignment.CENTER;
            return s;
        }

        // Helper: create a MySprite text
        MySprite MakeTextSprite(string text, Vector2 pos, float scale, Color color, TextAlignment align)
        {
            var s = new MySprite();
            s.Type = SpriteType.TEXT;
            s.Data = text;
            s.Position = pos;
            s.Color = color;
            s.RotationOrScale = scale;
            s.FontId = "White";
            s.Alignment = align;
            return s;
        }

        // Build sprites for a list of InfoItem
        List<MySprite> BuildInfoSprites(List<InfoItem> items, Vector2 panelSize, int cols)
        {
            var sprites = new List<MySprite>();
            if (items == null || items.Count == 0)
            {
                sprites.Add(MakeRectSprite(panelSize * 0.5f, panelSize, new Color(10, 10, 10, 220)));
                sprites.Add(MakeTextSprite("No data", panelSize * 0.5f, 1.2f, Color.LightGray, TextAlignment.CENTER));
                return sprites;
            }

            // background
            sprites.Add(MakeRectSprite(panelSize * 0.5f, panelSize, new Color(10, 10, 10, 220)));

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

                sprites.Add(MakeRectSprite(center, size, new Color(40, 40, 40, 220)));

                var labelPos = new Vector2(x - boxW * 0.38f + 6f, y - boxH * 0.25f);
                sprites.Add(MakeTextSprite(items[i].Label, labelPos, 0.7f, Color.LightGray, TextAlignment.LEFT));

                var valuePos = new Vector2(x, y + boxH * 0.08f);
                sprites.Add(MakeTextSprite(items[i].Value, valuePos, 1.1f, Color.White, TextAlignment.CENTER));
            }

            return sprites;
        }

        // Usage example inside Main()
        public void DrawInfoPanel(IMyTextSurface panel)
        {
            panel.ContentType = ContentType.SCRIPT;

            var items = new List<InfoItem>();
            items.Add(new InfoItem("Power", GetPowerString()));
            items.Add(new InfoItem("Cargo", GetCargoString()));
            items.Add(new InfoItem("Crew", GetCrewString()));
            items.Add(new InfoItem("Power", GetPowerString()));
            items.Add(new InfoItem("Cargo", GetCargoString()));
            items.Add(new InfoItem("Crew", GetCrewString()));
            // add more items as needed

            var sprites = BuildInfoSprites(items, panel.SurfaceSize, 3);

            using (var frame = panel.DrawFrame())
            {
                for (int i = 0; i < sprites.Count; i++) frame.Add(sprites[i]);
            }
        }

        // Example providers (reuse from your script)
        string GetPowerString()
        {
            var batteries = new List<IMyBatteryBlock>();
            GridTerminalSystem.GetBlocksOfType(batteries);
            if (batteries.Count == 0) return "No Batt";
            double stored = 0, capacity = 0;
            foreach (var b in batteries) { stored += (double)b.CurrentStoredPower; capacity += (double)b.MaxStoredPower; }
            if (capacity <= 0) return "N/A";
            int pct = (int)Math.Round(100.0 * stored / capacity);
            return pct + "%";
        }

        string GetCargoString()
        {
            var containers = new List<IMyCargoContainer>();
            GridTerminalSystem.GetBlocksOfType(containers);
            if (containers.Count == 0) return "No Cargo";
            double used = 0, max = 0;
            foreach (var c in containers) { var inv = c.GetInventory(); used += inv.CurrentVolume.RawValue; max += inv.MaxVolume.RawValue; }
            if (max <= 0) return "N/A";
            int pct = (int)Math.Round(100.0 * used / max);
            return pct + "%";
        }

        string GetCrewString()
        {
            var seats = new List<IMyCockpit>();
            GridTerminalSystem.GetBlocksOfType(seats);
            if (seats.Count == 0) return "No Cockpits";
            int occ = 0, tot = 0;
            foreach (var s in seats) { tot++; if (s.IsUnderControl) occ++; }
            return occ + "/" + tot;
        }

    }
}
