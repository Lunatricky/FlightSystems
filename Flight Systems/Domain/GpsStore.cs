using System;
using System.Collections.Generic;
using System.Text;
using VRageMath;

namespace IngameScript.Domain
{
    class GpsStore
    {
        public const string LibrarySection = "GPS Library";
        public const string StartMark = "GPS start";
        public const string EndMark = "GPS end";
        const string LibraryHeader = "[GPS Library]";

        readonly List<GpsWaypoint> items = new List<GpsWaypoint>();

        public int Count => items.Count;

        public GpsWaypoint Get(int index)
        {
            if (index < 0 || index >= items.Count)
                return null;
            return items[index];
        }

        public void Clear()
        {
            items.Clear();
        }

        public void LoadText(string text)
        {
            items.Clear();
            if (string.IsNullOrEmpty(text))
                return;

            int header = FindLine(text, 0, LibraryHeader);
            if (header < 0)
                return;

            int i = LinePast(text, header);
            while (i < text.Length)
            {
                int contentEnd;
                string newline;
                string raw = TakeLine(text, ref i, out contentEnd, out newline);
                string trimmed = raw.Trim();
                if (IsSectionHeader(trimmed))
                    break;

                string key;
                string value;
                int number;
                if (!TryNumberKey(trimmed, out key, out value, out number))
                    continue;

                string name;
                double x;
                double y;
                double z;
                if (!TryParsePoint(value, out name, out x, out y, out z))
                    continue;

                GpsWaypoint wp = new GpsWaypoint();
                wp.Name = name;
                wp.Position = new Vector3D(x, y, z);
                wp.Key = number;
                InsertSorted(wp);
            }
        }

        public static string AddPoint(string text, string gpsLine)
        {
            if (gpsLine == null || gpsLine.Length == 0)
                return null;
            if (text == null)
                text = "";
            if (FindLine(text, 0, LibraryHeader) < 0)
                return AppendLibrary(text, gpsLine);

            int maxKey;
            int numbered;
            bool haveCount;
            if (!ScanLibrary(text, 0, out maxKey, out numbered, out haveCount))
                return null;
            return WriteLibrary(text, haveCount, numbered + 1, maxKey + 1, gpsLine, 0);
        }

        public static string RemoveKey(string text, int keyNumber)
        {
            if (string.IsNullOrEmpty(text) || keyNumber <= 0)
                return null;
            if (FindLine(text, 0, LibraryHeader) < 0)
                return null;

            int maxKey;
            int numbered;
            bool haveCount;
            if (!ScanLibrary(text, keyNumber, out maxKey, out numbered, out haveCount))
                return null;
            return WriteLibrary(text, haveCount, numbered - 1, 0, null, keyNumber);
        }

        public static bool TryMarkerBlock(string text, out string block)
        {
            block = StartMark + "\n" + EndMark + "\n";
            if (string.IsNullOrEmpty(text))
                return true;

            int start = FindLine(text, 0, StartMark);
            if (start < 0)
                return true;

            int end = FindLine(text, LinePast(text, start), EndMark);
            if (end < 0)
            {
                block = null;
                return false;
            }

            int stop = LinePast(text, end);
            block = text.Substring(start, stop - start);
            return true;
        }

        // GPS lines are not ini. A missing end still drops from GPS start through the rest of the text.
        public static string WithoutMarker(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            int start = FindLine(text, 0, StartMark);
            if (start < 0)
                return text;

            int end = FindLine(text, LinePast(text, start), EndMark);
            int stop = end < 0 ? text.Length : LinePast(text, end);
            return text.Substring(0, start) + text.Substring(stop);
        }

        public bool Near(Vector3D pos)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (Vector3D.DistanceSquared(items[i].Position, pos) < 1)
                    return true;
            }
            return false;
        }

        public string NextMarkName()
        {
            if (!HasName("New GPS"))
                return "New GPS";
            int n = 2;
            while (HasName("New GPS " + n))
                n++;
            return "New GPS " + n;
        }

        public static int NearestMeter(double value)
        {
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        static string TakeLine(string text, ref int i, out int contentEnd, out string newline)
        {
            contentEnd = i;
            while (contentEnd < text.Length && text[contentEnd] != '\n' && text[contentEnd] != '\r')
                contentEnd++;
            string raw = text.Substring(i, contentEnd - i);
            int n = contentEnd;
            if (n < text.Length && text[n] == '\r')
                n++;
            if (n < text.Length && text[n] == '\n')
                n++;
            newline = text.Substring(contentEnd, n - contentEnd);
            i = n;
            return raw;
        }

        static bool ScanLibrary(string text, int mustFind, out int maxKey, out int numbered, out bool haveCount)
        {
            maxKey = 0;
            numbered = 0;
            haveCount = false;
            bool found = mustFind <= 0;
            int header = FindLine(text, 0, LibraryHeader);
            if (header < 0)
                return false;

            int i = LinePast(text, header);
            while (i < text.Length)
            {
                int contentEnd;
                string newline;
                string raw = TakeLine(text, ref i, out contentEnd, out newline);
                string trimmed = raw.Trim();
                if (IsSectionHeader(trimmed))
                    break;

                string key;
                string value;
                if (!SplitKey(trimmed, out key, out value))
                    continue;
                if (IsCount(key))
                {
                    haveCount = true;
                    continue;
                }

                int number;
                if (!int.TryParse(key, out number) || number <= 0)
                    continue;
                numbered++;
                if (number > maxKey)
                    maxKey = number;
                if (number == mustFind)
                    found = true;
            }
            return found;
        }

        static string WriteLibrary(string text, bool haveCount, int newCount, int newKey, string gpsLine, int dropKey)
        {
            StringBuilder sb = new StringBuilder(text.Length + 48);
            bool inSection = false;
            bool dropped = dropKey <= 0;
            bool wrotePoint = gpsLine == null;
            int i = 0;
            while (i < text.Length)
            {
                int contentEnd;
                string newline;
                string raw = TakeLine(text, ref i, out contentEnd, out newline);
                string trimmed = raw.Trim();
                if (!inSection)
                {
                    sb.Append(raw);
                    sb.Append(newline);
                    if (trimmed == LibraryHeader)
                    {
                        inSection = true;
                        if (!haveCount)
                        {
                            sb.Append("Count=");
                            sb.Append(newCount);
                            sb.Append('\n');
                        }
                    }
                    continue;
                }

                if (IsSectionHeader(trimmed))
                {
                    AppendPoint(sb, newKey, gpsLine, ref wrotePoint);
                    if (!dropped || !wrotePoint)
                        return null;
                    sb.Append(raw);
                    sb.Append(newline);
                    if (i < text.Length)
                        sb.Append(text, i, text.Length - i);
                    return sb.ToString();
                }

                string key;
                string value;
                if (SplitKey(trimmed, out key, out value) && IsCount(key))
                {
                    sb.Append("Count=");
                    sb.Append(newCount);
                    if (newline.Length == 0)
                        sb.Append('\n');
                    else
                        sb.Append(newline);
                    continue;
                }

                int number;
                if (!dropped && SplitKey(trimmed, out key, out value) && int.TryParse(key, out number) && number == dropKey)
                {
                    dropped = true;
                    continue;
                }

                sb.Append(raw);
                sb.Append(newline);
            }

            if (inSection)
                AppendPoint(sb, newKey, gpsLine, ref wrotePoint);
            if (!dropped || !wrotePoint)
                return null;
            return sb.ToString();
        }

        static string AppendLibrary(string text, string gpsLine)
        {
            StringBuilder sb = new StringBuilder(text.Length + gpsLine.Length + 32);
            if (text.Length > 0)
            {
                sb.Append(text);
                if (text[text.Length - 1] != '\n')
                    sb.Append('\n');
            }
            sb.Append(LibraryHeader);
            sb.Append("\nCount=1\n1=");
            sb.Append(gpsLine);
            sb.Append('\n');
            return sb.ToString();
        }

        static void AppendPoint(StringBuilder sb, int newKey, string gpsLine, ref bool wrotePoint)
        {
            if (wrotePoint || gpsLine == null)
                return;
            sb.Append(newKey);
            sb.Append('=');
            sb.Append(gpsLine);
            sb.Append('\n');
            wrotePoint = true;
        }

        static bool TryParsePoint(string line, out string name, out double x, out double y, out double z)
        {
            name = "";
            x = 0;
            y = 0;
            z = 0;
            if (string.IsNullOrEmpty(line))
                return false;

            string[] parts = line.Split(':');
            if (parts.Length < 5 || parts[0] != "GPS")
                return false;

            name = parts[1].Trim();
            if (name.Length == 0)
                return false;
            if (!double.TryParse(parts[2].Trim(), out x))
                return false;
            if (!double.TryParse(parts[3].Trim(), out y))
                return false;
            if (!double.TryParse(parts[4].Trim(), out z))
                return false;
            return true;
        }

        static bool SplitKey(string trimmed, out string key, out string value)
        {
            key = "";
            value = "";
            int eq = trimmed.IndexOf('=');
            if (eq <= 0)
                return false;
            key = trimmed.Substring(0, eq).Trim();
            value = trimmed.Substring(eq + 1).Trim();
            return key.Length > 0;
        }

        static bool TryNumberKey(string trimmed, out string key, out string value, out int number)
        {
            key = "";
            value = "";
            number = 0;
            if (!SplitKey(trimmed, out key, out value))
                return false;
            if (IsCount(key))
                return false;
            return int.TryParse(key, out number) && number > 0;
        }

        static bool IsCount(string key)
        {
            return string.Equals(key, "Count", StringComparison.OrdinalIgnoreCase);
        }

        static bool IsSectionHeader(string trimmed)
        {
            return trimmed.Length >= 2 && trimmed[0] == '[' && trimmed[trimmed.Length - 1] == ']';
        }

        void InsertSorted(GpsWaypoint wp)
        {
            int at = items.Count;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Key > wp.Key)
                {
                    at = i;
                    break;
                }
            }
            items.Insert(at, wp);
        }

        bool HasName(string name)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Name == name)
                    return true;
            }
            return false;
        }

        static int FindLine(string text, int from, string marker)
        {
            int i = from;
            if (i < 0)
                i = 0;
            while (i < text.Length)
            {
                int contentEnd = i;
                while (contentEnd < text.Length && text[contentEnd] != '\n' && text[contentEnd] != '\r')
                    contentEnd++;
                if (LineEquals(text, i, contentEnd, marker))
                    return i;
                i = contentEnd;
                if (i < text.Length && text[i] == '\r')
                    i++;
                if (i < text.Length && text[i] == '\n')
                    i++;
            }
            return -1;
        }

        static bool LineEquals(string text, int start, int end, string marker)
        {
            while (start < end && text[start] == ' ')
                start++;
            while (end > start && text[end - 1] == ' ')
                end--;
            if (end - start != marker.Length)
                return false;
            for (int i = 0; i < marker.Length; i++)
            {
                if (text[start + i] != marker[i])
                    return false;
            }
            return true;
        }

        static int LinePast(string text, int lineStart)
        {
            int i = lineStart;
            while (i < text.Length && text[i] != '\n' && text[i] != '\r')
                i++;
            if (i < text.Length && text[i] == '\r')
                i++;
            if (i < text.Length && text[i] == '\n')
                i++;
            return i;
        }
    }
}
