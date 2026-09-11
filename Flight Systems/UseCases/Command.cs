using IngameScript.Enums;
using IngameScript.Utils;
using System;
using VRageMath;

namespace IngameScript.UseCases
{
    class Command
    {
        public MainState State { get; set; }
        public CommandParam Param { get; set; }

        public void Empty(MainState ms = MainState.Idle)
        {
            State = ms;
            Param.Empty();
            Param.Step = Step.Toggle;
        }

        public Command()
        {
            State = MainState.Idle;
            Param = new CommandParam();
        }

        public Command(string argument)
        {
            var parts = argument.Trim().Split(
                new[] { ' ', '\t' },
                StringSplitOptions.RemoveEmptyEntries
            );

            if (parts.Length == 0)
                return;

            // First word = command (lowercase)
            State = TryParseArgument(parts[0].ToLowerInvariant());
            Param = new CommandParam();

            // No second part → no parameter
            if (parts.Length == 1) return;

            // Second part: try number, then string
            string second = parts[1].Trim();
            string end = argument.Substring(parts[0].Length + 1);

            double num;

            Vector3D v = new Vector3D();
            if (UtilsHelpder.TryParseGPS(end, out v))
            {
                Param.TargetCoordinates = v;
                return;
            }

            if (double.TryParse(second, out num))
                Param = new CommandParam(num);
            else
                Param = new CommandParam(TryParseStep(second));
        }

        static MainState TryParseArgument(string input)
        {
            if (input == "abort") return MainState.Abort;
            if (input == "reload") return MainState.Reload;
            if (input == "idle") return MainState.Idle;
            if (input == "cruise") return MainState.Cruise;
            if (input == "orbit") return MainState.Orbit;
            if (input == "glide") return MainState.Glide;
            if (input == "cnav") return MainState.CNav;
            if (input == "land") return MainState.Land;
            if (input == "sburn") return MainState.SBurn;
            if (input == "gps") return MainState.Gps;
            return MainState.Abort;
        }

        static Step TryParseStep(string input)
        {
            string s = input.ToLowerInvariant();
            if (s == "toggle") return Step.Toggle;
            if (s == "on") return Step.On;
            if (s == "off") return Step.Off;
            if (s == "aimtogps") return Step.AimToGPS;
            if (s == "cruise") return Step.Cruise;
            if (s == "preclimb") return Step.Preclimb;
            if (s == "climb") return Step.Climb;
            if (s == "orbit") return Step.Orbit;
            return Step.Off;
        }
    }
}
