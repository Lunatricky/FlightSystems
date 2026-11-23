using IngameScript.Domain;
using IngameScript.Enums;
using IngameScript.Physics;
using IngameScript.UseCases;
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
            spt.Add2(Mass(pc.Mass.PhysicalMass), "E " + Mass(pc.Mass.BaseMass));

            if (pc.H2Cache.Capacity > 0)
                AddResource(spt, "H2", pc.H2Cache.Percent, pc.H2Cache.Time, pc.H2Cache.Rate, ic.MinimumAcceptedFuel);
            if (pc.BatCache.Capacity > 0)
                AddResource(spt, "Bat", pc.BatCache.Percent, pc.BatCache.Time, pc.BatCache.Rate, ic.MinimumAcceptedFuel);
        }

        static string Mass(double kg)
        {
            double t = kg / 1000.0;
            if (t < 1000)
                return string.Format("{0:0} t", t);
            if (t < 1000000)
                return string.Format("{0:0} Kt", t / 1000.0);
            return string.Format("{0:0} Mt", t / 1000000.0);
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
