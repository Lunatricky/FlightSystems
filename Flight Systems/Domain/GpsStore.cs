using IngameScript.Enums;
using IngameScript.Utils;
using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace IngameScript.Domain
{
    class GpsStore
    {
        public const string Section = "GPS";

        readonly List<GpsWaypoint> items = new List<GpsWaypoint>();

        public List<GpsWaypoint> Items => items;
        public int Count => items.Count;

        public GpsWaypoint Get(int index)
        {
            if (index < 0 || index >= items.Count)
                return null;
            return items[index];
        }

        public void Load(MyIni ini, string customData)
        {
            items.Clear();
            int count = ini.Get(Section, "Count").ToInt32(0);
            for (int i = 1; i <= count; i++)
            {
                string sec = Section + "." + i;
                if (!ini.ContainsSection(sec) && string.IsNullOrEmpty(ini.Get(sec, "Name").ToString("")))
                    continue;
                items.Add(GpsWaypoint.Read(ini, sec));
            }

            LoadPasted(customData);
        }

        public void Save(MyIni ini)
        {
            ini.Set(Section, "Count", items.Count);
            for (int i = 0; i < items.Count; i++)
                items[i].Write(ini, Section + "." + (i + 1));
        }

        public void LoadFromBlock(IMyTerminalBlock block)
        {
            if (block == null)
                return;
            MyIni blockIni = new MyIni();
            string data = block.CustomData ?? "";
            blockIni.TryParse(data);
            Load(blockIni, data);
        }

        public void SaveToBlock(IMyTerminalBlock block)
        {
            if (block == null)
                return;
            MyIni blockIni = new MyIni();
            blockIni.TryParse(block.CustomData ?? "");
            Save(blockIni);
            block.CustomData = blockIni.ToString();
        }

        public void Add(GpsWaypoint wp)
        {
            if (wp == null)
                return;
            if (string.IsNullOrEmpty(wp.Name))
                wp.Name = wp.BuildName();
            wp.Name = UniqueName(wp.Name);
            items.Add(wp);
        }

        public void Replace(int index, GpsWaypoint wp)
        {
            if (index < 0 || index >= items.Count || wp == null)
                return;
            if (string.IsNullOrEmpty(wp.Name))
                wp.Name = wp.BuildName();
            wp.Name = UniqueName(wp.Name, index);
            items[index] = wp;
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= items.Count)
                return;
            items.RemoveAt(index);
        }

        public string UniqueName(string baseName, int ignoreIndex = -1)
        {
            if (string.IsNullOrEmpty(baseName))
                baseName = "GPS";
            if (!ContainsName(baseName, ignoreIndex))
                return baseName;
            int n = 2;
            while (ContainsName(baseName + " " + n, ignoreIndex))
                n++;
            return baseName + " " + n;
        }

        bool ContainsName(string name, int ignoreIndex = -1)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (i == ignoreIndex)
                    continue;
                if (items[i].Name == name)
                    return true;
            }
            return false;
        }

        bool ContainsPosition(Vector3D pos)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (Vector3D.DistanceSquared(items[i].Position, pos) < 1)
                    return true;
            }
            return false;
        }

        void LoadPasted(string customData)
        {
            if (string.IsNullOrEmpty(customData))
                return;

            string[] lines = customData.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length > 0 && line[0] == ';')
                    continue;
                int gpsAt = line.IndexOf("GPS:");
                if (gpsAt < 0)
                    continue;
                if (gpsAt > 0 && line.IndexOf('=') >= 0 && line.IndexOf('=') < gpsAt)
                    continue;

                string gps = gpsAt == 0 ? line : line.Substring(gpsAt);
                string gpsName;
                Vector3D pos;
                if (!UtilsHelpder.TryParseGPS(gps, out gpsName, out pos))
                    continue;
                if (ContainsPosition(pos))
                    continue;

                GpsWaypoint wp = new GpsWaypoint();
                wp.Position = pos;
                wp.Name = gpsName;
                wp.Location = LocationType.PoI;
                if (string.IsNullOrEmpty(wp.Name))
                    wp.Name = wp.BuildName();
                wp.Name = UniqueName(wp.Name);
                items.Add(wp);
            }
        }
    }
}
