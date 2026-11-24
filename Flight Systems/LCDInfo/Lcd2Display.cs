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
            SystemBools sb, 
            bool isDocked)
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

                if (grav)
                    spt.Add2(
                        string.Format("g: {0:0.0}", pc.Accel.Length() / 9.81),
                        string.Format("Pg: {0:0.00}", pc.Gravity / 9.81));
                else
                    spt.Add(string.Format("g: {0:0.0}", pc.Accel.Length() / 9.81));

                bool warn = !color.Equals(new Color());
                string ground = string.Format("Grnd: {0}", Dist(pc.GroundLevel));
                string sea = string.Format("Sea: {0}", Dist(pc.SeaLevel));
                string climb = string.Format("Climb: {0:0}", pc.ClimbRate);
                string stopY = string.Format("StopY: {0} m {1:0} s", Dist(pc.StopYDist), pc.TimeToStopY);
                if (warn)
                {
                    spt.Add2B(ground, sea, color);
                    spt.AddB(climb, color);
                    spt.AddB(stopY, color);
                }
                else
                {
                    spt.Add2(ground, sea);
                    spt.Add(climb);
                    spt.Add(stopY);
                }
            }

            spt.Add(string.Format("StopZ: {0} m {1:0} s", Dist(pc.StopZDist), pc.TimeToStopZ));

            spt.Add3(string.Format("X: {0:0} m/s", pc.ForwardVelocity), string.Format("Z: {0:0} m/s", pc.RightVelocity), string.Format("Y: {0:0} m/s", pc.UpVelocity));

            if (sb.GpsToggle)
                spt.Add("ETA: " + UtilsHelpder.FormatTime(pc.TimeToDistanceSmoothed));
            else if (command.State == MainState.Land)
                spt.Add(pc.TimeToImpact == 0 ? "TTI: --" : string.Format("TTI: {0:0} s", pc.TimeToImpact));
        }

        static string Dist(double meters)
        {
            if (meters == double.PositiveInfinity)
                return "--";
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
