using IngameScript.UseCases;
using System;
using VRageMath;

namespace IngameScript.Utils
{
    class UtilsHelpder
    {

        public static Command ParseCommand(Command command, string argument)
        {
            var parts = argument.Trim().Split(
                new[] { ' ', '\t' },
                StringSplitOptions.RemoveEmptyEntries
            );

            if (parts.Length == 0)
                return command;

            // First word = command (lowercase)
            MainStateEnum cmd = TryParseArgument(parts[0].ToLowerInvariant());

            // No second part → no parameter
            if (parts.Length == 1)
                return new Command(cmd, CommandParam.Empty);

            // Second part: try number, then string
            string second = parts[1].Trim();
            string end = argument.Substring(parts[0].Length + 1);

            CommandParam param;
            double num;
            Vector3D gps;

            if (TryParseGPS(end, out gps))
            {
                param = new CommandParam(gps);
            }
            else if (double.TryParse(second, out num))
                param = new CommandParam(num);
            else
                param = new CommandParam(second.ToLowerInvariant());

            return new Command(cmd, param);
        }

        static MainStateEnum TryParseArgument(string input)
        {
            MainStateEnum mainStateEnum;
            try
            {
                mainStateEnum = (MainStateEnum)Enum.Parse(typeof(MainStateEnum), input, true);
            }
            catch
            {
                mainStateEnum = MainStateEnum.Abort;
            }
            return mainStateEnum;
        }

        // GPS parser for "GPS:name:X:Y:Z:color:" format
        static bool TryParseGPS(string gps, out Vector3D result)
        {
            result = new Vector3D();
            if (string.IsNullOrWhiteSpace(gps)) return false;
            if (!gps.StartsWith("GPS:")) return false;

            var parts = gps.Split(':');
            if (parts.Length < 6) return false;

            double x, y, z;
            if (!double.TryParse(parts[2], out x)) return false;
            if (!double.TryParse(parts[3], out y)) return false;
            if (!double.TryParse(parts[4], out z)) return false;

            result = new Vector3D(x, y, z);
            return true;
        }

        public static string FormatTime(double time)
        {
            if (double.IsInfinity(time) || time < 0)
                return "--";

            int intTime = (int)time;
            int days = intTime / 3600 / 24;
            int hours = (intTime % 24) / 3600;
            int minutes = (intTime % 3600) / 60;
            int seconds = intTime % 60;

            if (days > 0)
                return $"{days}d {hours}h {minutes}m";
            if (hours > 0)
                return $"{hours}h {minutes}m";
            if (minutes > 0)
                return $"{minutes}m {seconds}s";
            return $"{seconds}s";
        }
    }
}
