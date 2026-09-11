namespace IngameScript.Enums
{
    static class EnumLabels
    {
        public static string Ship(ShipType t)
        {
            switch (t)
            {
                case ShipType.Atmo: return "Atmo";
                case ShipType.Space: return "Space";
                default: return "Interplanetary";
            }
        }

        public static string PlanetKind(PlanetType t)
        {
            switch (t)
            {
                case PlanetType.EarthFamily: return "EarthFamily";
                case PlanetType.Mars: return "Mars";
                case PlanetType.Alien: return "Alien";
                case PlanetType.Triton: return "Triton";
                case PlanetType.Pertam: return "Pertam";
                case PlanetType.MoonFamily: return "MoonFamily";
                default: return "Unknown";
            }
        }

        public static string State(MainState t)
        {
            switch (t)
            {
                case MainState.Abort: return "Abort";
                case MainState.Reload: return "Reload";
                case MainState.Idle: return "Idle";
                case MainState.Cruise: return "Cruise";
                case MainState.Orbit: return "Orbit";
                case MainState.Glide: return "Glide";
                case MainState.CNav: return "CNav";
                case MainState.Land: return "Land";
                case MainState.SBurn: return "SBurn";
                case MainState.Gps: return "Gps";
                default: return "Idle";
            }
        }

        public static string Land(AutoLandState t)
        {
            switch (t)
            {
                case AutoLandState.Abort: return "Abort";
                case AutoLandState.Align: return "Align";
                case AutoLandState.Drop: return "Drop";
                case AutoLandState.Cushion: return "Cushion";
                case AutoLandState.LockGear: return "LockGear";
                default: return "Idle";
            }
        }

        public static string StepName(Step t)
        {
            switch (t)
            {
                case Step.On: return "On";
                case Step.Off: return "Off";
                case Step.AimToGPS: return "AimToGPS";
                case Step.Cruise: return "Cruise";
                case Step.Preclimb: return "Preclimb";
                case Step.Climb: return "Climb";
                case Step.Orbit: return "Orbit";
                default: return "Toggle";
            }
        }

        public static string TaskName(Task t)
        {
            switch (t)
            {
                case Task.ResetControllers: return "ResetControllers";
                case Task.IsDocked: return "IsDocked";
                case Task.CheckIni: return "CheckIni";
                case Task.PhysicsUpdate: return "PhysicsUpdate";
                case Task.FlightSystems: return "FlightSystems";
                default: return "LCDs";
            }
        }

        public static string Location(LocationType t)
        {
            switch (t)
            {
                case LocationType.Planet: return "Planet";
                case LocationType.Asteroid: return "Asteroid";
                case LocationType.Station: return "Station/Base";
                case LocationType.Base: return "Base";
                case LocationType.OreVein: return "Ore";
                default: return "PoI";
            }
        }

        public static string RegionName(Region t)
        {
            switch (t)
            {
                case Region.Orbit: return "Orbit";
                case Region.Vicinity: return "Vicinity";
                case Region.Void: return "Void";
                default: return "Surface";
            }
        }

        public static string Planet(PlanetName t)
        {
            switch (t)
            {
                case PlanetName.Earth: return "Earth";
                case PlanetName.Mars: return "Mars";
                case PlanetName.Europa: return "Europa";
                case PlanetName.Alien: return "Alien";
                case PlanetName.Titan: return "Titan";
                case PlanetName.Triton: return "Triton";
                case PlanetName.Pertam: return "Pertam";
                default: return "";
            }
        }

        public static string Ore(OreType t)
        {
            switch (t)
            {
                case OreType.Fe: return "Fe";
                case OreType.Ni: return "Ni";
                case OreType.Si: return "Si";
                case OreType.Co: return "Co";
                case OreType.Ag: return "Ag";
                case OreType.Au: return "Au";
                case OreType.Pt: return "Pt";
                case OreType.Ur: return "Ur";
                default: return "Ice";
            }
        }

        public static string FactionName(Faction t)
        {
            switch (t)
            {
                case Faction.Friendly: return "Friendly";
                case Faction.Enemy: return "Enemy";
                default: return "Neutral";
            }
        }

        public static string Station(StationType t)
        {
            switch (t)
            {
                case StationType.Comps: return "Comps";
                case StationType.Ships: return "Ships";
                default: return "Ingots";
            }
        }

        public static LocationType ParseLocation(string s)
        {
            if (s == "Planet") return LocationType.Planet;
            if (s == "Asteroid") return LocationType.Asteroid;
            if (s == "Station" || s == "Station/Base") return LocationType.Station;
            if (s == "Base") return LocationType.Base;
            if (s == "Ore" || s == "OreVein") return LocationType.OreVein;
            return LocationType.PoI;
        }

        public static Region ParseRegion(string s)
        {
            if (s == "Orbit") return Region.Orbit;
            if (s == "Vicinity") return Region.Vicinity;
            if (s == "Void") return Region.Void;
            return Region.Surface;
        }

        public static PlanetName ParsePlanet(string s)
        {
            if (s == "Earth") return PlanetName.Earth;
            if (s == "Mars") return PlanetName.Mars;
            if (s == "Europa") return PlanetName.Europa;
            if (s == "Alien") return PlanetName.Alien;
            if (s == "Titan") return PlanetName.Titan;
            if (s == "Triton") return PlanetName.Triton;
            if (s == "Pertam") return PlanetName.Pertam;
            return PlanetName.None;
        }

        public static OreType ParseOre(string s)
        {
            if (s == "Fe") return OreType.Fe;
            if (s == "Ni") return OreType.Ni;
            if (s == "Si") return OreType.Si;
            if (s == "Co") return OreType.Co;
            if (s == "Ag") return OreType.Ag;
            if (s == "Au") return OreType.Au;
            if (s == "Pt") return OreType.Pt;
            if (s == "Ur") return OreType.Ur;
            return OreType.Ice;
        }

        public static Faction ParseFaction(string s)
        {
            if (s == "Friendly") return Faction.Friendly;
            if (s == "Enemy") return Faction.Enemy;
            return Faction.Neutral;
        }

        public static StationType ParseStation(string s)
        {
            if (s == "Comps") return StationType.Comps;
            if (s == "Ships") return StationType.Ships;
            return StationType.Ingots;
        }
    }
}
