using System.Collections.Generic;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript.Domain
{
    public class IniContext
    {
        readonly MyIni ini = new MyIni();
        readonly GridContext gc;

        //Ini
        public Dictionary<string, string> IniSnapshot;
        bool iniChanged;

        //ToggleSection
        public const string ToggleSection = "Toggles";

        public const string FLIGHT_SYSTEMS = "Flight Systems";
        public const string ANALOG_THROTLE = "Analog Throtle";
        public const string LOW_FUEL_LAND = "Low Fuel Auto Land";
        public const string DOCK_MODE = "Dock Mode";
        public const string CONTROL_ANTENNAS = "Control Antennas";
        public const string RENAME_SUBGRIDS = "Rename Subgrids";
        public const string PAINT_SURFACES = "Change Screen Colors";
        public const string TRANSPARENTLCD = "Keep LCD Transparency";

        bool allowFlightSystems = true;
        bool analogThrotle = false;
        bool allowLowFuelLand = false;
        bool allowDockMode = false;
        bool controlAntennas = false;
        bool renameSubgrids = false;
        bool paintSurfaces = false;
        bool transparentLCD = true;

        //NamesTagsSection
        const string NamesTagsSection = "Names & Tags";

        const string INI_GRID_NAME = "Grid Name";
        const string INI_DOCK_GROUP_TAG = "Dock Group";
        const string INI_CONTROLLER_TAG = "Controller";
        const string INI_OVERRIDE_BLOCKS_TAG = "Override Blocks";
        const string INI_IGNORE_TAG = "Ignore";
        const string INI_LCD1_TAG = "LCD 1";
        const string INI_LCD2_TAG = "LCD 2";
        const string INI_LCD_SETTINGS_TAG = "LCD Settings";
        const string BACKUP_BATTERY_TAG = "Backup battery";

        string dockGroupTag = "Flight Systems";
        string controllerTag = "[FS_reference]";
        string overrideBlockTag = "[FS_override]";
        string ignoreTag = "[FS_ignore]";
        string lcd1Tag = "[FS_LCD1]";
        string lcd2Tag = "[FS_LCD2]";
        string lcdSettingsTag = "[FS_LCD_SETTINGS]";
        string backupBatteryTag = "[FS_backup]";

        //ParamsSection
        public const string ParamsSection = "Params";

        public const string MAX_SPEED = "Max Speed";
        public const string CNAV_ALTITUDE = "Cnav Altitude";
        public const string DISTANCE_TO_GPS = "Distance to GPS";
        public const string MINIMUM_ACCEPTED_FUEL = "Minimum Fuel";

        double maxSpeed = 99; // m/s
        double cnavAltitude = 1000; // m
        double distanceToGPS = 500; // m
        double minimumAcceptedFuel = 20; //%

        //SurfaceColorsSection
        public const string SurfaceColorsSection = "Screen Colors";

        const string LCDBACKGROUNDCOLOR = "LCD Background Color";
        const string LCDFONTCOLOR = "LCD Font Color";
        const string SPRITEBACKGROUNDCOLOR = "Sprite Background Color";
        const string SPRITEFONTCOLOR = "Sprite Font Color";

        string lcdBackgroundColor = "Black";
        string lcdFontColor = "White";
        string spriteBackgroundColor = "Black";
        string spriteFontColor = "White";

        const string AvailableColorsSection = "Available Colors";

        const string COLORS = "Available Colors";

        string colors = ColorMap.All.ToString();


        public IniContext(GridContext gc)
        {
            IniSnapshot = new Dictionary<string, string>();
            this.gc = gc;
        }

        public bool IniChanged => iniChanged;
        public string DockGroupTag => dockGroupTag;
        public string ControllerTag => controllerTag;
        public string OverrideBlockTag => overrideBlockTag;
        public string IgnoreTag => ignoreTag;
        public string Lcd1Tag => lcd1Tag;
        public string Lcd2Tag => lcd2Tag;
        public string LcdSettingsTag => lcdSettingsTag;
        public string BackupBatteryTag => backupBatteryTag;
        public double MaxSpeed
        {
            get { return maxSpeed; }
            set {
                maxSpeed = value;
                UpdateIni(ParamsSection, MAX_SPEED, maxSpeed);
            }
        }
        public double CnavAltitude
        {
            get { return cnavAltitude; }
            set {
                cnavAltitude = value;
                UpdateIni(ParamsSection, CNAV_ALTITUDE, cnavAltitude);
            }
        }
        public double DistanceToGPS
        {
            get { return distanceToGPS; }
            set {
                distanceToGPS = value;
                UpdateIni(ParamsSection, DISTANCE_TO_GPS, distanceToGPS);
            }
        }
        public double MinimumAcceptedFuel
        {
            get { return minimumAcceptedFuel; }
            set 
            { 
                minimumAcceptedFuel = value;
                UpdateIni(ParamsSection, MINIMUM_ACCEPTED_FUEL, minimumAcceptedFuel);
            }
        }
        public bool AllowFlightSystems
        {
            get {return allowFlightSystems;}
            set 
            {
                allowFlightSystems = value;
                UpdateIni(ToggleSection, FLIGHT_SYSTEMS, allowFlightSystems);
            }
        }

        public bool AnalogThrotle
        {
            get {return analogThrotle;}
            set 
            {
                analogThrotle = value;
                UpdateIni(ToggleSection, ANALOG_THROTLE, analogThrotle);
            }
        }

        public bool AllowLowFuelLand
        {
            get {return allowLowFuelLand; }
            set
            {
                allowLowFuelLand = value;
                UpdateIni(ToggleSection, LOW_FUEL_LAND, allowLowFuelLand);
            }
        }

        public bool AllowDockMode
        {
            get {return allowDockMode; }
            set
            {
                allowDockMode = value;
                UpdateIni(ToggleSection, DOCK_MODE, allowDockMode);
            }
        }

        public bool ControlAntennas
        {
            get { return controlAntennas; }
            set
            {
                controlAntennas = value;
                UpdateIni(ToggleSection, CONTROL_ANTENNAS, controlAntennas);
            }
        }

        public bool RenameSubgrids
        {
            get { return renameSubgrids; }
            set
            {
                renameSubgrids = value;
                UpdateIni(ToggleSection, RENAME_SUBGRIDS, renameSubgrids);
            }
        }

        public bool PaintSurfaces
        {
            get { return paintSurfaces; }
            set
            {
                paintSurfaces = value;
                UpdateIni(ToggleSection, PAINT_SURFACES, paintSurfaces);
            }
        }

        public bool TransparentLCD
        {
            get { return transparentLCD; }
            set
            {
                transparentLCD = value;
                UpdateIni(ToggleSection, TRANSPARENTLCD, transparentLCD);
            }
        }

        public string LcdBackgroundColor => lcdBackgroundColor;
        public string LcdFontColor => lcdFontColor;
        public string SpriteBackgroundColor => spriteBackgroundColor;
        public string SpriteFontColor => spriteFontColor;


        // ───────────────────────────────────────
        // Load config from CustomData (INI style)
        // ───────────────────────────────────────      
        public bool ParseIni()
        {
            ini.Clear();
            iniChanged = false;

            if (!ini.TryParse(gc.Me.CustomData)) return IniChanged;

            List<string> sectionsNames = new List<string>();
            string[] array = { NamesTagsSection, ParamsSection, ToggleSection };
            sectionsNames.AddArray(array);

            foreach (string sectionName in sectionsNames)
                if (!ini.ContainsSection(sectionName))
                {
                    ini.AddSection(sectionName);
                }


            // ───────────────────────────────────────────
            // Read PB Custom Data and populate properties
            // ───────────────────────────────────────────

            //ToggleSection
            allowFlightSystems = ini.Get(ToggleSection, FLIGHT_SYSTEMS).ToBoolean(AllowFlightSystems);
            analogThrotle = ini.Get(ToggleSection, ANALOG_THROTLE).ToBoolean(AnalogThrotle);
            allowLowFuelLand = ini.Get(ToggleSection, LOW_FUEL_LAND).ToBoolean(AllowLowFuelLand);
            allowDockMode = ini.Get(ToggleSection, DOCK_MODE).ToBoolean(AllowDockMode);
            controlAntennas = ini.Get(ToggleSection, CONTROL_ANTENNAS).ToBoolean(ControlAntennas);
            renameSubgrids = ini.Get(ToggleSection, RENAME_SUBGRIDS).ToBoolean(RenameSubgrids);
            paintSurfaces = ini.Get(ToggleSection, PAINT_SURFACES).ToBoolean(PaintSurfaces);
            transparentLCD = ini.Get(ToggleSection, TRANSPARENTLCD).ToBoolean(TransparentLCD);

            //NamesTagsSection
            string tempGridName = ini.Get(NamesTagsSection, INI_GRID_NAME).ToString(gc.GridName);
            gc.GridName = string.IsNullOrWhiteSpace(tempGridName) ? gc.GridName : tempGridName;

            dockGroupTag = ini.Get(NamesTagsSection, INI_DOCK_GROUP_TAG).ToString(DockGroupTag);
            controllerTag = ini.Get(NamesTagsSection, INI_CONTROLLER_TAG).ToString(ControllerTag);
            overrideBlockTag = ini.Get(NamesTagsSection, INI_OVERRIDE_BLOCKS_TAG).ToString(OverrideBlockTag);
            ignoreTag = ini.Get(NamesTagsSection, INI_IGNORE_TAG).ToString(IgnoreTag);
            lcd1Tag = ini.Get(NamesTagsSection, INI_LCD1_TAG).ToString(Lcd1Tag);
            lcd2Tag = ini.Get(NamesTagsSection, INI_LCD2_TAG).ToString(Lcd2Tag);
            lcdSettingsTag = ini.Get(NamesTagsSection, INI_LCD_SETTINGS_TAG).ToString(LcdSettingsTag);
            backupBatteryTag = ini.Get(NamesTagsSection, BACKUP_BATTERY_TAG).ToString(BackupBatteryTag);

            //ParamsSection
            maxSpeed = ini.Get(ParamsSection, MAX_SPEED).ToDouble(MaxSpeed);
            cnavAltitude = ini.Get(ParamsSection, CNAV_ALTITUDE).ToDouble(CnavAltitude);
            distanceToGPS = ini.Get(ParamsSection, DISTANCE_TO_GPS).ToDouble(DistanceToGPS);
            minimumAcceptedFuel = ini.Get(ParamsSection, MINIMUM_ACCEPTED_FUEL).ToDouble(MinimumAcceptedFuel);

            //SurfaceColorsSection
            lcdBackgroundColor = ini.Get(SurfaceColorsSection, LCDBACKGROUNDCOLOR).ToString(LcdBackgroundColor);
            lcdFontColor = ini.Get(SurfaceColorsSection, LCDFONTCOLOR).ToString(LcdFontColor);
            spriteBackgroundColor = ini.Get(SurfaceColorsSection, SPRITEBACKGROUNDCOLOR).ToString(SpriteBackgroundColor);
            spriteFontColor = ini.Get(SurfaceColorsSection, SPRITEFONTCOLOR).ToString(SpriteFontColor);


            // ───────────────────── 
            // Delete PB Custom Data
            // ─────────────────────  
            ini.Clear();
            gc.Me.CustomData = "";

            // ───────────────────────────────────────────────────────────────────────────────────────
            // Populate PB Custom Data whilst checking if any value changed to force ReloadGridContext
            // ───────────────────────────────────────────────────────────────────────────────────────

            //ToggleSection
            iniChanged |= ReadAndDetectChange(ini, ToggleSection, FLIGHT_SYSTEMS, AllowFlightSystems);
            iniChanged |= ReadAndDetectChange(ini, ToggleSection, ANALOG_THROTLE, AnalogThrotle);
            iniChanged |= ReadAndDetectChange(ini, ToggleSection, LOW_FUEL_LAND, AllowLowFuelLand);
            iniChanged |= ReadAndDetectChange(ini, ToggleSection, DOCK_MODE, AllowDockMode);
            iniChanged |= ReadAndDetectChange(ini, ToggleSection, CONTROL_ANTENNAS, ControlAntennas);
            iniChanged |= ReadAndDetectChange(ini, ToggleSection, RENAME_SUBGRIDS, RenameSubgrids);
            iniChanged |= ReadAndDetectChange(ini, ToggleSection, PAINT_SURFACES, PaintSurfaces);
            iniChanged |= ReadAndDetectChange(ini, ToggleSection, TRANSPARENTLCD, TransparentLCD);

            //NamesTagsSection
            iniChanged |= ReadAndDetectChange(ini, NamesTagsSection, INI_GRID_NAME, gc.GridName);
            iniChanged |= ReadAndDetectChange(ini, NamesTagsSection, INI_DOCK_GROUP_TAG, DockGroupTag);
            iniChanged |= ReadAndDetectChange(ini, NamesTagsSection, INI_CONTROLLER_TAG, ControllerTag);
            iniChanged |= ReadAndDetectChange(ini, NamesTagsSection, INI_OVERRIDE_BLOCKS_TAG, OverrideBlockTag);
            iniChanged |= ReadAndDetectChange(ini, NamesTagsSection, INI_IGNORE_TAG, IgnoreTag);
            iniChanged |= ReadAndDetectChange(ini, NamesTagsSection, INI_LCD1_TAG, Lcd1Tag);
            iniChanged |= ReadAndDetectChange(ini, NamesTagsSection, INI_LCD2_TAG, Lcd2Tag);
            iniChanged |= ReadAndDetectChange(ini, NamesTagsSection, INI_LCD_SETTINGS_TAG, LcdSettingsTag);
            iniChanged |= ReadAndDetectChange(ini, NamesTagsSection, BACKUP_BATTERY_TAG, BackupBatteryTag);

            //ParamsSection
            iniChanged |= ReadAndDetectChange(ini, ParamsSection, MAX_SPEED, MaxSpeed);
            iniChanged |= ReadAndDetectChange(ini, ParamsSection, CNAV_ALTITUDE, CnavAltitude);
            iniChanged |= ReadAndDetectChange(ini, ParamsSection, DISTANCE_TO_GPS, DistanceToGPS);
            iniChanged |= ReadAndDetectChange(ini, ParamsSection, MINIMUM_ACCEPTED_FUEL, MinimumAcceptedFuel);

            //SurfaceColorsSection
            iniChanged |= ReadAndDetectChange(ini, SurfaceColorsSection, LCDBACKGROUNDCOLOR, LcdBackgroundColor);
            iniChanged |= ReadAndDetectChange(ini, SurfaceColorsSection, LCDFONTCOLOR, LcdFontColor);
            iniChanged |= ReadAndDetectChange(ini, SurfaceColorsSection, SPRITEBACKGROUNDCOLOR, SpriteBackgroundColor);
            iniChanged |= ReadAndDetectChange(ini, SurfaceColorsSection, SPRITEFONTCOLOR, SpriteFontColor);

            ini.Set(AvailableColorsSection, COLORS, colors);

            gc.Me.CustomData = $"[Flight Systems: {gc.Me.EntityId}]\n\n" + ini.ToString();

            return iniChanged;
        }

        void UpdateIni(string section, string key, object newVal)
        {
            gc.Me.CustomData = "";
            ini.Set(section, key, newVal.ToString());
            gc.Me.CustomData = $"[Flight Systems: {gc.Me.EntityId}]\n\n" + ini.ToString();
        }

        bool ReadAndDetectChange(MyIni ini, string section, string key, object newVal)
        {
            ini.Set(section, key, newVal.ToString());

            string old;
            string newValString = newVal.ToString();
            IniSnapshot.TryGetValue(key, out old);
            if (old != newValString)
            {
                IniSnapshot[key] = newValString;
                return true;
            }
            return false;
        }
    }
}
