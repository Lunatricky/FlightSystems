using IngameScript.Domain;
using IngameScript.Physics;
using IngameScript.UseCases;
using IngameScript.Utils;
using Sandbox.ModAPI.Ingame;
using System.Text;

namespace IngameScript.LCDInfo
{
    class Text
    {
        readonly GridContext gc;
        readonly IniContext ic;
        readonly PhysicsContext pc;
        readonly Booleans b;
        readonly Command command;

        public Text(GridContext gc, IniContext ic, PhysicsContext pc, Booleans b, Command command)
        {
            this.gc = gc;
            this.ic = ic;
            this.pc = pc;
            this.b = b;
            this.command = command;
        }

        public StringBuilder ScriptInfo()
        {
            StringBuilder scriptInfo = new StringBuilder();
            ScriptInfoHeader(scriptInfo);
            scriptInfo.AppendLine("");
            ScriptInfoBlocks(ic, scriptInfo);
            return scriptInfo;
        }

        StringBuilder ScriptInfoHeader(StringBuilder scriptInfo)
        {
            scriptInfo.AppendLine(gc.GridName);
            scriptInfo.AppendLine(new string('-', 28));
            scriptInfo.Append("State: " + command.State);

            if (!string.IsNullOrEmpty(command.Param.Text))
                scriptInfo.Append(" - " + command.Param.Text);
            if (command.Param.Number != 0)
                scriptInfo.Append(" - " + command.Param.Number);
            if (command.Param.AutoLandState != AutoLandState.Idle)
                scriptInfo.Append(" - " + command.Param.AutoLandState);

            return scriptInfo;
        }

        StringBuilder ScriptInfoBlocks(IniContext ic, StringBuilder scriptInfo)
        {
            scriptInfo.AppendLine();
            scriptInfo.AppendLine("Toggles");
            scriptInfo.AppendLine(IniContext.FLIGHT_SYSTEMS + ": " + ic.AllowFlightSystems);
            scriptInfo.AppendLine(IniContext.LOW_FUEL_LAND + ": " + ic.AllowLowFuelLand);
            scriptInfo.AppendLine(IniContext.DOCK_MODE + ": " + ic.AllowDockMode);
            scriptInfo.AppendLine(IniContext.CONTROL_ANTENNAS + ": " + ic.ControlAntennas);
            scriptInfo.AppendLine(IniContext.RENAME_SUBGRIDS + ": " + ic.RenameSubgrids);
            scriptInfo.AppendLine(IniContext.PAINT_SURFACES + ": " + ic.PaintSurfaces);
            scriptInfo.AppendLine(IniContext.USE_SPRITES + ": " + ic.UseSprites);
            scriptInfo.AppendLine();
            scriptInfo.AppendLine("Blocks");
            scriptInfo.AppendLine("Controller: " + gc.Controller.CustomName);

            if (ic.AllowFlightSystems)
            {
                scriptInfo.AppendLine("Batteries: " + gc.Batteries.Count + " | Tanks: " + gc.Tanks.Count);
                scriptInfo.AppendLine("Forward thruster: " + gc.ForwardThrusters.Count);
                scriptInfo.AppendLine("Breaking thruster: " + gc.BreakingThrusters.Count);
                scriptInfo.AppendLine("Upward thruster: " + gc.UpwardThrusters.Count);
                scriptInfo.AppendLine("Gyros: " + gc.Gyros.Count);
            }

            if (ic.AllowDockMode || ic.AllowFlightSystems)
                scriptInfo.AppendLine("Gears: " + gc.Gears.Count);

            if (ic.AllowDockMode)
            {
                scriptInfo.AppendLine("Dock Mode blocks: " + gc.ControlledBlocks.Count);
            }

            scriptInfo.AppendLine("LCDs1: " + gc.Lcds1.Count);
            scriptInfo.AppendLine("LCDs2: " + gc.Lcds2.Count);

            return scriptInfo;
        }

        public void WriteInfo()
        {
            // Output
            StringBuilder stringBuilder = new StringBuilder();

            ScriptInfoHeader(stringBuilder);
            stringBuilder.AppendLine("\n");

            stringBuilder.AppendLine($"Mass: {pc.Mass.PhysicalMass / 1000:0.0} t");
            stringBuilder.AppendLine($"Empty Mass: {pc.Mass.BaseMass / 1000:0.0} t");

            stringBuilder.AppendLine($"H2: {pc.H2Cache.Percent:0}% - {pc.H2Cache.Time}");

            stringBuilder.AppendLine($"Bat:  {pc.BatCache.Filled / pc.BatCache.Capacity * 100:0}% - {pc.BatCache.Time}");

            foreach (IMyTextSurface lcd1 in gc.Lcds1)
                lcd1.WriteText(stringBuilder.ToString());
        }

        public void WriteInfo2()
        {
            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.AppendLine(gc.GridName);
            stringBuilder.AppendLine(new string('-', 28));

            if (pc.Gravity > 0)
            {
                stringBuilder.AppendLine($"Ground level : {pc.GroundLevel:F1} m");
                stringBuilder.AppendLine($"Rate of climb: {pc.ClimbRate:F1} m/s");
                stringBuilder.AppendLine($"Accel: {pc.Accel.Length() / 9.81:F1} g");
                stringBuilder.AppendLine($"Stop Y: {pc.StopYDist:F1} m | {pc.TimeToStopY:F1} s");
            }
            stringBuilder.AppendLine($"Stop Z: {pc.StopZDist:F1} m | {pc.TimeToStopZ:F1} s");

            if (b.autoPilotToggle)
            {
                stringBuilder.AppendLine($"\nETA: {UtilsHelpder.FormatTime(pc.TimeToDistanceSmoothed)}");
            }
            else if (command.State == MainState.Land || command.State == MainState.SBurn)
            {
                stringBuilder.AppendLine($"Gravity: {pc.Gravity:F1} m²/s");
                stringBuilder.AppendLine($"TTI: {pc.TimeToImpact:F1} s");
            }
            else
            {
                stringBuilder.AppendLine($"Longitudinal v: {pc.ForwardVelocity:F1} m/s");
                stringBuilder.AppendLine($"Lateral v: {pc.RightVelocity:F1} m/s");
                stringBuilder.AppendLine($"Vertical v: {pc.UpVelocity:F1} m/s");
            }

            stringBuilder.AppendLine();

            foreach (IMyTextSurface lcd2 in gc.Lcds2)
                lcd2.WriteText(stringBuilder.ToString());
        }
    }
}
