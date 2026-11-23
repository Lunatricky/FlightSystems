using IngameScript.Enums;
using IngameScript.UseCases;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace IngameScript.Domain
{
    public class IniContext
    {
        readonly MyIni ini = new MyIni();
        readonly GridContext gc;
        readonly GpsStore gps = new GpsStore();
        readonly List<string> snapSection = new List<string>();
        readonly List<string> snapKey = new List<string>();
        readonly List<string> snapValue = new List<string>();
        readonly List<string> sectionScratch = new List<string>();
        readonly List<MyIniKey> keyScratch = new List<MyIniKey>();
        string seenCustomData;
        string pendingSection;
        string pendingKey;
        string pendingValue;
        bool reloadPending;
        bool commandRead;
        bool restoreHold;
        bool colorSeed;
        bool colorRead;
        bool forceColorWrite;
        MainState savedState = MainState.Idle;
        Step savedStep = Step.Toggle;
        AutoLandState savedLand = AutoLandState.Idle;
        double savedNumber;
        bool savedTarget;
        int savedX;
        int savedY;
        int savedZ;
        string warnA;
        string warnB;
        string warnEcho;
        int warnCount;

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
        public const string HomeConnectorTag = "[FS_home]";

        const bool DefaultFlightSystems = true;
        const bool DefaultAnalogThrotle = false;
        const bool DefaultLowFuelLand = false;
        const bool DefaultDockMode = false;
        const bool DefaultControlAntennas = false;
        const bool DefaultRenameSubgrids = false;
        const bool DefaultPaintSurfaces = false;
        const bool DefaultTransparentLCD = true;
        const bool DefaultTextPanels = false;

        bool allowFlightSystems = DefaultFlightSystems;
        bool analogThrotle = DefaultAnalogThrotle;
        bool allowLowFuelLand = DefaultLowFuelLand;
        bool allowDockMode = DefaultDockMode;
        bool controlAntennas = DefaultControlAntennas;
        bool renameSubgrids = DefaultRenameSubgrids;
        bool paintSurfaces = DefaultPaintSurfaces;
        bool transparentLCD = DefaultTransparentLCD;
        bool textPanels = DefaultTextPanels;
        int toggleBits;

        //Restore state from server restart section
        const string CommandSection = "Command";
        const string KeyState = "State";
        const string KeyStep = "Step";
        const string KeyLand = "AutoLand";
        const string KeyNumber = "Number";
        const string KeyX = "X";
        const string KeyY = "Y";
        const string KeyZ = "Z";

        //NamesTagsSection
        const string GridSection = "Grid tags []";

        const string INI_GRID_NAME = "Name";

        const string INI_CONTROLLER_TAG = "RC";
        const string INI_COCKPIT_TAG = "Cockpit";
        const string INI_IGNORE_TAG = "Ignore";
        const string INI_LCD1_TAG = "LCD 1";
        const string INI_LCD2_TAG = "LCD 2";
        const string INI_MENU_TAG = "Menu";
        const string INI_RAY_CAMERA_TAG = "Ray Cam";
        const string BACKUP_BATTERY_TAG = "Backup batt";

        string controllerTag = "[FS_reference]";
        string cockpitTag = "[FS_cockpit]";
        string ignoreTag = "[FS_ignore]";
        string lcd1Tag = "[FS_lcd1]";
        string lcd2Tag = "[FS_lcd2]";
        string lcdMenuTag = "[FS_menu]";
        string rayCameraTag = "[FS_ray]";
        string backupBatteryTag = "[FS_backup]";

        //Dock Mode section
        const string DockSection = "Dock groups - and tags []";

        const string INI_DOCK_GROUP = "Dock";
        const string INI_DOCK_TRIGGER_GROUP = "Trigger";
        const string INI_LANDING_PAD_GROUP = "LPad";

        const string INI_OVERRIDE_BLOCKS_TAG = "Override Blocks";
        const string INI_DOCK_TRIGGER_TAG = "Trigger Tag";
        const string INI_LANDING_PAD_TAG = "LPad Tag";

        string dockGroup = "-Dock Mode";
        string dockTriggerGroup = "-Dock trigger";
        string landingPadGroup = "-Landing pad";

        string overrideBlockTag = "[FS_override]";
        string dockTriggerTag = "[FS_trigger]";
        string landingPadTag = "[FS_pad]";

        //Params section
        public const string ParamsSection = "Params";

        public const string MAX_SPEED = "Max Speed";
        public const string CRUISE_SPEED = "Cruise Speed";
        public const string CNAV_ALTITUDE = "Cnav Altitude";
        public const string DISTANCE_TO_GPS = "Distance to GPS";
        public const string MINIMUM_ACCEPTED_FUEL = "Min Fuel";

        public string[] IniParamList = new string[5] { MAX_SPEED, CRUISE_SPEED, CNAV_ALTITUDE, DISTANCE_TO_GPS, MINIMUM_ACCEPTED_FUEL };

        double maxSpeed = double.PositiveInfinity; // m/s
        double cruiseSpeed = 100; // m/s
        double cnavAltitude = 1000; // m
        double distanceToGPS = 500; // m
        double minimumAcceptedFuel = 20; //%

        //Surface colors section
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

        public string DockGroupTag => dockGroup;
        public string ControllerTag => controllerTag;
        public string OverrideBlockTag => overrideBlockTag;
        public string IgnoreTag => ignoreTag;
        public string DockTriggerGroup => dockTriggerGroup;
        public string LandingPadGroup => landingPadGroup;
        public string DockTriggerTag => dockTriggerTag;
        public string LandingPadTag => landingPadTag;
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
                if ((toggleBits & 1) == 0)
                    return;
                allowFlightSystems = value;
                UpdateIni(ToggleSection, FLIGHT_SYSTEMS, allowFlightSystems);
            }
        }

        public bool AnalogThrotle
        {
            get {return analogThrotle;}
            set 
            {
                if ((toggleBits & 2) == 0)
                    return;
                analogThrotle = value;
                UpdateIni(ToggleSection, ANALOG_THROTLE, analogThrotle);
            }
        }

        public bool AllowLowFuelLand
        {
            get {return allowLowFuelLand; }
            set
            {
                if ((toggleBits & 4) == 0)
                    return;
                allowLowFuelLand = value;
                UpdateIni(ToggleSection, LOW_FUEL_LAND, allowLowFuelLand);
            }
        }

        public bool AllowDockMode
        {
            get {return allowDockMode; }
            set
            {
                if ((toggleBits & 8) == 0)
                    return;
                allowDockMode = value;
                UpdateIni(ToggleSection, DOCK_MODE, allowDockMode);
            }
        }

        public bool ControlAntennas
        {
            get { return controlAntennas; }
            set
            {
                if ((toggleBits & 16) == 0)
                    return;
                controlAntennas = value;
                UpdateIni(ToggleSection, CONTROL_ANTENNAS, controlAntennas);
            }
        }

        public bool RenameSubgrids
        {
            get { return renameSubgrids; }
            set
            {
                if ((toggleBits & 32) == 0)
                    return;
                renameSubgrids = value;
                UpdateIni(ToggleSection, RENAME_SUBGRIDS, renameSubgrids);
            }
        }

        public bool PaintSurfaces
        {
            get { return paintSurfaces; }
            set
            {
                if ((toggleBits & 64) == 0)
                    return;
                paintSurfaces = value;
                UpdateIni(ToggleSection, PAINT_SURFACES, paintSurfaces);
            }
        }

        public bool TransparentLCD
        {
            get { return transparentLCD; }
            set
            {
                if ((toggleBits & 128) == 0)
                    return;
                transparentLCD = value;
                UpdateIni(ToggleSection, TRANSPARENTLCD, transparentLCD);
            }
        }

        public bool TextPanels
        {
            get { return textPanels; }
            set
            {
                if ((toggleBits & 256) == 0)
                    return;
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

        public bool NeedsColorSeed { get { return colorSeed && !colorRead; } }

        public void SeedScreenColors(Color font, Color background)
        {
            if (!colorSeed || colorRead)
                return;
            colorRead = true;
            colorSeed = false;
            lcdBackgroundColor = background;
            lcdFontColor = font;
            spriteBackgroundColor = background;
            spriteFontColor = font;
            spriteMenuColor = background;
            forceColorWrite = true;
            UpdateIni(SurfaceColorsSection, LCDBACKGROUNDCOLOR, FormatRgb(background));
            forceColorWrite = false;
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
            gps.LoadText(rawCustomData);
            string markerBlock;
            bool gpsOk = GpsStore.TryMarkerBlock(rawCustomData, out markerBlock);
            // GPS lines are not ini. A missing end still leaves the ini above GPS start.
            bool parsed = ini.TryParse(GpsStore.WithoutMarker(rawCustomData));
            if (!parsed)
            {
                ReadToggles(false);
                return false;
            }

            List<string> sectionsNames = new List<string>();
            string[] array = { GridSection, ParamsSection, ToggleSection };
            sectionsNames.AddArray(array);

            foreach (string sectionName in sectionsNames)
                if (!ini.ContainsSection(sectionName))
                {
                    ini.AddSection(sectionName);
                }


            // ───────────────────────────────────────────
            // Read PB Custom Data and populate properties
            // ───────────────────────────────────────────

            //ToggleSection. A missing key uses the code default, not the live field.
            ReadToggles(true);

            //NamesTagsSection
            string tempGridName = ini.Get(GridSection, INI_GRID_NAME).ToString(gc.GridName);
            gc.GridName = string.IsNullOrWhiteSpace(tempGridName) ? gc.GridName : tempGridName;

            dockGroup = ReadMoved(DockSection, INI_DOCK_GROUP, DockGroupTag);
            controllerTag = ini.Get(GridSection, INI_CONTROLLER_TAG).ToString(ControllerTag);
            overrideBlockTag = ini.Get(GridSection, INI_OVERRIDE_BLOCKS_TAG).ToString(OverrideBlockTag);
            ignoreTag = ini.Get(GridSection, INI_IGNORE_TAG).ToString(IgnoreTag);
            lcd1Tag = ini.Get(GridSection, INI_LCD1_TAG).ToString(lcd1Tag);
            lcd2Tag = ini.Get(GridSection, INI_LCD2_TAG).ToString(lcd2Tag);
            lcdMenuTag = ini.Get(GridSection, INI_MENU_TAG).ToString(lcdMenuTag);
            cockpitTag = ini.Get(GridSection, INI_COCKPIT_TAG).ToString(cockpitTag);
            rayCameraTag = ini.Get(GridSection, INI_RAY_CAMERA_TAG).ToString(rayCameraTag);
            backupBatteryTag = ini.Get(GridSection, BACKUP_BATTERY_TAG).ToString(BackupBatteryTag);
            dockTriggerGroup = ReadMoved(DockSection, INI_DOCK_TRIGGER_GROUP, DockTriggerGroup);
            landingPadGroup = ReadMoved(DockSection, INI_LANDING_PAD_GROUP, LandingPadGroup);
            dockTriggerTag = ReadMoved(DockSection, INI_DOCK_TRIGGER_TAG, "[FS_trigger]");
            landingPadTag = ReadMoved(DockSection, INI_LANDING_PAD_TAG, "[FS_pad]");

            //ParamsSection
            bool speedMissing = !ini.ContainsKey(ParamsSection, MAX_SPEED);
            string speedText = ini.Get(ParamsSection, MAX_SPEED).ToString("");
            maxSpeed = ini.Get(ParamsSection, MAX_SPEED).ToDouble(MaxSpeed);
            double parsedSpeed;
            if (speedText == "Infinity" || speedText == "Infinite" || speedText == "" || speedText == "00")
            {
                maxSpeed = double.PositiveInfinity;
            }
            else if (!double.TryParse(speedText, out parsedSpeed)) maxSpeed = parsedSpeed;

            bool cruiseMissing = !ini.ContainsKey(ParamsSection, CRUISE_SPEED);
            string cruiseText = ini.Get(ParamsSection, CRUISE_SPEED).ToString("");
            cruiseSpeed = ini.Get(ParamsSection, CRUISE_SPEED).ToDouble(CruiseSpeed);
            double parsedCruise;if (!double.TryParse(cruiseText, out parsedCruise)) cruiseSpeed = parsedCruise;

            cnavAltitude = ini.Get(ParamsSection, CNAV_ALTITUDE).ToDouble(SafeAltitude);
            distanceToGPS = ini.Get(ParamsSection, DISTANCE_TO_GPS).ToDouble(DistanceToGPS);
            minimumAcceptedFuel = ini.Get(ParamsSection, MINIMUM_ACCEPTED_FUEL).ToDouble(MinimumAcceptedFuel);

            //SurfaceColorsSection
            lcdBackgroundColor = ReadRgb(LCDBACKGROUNDCOLOR, DefaultLcdBackground);
            lcdFontColor = ReadRgb(LCDFONTCOLOR, DefaultLcdFont);
            spriteMenuColor = ReadRgb(SPRITEMENUCOLOR, DefaultSpriteMenu);
            spriteBackgroundColor = ReadRgb(SPRITEBACKGROUNDCOLOR, DefaultSpriteBackground);
            spriteFontColor = ReadRgb(SPRITEFONTCOLOR, DefaultSpriteFont);
            if (!colorRead)
                colorSeed = !ini.ContainsKey(SurfaceColorsSection, LCDBACKGROUNDCOLOR)
                    || !ini.ContainsKey(SurfaceColorsSection, LCDFONTCOLOR)
                    || !ini.ContainsKey(SurfaceColorsSection, SPRITEBACKGROUNDCOLOR)
                    || !ini.ContainsKey(SurfaceColorsSection, SPRITEFONTCOLOR);

            if (!gpsOk || markerBlock == null)
                return false;
            iniChanged = reloadPending || seenCustomData != rawCustomData;
            reloadPending = false;
            RewriteFromParsed();
            return iniChanged;
        }

        void ReadToggles(bool parsed)
        {
            allowFlightSystems = ReadToggle(FLIGHT_SYSTEMS, DefaultFlightSystems, parsed, 1);
            analogThrotle = ReadToggle(ANALOG_THROTLE, DefaultAnalogThrotle, parsed, 2);
            allowLowFuelLand = ReadToggle(LOW_FUEL_LAND, DefaultLowFuelLand, parsed, 4);
            allowDockMode = ReadToggle(DOCK_MODE, DefaultDockMode, parsed, 8);
            controlAntennas = ReadToggle(CONTROL_ANTENNAS, DefaultControlAntennas, parsed, 16);
            renameSubgrids = ReadToggle(RENAME_SUBGRIDS, DefaultRenameSubgrids, parsed, 32);
            paintSurfaces = ReadToggle(PAINT_SURFACES, DefaultPaintSurfaces, parsed, 64);
            transparentLCD = ReadToggle(TRANSPARENTLCD, DefaultTransparentLCD, parsed, 128);
            textPanels = ReadToggle(TEXT_PANELS, DefaultTextPanels, parsed, 256);
            GridContext.PaintAsText = textPanels;
        }

        bool ReadToggle(string key, bool codeDefault, bool parsed, int bit)
        {
            toggleBits &= ~bit;
            if (!ini.ContainsKey(ToggleSection, key))
            {
                if (parsed)
                    toggleBits |= bit;
                return codeDefault;
            }

            bool value;
            if (!bool.TryParse(ini.Get(ToggleSection, key).ToString("").Trim(), out value))
                return codeDefault;
            toggleBits |= bit;
            return value;
        }

        // New section wins. A missing key is read from Names & Tags once, then omitted on the write.
        string ReadMoved(string section, string key, string fallback)
        {
            if (ini.ContainsKey(section, key))
                return ini.Get(section, key).ToString(fallback);
            if (section != GridSection && ini.ContainsKey(GridSection, key))
                return ini.Get(GridSection, key).ToString(fallback);
            return fallback;
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
            if (gc.Me == null)
                return;
            string raw = gc.Me.CustomData ?? "";
            string block;
            if (!GpsStore.TryMarkerBlock(raw, out block))
                return;
            ini.Clear();
            if (!ini.TryParse(GpsStore.WithoutMarker(raw)))
                return;

            string text = newVal.ToString();
            bool differ = !ini.ContainsKey(section, key) || ini.Get(section, key).ToString("") != text;
            pendingSection = section;
            pendingKey = key;
            pendingValue = text;
            RewriteFromParsed();
            pendingSection = null;
            pendingKey = null;
            pendingValue = null;
            if (differ)
                reloadPending = true;
        }

        // Owned keys go out in the order they are set. A present key keeps the player's text.
        void RewriteFromParsed()
        {
            ReadSavedCommand();
            SnapshotParsed();
            ini.Clear();

            SetKept(ToggleSection, FLIGHT_SYSTEMS, AllowFlightSystems.ToString());
            SetKept(ToggleSection, ANALOG_THROTLE, AnalogThrotle.ToString());
            SetKept(ToggleSection, LOW_FUEL_LAND, AllowLowFuelLand.ToString());
            SetKept(ToggleSection, DOCK_MODE, AllowDockMode.ToString());
            SetKept(ToggleSection, CONTROL_ANTENNAS, ControlAntennas.ToString());
            SetKept(ToggleSection, RENAME_SUBGRIDS, RenameSubgrids.ToString());
            SetKept(ToggleSection, PAINT_SURFACES, PaintSurfaces.ToString());
            SetKept(ToggleSection, TRANSPARENTLCD, TransparentLCD.ToString());
            SetKept(ToggleSection, TEXT_PANELS, TextPanels.ToString());

            SetKept(GridSection, INI_GRID_NAME, gc.GridName ?? "");
            SetKept(GridSection, INI_CONTROLLER_TAG, ControllerTag ?? "");
            SetKept(GridSection, INI_IGNORE_TAG, IgnoreTag ?? "");
            SetKept(GridSection, INI_LCD1_TAG, Lcd1Tag ?? "");
            SetKept(GridSection, INI_LCD2_TAG, Lcd2Tag ?? "");
            SetKept(GridSection, INI_MENU_TAG, LcdMenuTag ?? "");
            SetKept(GridSection, INI_COCKPIT_TAG, CockpitTag ?? "");
            SetKept(GridSection, INI_RAY_CAMERA_TAG, RayCameraTag ?? "");
            SetKept(GridSection, BACKUP_BATTERY_TAG, BackupBatteryTag ?? "");


            SetKept(DockSection, INI_DOCK_GROUP, DockGroupTag ?? "");
            SetKept(DockSection, INI_DOCK_TRIGGER_GROUP, DockTriggerGroup ?? "");
            SetKept(DockSection, INI_LANDING_PAD_GROUP, LandingPadGroup ?? "");
            SetKept(DockSection, INI_OVERRIDE_BLOCKS_TAG, OverrideBlockTag ?? "");
            SetKept(DockSection, INI_DOCK_TRIGGER_TAG, DockTriggerTag ?? "");
            SetKept(DockSection, INI_LANDING_PAD_TAG, LandingPadTag ?? "");

            SetKept(ParamsSection, MAX_SPEED, MaxSpeed.ToString());
            SetKept(ParamsSection, CRUISE_SPEED, CruiseSpeed.ToString());
            SetKept(ParamsSection, CNAV_ALTITUDE, SafeAltitude.ToString());
            SetKept(ParamsSection, DISTANCE_TO_GPS, DistanceToGPS.ToString());
            SetKept(ParamsSection, MINIMUM_ACCEPTED_FUEL, MinimumAcceptedFuel.ToString());

            SetColor(LCDBACKGROUNDCOLOR, lcdBackgroundColor);
            SetColor(LCDFONTCOLOR, lcdFontColor);
            SetKept(SurfaceColorsSection, SPRITEMENUCOLOR, FormatRgb(spriteMenuColor));
            SetColor(SPRITEBACKGROUNDCOLOR, spriteBackgroundColor);
            SetColor(SPRITEFONTCOLOR, spriteFontColor);

            WriteCommand();
            WriteForeign();
            WriteCustomData();
        }

        internal void ClearRestoreHold()
        {
            restoreHold = false;
        }

        internal void ApplySavedCommand(Command command, ref SystemBools modes)
        {
            if (!restoreHold || savedState == MainState.Idle || command == null || command.Param == null)
                return;
            command.State = savedState;
            command.Param.Step = savedStep;
            command.Param.AutoLandState = savedLand;
            command.Param.Number = savedNumber;
            command.Param.TargetCoordinates = savedTarget ? new Vector3D(savedX, savedY, savedZ) : new Vector3D();
            modes.SetModeOn(savedState);
        }

        internal void SaveCommand(Command command)
        {
            if (!commandRead || command == null || command.Param == null)
                return;

            MainState state = command.State;
            if (state == MainState.Abort || state == MainState.Reload)
            {
                restoreHold = false;
                state = MainState.Idle;
            }

            Step step = command.Param.Step;
            AutoLandState land = command.Param.AutoLandState;
            double number = command.Param.Number;
            Vector3D at = command.Param.TargetCoordinates;
            bool target = at.X != 0 || at.Y != 0 || at.Z != 0;
            int x = 0;
            int y = 0;
            int z = 0;
            if (target)
            {
                x = GpsStore.NearestMeter(at.X);
                y = GpsStore.NearestMeter(at.Y);
                z = GpsStore.NearestMeter(at.Z);
                if (x == 0 && y == 0 && z == 0)
                    target = false;
            }

            if (restoreHold && state == MainState.Idle)
                return;

            if (restoreHold && (state != savedState || step != savedStep || land != savedLand || number != savedNumber || target != savedTarget || x != savedX || y != savedY || z != savedZ))
                restoreHold = false;

            if (state == savedState && step == savedStep && land == savedLand && number == savedNumber && target == savedTarget && x == savedX && y == savedY && z == savedZ)
                return;

            savedState = state;
            savedStep = step;
            savedLand = land;
            savedNumber = number;
            savedTarget = target;
            savedX = x;
            savedY = y;
            savedZ = z;
            RewriteFromParsed();
        }

        void ReadSavedCommand()
        {
            if (commandRead)
                return;
            commandRead = true;
            if (!ini.ContainsKey(CommandSection, KeyState))
                return;

            MainState state;
            if (!EnumLabels.TryState(ini.Get(CommandSection, KeyState).ToString(""), out state))
            {
                return;
            }
            if (state == MainState.Idle || state == MainState.Abort || state == MainState.Reload)
                return;

            Step step = Step.On;
            if (ini.ContainsKey(CommandSection, KeyStep))
            {
                Step parsed;
                if (!EnumLabels.TryStep(ini.Get(CommandSection, KeyStep).ToString(""), out parsed))
                {
                    return;
                }
                step = parsed == Step.Toggle ? Step.On : parsed;
            }

            AutoLandState land = AutoLandState.Idle;
            if (ini.ContainsKey(CommandSection, KeyLand))
            {
                AutoLandState parsedLand;
                if (!EnumLabels.TryLand(ini.Get(CommandSection, KeyLand).ToString(""), out parsedLand))
                {
                    return;
                }
                land = parsedLand;
            }

            double number = 0;
            if (ini.ContainsKey(CommandSection, KeyNumber))
            {
                double parsedNumber;
                if (!double.TryParse(ini.Get(CommandSection, KeyNumber).ToString(""), out parsedNumber))
                {
                    return;
                }
                number = parsedNumber;
            }

            bool hasX = ini.ContainsKey(CommandSection, KeyX);
            bool hasY = ini.ContainsKey(CommandSection, KeyY);
            bool hasZ = ini.ContainsKey(CommandSection, KeyZ);
            if (state == MainState.Gps && (!hasX || !hasY || !hasZ))
            {
                return;
            }

            bool target = false;
            int x = 0;
            int y = 0;
            int z = 0;
            if (hasX || hasY || hasZ)
            {
                if (!hasX || !hasY || !hasZ)
                {
                    return;
                }
                double dx;
                double dy;
                double dz;
                if (!double.TryParse(ini.Get(CommandSection, KeyX).ToString(""), out dx))
                {
                    return;
                }
                if (!double.TryParse(ini.Get(CommandSection, KeyY).ToString(""), out dy))
                {
                    return;
                }
                if (!double.TryParse(ini.Get(CommandSection, KeyZ).ToString(""), out dz))
                {
                    return;
                }
                x = GpsStore.NearestMeter(dx);
                y = GpsStore.NearestMeter(dy);
                z = GpsStore.NearestMeter(dz);
                target = x != 0 || y != 0 || z != 0;
                if (state == MainState.Gps && !target)
                {
                    return;
                }
            }

            savedState = state;
            savedStep = step;
            savedLand = land;
            savedNumber = number;
            savedTarget = target;
            savedX = x;
            savedY = y;
            savedZ = z;
            restoreHold = true;
        }

        void WriteCommand()
        {
            if (savedState == MainState.Idle)
                return;
            ini.Set(CommandSection, KeyState, EnumLabels.State(savedState));
            ini.Set(CommandSection, KeyStep, EnumLabels.StepName(savedStep));
            if (savedLand != AutoLandState.Idle)
                ini.Set(CommandSection, KeyLand, EnumLabels.Land(savedLand));
            if (savedNumber != 0)
                ini.Set(CommandSection, KeyNumber, savedNumber.ToString());
            if (!savedTarget)
                return;
            ini.Set(CommandSection, KeyX, savedX.ToString());
            ini.Set(CommandSection, KeyY, savedY.ToString());
            ini.Set(CommandSection, KeyZ, savedZ.ToString());
        }

        void SnapshotParsed()
        {
            snapSection.Clear();
            snapKey.Clear();
            snapValue.Clear();
            sectionScratch.Clear();
            ini.GetSections(sectionScratch);
            for (int s = 0; s < sectionScratch.Count; s++)
            {
                keyScratch.Clear();
                ini.GetKeys(sectionScratch[s], keyScratch);
                for (int k = 0; k < keyScratch.Count; k++)
                {
                    MyIniKey item = keyScratch[k];
                    snapSection.Add(item.Section);
                    snapKey.Add(item.Name);
                    snapValue.Add(ini.Get(item.Section, item.Name).ToString(""));
                }
            }
        }

        int FindSnap(string section, string key)
        {
            for (int i = 0; i < snapKey.Count; i++)
            {
                if (string.Equals(snapSection[i], section, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(snapKey[i], key, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        void SetColor(string key, Color color)
        {
            if (!forceColorWrite && FindSnap(SurfaceColorsSection, key) < 0)
            {
                if (pendingSection == null
                    || !string.Equals(pendingSection, SurfaceColorsSection, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(pendingKey, key, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            SetKept(SurfaceColorsSection, key, FormatRgb(color));
        }

        void SetKept(string section, string key, string fallback)
        {
            if (pendingSection != null
                && string.Equals(section, pendingSection, StringComparison.OrdinalIgnoreCase)
                && string.Equals(key, pendingKey, StringComparison.OrdinalIgnoreCase))
            {
                ini.Set(section, key, pendingValue);
                return;
            }

            int at = FindSnap(section, key);
            ini.Set(section, key, at >= 0 ? snapValue[at] : fallback);
        }

        static bool OwnedSection(string section)
        {
            return string.Equals(section, GridSection, StringComparison.OrdinalIgnoreCase)
                || string.Equals(section, DockSection, StringComparison.OrdinalIgnoreCase)
                || string.Equals(section, ToggleSection, StringComparison.OrdinalIgnoreCase)
                || string.Equals(section, ParamsSection, StringComparison.OrdinalIgnoreCase)
                || string.Equals(section, SurfaceColorsSection, StringComparison.OrdinalIgnoreCase)
                || string.Equals(section, CommandSection, StringComparison.OrdinalIgnoreCase);
        }

        void WriteForeign()
        {
            for (int i = 0; i < snapKey.Count; i++)
            {
                string section = snapSection[i];
                if (OwnedSection(section))
                    continue;
                if (section.StartsWith(FLIGHT_SYSTEMS + ": ", StringComparison.OrdinalIgnoreCase))
                    continue;
                ini.Set(section, snapKey[i], snapValue[i]);
            }
        }

        public bool CreateGpsHere(GridContext grid)
        {
            if (grid == null || grid.Controller == null || grid.Controller.Closed || gc.Me == null)
                return false;

            Vector3D here = grid.Controller.GetPosition();
            double x = Math.Round(here.X, 1, MidpointRounding.AwayFromZero);
            double y = Math.Round(here.Y, 1, MidpointRounding.AwayFromZero);
            double z = Math.Round(here.Z, 1, MidpointRounding.AwayFromZero);
            if (gps.Near(here) || gps.Near(new Vector3D(x, y, z)))
                return false;

            string line = string.Format("GPS:{0}:{1:0.0}:{2:0.0}:{3:0.0}:#FF75C9F1:", gps.NextMarkName(), x, y, z);
            string updated = GpsStore.AddPoint(gc.Me.CustomData ?? "", line);
            if (updated == null)
                return false;
            gc.Me.CustomData = updated;
            seenCustomData = updated;
            gps.LoadText(updated);
            return true;
        }

        public static IMyShipConnector HomeConnector(GridContext grid)
        {
            if (grid == null)
                return null;
            List<IMyShipConnector> list = grid.Connectors;
            for (int i = 0; i < list.Count; i++)
            {
                IMyShipConnector connector = list[i];
                if (connector == null || connector.Closed)
                    continue;
                if (StaticConnectorStamp.HasExactLine(connector.CustomData, HomeConnectorTag))
                    return connector;
            }
            return null;
        }

        public bool DeleteGpsPoint(int index)
        {
            if (gc.Me == null)
                return false;
            GpsWaypoint wp = gps.Get(index);
            if (wp == null)
                return false;
            string updated = GpsStore.RemoveKey(gc.Me.CustomData ?? "", wp.Key);
            if (updated == null)
                return false;
            gc.Me.CustomData = updated;
            seenCustomData = updated;
            gps.LoadText(updated);
            return true;
        }

        void WriteCustomData()
        {
            if (gc.Me == null)
                return;
            string old = gc.Me.CustomData ?? "";
            string block;
            if (!GpsStore.TryMarkerBlock(old, out block) || block == null)
                return;
            string body = ini.ToString();
            if (body.Length == 0 || body[body.Length - 1] != '\n')
                body += "\n";
            string written = $"[{FLIGHT_SYSTEMS}: {gc.Me.EntityId} ]\n\n" + body;
            if (old != written)
                gc.Me.CustomData = written;
            seenCustomData = written;
        }
    }
}
