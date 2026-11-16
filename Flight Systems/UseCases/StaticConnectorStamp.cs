using Sandbox.ModAPI.Ingame;
using System.Text;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript
{
    // Exact custom-data lines, plus the [Connector] Static flag visitors read.
    class StaticConnectorStamp
    {
        public const string PadLine = "[FS_pad]";
        const string FlightSection = "Flight Systems";
        const string ConnectorSection = "Connector";
        const string StaticKey = "Static";
        const string LegacyDockMe = "FS_DOCK=Me";
        const string LegacyDockOther = "FS_DOCK=Other";
        const string LegacyStatic = "FS_STATIC=1";

        static readonly MyIni parsed = new MyIni();
        static readonly StringBuilder built = new StringBuilder();

        public static void StripLegacyLines(IMyTerminalBlock block)
        {
            if (block == null || block.Closed)
                return;
            RemoveExactLineFrom(block, LegacyDockMe);
            RemoveExactLineFrom(block, LegacyDockOther);
            RemoveExactLineFrom(block, LegacyStatic);
        }

        // Own-grid connectors only. An arm on a rotor is left alone.
        public static void WriteStatic(IMyTerminalBlock block, bool on)
        {
            if (block == null || block.Closed)
                return;

            string data = block.CustomData ?? "";
            string next = TryWriteIni(data, on);
            if (next == null)
                next = WriteStaticInPlace(data, on);
            if (next != data)
                block.CustomData = next;
        }

        public static bool ReadsStatic(string data)
        {
            if (string.IsNullOrEmpty(data))
                return false;

            parsed.Clear();
            if (parsed.TryParse(data) && parsed.ContainsKey(ConnectorSection, StaticKey))
                return parsed.Get(ConnectorSection, StaticKey).ToBoolean(false);

            return ManualStatic(data);
        }

        static string TryWriteIni(string data, bool on)
        {
            parsed.Clear();
            if (!parsed.TryParse(data))
                return null;

            if (!parsed.ContainsSection(FlightSection))
                parsed.AddSection(FlightSection);
            parsed.Set(ConnectorSection, StaticKey, on ? "true" : "false");
            string written = parsed.ToString();
            // ToString drops lines that are not ini keys. [FS_pad] and the trigger line have to stay.
            if (DropsKeptLines(data, written))
                return null;
            return written;
        }

        static bool DropsKeptLines(string before, string after)
        {
            int i = 0;
            while (i <= before.Length)
            {
                int nl = before.IndexOf('\n', i);
                int end = nl < 0 ? before.Length : nl;
                int contentEnd = end;
                if (contentEnd > i && before[contentEnd - 1] == '\r')
                    contentEnd--;
                if (contentEnd > i)
                {
                    string line = before.Substring(i, contentEnd - i);
                    if (!IsStaticAssignment(line) && !HasExactLine(after, line))
                        return true;
                }
                if (nl < 0)
                    break;
                i = nl + 1;
            }
            return false;
        }

        static string WriteStaticInPlace(string data, bool on)
        {
            string assignment = on ? "Static=true" : "Static=false";
            built.Clear();
            bool started = false;
            bool inConnector = false;
            bool sawConnector = false;
            bool wroteStatic = false;
            int i = 0;
            while (i <= data.Length)
            {
                int nl = data.IndexOf('\n', i);
                int end = nl < 0 ? data.Length : nl;
                int contentEnd = end;
                if (contentEnd > i && data[contentEnd - 1] == '\r')
                    contentEnd--;
                // No trailing newline. Do not invent a blank line past the last one.
                if (contentEnd == i && nl < 0)
                    break;

                string line = data.Substring(i, contentEnd - i);
                string trimmed = line.Trim();
                bool section = trimmed.Length > 1 && trimmed[0] == '[' && trimmed[trimmed.Length - 1] == ']';
                if (section)
                {
                    if (inConnector && !wroteStatic)
                    {
                        AppendBuilt(assignment, ref started);
                        wroteStatic = true;
                    }
                    inConnector = trimmed == "[Connector]";
                    if (inConnector)
                        sawConnector = true;
                    AppendBuilt(line, ref started);
                }
                else if (inConnector && IsStaticAssignment(trimmed))
                {
                    AppendBuilt(assignment, ref started);
                    wroteStatic = true;
                }
                else
                    AppendBuilt(line, ref started);

                if (nl < 0)
                    break;
                i = nl + 1;
            }

            if (sawConnector && !wroteStatic)
                AppendBuilt(assignment, ref started);
            if (!sawConnector)
            {
                if (started)
                    built.Append('\n');
                built.Append("[Flight Systems]\n\n[Connector]\n");
                built.Append(assignment);
            }
            return built.ToString();
        }

        static void AppendBuilt(string line, ref bool started)
        {
            if (started)
                built.Append('\n');
            started = true;
            built.Append(line);
        }

        static bool ManualStatic(string data)
        {
            bool inConnector = false;
            int i = 0;
            while (i <= data.Length)
            {
                int nl = data.IndexOf('\n', i);
                int end = nl < 0 ? data.Length : nl;
                int contentEnd = end;
                if (contentEnd > i && data[contentEnd - 1] == '\r')
                    contentEnd--;
                string line = contentEnd > i ? data.Substring(i, contentEnd - i).Trim() : "";
                if (line.Length > 1 && line[0] == '[' && line[line.Length - 1] == ']')
                    inConnector = line == "[Connector]";
                else if (inConnector && IsStaticAssignment(line))
                    return StaticValueTrue(line);
                if (nl < 0)
                    break;
                i = nl + 1;
            }
            return false;
        }

        static bool IsStaticAssignment(string line)
        {
            if (string.IsNullOrEmpty(line) || line.Length < 7)
                return false;
            return line.StartsWith("Static=") || line.StartsWith("static=") || line.StartsWith("STATIC=");
        }

        static bool StaticValueTrue(string line)
        {
            int eq = line.IndexOf('=');
            if (eq < 0 || eq + 1 >= line.Length)
                return false;
            string value = line.Substring(eq + 1).Trim();
            return value == "true" || value == "True" || value == "TRUE";
        }

        static void RemoveExactLineFrom(IMyTerminalBlock block, string line)
        {
            string data = block.CustomData ?? "";
            if (!HasExactLine(data, line))
                return;
            string stripped = data;
            while (HasExactLine(stripped, line))
                stripped = RemoveExactLine(stripped, line);
            if (stripped != data)
                block.CustomData = stripped;
        }

        public static bool HasExactLine(string data, string line)
        {
            if (string.IsNullOrEmpty(data) || string.IsNullOrEmpty(line))
                return false;

            int i = 0;
            while (i <= data.Length)
            {
                int nl = data.IndexOf('\n', i);
                int end = nl < 0 ? data.Length : nl;
                int contentEnd = end;
                if (contentEnd > i && data[contentEnd - 1] == '\r')
                    contentEnd--;
                if (contentEnd - i == line.Length && data.Substring(i, line.Length) == line)
                    return true;
                if (nl < 0)
                    break;
                i = nl + 1;
            }
            return false;
        }

        public static string AppendExactLine(string data, string line)
        {
            if (string.IsNullOrEmpty(data))
                return line;
            if (data[data.Length - 1] == '\n')
                return data + line;
            return data + "\n" + line;
        }

        public static string RemoveExactLine(string data, string line)
        {
            if (string.IsNullOrEmpty(data) || !HasExactLine(data, line))
                return data;

            int i = 0;
            while (i <= data.Length)
            {
                int nl = data.IndexOf('\n', i);
                int end = nl < 0 ? data.Length : nl;
                int contentEnd = end;
                if (contentEnd > i && data[contentEnd - 1] == '\r')
                    contentEnd--;
                if (contentEnd - i == line.Length && data.Substring(i, line.Length) == line)
                {
                    if (nl < 0)
                    {
                        int cut = i;
                        if (cut > 0 && data[cut - 1] == '\n')
                        {
                            cut--;
                            if (cut > 0 && data[cut - 1] == '\r')
                                cut--;
                        }
                        return data.Substring(0, cut);
                    }
                    return data.Substring(0, i) + data.Substring(nl + 1);
                }
                if (nl < 0)
                    break;
                i = nl + 1;
            }
            return data;
        }
    }
}
