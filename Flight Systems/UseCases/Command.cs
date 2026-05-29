using System;
using VRageMath;

namespace IngameScript.UseCases
{
    class Command
    {
        public MainState State { get; set; }
        public CommandParam Param { get; set; }

        public Command(MainState cmd, CommandParam p)
        {
            if (Enum.IsDefined(typeof(MainState), cmd)) State = cmd;
            Param = p;
        }

        public static Command Empty => new Command(MainState.Idle, CommandParam.Empty);


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

            if (TryParseGPS(end))
                return;
            
            if (double.TryParse(second, out num))
                Param = new CommandParam(num);
            else
                Param = new CommandParam(second.ToLowerInvariant());
        }

        MainState TryParseArgument(string input)
        {
            try
            {
                State = (MainState)Enum.Parse(typeof(MainState), input, true);
            }
            catch
            {
                State = MainState.Abort;
            }
            return State;
        }

        // GPS parser for "GPS:name:X:Y:Z:color:" format
        bool TryParseGPS(string gps)
        {
            Param.TargetCoordinates = new Vector3D();
            if (string.IsNullOrWhiteSpace(gps)) return false;
            if (!gps.StartsWith("GPS:")) return false;

            var parts = gps.Split(':');
            if (parts.Length < 6) return false;

            double x, y, z;
            if (!double.TryParse(parts[2], out x)) return false;
            if (!double.TryParse(parts[3], out y)) return false;
            if (!double.TryParse(parts[4], out z)) return false;

            Param.TargetCoordinates = new Vector3D(x, y, z);
            return true;
        }
    }
}
