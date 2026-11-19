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
        string markerOverride;

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
        public const string TEXT_PANELS = "Text Panels";

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
        bool textPanels = false;

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
        string lcdMenuTag = "[FS_menu]";
        string cockpitTag = "[FS_cockpit]";
        string rayCameraTag = "[FS_ray]";
        string backupBatteryTag = "[FS_backup]";
        string dockTriggerGroup = "Dock trigger";
        string landingPadGroup = "Landing pad";
        string dockTriggerTag = "[FS_trigger]";
        string dockTriggerRestore = "";
        string landingPadTag = "[FS_pad]";
        string landingPadRestore = "";

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

        public const string LCDBACKGROUNDCOLOR = "LCD Background Color";
        public const string LCDFONTCOLOR = "LCD Font Color";
        public const string SPRITEMENUCOLOR = "Sprite Menu Color";
        public const string SPRITEBACKGROUNDCOLOR = "Sprite Background Color";
        public const string SPRITEFONTCOLOR = "Sprite Font Color";

        static readonly Color DefaultLcdBackground = new Color(0, 0, 0);
        static readonly Color DefaultLcdFont = new Color(255, 255, 255);
        static readonly Color DefaultSpriteMenu = new Color(255, 255, 255);
        static readonly Color DefaultSpriteBackground = new Color(0, 0, 0);
        static readonly Color DefaultSpriteFont = new Color(255, 255, 255);

        Color lcdBackgroundColor = DefaultLcdBackground;
        Color lcdFontColor = DefaultLcdFont;
        Color spriteMenuColor = DefaultSpriteMenu;
        Color spriteBackgroundColor = DefaultSpriteBackground;
        Color spriteFontColor = DefaultSpriteFont;

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
        public string LcdMenuTag => lcdMenuTag;
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

        public bool TextPanels
        {
            get { return textPanels; }
            set
            {
                textPanels = value;
                GridContext.PaintAsText = value;
                UpdateIni(ToggleSection, TEXT_PANELS, textPanels);
            }
        }

        public Color LcdBackgroundColor
        {
            get { return lcdBackgroundColor; }
            set
            {
                lcdBackgroundColor = value;
                UpdateIni(SurfaceColorsSection, LCDBACKGROUNDCOLOR, FormatRgb(value));
            }
        }
        public Color LcdFontColor
        {
            get { return lcdFontColor; }
            set
            {
                lcdFontColor = value;
                UpdateIni(SurfaceColorsSection, LCDFONTCOLOR, FormatRgb(value));
            }
        }
        public Color SpriteMenuColor
        {
            get { return spriteMenuColor; }
            set
            {
                spriteMenuColor = value;
                UpdateIni(SurfaceColorsSection, SPRITEMENUCOLOR, FormatRgb(value));
            }
        }
        public Color SpriteBackgroundColor
        {
            get { return spriteBackgroundColor; }
            set
            {
                spriteBackgroundColor = value;
                UpdateIni(SurfaceColorsSection, SPRITEBACKGROUNDCOLOR, FormatRgb(value));
            }
        }
        public Color SpriteFontColor
        {
            get { return spriteFontColor; }
            set
            {
                spriteFontColor = value;
                UpdateIni(SurfaceColorsSection, SPRITEFONTCOLOR, FormatRgb(value));
            }
        }

        public static string FormatRgb(Color color)
        {
            return color.R + "," + color.G + "," + color.B;
        }


        // ───────────────────────────────────────
        // Load config from CustomData (INI style)
        // ───────────────────────────────────────      
        public bool ParseIni()
        {
            ini.Clear();
            iniChanged = false;

            string rawCustomData = gc.Me.CustomData ?? "";
            // A missing GPS end must not wipe the lines the player pasted.
            if (!gps.LoadBlock(rawCustomData))
                return false;

            if (!ini.TryParse(rawCustomData)) return IniChanged;

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
            textPanels = ini.Get(ToggleSection, TEXT_PANELS).ToBoolean(TextPanels);
            GridContext.PaintAsText = textPanels;

            //NamesTagsSection
            string tempGridName = ini.Get(NamesTagsSection, INI_GRID_NAME).ToString(gc.GridName);
            gc.GridName = string.IsNullOrWhiteSpace(tempGridName) ? gc.GridName : tempGridName;

            dockGroupTag = ReadMoved(GroupsSection, INI_DOCK_GROUP_TAG, DockGroupTag);
            controllerTag = ini.Get(NamesTagsSection, INI_CONTROLLER_TAG).ToString(ControllerTag);
            overrideBlockTag = ini.Get(NamesTagsSection, INI_OVERRIDE_BLOCKS_TAG).ToString(OverrideBlockTag);
            ignoreTag = ini.Get(NamesTagsSection, INI_IGNORE_TAG).ToString(IgnoreTag);
            lcd1Tag = DropOldDefault(ini.Get(NamesTagsSection, INI_LCD1_TAG).ToString(NewLcd1), OldLcd1, NewLcd1);
            lcd2Tag = DropOldDefault(ini.Get(NamesTagsSection, INI_LCD2_TAG).ToString(NewLcd2), OldLcd2, NewLcd2);
            lcdMenuTag = DropOldDefault(ini.Get(NamesTagsSection, INI_MENU_TAG).ToString(NewMenu), OldMenu, NewMenu);
            cockpitTag = DropOldDefault(ini.Get(NamesTagsSection, INI_COCKPIT_TAG).ToString(NewCockpit), OldCockpit, NewCockpit);
            rayCameraTag = DropOldDefault(ini.Get(NamesTagsSection, INI_RAY_CAMERA_TAG).ToString(NewRay), OldRay, NewRay);
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
            lcdBackgroundColor = ReadRgb(LCDBACKGROUNDCOLOR, DefaultLcdBackground);
            lcdFontColor = ReadRgb(LCDFONTCOLOR, DefaultLcdFont);
            spriteMenuColor = ReadRgb(SPRITEMENUCOLOR, DefaultSpriteMenu);
            spriteBackgroundColor = ReadRgb(SPRITEBACKGROUNDCOLOR, DefaultSpriteBackground);
            spriteFontColor = ReadRgb(SPRITEFONTCOLOR, DefaultSpriteFont);

            // ───────────────────── 
            // Delete PB Custom Data
            // ─────────────────────  
            ini.Clear();

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
            iniChanged |= ReadAndDetectChange(ini, ToggleSection, TEXT_PANELS, TextPanels);

            //NamesTagsSection. LCD Menu is not read, so a leftover key is dropped on write.
            iniChanged |= ReadAndDetectChange(ini, NamesTagsSection, INI_GRID_NAME, gc.GridName);
            iniChanged |= ReadAndDetectChange(ini, NamesTagsSection, INI_CONTROLLER_TAG, ControllerTag);
            iniChanged |= ReadAndDetectChange(ini, NamesTagsSection, INI_OVERRIDE_BLOCKS_TAG, OverrideBlockTag);
            iniChanged |= ReadAndDetectChange(ini, NamesTagsSection, INI_IGNORE_TAG, IgnoreTag);
            iniChanged |= ReadAndDetectChange(ini, NamesTagsSection, INI_LCD1_TAG, Lcd1Tag);
            iniChanged |= ReadAndDetectChange(ini, NamesTagsSection, INI_LCD2_TAG, Lcd2Tag);
            iniChanged |= ReadAndDetectChange(ini, NamesTagsSection, INI_MENU_TAG, LcdMenuTag);
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
            iniChanged |= ReadAndDetectChange(ini, SurfaceColorsSection, LCDBACKGROUNDCOLOR, FormatRgb(lcdBackgroundColor));
            iniChanged |= ReadAndDetectChange(ini, SurfaceColorsSection, LCDFONTCOLOR, FormatRgb(lcdFontColor));
            iniChanged |= ReadAndDetectChange(ini, SurfaceColorsSection, SPRITEMENUCOLOR, FormatRgb(spriteMenuColor));
            iniChanged |= ReadAndDetectChange(ini, SurfaceColorsSection, SPRITEBACKGROUNDCOLOR, FormatRgb(spriteBackgroundColor));
            iniChanged |= ReadAndDetectChange(ini, SurfaceColorsSection, SPRITEFONTCOLOR, FormatRgb(spriteFontColor));

            WriteCustomData();

            return iniChanged;
        }

        // An exact old default is not a tag. A value the player changed is kept. Blocks are not renamed.
        static string DropOldDefault(string saved, string oldDefault, string newDefault)
        {
            if (saved == oldDefault)
                return newDefault;
            return saved;
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

        Color ReadRgb(string key, Color fallback)
        {
            string raw = ini.Get(SurfaceColorsSection, key).ToString("");
            Color parsed;
            if (TryParseRgb(raw, out parsed))
                return parsed;
            return fallback;
        }

        static bool TryParseRgb(string text, out Color color)
        {
            color = new Color(0, 0, 0);
            if (string.IsNullOrEmpty(text))
                return false;

            int i = 0;
            int r;
            int g;
            int b;
            if (!ReadChannel(text, ref i, out r))
                return false;
            if (!ReadComma(text, ref i))
                return false;
            if (!ReadChannel(text, ref i, out g))
                return false;
            if (!ReadComma(text, ref i))
                return false;
            if (!ReadChannel(text, ref i, out b))
                return false;
            while (i < text.Length && text[i] == ' ')
                i++;
            if (i != text.Length)
                return false;

            color = new Color(r, g, b);
            return true;
        }

        static bool ReadChannel(string text, ref int i, out int value)
        {
            value = 0;
            while (i < text.Length && text[i] == ' ')
                i++;
            if (i >= text.Length || text[i] < '0' || text[i] > '9')
                return false;

            int n = 0;
            while (i < text.Length && text[i] >= '0' && text[i] <= '9')
            {
                int digit = text[i] - '0';
                if (n > 25)
                    n = 255;
                else
                    n = n * 10 + digit;
                if (n > 255)
                    n = 255;
                i++;
            }

            value = n;
            return true;
        }

        static bool ReadComma(string text, ref int i)
        {
            while (i < text.Length && text[i] == ' ')
                i++;
            if (i >= text.Length || text[i] != ',')
                return false;
            i++;
            return true;
        }

        void UpdateIni(string section, string key, object newVal)
        {
            ini.Set(section, key, newVal.ToString());
            WriteCustomData();
        }

        public bool CreateGpsHere(GridContext grid)
        {
            if (grid == null || grid.Controller == null || grid.Controller.Closed || gc.Me == null)
                return false;

            Vector3D here = grid.Controller.GetPosition();
            int x = GpsStore.NearestMeter(here.X);
            int y = GpsStore.NearestMeter(here.Y);
            int z = GpsStore.NearestMeter(here.Z);
            Vector3D rounded = new Vector3D(x, y, z);
            if (gps.Near(here) || gps.Near(rounded))
                return false;

            string line = "GPS:" + gps.NextMarkName() + ":" + x + ":" + y + ":" + z + ":#FF75C9F1:";
            string old = gc.Me.CustomData ?? "";
            string block;
            if (!GpsStore.TryMarkerBlock(old, out block))
                return false;
            string updated = GpsStore.WithPoint(block, line);
            if (updated == null)
                return false;
            ReplaceMarkerBlock(updated);
            return true;
        }

        public bool DeleteGpsPoint(int index)
        {
            if (gc.Me == null)
                return false;
            string old = gc.Me.CustomData ?? "";
            string block;
            if (!GpsStore.TryMarkerBlock(old, out block))
                return false;
            string updated = GpsStore.WithoutPoint(block, index);
            if (updated == null)
                return false;
            ReplaceMarkerBlock(updated);
            return true;
        }

        void ReplaceMarkerBlock(string block)
        {
            markerOverride = block;
            WriteCustomData();
            markerOverride = null;
            gps.LoadBlock(gc.Me.CustomData ?? "");
        }

        void WriteCustomData()
        {
            string old = gc.Me.CustomData ?? "";
            string block;
            if (markerOverride != null)
                block = markerOverride;
            else if (!GpsStore.TryMarkerBlock(old, out block))
                return;

            string body = ini.ToString();
            if (body.Length == 0 || body[body.Length - 1] != '\n')
                body += "\n";
            gc.Me.CustomData = "[Flight Systems: " + gc.Me.EntityId + "]\n\n" + body + block;
        }

        bool ReadAndDetectChange(MyIni ini, string section, string key, object newVal)
        {
            return snapshot.ReadAndDetectChange(ini, section, key, newVal);
        }
    }
}
