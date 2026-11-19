using System;
using System.Collections.Generic;
using System.Text;
using VRageMath;

namespace IngameScript.Domain
{
    class GpsStore
    {
        public const string StartMark = "GPS start";
        public const string EndMark = "GPS end";

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

        // False means GPS start is present and GPS end is not. The caller must not write custom data.
        public bool LoadBlock(string customData)
        {
            items.Clear();
            string block;
            if (!TryMarkerBlock(customData, out block))
                return false;
            ReadBetween(block);
            return true;
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

        void ReadBetween(string block)
        {
            if (string.IsNullOrEmpty(block))
                return;

            bool inside = false;
            int i = 0;
            while (i < block.Length)
            {
                int contentEnd = i;
                while (contentEnd < block.Length && block[contentEnd] != '\n' && block[contentEnd] != '\r')
                    contentEnd++;

                string line = block.Substring(i, contentEnd - i).Trim();
                if (!inside)
                {
                    if (line == StartMark)
                        inside = true;
                }
                else if (line == EndMark)
                    break;
                else
                    TryAddLine(line);

                i = contentEnd;
                if (i < block.Length && block[i] == '\r')
                    i++;
                if (i < block.Length && block[i] == '\n')
                    i++;
            }
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

        public static string WithPoint(string block, string line)
        {
            if (string.IsNullOrEmpty(block))
                return null;
            StringBuilder kept = new StringBuilder();
            int i = 0;
            while (i < block.Length)
            {
                int contentEnd;
                string newline;
                string raw = TakeLine(block, ref i, out contentEnd, out newline);
                if (raw.Trim() == EndMark)
                {
                    kept.Append(line);
                    kept.Append('\n');
                    kept.Append(raw);
                    kept.Append(newline);
                    if (i < block.Length)
                        kept.Append(block, i, block.Length - i);
                    return kept.ToString();
                }
                kept.Append(raw);
                kept.Append(newline);
            }
            return null;
        }

        public static string WithoutPoint(string block, int pointIndex)
        {
            if (string.IsNullOrEmpty(block) || pointIndex < 0)
                return null;
            StringBuilder kept = new StringBuilder();
            bool inside = false;
            int seen = 0;
            bool removed = false;
            int i = 0;
            while (i < block.Length)
            {
                int contentEnd;
                string newline;
                string raw = TakeLine(block, ref i, out contentEnd, out newline);
                string trimmed = raw.Trim();
                bool drop = false;
                if (!inside)
                {
                    if (trimmed == StartMark)
                        inside = true;
                }
                else if (trimmed == EndMark)
                    inside = false;
                else
                {
                    string name;
                    int x;
                    int y;
                    int z;
                    if (TryParsePoint(trimmed, out name, out x, out y, out z))
                    {
                        if (seen == pointIndex)
                        {
                            drop = true;
                            removed = true;
                        }
                        seen++;
                    }
                }
                if (!drop)
                {
                    kept.Append(raw);
                    kept.Append(newline);
                }
            }
            if (!removed)
                return null;
            return kept.ToString();
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

        void TryAddLine(string line)
        {
            string name;
            int x;
            int y;
            int z;
            if (!TryParsePoint(line, out name, out x, out y, out z))
                return;
            GpsWaypoint wp = new GpsWaypoint();
            wp.Name = name;
            wp.Position = new Vector3D(x, y, z);
            items.Add(wp);
        }

        static bool TryParsePoint(string line, out string name, out int x, out int y, out int z)
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
            if (!int.TryParse(parts[2].Trim(), out x))
                return false;
            if (!int.TryParse(parts[3].Trim(), out y))
                return false;
            if (!int.TryParse(parts[4].Trim(), out z))
                return false;
            return true;
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
