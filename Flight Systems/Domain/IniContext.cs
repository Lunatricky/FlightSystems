using System.Collections.Generic;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript.Domain
{
    public class IniContext
    {
        readonly MyIni ini = new MyIni();
        readonly GridContext gc;

        //Ini
        Dictionary<string, string> __snapshot = new Dictionary<string, string>();
        bool iniAnyChanged;

        //TogglesSection
        const string TogglesSection = "Toggles";

        public const string FLIGHT_SYSTEMS = "Flight Systems";
        public const string LOW_FUEL_LAND = "Low Fuel Auto Land";
        public const string DOCK_MODE = "Dock Mode";
        public const string CONTROL_ANTENNAS = "Control Antennas";
        public const string RENAME_SUBGRIDS = "Rename Subgrids";
        public const string PAINT_SURFACES = "Change Screen Colors";

        bool allowFlightSystems = true;
        bool allowLowFuelLand = false;
        bool allowDockMode = false;
        bool controlAntennas = false;
        bool renameSubgrids = false;
        bool paintSurfaces = false;

        //NamesTagsSection
        const string NamesTagsSection = "Names & Tags";

        const string INI_GRID_NAME = "Grid Name";
        const string INI_DOCK_GROUP_TAG = "Dock Group";
        const string INI_CONTROLLER_TAG = "Controller";
        const string INI_OVERRIDE_BLOCKS_TAG = "Override Blocks";
        const string INI_IGNORE_TAG = "Ignore";
        const string INI_LCD1_TAG = "LCD 1";
        const string INI_LCD2_TAG = "LCD 2";
        const string BACKUP_BATTERY_TAG = "Backup battery";

        string dockGroupTag = "Flight Systems";
        string controllerTag = "[FS_reference]";
        string overrideBlockTag = "[FS_override]";
        string ignoreTag = "[FS_ignore]";
        string lcd1Tag = "[FS_LCD1]";
        string lcd2Tag = "[FS_LCD2]";
        string backupBatteryTag = "[FS_backup]";

        //ParamsSection
        const string ParamsSection = "Params";

        const string MAX_SPEED = "Max Speed";
        const string CNAV_ALTITUDE = "Cnav Altitude";
        const string DISTANCE_TO_GPS = "Distance to GPS";
        const string MINIMUM_ACCEPTED_FUEL = "Minimum Accepted Fuel";

        double maxSpeed = 99; // m/s
        double cnavAltitude = 1000; // m
        double distanceToGPS = 500; // m
        double minimumAcceptedFuel = 20; //%

        //SurfaceColorsSection
        const string SurfaceColorsSection = "Screen Colors";

        const string BACKGROUNDCOLOR = "Background Color";
        const string FONTCOLOR = "Font Color";
        const string COLORS = "Available Colors";

        string backgroundColor = "Black";
        string fontColor = "White";
        string colors = ColorMap.All.ToString();

        public IniContext(GridContext gc)
        {
            this.gc = gc;
        }

        public bool IniAnyChanged => iniAnyChanged;
        public string DockGroupTag => dockGroupTag;
        public string ControllerTag => controllerTag;
        public string OverrideBlockTag => overrideBlockTag;
        public string IgnoreTag => ignoreTag;
        public string Lcd1Tag => lcd1Tag;
        public string Lcd2Tag => lcd2Tag;
        public string BackupBatteryTag => backupBatteryTag;
        public double MaxSpeed => maxSpeed;
        public double CnavAltitude => cnavAltitude;
        public double DistanceToGPS => distanceToGPS;
        public double MinimumAcceptedFuel => minimumAcceptedFuel;
        public bool AllowFlightSystems => allowFlightSystems;
        public bool AllowLowFuelLand => allowLowFuelLand;
        public bool AllowDockMode => allowDockMode;
        public bool ControlAntennas => controlAntennas;
        public bool RenameSubgrids => renameSubgrids;
        public bool PaintSurfaces => paintSurfaces;
        public string BackgroundColor => backgroundColor;
        public string FontColor => fontColor;
        public string Color => colors;


        // ───────────────────────────────────────
        // Load config from CustomData (INI style)
        // ───────────────────────────────────────      
        public bool ParseIni()
        {
            ini.Clear();
            iniAnyChanged = false;

            if (!ini.TryParse(gc.Me.CustomData)) return IniAnyChanged;

            List<string> sectionsNames = new List<string>();
            string[] array = { NamesTagsSection, ParamsSection, TogglesSection };
            sectionsNames.AddArray(array);

            foreach (string sectionName in sectionsNames)
                if (!ini.ContainsSection(sectionName))
                {
                    ini.AddSection(sectionName);
                }


            // ───────────────────────────────────────────
            // Read PB Custom Data and populate properties
            // ───────────────────────────────────────────

            //TogglesSection
            allowFlightSystems = ini.Get(TogglesSection, FLIGHT_SYSTEMS).ToBoolean(AllowFlightSystems);
            allowLowFuelLand = ini.Get(TogglesSection, LOW_FUEL_LAND).ToBoolean(AllowLowFuelLand);
            allowDockMode = ini.Get(TogglesSection, DOCK_MODE).ToBoolean(AllowDockMode);
            controlAntennas = ini.Get(TogglesSection, CONTROL_ANTENNAS).ToBoolean(ControlAntennas);
            renameSubgrids = ini.Get(TogglesSection, RENAME_SUBGRIDS).ToBoolean(RenameSubgrids);
            paintSurfaces = ini.Get(TogglesSection, PAINT_SURFACES).ToBoolean(PaintSurfaces);

            //NamesTagsSection
            string tempGridName = ini.Get(NamesTagsSection, INI_GRID_NAME).ToString(gc.GridName);
            gc.GridName = string.IsNullOrWhiteSpace(tempGridName) ? gc.GridName : tempGridName;

            dockGroupTag = ini.Get(NamesTagsSection, INI_DOCK_GROUP_TAG).ToString(DockGroupTag);
            controllerTag = ini.Get(NamesTagsSection, INI_CONTROLLER_TAG).ToString(ControllerTag);
            overrideBlockTag = ini.Get(NamesTagsSection, INI_OVERRIDE_BLOCKS_TAG).ToString(OverrideBlockTag);
            ignoreTag = ini.Get(NamesTagsSection, INI_IGNORE_TAG).ToString(IgnoreTag);
            lcd1Tag = ini.Get(NamesTagsSection, INI_LCD1_TAG).ToString(Lcd1Tag);
            lcd2Tag = ini.Get(NamesTagsSection, INI_LCD2_TAG).ToString(Lcd2Tag);
            backupBatteryTag = ini.Get(NamesTagsSection, BACKUP_BATTERY_TAG).ToString(BackupBatteryTag);

            //ParamsSection
            maxSpeed = ini.Get(ParamsSection, MAX_SPEED).ToDouble(MaxSpeed);
            cnavAltitude = ini.Get(ParamsSection, CNAV_ALTITUDE).ToDouble(CnavAltitude);
            distanceToGPS = ini.Get(ParamsSection, DISTANCE_TO_GPS).ToDouble(DistanceToGPS);
            minimumAcceptedFuel = ini.Get(ParamsSection, MINIMUM_ACCEPTED_FUEL).ToDouble(MinimumAcceptedFuel);

            //SurfaceColorsSection
            backgroundColor = ini.Get(SurfaceColorsSection, BACKGROUNDCOLOR).ToString(BackgroundColor);
            fontColor = ini.Get(SurfaceColorsSection, FONTCOLOR).ToString(FontColor);


            // ───────────────────── 
            // Delete PB Custom Data
            // ─────────────────────   
            ini.Clear();
            gc.Me.CustomData = "";

            // ───────────────────────────────────────────────────────────────────────────────────────
            // Populate PB Custom Data whilst checking if any value changed to force ReloadGridContext
            // ───────────────────────────────────────────────────────────────────────────────────────

            //TogglesSection
            iniAnyChanged |= ReadAndDetectChange(ini, TogglesSection, FLIGHT_SYSTEMS, AllowFlightSystems);
            iniAnyChanged |= ReadAndDetectChange(ini, TogglesSection, LOW_FUEL_LAND, AllowLowFuelLand);
            iniAnyChanged |= ReadAndDetectChange(ini, TogglesSection, DOCK_MODE, AllowDockMode);
            iniAnyChanged |= ReadAndDetectChange(ini, TogglesSection, CONTROL_ANTENNAS, ControlAntennas);
            iniAnyChanged |= ReadAndDetectChange(ini, TogglesSection, RENAME_SUBGRIDS, RenameSubgrids);
            iniAnyChanged |= ReadAndDetectChange(ini, TogglesSection, PAINT_SURFACES, PaintSurfaces);

            //NamesTagsSection
            iniAnyChanged |= ReadAndDetectChange(ini, NamesTagsSection, INI_GRID_NAME, gc.GridName);
            iniAnyChanged |= ReadAndDetectChange(ini, NamesTagsSection, INI_DOCK_GROUP_TAG, DockGroupTag);
            iniAnyChanged |= ReadAndDetectChange(ini, NamesTagsSection, INI_CONTROLLER_TAG, ControllerTag);
            iniAnyChanged |= ReadAndDetectChange(ini, NamesTagsSection, INI_OVERRIDE_BLOCKS_TAG, OverrideBlockTag);
            iniAnyChanged |= ReadAndDetectChange(ini, NamesTagsSection, INI_IGNORE_TAG, IgnoreTag);
            iniAnyChanged |= ReadAndDetectChange(ini, NamesTagsSection, INI_LCD1_TAG, Lcd1Tag);
            iniAnyChanged |= ReadAndDetectChange(ini, NamesTagsSection, INI_LCD2_TAG, Lcd2Tag);
            iniAnyChanged |= ReadAndDetectChange(ini, NamesTagsSection, BACKUP_BATTERY_TAG, BackupBatteryTag);

            //ParamsSection
            iniAnyChanged |= ReadAndDetectChange(ini, ParamsSection, MAX_SPEED, MaxSpeed);
            iniAnyChanged |= ReadAndDetectChange(ini, ParamsSection, CNAV_ALTITUDE, CnavAltitude);
            iniAnyChanged |= ReadAndDetectChange(ini, ParamsSection, DISTANCE_TO_GPS, DistanceToGPS);
            iniAnyChanged |= ReadAndDetectChange(ini, ParamsSection, MINIMUM_ACCEPTED_FUEL, MinimumAcceptedFuel);

            //SurfaceColorsSection
            iniAnyChanged |= ReadAndDetectChange(ini, SurfaceColorsSection, BACKGROUNDCOLOR, BackgroundColor);
            iniAnyChanged |= ReadAndDetectChange(ini, SurfaceColorsSection, FONTCOLOR, FontColor);
            ini.Set(SurfaceColorsSection, COLORS, colors);




            gc.Me.CustomData = $"[Flight Systems: {gc.Me.EntityId}]\n\n" + ini.ToString();

            return IniAnyChanged;
        }

        bool ReadAndDetectChange(MyIni ini, string section, string key, object newVal)
        {
            ini.Set(section, key, newVal.ToString());

            string old;
            string newValString = newVal.ToString();
            __snapshot.TryGetValue(key, out old);
            if (old != newValString)
            {
                __snapshot[key] = newValString;
                return true;
            }
            return false;
        }
    }
}
