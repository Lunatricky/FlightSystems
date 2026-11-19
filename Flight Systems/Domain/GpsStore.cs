using System.Collections.Generic;
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

        void TryAddLine(string line)
        {
            if (string.IsNullOrEmpty(line))
                return;

            string[] parts = line.Split(':');
            if (parts.Length < 5 || parts[0] != "GPS")
                return;

            string name = parts[1].Trim();
            if (name.Length == 0)
                return;

            int x;
            int y;
            int z;
            if (!int.TryParse(parts[2].Trim(), out x))
                return;
            if (!int.TryParse(parts[3].Trim(), out y))
                return;
            if (!int.TryParse(parts[4].Trim(), out z))
                return;

            GpsWaypoint wp = new GpsWaypoint();
            wp.Name = name;
            wp.Position = new Vector3D(x, y, z);
            items.Add(wp);
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
