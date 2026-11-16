using System.Collections.Generic;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace IngameScript.Domain
{
    public class IniContext
    {
        readonly MyIni ini = new MyIni();
        readonly GridContext gc;
        readonly IniSnapshot snapshot = new IniSnapshot();
        readonly GpsStore gps = new GpsStore();

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
        bool flightRestoreSet;
        bool flightRestoreValue;
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
        const string INI_MENU_TAG = "Menu";
        const string INI_LCD_SETTINGS_TAG = "LCD Settings";
        const string INI_COCKPIT_TAG = "Cockpit";
        const string INI_RAY_CAMERA_TAG = "Ray Camera";
        const string BACKUP_BATTERY_TAG = "Backup battery";
        const string INI_DOCK_TRIGGER_GROUP = "Dock Trigger Group";
        const string INI_LANDING_PAD_GROUP = "Landing Pad Group";
        const string INI_DOCK_TRIGGER_TAG = "Dock Trigger Tag";
        const string INI_LANDING_PAD_TAG = "Landing Pad Tag";
        const string ScriptTagsSection = "Script Tags";
        const string GroupsSection = "Groups";
        const string RestoreSection = "Restore";

        const string OldLcd1 = "[FS_LCD1]";
        const string NewLcd1 = "[FS_lcd1]";
        const string OldLcd2 = "[FS_LCD2]";
        const string NewLcd2 = "[FS_lcd2]";
        const string OldMenu = "[FS_LCD_SETTINGS]";
        const string NewMenu = "[FS_menu]";
        const string OldCockpit = "[FS_COCKPIT]";
        const string NewCockpit = "[FS_cockpit]";
        const string OldRay = "[FS_RAY]";
        const string NewRay = "[FS_ray]";

        string dockGroupTag = "Flight Systems";
        string controllerTag = "[FS_reference]";
        string overrideBlockTag = "[FS_override]";
        string ignoreTag = "[FS_ignore]";
        string lcd1Tag = "[FS_lcd1]";
        string lcd2Tag = "[FS_lcd2]";
        string lcdSettingsTag = "[FS_menu]";
        string cockpitTag = "[FS_cockpit]";
        string rayCameraTag = "[FS_ray]";
        string backupBatteryTag = "[FS_backup]";
        string dockTriggerGroup = "Dock trigger";
        string landingPadGroup = "Landing pad";
        string dockTriggerTag = "[FS_trigger]";
        string dockTriggerRestore = "";
        string landingPadTag = "[FS_pad]";
        string landingPadRestore = "";
        readonly List<string> renameOld = new List<string>();
        readonly List<string> renameNew = new List<string>();

        //ParamsSection
        public const string ParamsSection = "Params";

        public const string MAX_SPEED = "Max Speed";
        public const string CRUISE_SPEED = "Cruise Speed";
        public const string CNAV_ALTITUDE = "Cnav Altitude";
        public const string DISTANCE_TO_GPS = "Distance to GPS";
        public const string MINIMUM_ACCEPTED_FUEL = "Minimum Fuel";

        public string[] IniParamList = new string[5] { MAX_SPEED, CRUISE_SPEED, CNAV_ALTITUDE, DISTANCE_TO_GPS, MINIMUM_ACCEPTED_FUEL };

        double maxSpeed = double.PositiveInfinity; // m/s
        double cruiseSpeed = 100; // m/s
        double cnavAltitude = 1000; // m
        double distanceToGPS = 500; // m
        double minimumAcceptedFuel = 20; //%

        //SurfaceColorsSection
        public const string SurfaceColorsSection = "Screen Colors";

        const string LCDBACKGROUNDCOLOR = "LCD Background Color";
        const string LCDFONTCOLOR = "LCD Font Color";
        const string SPRITEMENUCOLOR = "Sprite Menu Color";
        const string SPRITEBACKGROUNDCOLOR = "Sprite Background Color";
        const string SPRITEFONTCOLOR = "Sprite Font Color";

        string lcdBackgroundColor = "Black";
        string lcdFontColor = "White";
        string spriteMenuColor = "White";
        string spriteBackgroundColor = "Black";
        string spriteFontColor = "White";

        const string AvailableColorsSection = "Available Colors";

        const string COLORS = "Available Colors";

        string colors = ColorMap.All.ToString();


        public IniContext(GridContext gc)
        {
            this.gc = gc;
        }

        public bool IniChanged => iniChanged;
        public string DockGroupTag => dockGroupTag;
        public string ControllerTag => controllerTag;
        public string OverrideBlockTag => overrideBlockTag;
        public string IgnoreTag => ignoreTag;
        public string DockTriggerGroup => dockTriggerGroup;
        public string LandingPadGroup => landingPadGroup;
        public string DockTriggerTag => dockTriggerTag;
        public string DockTriggerRestore => dockTriggerRestore;
        public string LandingPadTag => landingPadTag;
        public string LandingPadRestore => landingPadRestore;
        public string Lcd1Tag => lcd1Tag;
        public string Lcd2Tag => lcd2Tag;
        public string LcdSettingsTag => lcdSettingsTag;
        public string CockpitTag => cockpitTag;
        public string RayCameraTag => rayCameraTag;
        public string BackupBatteryTag => backupBatteryTag;
        internal GpsStore Gps => gps;
        public double MaxSpeed
        {
            get { return maxSpeed; }
            set
            {
                maxSpeed = value;
                UpdateIni(ParamsSection, MAX_SPEED, maxSpeed);
            }
        }
        public double CruiseSpeed
        {
            get { return cruiseSpeed; }
            set
            {
                cruiseSpeed = value;
                UpdateIni(ParamsSection, CRUISE_SPEED, cruiseSpeed);
            }
        }
        public double SafeAltitude
        {
            get { return cnavAltitude; }
            set 
            {
                cnavAltitude = value;
                UpdateIni(ParamsSection, CNAV_ALTITUDE, cnavAltitude);
            }
        }
        public double DistanceToGPS
        {
            get { return distanceToGPS; }
            set 
            {
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
        public Color SpriteMenuColor => ColorMap.GetColorFromString(spriteMenuColor);
        public Color SpriteBackgroundColor => ColorMap.GetColorFromString(spriteBackgroundColor);
        public Color SpriteFontColor => ColorMap.GetColorFromString(spriteFontColor);


        // ───────────────────────────────────────
        // Load config from CustomData (INI style)
        // ───────────────────────────────────────      
        public bool ParseIni()
        {
            ini.Clear();
            iniChanged = false;
            renameOld.Clear();
            renameNew.Clear();

            if (!ini.TryParse(gc.Me.CustomData)) return IniChanged;
            string rawCustomData = gc.Me.CustomData;

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

            dockGroupTag = ReadMoved(GroupsSection, INI_DOCK_GROUP_TAG, DockGroupTag);
            controllerTag = ini.Get(NamesTagsSection, INI_CONTROLLER_TAG).ToString(ControllerTag);
            overrideBlockTag = ini.Get(NamesTagsSection, INI_OVERRIDE_BLOCKS_TAG).ToString(OverrideBlockTag);
            ignoreTag = ini.Get(NamesTagsSection, INI_IGNORE_TAG).ToString(IgnoreTag);
            lcd1Tag = MigrateDefault(ini.Get(NamesTagsSection, INI_LCD1_TAG).ToString(NewLcd1), OldLcd1, NewLcd1);
            lcd2Tag = MigrateDefault(ini.Get(NamesTagsSection, INI_LCD2_TAG).ToString(NewLcd2), OldLcd2, NewLcd2);
            lcdSettingsTag = MigrateDefault(ReadMenuTag(), OldMenu, NewMenu);
            cockpitTag = MigrateDefault(ini.Get(NamesTagsSection, INI_COCKPIT_TAG).ToString(NewCockpit), OldCockpit, NewCockpit);
            rayCameraTag = MigrateDefault(ini.Get(NamesTagsSection, INI_RAY_CAMERA_TAG).ToString(NewRay), OldRay, NewRay);
            backupBatteryTag = ini.Get(NamesTagsSection, BACKUP_BATTERY_TAG).ToString(BackupBatteryTag);
            dockTriggerGroup = ReadMoved(GroupsSection, INI_DOCK_TRIGGER_GROUP, DockTriggerGroup);
            landingPadGroup = ReadMoved(GroupsSection, INI_LANDING_PAD_GROUP, LandingPadGroup);
            dockTriggerTag = ReadMoved(ScriptTagsSection, INI_DOCK_TRIGGER_TAG, "[FS_trigger]");
            landingPadTag = ReadMoved(ScriptTagsSection, INI_LANDING_PAD_TAG, "[FS_pad]");
            // Missing Restore is empty. The in-memory field is wiped by a recompile, so it is not the default.
            dockTriggerRestore = ini.Get(RestoreSection, INI_DOCK_TRIGGER_TAG).ToString("");
            landingPadRestore = ini.Get(RestoreSection, INI_LANDING_PAD_TAG).ToString("");
            // Same key name as the visible toggle. Read it here so a static grid does not store false over the saved value.
            flightRestoreSet = ini.ContainsKey(RestoreSection, FLIGHT_SYSTEMS);
            if (flightRestoreSet)
                flightRestoreValue = ini.Get(RestoreSection, FLIGHT_SYSTEMS).ToBoolean(false);
            ApplyFlightSystemsStaticHold();

            //ParamsSection
            string s = ini.Get(ParamsSection, MAX_SPEED).ToString(MaxSpeed.ToString());

            maxSpeed = (s == "Infinity" || s == "Infinite" || s == "" || s == "00")
                ? double.PositiveInfinity
                : double.Parse(s);

            cruiseSpeed = ini.Get(ParamsSection, CRUISE_SPEED).ToDouble(CruiseSpeed);
            cnavAltitude = ini.Get(ParamsSection, CNAV_ALTITUDE).ToDouble(SafeAltitude);
            distanceToGPS = ini.Get(ParamsSection, DISTANCE_TO_GPS).ToDouble(DistanceToGPS);
            minimumAcceptedFuel = ini.Get(ParamsSection, MINIMUM_ACCEPTED_FUEL).ToDouble(MinimumAcceptedFuel);

            //SurfaceColorsSection
            lcdBackgroundColor = ini.Get(SurfaceColorsSection, LCDBACKGROUNDCOLOR).ToString(LcdBackgroundColor);
            lcdFontColor = ini.Get(SurfaceColorsSection, LCDFONTCOLOR).ToString(LcdFontColor);
            spriteMenuColor = ini.Get(SurfaceColorsSection, SPRITEMENUCOLOR).ToString(ColorMap.GetStringFromColor(SpriteMenuColor));
            spriteBackgroundColor = ini.Get(SurfaceColorsSection, SPRITEBACKGROUNDCOLOR).ToString(ColorMap.GetStringFromColor(SpriteBackgroundColor));
            spriteFontColor = ini.Get(SurfaceColorsSection, SPRITEFONTCOLOR).ToString(ColorMap.GetStringFromColor(SpriteFontColor));

            gps.Load(ini, rawCustomData);


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

            //NamesTagsSection. LCD Settings is not written, so the next parse drops that key.
            iniChanged |= ReadAndDetectChange(ini, NamesTagsSection, INI_GRID_NAME, gc.GridName);
            iniChanged |= ReadAndDetectChange(ini, NamesTagsSection, INI_CONTROLLER_TAG, ControllerTag);
            iniChanged |= ReadAndDetectChange(ini, NamesTagsSection, INI_OVERRIDE_BLOCKS_TAG, OverrideBlockTag);
            iniChanged |= ReadAndDetectChange(ini, NamesTagsSection, INI_IGNORE_TAG, IgnoreTag);
            iniChanged |= ReadAndDetectChange(ini, NamesTagsSection, INI_LCD1_TAG, Lcd1Tag);
            iniChanged |= ReadAndDetectChange(ini, NamesTagsSection, INI_LCD2_TAG, Lcd2Tag);
            iniChanged |= ReadAndDetectChange(ini, NamesTagsSection, INI_MENU_TAG, LcdSettingsTag);
            iniChanged |= ReadAndDetectChange(ini, NamesTagsSection, INI_COCKPIT_TAG, CockpitTag);
            iniChanged |= ReadAndDetectChange(ini, NamesTagsSection, INI_RAY_CAMERA_TAG, RayCameraTag);
            iniChanged |= ReadAndDetectChange(ini, NamesTagsSection, BACKUP_BATTERY_TAG, BackupBatteryTag);

            iniChanged |= ReadAndDetectChange(ini, ScriptTagsSection, INI_DOCK_TRIGGER_TAG, DockTriggerTag);
            iniChanged |= ReadAndDetectChange(ini, ScriptTagsSection, INI_LANDING_PAD_TAG, LandingPadTag);

            iniChanged |= ReadAndDetectChange(ini, GroupsSection, INI_DOCK_GROUP_TAG, DockGroupTag);
            iniChanged |= ReadAndDetectChange(ini, GroupsSection, INI_DOCK_TRIGGER_GROUP, DockTriggerGroup);
            iniChanged |= ReadAndDetectChange(ini, GroupsSection, INI_LANDING_PAD_GROUP, LandingPadGroup);
            // Snapshot keys are the ini key alone. Restore uses the same key name, so it must not go through that map.
            ini.Set(RestoreSection, INI_DOCK_TRIGGER_TAG, dockTriggerRestore ?? "");
            ini.Set(RestoreSection, INI_LANDING_PAD_TAG, landingPadRestore ?? "");
            if (flightRestoreSet)
                ini.Set(RestoreSection, FLIGHT_SYSTEMS, flightRestoreValue ? "true" : "false");

            //ParamsSection
            iniChanged |= ReadAndDetectChange(ini, ParamsSection, MAX_SPEED, MaxSpeed);
            iniChanged |= ReadAndDetectChange(ini, ParamsSection, CRUISE_SPEED, CruiseSpeed);
            iniChanged |= ReadAndDetectChange(ini, ParamsSection, CNAV_ALTITUDE, SafeAltitude);
            iniChanged |= ReadAndDetectChange(ini, ParamsSection, DISTANCE_TO_GPS, DistanceToGPS);
            iniChanged |= ReadAndDetectChange(ini, ParamsSection, MINIMUM_ACCEPTED_FUEL, MinimumAcceptedFuel);

            //SurfaceColorsSection
            iniChanged |= ReadAndDetectChange(ini, SurfaceColorsSection, LCDBACKGROUNDCOLOR, LcdBackgroundColor);
            iniChanged |= ReadAndDetectChange(ini, SurfaceColorsSection, LCDFONTCOLOR, LcdFontColor);
            iniChanged |= ReadAndDetectChange(ini, SurfaceColorsSection, SPRITEMENUCOLOR, ColorMap.GetStringFromColor(SpriteMenuColor));
            iniChanged |= ReadAndDetectChange(ini, SurfaceColorsSection, SPRITEBACKGROUNDCOLOR, ColorMap.GetStringFromColor(SpriteBackgroundColor));
            iniChanged |= ReadAndDetectChange(ini, SurfaceColorsSection, SPRITEFONTCOLOR, ColorMap.GetStringFromColor(SpriteFontColor));

            ini.Set(AvailableColorsSection, COLORS, colors);

            gps.Save(ini);

            // The saved value was the old default on this parse. The next parse holds the new value and does not scan.
            if (renameOld.Count > 0)
                gc.ReplaceNameTokens(renameOld, renameNew);

            WriteCustomData();

            return iniChanged;
        }

        string ReadMenuTag()
        {
            if (ini.ContainsKey(NamesTagsSection, INI_MENU_TAG))
                return ini.Get(NamesTagsSection, INI_MENU_TAG).ToString(NewMenu);
            if (ini.ContainsKey(NamesTagsSection, INI_LCD_SETTINGS_TAG))
                return ini.Get(NamesTagsSection, INI_LCD_SETTINGS_TAG).ToString(OldMenu);
            return NewMenu;
        }

        // New section wins. A missing key is read from Names & Tags once, then omitted on the write.
        string ReadMoved(string section, string key, string fallback)
        {
            if (ini.ContainsKey(section, key))
                return ini.Get(section, key).ToString(fallback);
            if (section != NamesTagsSection && ini.ContainsKey(NamesTagsSection, key))
                return ini.Get(NamesTagsSection, key).ToString(fallback);
            return fallback;
        }

        string MigrateDefault(string saved, string oldDefault, string newDefault)
        {
            if (saved == oldDefault)
            {
                QueueNameToken(oldDefault, newDefault);
                return newDefault;
            }
            return saved;
        }

        void QueueNameToken(string oldToken, string newToken)
        {
            if (string.IsNullOrEmpty(oldToken) || oldToken == newToken)
                return;
            renameOld.Add(oldToken);
            renameNew.Add(newToken);
        }

        public void FlushGps()
        {
            gps.Save(ini);
            WriteCustomData();
        }

        void ApplyFlightSystemsStaticHold()
        {
            bool gridStatic = gc.Me != null && gc.Me.CubeGrid != null && !gc.Me.CubeGrid.Closed && gc.Me.CubeGrid.IsStatic;
            if (gridStatic)
            {
                if (!flightRestoreSet)
                {
                    flightRestoreValue = allowFlightSystems;
                    flightRestoreSet = true;
                }
                allowFlightSystems = false;
            }
            else if (flightRestoreSet)
            {
                allowFlightSystems = flightRestoreValue;
                flightRestoreSet = false;
            }
        }

        public void SetDockTriggerRestore(string current)
        {
            dockTriggerRestore = current ?? "";
            if (!ini.ContainsSection(NamesTagsSection))
                return;
            UpdateIni(RestoreSection, INI_DOCK_TRIGGER_TAG, dockTriggerRestore);
        }

        public void SetLandingPadRestore(string current)
        {
            landingPadRestore = current ?? "";
            if (!ini.ContainsSection(NamesTagsSection))
                return;
            UpdateIni(RestoreSection, INI_LANDING_PAD_TAG, landingPadRestore);
        }

        void UpdateIni(string section, string key, object newVal)
        {
            ini.Set(section, key, newVal.ToString());
            WriteCustomData();
        }

        void WriteCustomData()
        {
            gc.Me.CustomData = $"[Flight Systems: {gc.Me.EntityId}]\n\n" + ini.ToString();
        }

        bool ReadAndDetectChange(MyIni ini, string section, string key, object newVal)
        {
            return snapshot.ReadAndDetectChange(ini, section, key, newVal);
        }
    }
}
