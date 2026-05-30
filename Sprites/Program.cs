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
        // Simple LCD boxes display for Space Engineers (uses MySpriteDrawFrame)
        // Put this in a Programmable Block. Set run frequency (e.g., Update100).

        const string LCD_NAME = "LCD Panel"; // change to your LCD name or leave empty to use first surface

        double currentOverride = 0.0; // unused here but kept if you extend
        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void Save() { }

        public void Main(string argument, UpdateType updateSource)
        {
            var panel = GetTextSurface();
            if (panel == null) return;

            panel.ContentType = ContentType.SCRIPT;
            panel.Alignment = TextAlignment.LEFT;

            var sprites = new List<MySprite>();

            // Background full
            var bg = new MySprite()
            {
                Type = SpriteType.TEXTURE,
                Data = "SquareSimple",
                Position = panel.SurfaceSize * 0.5f,
                Size = panel.SurfaceSize,
                Color = new Color(10, 10, 10, 220),
                Alignment = TextAlignment.CENTER
            };
            sprites.Add(bg);

            // Header box
            Vector2 headerSize = new Vector2(panel.SurfaceSize.X, 40f);
            var header = new MySprite()
            {
                Type = SpriteType.TEXTURE,
                Data = "SquareSimple",
                Position = new Vector2(panel.SurfaceSize.X * 0.5f, 20f),
                Size = headerSize,
                Color = new Color(30, 144, 255, 220),
                Alignment = TextAlignment.CENTER
            };
            sprites.Add(header);

            var headerText = new MySprite()
            {
                Type = SpriteType.TEXT,
                Data = "Status Overview",
                Position = new Vector2(panel.SurfaceSize.X * 0.5f, 20f),
                Color = Color.White,
                RotationOrScale = 1.2f,
                FontId = "White"
            };
            sprites.Add(headerText);

            // Info boxes
            float margin = 8f;
            float boxWidth = (panel.SurfaceSize.X - margin * 4) / 3f;
            float boxHeight = 80f;
            float top = 60f + margin;

            string[] labels = { "Power", "Cargo", "Crew" };
            string[] values = { GetPowerString(), GetCargoString(), GetCrewString() };

            for (int i = 0; i < 3; i++)
            {
                float x = margin + boxWidth * 0.5f + i * (boxWidth + margin);
                var box = new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Data = "SquareSimple",
                    Position = new Vector2(x, top + boxHeight * 0.5f),
                    Size = new Vector2(boxWidth, boxHeight),
                    Color = new Color(40, 40, 40, 220),
                    Alignment = TextAlignment.CENTER
                };
                sprites.Add(box);

                var label = new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Data = labels[i],
                    Position = new Vector2(x - boxWidth * 0.35f + 6f, top + 12f),
                    Color = Color.LightGray,
                    RotationOrScale = 0.8f,
                    FontId = "White"
                };
                sprites.Add(label);

                var val = new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Data = values[i],
                    Position = new Vector2(x, top + boxHeight * 0.6f),
                    Color = Color.White,
                    RotationOrScale = 1.2f,
                    FontId = "White"
                };
                sprites.Add(val);
            }

            // Draw with frame API
            using (var frame = panel.DrawFrame())
            {
                foreach (var s in sprites) frame.Add(s);
            }
        }

        // Get first usable text surface or named
        IMyTextSurface GetTextSurface()
        {
            if (!string.IsNullOrEmpty(LCD_NAME))
            {
                var blocks = new List<IMyTerminalBlock>();
                GridTerminalSystem.SearchBlocksOfName(LCD_NAME, blocks, b => b is IMyTextPanel);
                if (blocks.Count > 0)
                {
                    var panelBlock = blocks[0] as IMyTextPanel;
                    if (panelBlock != null) return panelBlock as IMyTextSurface;
                }
            }

            // fallback: first text panel
            var allPanels = new List<IMyTextPanel>();
            GridTerminalSystem.GetBlocksOfType(allPanels);
            if (allPanels.Count > 0) return allPanels[0] as IMyTextSurface;

            // try generic surfaces (cockpits)
            var surfaces = new List<IMyTextSurfaceProvider>();
            GridTerminalSystem.GetBlocksOfType(surfaces);
            foreach (var p in surfaces)
            {
                var s = p.GetSurface(0);
                if (s != null) return s;
            }

            return null;
        }

        // Example value providers
        string GetPowerString()
        {
            var batteries = new List<IMyBatteryBlock>();
            GridTerminalSystem.GetBlocksOfType(batteries);
            if (batteries.Count == 0) return "No Batt";

            double stored = 0, capacity = 0;
            foreach (var b in batteries)
            {
                stored += (double)b.CurrentStoredPower;
                capacity += (double)b.MaxStoredPower;
            }
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
            foreach (var c in containers)
            {
                var inv = c.GetInventory();
                used += inv.CurrentVolume.RawValue;
                max += inv.MaxVolume.RawValue;
            }
            if (max <= 0) return "N/A";
            int pct = (int)Math.Round(100.0 * used / max);
            return pct + "%";
        }

        string GetCrewString()
        {
            var seats = new List<IMyCockpit>();
            GridTerminalSystem.GetBlocksOfType(seats);
            if (seats.Count == 0) return "No Cockpits";
            int occupied = 0;
            int total = 0;
            foreach (var s in seats)
            {
                total++;
                if (s.IsUnderControl) occupied++;
            }
            return $"{occupied}/{total}";
        }
    }
}
