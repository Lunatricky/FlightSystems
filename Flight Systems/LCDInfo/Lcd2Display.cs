using IngameScript.Domain;
using IngameScript.Enums;
using IngameScript.Physics;
using IngameScript.UseCases;
using IngameScript.Utils;
using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    class Lcd2Display
    {
        public static void Draw(
            List<IMyTextSurface> surfaces,
            IniContext ic,
            PhysicsContext pc,
            Command command,
            SystemBools sb)
        {
            if (surfaces == null || surfaces.Count == 0 || pc == null)
                return;

            Sprites spt = new Sprites(ic);

            if (pc.Gravity > 0)
            {
                Color color = new Color();
                if (pc.ClimbRate < 0)
                {
                    color = pc.GroundLevel < 2 * pc.StopYDist
                        ? Color.DarkRed : pc.GroundLevel < 4 * pc.StopYDist
                        ? Color.DarkOrange : new Color();
                }

                if (!color.Equals(new Color()))
                {
                    spt.AddB($"Ground: {pc.GroundLevelStr}", color);
                    spt.Add($"Rate of climb: {pc.ClimbRate:F1} m/s");
                    spt.AddB($"Stop Y: {pc.StopYDist:F1} m | {pc.TimeToStopY:F1} s", color);
                }
                else
                {
                    spt.Add($"Ground: {pc.GroundLevelStr}");
                    spt.Add($"Rate of climb: {pc.ClimbRate:F1} m/s");
                    spt.Add($"Stop Y: {pc.StopYDist:F1} m | {pc.TimeToStopY:F1} s");
                }
            }

            spt.Add($"Stop Z: {pc.StopZDist:F1} m | {pc.TimeToStopZ:F1} s");
            if (pc.Gravity > 0)
                spt.Add($"Accel: {pc.Accel.Length() / 9.81:F1} g | Grav: {pc.Gravity / 9.81:F2} g ");
            else
                spt.Add($"Accel: {pc.Accel.Length() / 9.81:F1} g");

            if (sb.GpsToggle)
            {
                spt.Add($"ETA: {UtilsHelpder.FormatTime(pc.TimeToDistanceSmoothed)}");
            }
            else if (command.State == MainState.Land || command.State == MainState.SBurn)
            {
                spt.Add("TTI: " + (pc.TimeToImpact == 0 ? "--" : $"{pc.TimeToImpact:F0}") + " s");
            }
            else
            {
                spt.Add($"Longitudinal v: {pc.ForwardVelocity:F1} m/s");
                spt.Add($"Lateral v: {pc.RightVelocity:F1} m/s");
                spt.Add($"Vertical v: {pc.UpVelocity:F1} m/s");
            }

            spt.DrawTo(surfaces);
        }
    }
}
