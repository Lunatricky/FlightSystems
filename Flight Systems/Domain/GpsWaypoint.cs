using IngameScript.Enums;
using IngameScript.Utils;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace IngameScript.Domain
{
    class GpsWaypoint
    {
        public string Name;
        public Vector3D Position;
        public LocationType Location;
        public PlanetName Planet;
        public Region Region;
        public OreType Ore;
        public Faction Faction;
        public StationType Station;

        public GpsWaypoint()
        {
            Name = "";
            Location = LocationType.PoI;
            Planet = PlanetName.None;
            Region = Region.Surface;
            Ore = OreType.Ice;
            Faction = Faction.Neutral;
            Station = StationType.Ingots;
        }

        public GpsWaypoint Copy()
        {
            return new GpsWaypoint
            {
                Name = Name,
                Position = Position,
                Location = Location,
                Planet = Planet,
                Region = Region,
                Ore = Ore,
                Faction = Faction,
                Station = Station
            };
        }

        public string BuildName()
        {
            return Join(
                KindLabel(),
                PlanetLabel(),
                EnumLabels.RegionName(Region),
                EnumLabels.Ore(Ore),
                EnumLabels.Station(Station),
                EnumLabels.FactionName(Faction));
        }

        public string KindLabel()
        {
            return EnumLabels.Location(Location);
        }

        public string ToGpsString()
        {
            return UtilsHelpder.FormatGps(Name, Position);
        }

        public void Write(MyIni ini, string section)
        {
            ini.Set(section, "Name", Name ?? "");
            ini.Set(section, "X", Position.X);
            ini.Set(section, "Y", Position.Y);
            ini.Set(section, "Z", Position.Z);
            ini.Set(section, "Type", EnumLabels.Location(Location));
            ini.Set(section, "Planet", EnumLabels.Planet(Planet));
            ini.Set(section, "Region", EnumLabels.RegionName(Region));
            ini.Set(section, "Ore", EnumLabels.Ore(Ore));
            ini.Set(section, "Faction", EnumLabels.FactionName(Faction));
            ini.Set(section, "Station", EnumLabels.Station(Station));
            ini.Set(section, "GPS", ToGpsString());
        }

        public static GpsWaypoint Read(MyIni ini, string section)
        {
            GpsWaypoint wp = new GpsWaypoint();
            wp.Name = ini.Get(section, "Name").ToString("");
            wp.Position = new Vector3D(
                ini.Get(section, "X").ToDouble(0),
                ini.Get(section, "Y").ToDouble(0),
                ini.Get(section, "Z").ToDouble(0));
            wp.Location = EnumLabels.ParseLocation(ini.Get(section, "Type").ToString(""));
            wp.Planet = EnumLabels.ParsePlanet(ini.Get(section, "Planet").ToString(""));
            wp.Region = EnumLabels.ParseRegion(ini.Get(section, "Region").ToString(""));
            wp.Ore = EnumLabels.ParseOre(ini.Get(section, "Ore").ToString(""));
            wp.Faction = EnumLabels.ParseFaction(ini.Get(section, "Faction").ToString(""));
            wp.Station = EnumLabels.ParseStation(ini.Get(section, "Station").ToString(""));

            string gps = ini.Get(section, "GPS").ToString("");
            Vector3D fromGps;
            if (UtilsHelpder.TryParseGPS(gps, out fromGps))
            {
                if (wp.Position.LengthSquared() < 1)
                    wp.Position = fromGps;
                if (string.IsNullOrEmpty(wp.Name))
                    wp.Name = UtilsHelpder.GpsName(gps);
            }

            if (string.IsNullOrEmpty(wp.Name))
                wp.Name = wp.BuildName();
            return wp;
        }

        string PlanetLabel()
        {
            return EnumLabels.Planet(Planet);
        }

        static string Join(params string[] parts)
        {
            string s = "";
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]))
                    continue;
                s = s.Length == 0 ? parts[i] : s + " " + parts[i];
            }
            return s.Length == 0 ? "GPS" : s;
        }
    }
}
