using IngameScript.Domain;
using IngameScript.Enums;
using IngameScript.Physics;
using IngameScript.UseCases;
using System;
using System.Text;
using VRageMath;

namespace IngameScript
{
    class Lcd1Display
    {
        static readonly StringBuilder stateLine = new StringBuilder();

        public static void Fill(
            Sprites spt,
            IniContext ic,
            GridContext gc,
            PhysicsContext pc,
            Command command)
        {
            if (spt == null || pc == null)
                return;

            spt.Clear();
            spt.Add(gc.GridName ?? "");

            stateLine.Clear();
            stateLine.Append("State: ");
            stateLine.Append(EnumLabels.State(command.State));

            if (command.Param.AutoLandState != AutoLandState.Idle)
            {
                stateLine.Append(" - ");
                stateLine.Append(EnumLabels.Land(command.Param.AutoLandState));
            }
            else if (command.Param.Step != Step.Toggle)
            {
                stateLine.Append(" - ");
                stateLine.Append(EnumLabels.StepName(command.Param.Step));
            }
            if (command.Param.Number != 0)
            {
                stateLine.Append(" - ");
                stateLine.Append(command.Param.Number);
            }

            spt.Add(stateLine.ToString());

            spt.Add2(Mass(pc.Mass.PhysicalMass), "E" + Mass(pc.Mass.BaseMass));

            if (pc.H2Cache.Capacity > 0)
                AddResource(spt, ic, "H2: ", pc.H2Cache.Percent, pc.H2Cache.Time, pc.H2Cache.Rate);
            if (pc.BatCache.Capacity > 0)
                AddResource(spt, ic, "Bat: ", pc.BatCache.Percent, pc.BatCache.Time, pc.BatCache.Rate);
        }

        static string Mass(double kg)
        {
            double t = kg / 1000.0;
            if (t < 1000)
                return string.Format("M: {0:0} t", t);
            if (t < 1000000)
                return string.Format("M: {0:0} Kt", t / 1000.0);
            return string.Format("M: {0:0} Mt", t / 1000000.0);
        }

        static void AddResource(Sprites spt, IniContext ic, string label, double percent, string time, double rate)
        {
            if (double.IsNaN(percent) || percent < 0)
                percent = 0;
            if (percent > 100)
                percent = 100;
            if (ic.TextPanels)
            {
                spt.Add(string.Format("{0,-3}  {1}", label, time));
                int n = (int)Math.Round(percent / 100.0 * 8.0);
                if (n < 0)
                    n = 0;
                if (n > 8)
                    n = 8;
                spt.Add("||||||||        ".Substring(8 - n, 8));
                return;
            }

            spt.Add(label + time ?? "");
            spt.AddBar(percent, BarColor(rate, percent, ic.SpriteBackgroundColor, ic.SpriteFontColor));
        }

        static Color BarColor(double rate, double percent, Color background, Color font)
        {
            if (rate > 0)
                return Color.Blue;
            if (percent >= 25)
                return font;
            if (percent >= 10)
                return Color.Orange;
            if (percent > 5)
                return Color.Red;
            if (background.R == 0 && background.G == 0 && background.B == 0)
                return new Color(15, 15, 15); ;
            return Color.Black;
        }
    }
}
