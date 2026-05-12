using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRageMath;

namespace IngameScript.UseCases
{
    class Command
    {
        public MainStateEnum State { get; set; }
        public CommandParam Param { get; set; }

        public Command(MainStateEnum cmd, CommandParam p)
        {
            if (Enum.IsDefined(typeof(MainStateEnum), cmd)) State = cmd;
            Param = p;
        }

        public static Command Empty => new Command(MainStateEnum.Idle, CommandParam.Empty);


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

            // No second part → no parameter
            if (parts.Length == 1)
                return;

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

        private MainStateEnum TryParseArgument(string input)
        {
            try
            {
                State = (MainStateEnum)Enum.Parse(typeof(MainStateEnum), input, true);
            }
            catch
            {
                State = MainStateEnum.Abort;
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
