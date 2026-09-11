using VRageMath;

namespace IngameScript.Utils
{
    class UtilsHelpder
    {

        // GPS parser for "GPS:name:X:Y:Z:color:" format
        public static bool TryParseGPS(string gps, out Vector3D v)
        {
            v = new Vector3D();
            if (string.IsNullOrWhiteSpace(gps)) return false;
            if (!gps.StartsWith("GPS:")) return false;

            var parts = gps.Split(':');
            if (parts.Length < 6) return false;

            double x, y, z;
            if (!double.TryParse(parts[2], out x)) return false;
            if (!double.TryParse(parts[3], out y)) return false;
            if (!double.TryParse(parts[4], out z)) return false;

            v = new Vector3D(x, y, z);
            return true;
        }

        public static string GpsName(string gps)
        {
            if (string.IsNullOrWhiteSpace(gps) || !gps.StartsWith("GPS:"))
                return "";
            string[] parts = gps.Split(':');
            return parts.Length >= 2 ? parts[1] : "";
        }

        public static string FormatGps(string name, Vector3D pos)
        {
            if (string.IsNullOrEmpty(name))
                name = "GPS";
            return "GPS:" + name + ":" + pos.X + ":" + pos.Y + ":" + pos.Z + ":#FF75C9F1:";
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
