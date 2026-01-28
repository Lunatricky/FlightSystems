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
        // Info LCD Telemetry Script

        const string LCD_GROUP_NAME = "Info LCDs";

        List<IMyTextSurface> lcds = new List<IMyTextSurface>();
        List<IMyGasTank> h2Tanks = new List<IMyGasTank>();
        List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();

        IMyShipController controller;
        string gridName;

        Vector3D lastVelocity;
        double lastH2Fill = 0;
        bool firstRun = true;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            Reload();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (!string.IsNullOrWhiteSpace(argument) && argument.ToLower() == "reload")
                Reload();

            if (controller == null || lcds.Count == 0)
                return;

            WriteInfo();
        }

        void Reload()
        {
            lcds.Clear();
            h2Tanks.Clear();
            batteries.Clear();
            controller = null;

            gridName = Me.CubeGrid.CustomName;

            // LCDs
            var group = GridTerminalSystem.GetBlockGroupWithName(LCD_GROUP_NAME);
            if (group != null)
            {
                var blocks = new List<IMyTerminalBlock>();
                group.GetBlocks(blocks);

                foreach (var b in blocks)
                {
                    var p = b as IMyTextSurfaceProvider;
                    if (p == null) continue;

                    for (int i = 0; i < p.SurfaceCount; i++)
                    {
                        var s = p.GetSurface(i);
                        s.ContentType = ContentType.TEXT_AND_IMAGE;
                        s.Font = "DEBUG";
                        s.FontSize = 1.5f;
                        s.Alignment = TextAlignment.LEFT;
                        lcds.Add(s);
                    }
                }
            }

            // Controller
            var controllers = new List<IMyShipController>();
            GridTerminalSystem.GetBlocksOfType(controllers, c => c.CubeGrid == Me.CubeGrid);
            if (controllers.Count > 0)
                controller = controllers[0];

            // Tanks & batteries
            GridTerminalSystem.GetBlocksOfType(h2Tanks, t => t.CubeGrid == Me.CubeGrid);
            GridTerminalSystem.GetBlocksOfType(batteries, b => b.CubeGrid == Me.CubeGrid);

            firstRun = true;
        }

        void WriteInfo()
        {
            var sb = new StringBuilder();

            // Altitude
            string seaAltText = "No gravity";
            string groundAltText = "No gravity";

            double sea, ground;
            if (controller.GetNaturalGravity().LengthSquared() > 1e-3)
            {
                if (controller.TryGetPlanetElevation(MyPlanetElevation.Sealevel, out sea))
                    seaAltText = $"{sea:0} m";

                if (controller.TryGetPlanetElevation(MyPlanetElevation.Surface, out ground))
                    groundAltText = $"{ground:0} m";
            }

            // Velocity & acceleration
            Vector3D velocity = controller.GetShipVelocities().LinearVelocity;
            Vector3D accel = Vector3D.Zero;

            if (!firstRun)
                accel = (velocity - lastVelocity) / Runtime.TimeSinceLastRun.TotalSeconds;

            lastVelocity = velocity;
            firstRun = false;

            // Mass
            var mass = controller.CalculateShipMass();

            // Hydrogen
            double h2Cap = 0, h2Fill = 0;
            foreach (var t in h2Tanks)
            {
                h2Cap += t.Capacity;
                h2Fill += t.Capacity * t.FilledRatio;
            }

            double h2Rate = (h2Fill - lastH2Fill) / Runtime.TimeSinceLastRun.TotalSeconds;
            lastH2Fill = h2Fill;

            string h2Time = "--";
            if (Math.Abs(h2Rate) > 1e-6)
            {
                if (h2Rate < 0)
                    h2Time = FormatTime(h2Fill / -h2Rate);
                else
                    h2Time = FormatTime((h2Cap - h2Fill) / h2Rate);
            }

            // Batteries
            double batCap = 0, batStored = 0;
            double batIn = 0, batOut = 0;

            foreach (var b in batteries)
            {
                batCap += b.MaxStoredPower;
                batStored += b.CurrentStoredPower;
                batIn += b.CurrentInput;
                batOut += b.CurrentOutput;
            }

            double netPower = batIn - batOut;
            string batTime = "--";

            if (Math.Abs(netPower) > 0.01)
            {
                if (netPower > 0)
                    batTime = FormatTime((batCap - batStored) / netPower);
                else
                    batTime = FormatTime(batStored / -netPower);
            }

            // Output
            sb.AppendLine(gridName);
            sb.AppendLine(new string('-', 28));

            sb.AppendLine($"Alt (Sea):    {seaAltText}");
            sb.AppendLine($"Alt (Ground): {groundAltText}");
            sb.AppendLine($"Accel {accel.Length()/9.81:F2} g");


            sb.AppendLine($"Mass:       {mass.PhysicalMass / 1000:0.0} t");
            sb.AppendLine($"Empty Mass: {mass.BaseMass / 1000:0.0} t");

            if (h2Cap > 0)
            {
                sb.AppendLine($"H2: {h2Fill / h2Cap * 100:0}%");
                sb.AppendLine($"H2 Time: {h2Time}");
            }

            if (batCap > 0)
            {
                sb.AppendLine($"Battery: {batStored / batCap * 100:0}%");
                sb.AppendLine($"Bat Time: {batTime}");
            }

            string text = sb.ToString();
            foreach (var lcd in lcds)
                lcd.WriteText(text);
        }

        string FormatTime(double seconds)
        {
            if (double.IsInfinity(seconds) || seconds < 0)
                return "--";

            int s = (int)seconds;
            int h = s / 3600;
            int m = (s % 3600) / 60;
            int sec = s % 60;

            if (h > 0)
                return $"{h}h {m}m";
            if (m > 0)
                return $"{m}m {sec}s";
            return $"{sec}s";
        }


    }
}
