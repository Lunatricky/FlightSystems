using System.Collections.Generic;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript.Domain
{
    class IniContext
    {
        readonly MyIni ini = new MyIni();
        readonly GridContext gc;

        //Ini
        Dictionary<string, string> __snapshot = new Dictionary<string, string>();
        private bool iniAnyChanged;

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

        string __DockGroupTag = "Flight Systems";
        string __ControllerTag = "[FS_reference]";
        string __OverrideBlockTag = "[FS_override]";
        string __IgnoreTag = "[FS_ignore]";
        string __Lcd1Tag = "[FS_LCD1]";
        string __Lcd2Tag = "[FS_LCD2]";
        string __BackupBatteryTag = "[FS_backup]";

        //ParamsSection
        const string ParamsSection = "Params";

        const string MAX_SPEED = "Max Speed";
        const string CNAV_ALTITUDE = "Cnav Altitude";
        const string DISTANCE_TO_GPS = "Distance to GPS";
        const string MINIMUM_ACCEPTED_FUEL = "Minimum Accepted Fuel";

        double __MaxSpeed = 99; // m/s
        double __CnavAltitude = 1000; // m
        double __DistanceToGPS = 500; // m
        double __MinimumAcceptedFuel = 20; //%

        //TogglesSection
        const string TogglesSection = "Toggles";

        public const string FLIGHT_SYSTEMS = "Flight Systems";
        public const string DOCK_MODE = "Dock Mode";
        public const string CONTROL_ANTENNAS = "Control Antennas";
        public const string RENAME_SUBGRIDS = "Rename Subgrids";
        public const string PAINT_SURFACES = "Change Screen Colors";

        bool __AllowFlightSystems = true;
        bool __AllowDockMode = false;
        bool __ControlAntennas = false;
        bool __RenameSubgrids = false;
        bool __PaintSurfaces = false;

        //SurfaceColorsSection
        const string SurfaceColorsSection = "Screen Colors";

        const string BACKGROUNDCOLOR = "Background Color";
        const string FONTCOLOR = "Font Color";
        const string COLORS = "Available Colors";

        string __BackgroundColor = "Black";
        string __FontColor = "White";
        string __Colors = ColorMap.All.ToString();

        public IniContext(GridContext gc)
        {
            this.gc = gc;
        }

        public bool IniAnyChanged => iniAnyChanged;
        public string DockGroupTag => __DockGroupTag;
        public string ControllerTag => __ControllerTag;
        public string OverrideBlockTag => __OverrideBlockTag;
        public string IgnoreTag => __IgnoreTag;
        public string Lcd1Tag => __Lcd1Tag;
        public string Lcd2Tag => __Lcd2Tag;
        public string BackupBatteryTag => __BackupBatteryTag;
        public double MaxSpeed => __MaxSpeed;
        public double CnavAltitude => __CnavAltitude;
        public double DistanceToGPS => __DistanceToGPS;
        public double MinimumAcceptedFuel => __MinimumAcceptedFuel;
        public bool AllowFlightSystems => __AllowFlightSystems;
        public bool AllowDockMode => __AllowDockMode;
        public bool ControlAntennas => __ControlAntennas;
        public bool RenameSubgrids => __RenameSubgrids;
        public bool PaintSurfaces => __PaintSurfaces;
        public string BackgroundColor => __BackgroundColor;
        public string FontColor => __FontColor;
        public string Color => __Colors;


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
            //NamesTagsSection
            string tempGridName = ini.Get(NamesTagsSection, INI_GRID_NAME).ToString(gc.GridName);
            gc.GridName = string.IsNullOrWhiteSpace(tempGridName) ? gc.GridName : tempGridName;

            __DockGroupTag = ini.Get(NamesTagsSection, INI_DOCK_GROUP_TAG).ToString(DockGroupTag);
            __ControllerTag = ini.Get(NamesTagsSection, INI_CONTROLLER_TAG).ToString(ControllerTag);
            __OverrideBlockTag = ini.Get(NamesTagsSection, INI_OVERRIDE_BLOCKS_TAG).ToString(OverrideBlockTag);
            __IgnoreTag = ini.Get(NamesTagsSection, INI_IGNORE_TAG).ToString(IgnoreTag);
            __Lcd1Tag = ini.Get(NamesTagsSection, INI_LCD1_TAG).ToString(Lcd1Tag);
            __Lcd2Tag = ini.Get(NamesTagsSection, INI_LCD2_TAG).ToString(Lcd2Tag);
            __BackupBatteryTag = ini.Get(NamesTagsSection, BACKUP_BATTERY_TAG).ToString(BackupBatteryTag);

            //ParamsSection
            __MaxSpeed = ini.Get(ParamsSection, MAX_SPEED).ToDouble(MaxSpeed);
            __CnavAltitude = ini.Get(ParamsSection, CNAV_ALTITUDE).ToDouble(CnavAltitude);
            __DistanceToGPS = ini.Get(ParamsSection, DISTANCE_TO_GPS).ToDouble(DistanceToGPS);
            __MinimumAcceptedFuel = ini.Get(ParamsSection, MINIMUM_ACCEPTED_FUEL).ToDouble(MinimumAcceptedFuel);

            //TogglesSection
            __AllowFlightSystems = ini.Get(TogglesSection, FLIGHT_SYSTEMS).ToBoolean(AllowFlightSystems);
            __AllowDockMode = ini.Get(TogglesSection, DOCK_MODE).ToBoolean(AllowDockMode);
            __ControlAntennas = ini.Get(TogglesSection, CONTROL_ANTENNAS).ToBoolean(ControlAntennas);
            __RenameSubgrids = ini.Get(TogglesSection, RENAME_SUBGRIDS).ToBoolean(RenameSubgrids);
            __PaintSurfaces = ini.Get(TogglesSection, PAINT_SURFACES).ToBoolean(PaintSurfaces);

            //SurfaceColorsSection
            __BackgroundColor = ini.Get(SurfaceColorsSection, BACKGROUNDCOLOR).ToString(BackgroundColor);
            __FontColor = ini.Get(SurfaceColorsSection, FONTCOLOR).ToString(FontColor);


            // ───────────────────── 
            // Delete PB Custom Data
            // ─────────────────────   
            ini.Clear();
            gc.Me.CustomData = "";

            // ───────────────────────────────────────────────────────────────────────────────────────
            // Populate PB Custom Data whilst checking if any value changed to force ReloadGridContext
            // ───────────────────────────────────────────────────────────────────────────────────────
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

            //TogglesSection
            iniAnyChanged |= ReadAndDetectChange(ini, TogglesSection, FLIGHT_SYSTEMS, AllowFlightSystems);
            iniAnyChanged |= ReadAndDetectChange(ini, TogglesSection, DOCK_MODE, AllowDockMode);
            iniAnyChanged |= ReadAndDetectChange(ini, TogglesSection, CONTROL_ANTENNAS, ControlAntennas);
            iniAnyChanged |= ReadAndDetectChange(ini, TogglesSection, RENAME_SUBGRIDS, RenameSubgrids);
            iniAnyChanged |= ReadAndDetectChange(ini, TogglesSection, PAINT_SURFACES, PaintSurfaces);

            //SurfaceColorsSection
            iniAnyChanged |= ReadAndDetectChange(ini, SurfaceColorsSection, BACKGROUNDCOLOR, BackgroundColor);
            iniAnyChanged |= ReadAndDetectChange(ini, SurfaceColorsSection, FONTCOLOR, FontColor);
            ini.Set(SurfaceColorsSection, COLORS, __Colors);




            gc.Me.CustomData = ini.ToString();

            return IniAnyChanged;
        }

        private bool ReadAndDetectChange(MyIni ini, string section, string key, object newVal)
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
