using IngameScript.Domain;
using IngameScript.Enums;
using IngameScript.Physics;
using IngameScript.UseCases;
using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;
using System.Text;
using VRageMath;

namespace IngameScript
{
    class Lcd1Display
    {
        static GridContext boundGc;
        static IniContext boundIc;
        static PhysicsContext boundPc;
        static Command boundCommand;
        static PlanetType boundPlanet;
        static double boundRadius;
        static SystemBools boundSb;
        static bool settingsOpen;
        static Sprites hud;
        static bool hudReady;

        public static void Bind(
            GridContext gc,
            IniContext ic,
            PhysicsContext pc,
            Command command,
            PlanetType planet,
            double planetRadius,
            SystemBools sb,
            bool settingsAreOpen)
        {
            boundGc = gc;
            boundIc = ic;
            boundPc = pc;
            boundCommand = command;
            boundPlanet = planet;
            boundRadius = planetRadius;
            boundSb = sb;
            settingsOpen = settingsAreOpen;
            hudReady = false;
        }

        public static void DrawHud(List<IMyTextSurface> surfaces1, List<IMyTextSurface> surfaces2)
        {
            Sprites lines = EnsureHud();
            if (lines == null)
                return;

            int n1 = surfaces1 == null ? 0 : surfaces1.Count;
            int n2 = surfaces2 == null ? 0 : surfaces2.Count;
            if (n1 == 0 && n2 == 0)
                return;

            if (surfaces1 != null)
            {
                for (int i = 0; i < surfaces1.Count; i++)
                {
                    IMyTextSurface surface = surfaces1[i];
                    if (Listed(surfaces2, surface))
                        continue;
                    DrawHudSurface(lines, surface, false);
                }
            }

            if (surfaces2 == null)
                return;

            for (int i = 0; i < surfaces2.Count; i++)
                DrawHudSurface(lines, surfaces2[i], true);
        }

        public static bool TryDrawAsThirdColumn(Sprites menu, IMyTextSurface panel)
        {
            if (!settingsOpen || menu == null || panel == null || boundGc == null)
                return false;
            if (!Listed(boundGc.Lcds1, panel) && !Listed(boundGc.Lcds2, panel))
                return false;
            if (!Listed(boundGc.LcdsSettings, panel))
                return false;

            Sprites lines = EnsureHud();
            if (lines == null)
                return false;
            return lines.TryDrawThird(panel, menu);
        }

        static void DrawHudSurface(Sprites lines, IMyTextSurface surface, bool fromLcd2List)
        {
            if (surface == null)
                return;
            if (settingsOpen && boundGc != null && Listed(boundGc.LcdsSettings, surface))
                return;

            lines.DrawFlightPanel(surface, fromLcd2List);
        }

        static Sprites EnsureHud()
        {
            if (boundIc == null || boundPc == null || boundGc == null || boundCommand == null)
                return null;
            if (hud == null)
                hud = new Sprites(boundIc);
            if (!hudReady)
            {
                hud.ClearStacks();
                Fill(hud, boundIc, boundGc, boundPc, boundCommand, boundPlanet, boundRadius);
                hud.UseStack(1);
                Lcd2Display.Fill(hud, boundIc, boundPc, boundCommand, boundSb);
                hud.UseStack(0);
                hudReady = true;
            }
            return hud;
        }

        static bool Listed(List<IMyTextSurface> list, IMyTextSurface surface)
        {
            if (list == null || surface == null)
                return false;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == surface)
                    return true;
            }
            return false;
        }

        public static void Fill(
            Sprites spt,
            IniContext ic,
            GridContext gc,
            PhysicsContext pc,
            Command command,
            PlanetType planet,
            double planetRadius)
        {
            if (spt == null || pc == null)
                return;

            spt.Add(gc.GridName ?? "");
            spt.Add("Type: " + EnumLabels.Ship(gc.ShipType));
            spt.Add($"Planet: {EnumLabels.PlanetKind(planet)} | {planetRadius / 1000:F0}km");

            StringBuilder state = new StringBuilder();
            state.Append("State: " + EnumLabels.State(command.State));

            if (command.Param.AutoLandState != AutoLandState.Idle)
                state.Append(" - " + EnumLabels.Land(command.Param.AutoLandState));
            else if (command.Param.Step != Step.Toggle)
                state.Append(" - " + EnumLabels.StepName(command.Param.Step));
            if (command.Param.AvoidPhase != TerrainAvoidPhase.Off)
                state.Append(" - Avoid");
            if (command.Param.Number != 0)
                state.Append(" - " + command.Param.Number);

            spt.Add(state.ToString());

            spt.Add($"Mass: {pc.Mass.PhysicalMass / 1000:0.0} t");
            spt.Add($"Empty Mass: {pc.Mass.BaseMass / 1000:0.0} t");

            if (pc.H2Cache.Capacity > 0)
                AddResource(spt, "H2", pc.H2Cache.Percent, pc.H2Cache.Time, pc.H2Cache.Rate, ic.MinimumAcceptedFuel);
            if (pc.BatCache.Capacity > 0)
                AddResource(spt, "Bat", pc.BatCache.Percent, pc.BatCache.Time, pc.BatCache.Rate, ic.MinimumAcceptedFuel);
        }

        static void AddResource(Sprites spt, string label, double percent, string time, double rate, double minFuel)
        {
            string line = $"{label}: {percent:0}% - {time}";
            Color color = ResourceColor(rate, percent, minFuel);
            if (!color.Equals(new Color()))
                spt.AddB(line, color);
            else
                spt.Add(line);
        }

        static Color ResourceColor(double rate, double percent, double minFuel)
        {
            if (rate > 0)
                return Color.LightBlue;
            if (percent < minFuel / 2)
                return Color.DarkRed;
            if (percent < minFuel)
                return Color.DarkOrange;
            return new Color();
        }
    }
}
