using VRageMath;

namespace IngameScript.Utils
{
    class UtilsHelpder
    {

        // GPS parser for "GPS:name:X:Y:Z:color:" format
        public static bool TryParseGPS(string gps, out string name, out Vector3D v)
        {
            name = "";
            v = new Vector3D();
            if (string.IsNullOrWhiteSpace(gps)) return false;
            if (!gps.StartsWith("GPS:")) return false;

            var parts = gps.Split(':');
            if (parts.Length < 6) return false;

            double x, y, z;
            if (!double.TryParse(parts[2], out x)) return false;
            if (!double.TryParse(parts[3], out y)) return false;
            if (!double.TryParse(parts[4], out z)) return false;

            name = parts[1];
            v = new Vector3D(x, y, z);
            return true;
        }

        public static bool TryParseGPS(string gps, out Vector3D v)
        {
            string name;
            return TryParseGPS(gps, out name, out v);
        }

        public static string GpsName(string gps)
        {
            string name;
            Vector3D v;
            return TryParseGPS(gps, out name, out v) ? name : "";
        }

        public static string FormatGps(string name, Vector3D pos)
        {
            if (string.IsNullOrEmpty(name))
                name = "GPS";
            return "GPS:" + name + ":" + pos.X + ":" + pos.Y + ":" + pos.Z + ":#FF75C9F1:";
        }

        public static string FormatFillEta(double filled, double capacity, double rate)
        {
            if (rate > 0)
                return FormatTime((capacity - filled) / rate) + " /\\";
            if (rate < 0)
                return FormatTime(filled / -rate) + " \\/";
            return "";
        }

        public static string FormatTime(double time)
        {
            if (double.IsInfinity(time) || time < 0)
                return "--";

            int intTime = (int)time;
            int days = intTime / 86400;
            int hours = (intTime / 3600) % 24;
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
