using IngameScript.Domain;
using IngameScript.Enums;
using IngameScript.Physics;
using IngameScript.UseCases;
using IngameScript.Utils;
using System;
using VRageMath;

namespace IngameScript
{
    class Lcd2Display
    {
        public static void Fill(
            Sprites spt,
            PhysicsContext pc,
            Command command,
            SystemBools sb)
        {
            if (spt == null || pc == null)
                return;

            spt.Clear();
            bool grav = pc.Gravity > 0;
            if (grav)
            {
                Color color = new Color();
                if (pc.ClimbRate < 0)
                {
                    color = pc.GroundLevel < 2 * pc.StopYDist
                        ? Color.DarkRed : pc.GroundLevel < 4 * pc.StopYDist
                        ? Color.DarkOrange : new Color();
                }
                bool warn = !color.Equals(new Color());
                string ground = string.Format("G {0}", Dist(pc.GroundLevel));
                string sea = string.Format("Sea {0}", Dist(pc.SeaLevel));
                string climb = string.Format("Climb {0:0}", pc.ClimbRate);
                string stopY = string.Format("StopY {0}  {1:0} s", Dist(pc.StopYDist), pc.TimeToStopY);
                if (warn)
                {
                    spt.Add2B(ground, sea, color);
                    spt.Add2B(climb, stopY, color);
                }
                else
                {
                    spt.Add2(ground, sea);
                    spt.Add2(climb, stopY);
                }
            }

            spt.Add(string.Format("StopZ {0}  {1:0} s", Dist(pc.StopZDist), pc.TimeToStopZ));
            if (grav)
                spt.Add2(
                    string.Format("{0:0.0} g", pc.Accel.Length() / 9.81),
                    string.Format("Grav {0:0.00}", pc.Gravity / 9.81));
            else
                spt.Add(string.Format("{0:0.0} g", pc.Accel.Length() / 9.81));
            spt.Add3(
                string.Format("L {0:0}", pc.ForwardVelocity),
                string.Format("Lat {0:0}", pc.RightVelocity),
                string.Format("V {0:0}", pc.UpVelocity));

            if (sb.GpsToggle)
                spt.Add("ETA " + UtilsHelpder.FormatTime(pc.TimeToDistanceSmoothed));
            else if (command.State == MainState.Land || command.State == MainState.SBurn)
                spt.Add(pc.TimeToImpact == 0 ? "TTI --" : string.Format("TTI {0:0} s", pc.TimeToImpact));
        }

        static string Dist(double meters)
        {
            if (meters < 0)
                return "-" + Dist(-meters);
            if (meters < 1000)
                return string.Format("{0:0} m", meters);
            long tenths = (long)Math.Round(meters / 100.0);
            long whole = tenths / 10;
            if (tenths % 10 == 0)
                return string.Format("{0:0} km", whole);
            return string.Format("{0:0}.{1} km", whole, tenths % 10);
        }
    }
}
