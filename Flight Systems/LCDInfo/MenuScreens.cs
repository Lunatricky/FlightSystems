using IngameScript.Domain;
using IngameScript.Enums;
using IngameScript.UseCases;
using IngameScript.Utils;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    enum MenuView
    {
        Home,
        Flight,
        Toggles,
        Numbers,
        Colors,
        DockKinds,
        DockBlocks
    }

    enum DockBlockKind
    {
        TriggerGears,
        TriggerConnectors,
        PadConnectors
    }

    class MenuScreens
    {
        public int SelectedRow = 1;
        public bool IsDefaultScreen;
        readonly GpsMenu gpsMenu = new GpsMenu();
        MenuView view = MenuView.Home;
        bool spaceWasDown;
        bool spaceEdge;

        const int HomeRows = 6;
        const int ToggleValueRows = 8;
        const int NumberValueRows = 5;
        const int ColorValueRows = 5;
        const int DockKindRows = 3;
        DockBlockKind dockKind = DockBlockKind.TriggerGears;
        int dockKindRow = 1;
        readonly List<IMyTerminalBlock> shownBlocks = new List<IMyTerminalBlock>();
        readonly List<IMyLandingGear> gearScan = new List<IMyLandingGear>();
        readonly List<IMyShipConnector> connectorScan = new List<IMyShipConnector>();
        readonly List<IMyLandingGear> gearGroup = new List<IMyLandingGear>();
        readonly List<IMyShipConnector> connectorGroup = new List<IMyShipConnector>();
        string savedLcdBackground;
        string savedLcdFont;
        string savedSpriteMenu;
        string savedSpriteBackground;
        string savedSpriteFont;

        public void ShowHome()
        {
            view = MenuView.Home;
            SelectedRow = 1;
            gpsMenu.Close();
            // A new open has not seen Space down, so the first press counts.
            spaceWasDown = false;
            spaceEdge = false;
        }

        public void NoteSpace(PlayerInput pi)
        {
            bool down = pi.Space();
            spaceEdge = down && !spaceWasDown;
            spaceWasDown = down;
        }

        public bool ShouldClose()
        {
            if (gpsMenu.IsOpen || view != MenuView.Home)
                return false;
            return SelectedRow == HomeRows && spaceEdge;
        }

        public void Handle(
            PlayerInput pi,
            GridContext gc,
            IniContext ic,
            SystemBools sb,
            Command command,
            bool menuToggle,
            Action abort,
            Action softAbort,
            Action close,
            Action lockInput)
        {
            // Closed surfaces are the flight-command list. Handle is the open menu.
            if (!menuToggle)
            {
                FlightSystemIdle(ic, gc, sb);
                return;
            }

            if (gpsMenu.IsOpen)
            {
                if (pi.Q())
                {
                    lockInput();
                    gpsMenu.Back();
                    // The book is the GPS list. Closing it returns to Flight, not home.
                    if (!gpsMenu.IsOpen)
                        view = MenuView.Flight;
                }

                if (gpsMenu.IsOpen)
                {
                    gpsMenu.Tick(pi, gc, ic, command, lockInput, close, softAbort, spaceEdge);
                    return;
                }

                ClampFor(gc, ic);
                PaintFlight(ic, gc, true);
                return;
            }

            if (pi.W())
            {
                lockInput();
                SelectedRow--;
            }

            if (pi.S())
            {
                lockInput();
                SelectedRow++;
            }

            // Q on the home list is not a page key.
            bool backed = false;
            if (pi.Q() && view != MenuView.Home)
            {
                lockInput();
                if (view == MenuView.DockBlocks)
                    BackToDockKinds();
                else
                {
                    if (view == MenuView.Colors)
                        RestoreColors(ic);
                    ReturnHome();
                }
                backed = true;
            }

            ClampFor(gc, ic);

            bool acted = false;
            bool closed = false;
            if (!backed && spaceEdge)
                acted = Activate(ic, gc, sb, command, abort, softAbort, close, ref closed);

            if (closed)
            {
                FlightSystemIdle(ic, gc, sb);
                return;
            }

            if (gpsMenu.IsOpen)
                return;

            if (!backed && !acted)
                AdjustValue(ic, pi, lockInput);

            ClampFor(gc, ic);
            Paint(ic, gc);
        }

        public void FlightSystemIdle(IniContext ic, GridContext gc, SystemBools sb)
        {
            FlightSystemIdle(ic, gc, sb, null);
        }

        public void FlightSystemIdle(IniContext ic, GridContext gc, SystemBools sb, Sprites reuse)
        {
            Sprites spt = reuse;
            if (spt == null)
                spt = new Sprites(ic);
            else
                spt.Clear();

            SelectedRow = RunningFlightRow(gc, sb);

            AddFlightRows(spt, ic, gc, false);
            spt.DrawIdleTo(gc.LcdsMenu);
        }

        bool Activate(
            IniContext ic,
            GridContext gc,
            SystemBools sb,
            Command command,
            Action abort,
            Action softAbort,
            Action close,
            ref bool closed)
        {
            if (view == MenuView.Home)
            {
                if (SelectedRow == 1)
                {
                    view = MenuView.Flight;
                    int running = RunningFlightRow(gc, sb);
                    SelectedRow = running == 0 ? 1 : running;
                    return true;
                }
                if (SelectedRow == 2)
                {
                    view = MenuView.Toggles;
                    SelectedRow = 1;
                    return true;
                }
                if (SelectedRow == 3)
                {
                    view = MenuView.Numbers;
                    SelectedRow = 1;
                    return true;
                }
                if (SelectedRow == 4)
                {
                    CopyColors(ic);
                    view = MenuView.Colors;
                    SelectedRow = 1;
                    return true;
                }
                if (SelectedRow == 5)
                {
                    view = MenuView.DockKinds;
                    SelectedRow = 1;
                    return true;
                }
                return false;
            }

            if (view == MenuView.DockKinds)
                return ActivateDockKinds();

            if (view == MenuView.DockBlocks)
                return ActivateDockBlock(ic, gc);

            if (view == MenuView.Flight)
                return ActivateFlight(gc, sb, command, abort, softAbort, close, ref closed);

            if (view == MenuView.Toggles && SelectedRow == ToggleValueRows + 1)
            {
                ReturnHome();
                return true;
            }

            if (view == MenuView.Numbers && SelectedRow == NumberValueRows + 1)
            {
                ReturnHome();
                return true;
            }

            if (view == MenuView.Colors && SelectedRow == ColorValueRows + 1)
            {
                ReturnHome();
                return true;
            }

            if (view == MenuView.Colors && SelectedRow == ColorValueRows + 2)
            {
                RestoreColors(ic);
                ReturnHome();
                return true;
            }

            return false;
        }

        bool ActivateFlight(
            GridContext gc,
            SystemBools sb,
            Command command,
            Action abort,
            Action softAbort,
            Action close,
            ref bool closed)
        {
            int gpsRow = FlightGpsRow(gc);
            if (SelectedRow == gpsRow + 1)
            {
                ReturnHome();
                return true;
            }

            if (SelectedRow == gpsRow)
            {
                gpsMenu.Open();
                return true;
            }

            MainState ms = MainState.Idle;
            int pick = 1;

            if (SelectedRow == pick++) ms = MainState.Cruise;
            else if (gc.ShipType != ShipType.Atmo && SelectedRow == pick++) ms = MainState.Orbit;
            else if (SelectedRow == pick++) ms = MainState.CNav;
            else if (SelectedRow == pick++) ms = MainState.Land;
            else if (SelectedRow == pick++) ms = MainState.Glide;
            else if (SelectedRow == pick++) ms = MainState.SBurn;

            if (ms == MainState.Idle)
                return false;

            close();
            closed = true;
            if (sb.GetModeState(ms))
                abort();
            else
            {
                softAbort();
                command.Empty(ms);
            }
            return true;
        }

        void AdjustValue(IniContext ic, PlayerInput pi, Action lockInput)
        {
            if (view == MenuView.Toggles)
                AdjustToggles(ic, pi, lockInput);
            else if (view == MenuView.Numbers)
                AdjustNumbers(ic, pi, lockInput);
            else if (view == MenuView.Colors)
                AdjustColors(ic, pi, lockInput);
        }

        void AdjustColors(IniContext ic, PlayerInput pi, Action lockInput)
        {
            if (!pi.A() && !pi.D())
                return;
            if (SelectedRow < 1 || SelectedRow > ColorValueRows)
                return;

            lockInput();
            bool next = pi.D();
            if (SelectedRow == 1)
                ic.LcdBackgroundColor = ic.CycleAvailableColor(ic.LcdBackgroundColor, next);
            else if (SelectedRow == 2)
                ic.LcdFontColor = ic.CycleAvailableColor(ic.LcdFontColor, next);
            else if (SelectedRow == 3)
                ic.SetSpriteMenuColor(ic.CycleAvailableColor(ic.SpriteMenuColorName, next));
            else if (SelectedRow == 4)
                ic.SetSpriteBackgroundColor(ic.CycleAvailableColor(ic.SpriteBackgroundColorName, next));
            else if (SelectedRow == 5)
                ic.SetSpriteFontColor(ic.CycleAvailableColor(ic.SpriteFontColorName, next));
        }

        void AdjustToggles(IniContext ic, PlayerInput pi, Action lockInput)
        {
            if (!pi.A() && !pi.D())
                return;
            if (SelectedRow < 1 || SelectedRow > ToggleValueRows)
                return;

            lockInput();
            int row = 1;
            if (SelectedRow == row++) ic.AllowFlightSystems = !ic.AllowFlightSystems;
            else if (SelectedRow == row++) ic.AnalogThrotle = !ic.AnalogThrotle;
            else if (SelectedRow == row++) ic.AllowLowFuelLand = !ic.AllowLowFuelLand;
            else if (SelectedRow == row++) ic.AllowDockMode = !ic.AllowDockMode;
            else if (SelectedRow == row++) ic.ControlAntennas = !ic.ControlAntennas;
            else if (SelectedRow == row++) ic.RenameSubgrids = !ic.RenameSubgrids;
            else if (SelectedRow == row++) ic.PaintSurfaces = !ic.PaintSurfaces;
            else if (SelectedRow == row++) ic.TransparentLCD = !ic.TransparentLCD;
        }

        void AdjustNumbers(IniContext ic, PlayerInput pi, Action lockInput)
        {
            int row = 1;
            if (SelectedRow == row++) ic.MaxSpeed = IncrementedValue(ic.MaxSpeed, pi, lockInput);
            else if (SelectedRow == row++) ic.CruiseSpeed = IncrementedValue(ic.CruiseSpeed, pi, lockInput);
            else if (SelectedRow == row++) ic.SafeAltitude = IncrementedValue(ic.SafeAltitude, pi, lockInput);
            else if (SelectedRow == row++) ic.DistanceToGPS = IncrementedValue(ic.DistanceToGPS, pi, lockInput);
            else if (SelectedRow == row++) ic.MinimumAcceptedFuel = IncrementedValue(ic.MinimumAcceptedFuel, pi, lockInput);
        }

        void Paint(IniContext ic, GridContext gc)
        {
            if (view == MenuView.Home)
                PaintHome(ic, gc);
            else if (view == MenuView.Flight)
                PaintFlight(ic, gc, true);
            else if (view == MenuView.Toggles)
                PaintToggles(ic, gc);
            else if (view == MenuView.Numbers)
                PaintNumbers(ic, gc);
            else if (view == MenuView.Colors)
                PaintColors(ic, gc);
            else if (view == MenuView.DockKinds)
                PaintDockKinds(ic, gc);
            else if (view == MenuView.DockBlocks)
                PaintDockBlocks(ic, gc);
        }

        void PaintHome(IniContext ic, GridContext gc)
        {
            Sprites spt = new Sprites(ic);
            spt.Add("Menu");
            int row = 1;
            spt.Add("Flight", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add("Toggles", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add("Numbers", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add("Colors", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add("Dock blocks", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add("Close", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.DrawTo(gc.LcdsMenu);
        }

        void PaintFlight(IniContext ic, GridContext gc, bool includeClose)
        {
            Sprites spt = new Sprites(ic);
            AddFlightRows(spt, ic, gc, includeClose);
            spt.DrawTo(gc.LcdsMenu);
        }

        void AddFlightRows(Sprites spt, IniContext ic, GridContext gc, bool includeClose)
        {
            int row = 1;
            spt.Add("Flight Systems");
            spt.Add("Cruise control", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            if (gc.ShipType != ShipType.Atmo)
                spt.Add("Fly to orbit", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add("Circumnavigate", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add("Vertical land", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add("Glide to surface", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add("Suicide burn", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add("Fly to GPS", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            if (includeClose)
                spt.Add("Back", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
        }

        void PaintToggles(IniContext ic, GridContext gc)
        {
            IsDefaultScreen = false;
            Sprites spt = new Sprites(ic);
            int row = 1;
            spt.Add(IniContext.ToggleSection);
            spt.Add(IniContext.FLIGHT_SYSTEMS, BoolSpriteColor(SelectedRow == row++, ic.AllowFlightSystems), Color.Black);
            spt.Add(IniContext.ANALOG_THROTLE, BoolSpriteColor(SelectedRow == row++, ic.AnalogThrotle), Color.Black);
            spt.Add(IniContext.LOW_FUEL_LAND, BoolSpriteColor(SelectedRow == row++, ic.AllowLowFuelLand), Color.Black);
            spt.Add(IniContext.DOCK_MODE, BoolSpriteColor(SelectedRow == row++, ic.AllowDockMode), Color.Black);
            spt.Add(IniContext.CONTROL_ANTENNAS, BoolSpriteColor(SelectedRow == row++, ic.ControlAntennas), Color.Black);
            spt.Add(IniContext.RENAME_SUBGRIDS, BoolSpriteColor(SelectedRow == row++, ic.RenameSubgrids), Color.Black);
            spt.Add(IniContext.PAINT_SURFACES, BoolSpriteColor(SelectedRow == row++, ic.PaintSurfaces), Color.Black);
            spt.Add(IniContext.TRANSPARENTLCD, BoolSpriteColor(SelectedRow == row++, ic.TransparentLCD), Color.Black);
            spt.Add("Back", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.DrawTo(gc.LcdsMenu);
        }

        void PaintNumbers(IniContext ic, GridContext gc)
        {
            IsDefaultScreen = false;
            Sprites spt = new Sprites(ic);
            int row = 1;
            spt.Add(IniContext.ParamsSection);
            spt.Add(IniContext.MAX_SPEED + ": " + ic.MaxSpeed, RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add(IniContext.CRUISE_SPEED + ": " + ic.CruiseSpeed, RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add(IniContext.CNAV_ALTITUDE + ": " + ic.SafeAltitude, RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add(IniContext.DISTANCE_TO_GPS + ": " + ic.DistanceToGPS, RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add(IniContext.MINIMUM_ACCEPTED_FUEL + ": " + ic.MinimumAcceptedFuel, RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add("Back", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.DrawTo(gc.LcdsMenu);
        }

        void PaintColors(IniContext ic, GridContext gc)
        {
            Sprites spt = new Sprites(ic);
            int row = 1;
            spt.Add(IniContext.SurfaceColorsSection);
            AddColorRow(spt, ic, ref row, IniContext.LCDBACKGROUNDCOLOR, ic.LcdBackgroundColor);
            AddColorRow(spt, ic, ref row, IniContext.LCDFONTCOLOR, ic.LcdFontColor);
            AddColorRow(spt, ic, ref row, IniContext.SPRITEMENUCOLOR, ic.SpriteMenuColorName);
            AddColorRow(spt, ic, ref row, IniContext.SPRITEBACKGROUNDCOLOR, ic.SpriteBackgroundColorName);
            AddColorRow(spt, ic, ref row, IniContext.SPRITEFONTCOLOR, ic.SpriteFontColorName);
            spt.Add("Confirm", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add("Cancel", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.DrawTo(gc.LcdsMenu);
        }

        void CopyColors(IniContext ic)
        {
            savedLcdBackground = ic.LcdBackgroundColor;
            savedLcdFont = ic.LcdFontColor;
            savedSpriteMenu = ic.SpriteMenuColorName;
            savedSpriteBackground = ic.SpriteBackgroundColorName;
            savedSpriteFont = ic.SpriteFontColorName;
        }

        void RestoreColors(IniContext ic)
        {
            ic.LcdBackgroundColor = savedLcdBackground;
            ic.LcdFontColor = savedLcdFont;
            ic.SetSpriteMenuColor(savedSpriteMenu);
            ic.SetSpriteBackgroundColor(savedSpriteBackground);
            ic.SetSpriteFontColor(savedSpriteFont);
        }

        void AddColorRow(Sprites spt, IniContext ic, ref int row, string key, string name)
        {
            spt.Add(key + ": " + name, RowColor(row, ic.SpriteBackgroundColor), RowColor(row, ic.SpriteFontColor));
            row++;
        }

        void ReturnHome()
        {
            if (view == MenuView.Flight)
                SelectedRow = 1;
            else if (view == MenuView.Toggles)
                SelectedRow = 2;
            else if (view == MenuView.Numbers)
                SelectedRow = 3;
            else if (view == MenuView.Colors)
                SelectedRow = 4;
            else if (view == MenuView.DockKinds)
                SelectedRow = 5;
            else
                SelectedRow = 1;
            view = MenuView.Home;
        }

        void ClampFor(GridContext gc, IniContext ic)
        {
            int max = HomeRows;
            if (view == MenuView.Flight)
                max = FlightGpsRow(gc) + 1;
            else if (view == MenuView.Toggles)
                max = ToggleValueRows + 1;
            else if (view == MenuView.Numbers)
                max = NumberValueRows + 1;
            else if (view == MenuView.Colors)
                max = ColorValueRows + 2;
            else if (view == MenuView.DockKinds)
                max = DockKindRows + 1;
            else if (view == MenuView.DockBlocks)
            {
                FillShown(gc, ic);
                max = shownBlocks.Count + 1;
            }
            ClampRow(1, max);
        }

        static int RunningFlightRow(GridContext gc, SystemBools sb)
        {
            int row = 1;
            int selected = 0;

            if (sb.CruiseToggle) selected = row;
            row++;
            if (gc.ShipType != ShipType.Atmo)
            {
                if (selected == 0 && sb.OrbitToggle) selected = row;
                row++;
            }
            if (selected == 0 && sb.CNavToggle) selected = row;
            row++;
            if (selected == 0 && sb.LandToggle) selected = row;
            row++;
            if (selected == 0 && sb.GlideToggle) selected = row;
            row++;
            if (selected == 0 && sb.SBurnToggle) selected = row;
            row++;
            if (selected == 0 && sb.GpsToggle) selected = row;
            return selected;
        }

        static int FlightGpsRow(GridContext gc)
        {
            if (gc.ShipType != ShipType.Atmo)
                return 7;
            return 6;
        }

        void ClampRow(int min, int max)
        {
            if (SelectedRow < min) SelectedRow = max;
            else if (SelectedRow > max) SelectedRow = min;
        }

        Color RowColor(int row, Color color)
        {
            double DarkenFactor = 0.2;
            return SelectedRow == row ? ColorMap.SelectedColor(color, DarkenFactor) : color;
        }

        static double IncrementedValue(double value, PlayerInput pi, Action lockInput)
        {
            double increment;
            if (value < 1) increment = 0.1;
            else if (value < 10) increment = 1;
            else if (value < 50) increment = 5;
            else if (value < 100) increment = 10;
            else if (value < 500) increment = 50;
            else if (value < 1000) increment = 100;
            else if (value < 5000) increment = 500;
            else increment = 1000;

            if (pi.D())
            {
                lockInput();
                value += increment;
            }

            if (pi.A())
            {
                lockInput();
                value -= increment;
            }

            return value < 0 ? 0 : value;
        }

        static Color BoolSpriteColor(bool isSelected, bool toggle)
        {
            return (isSelected ?
                toggle ? Color.Green : Color.Red :
                toggle ? Color.LightGreen : Color.OrangeRed);
        }

        bool ActivateDockKinds()
        {
            if (SelectedRow == DockKindRows + 1)
            {
                ReturnHome();
                return true;
            }

            if (SelectedRow == 1)
                dockKind = DockBlockKind.TriggerGears;
            else if (SelectedRow == 2)
                dockKind = DockBlockKind.TriggerConnectors;
            else if (SelectedRow == 3)
                dockKind = DockBlockKind.PadConnectors;
            else
                return false;

            dockKindRow = SelectedRow;
            view = MenuView.DockBlocks;
            SelectedRow = 1;
            return true;
        }

        bool ActivateDockBlock(IniContext ic, GridContext gc)
        {
            FillShown(gc, ic);
            int index = SelectedRow - 1;
            if (index < 0 || index >= shownBlocks.Count)
            {
                BackToDockKinds();
                return true;
            }

            ToggleShown(shownBlocks[index], ic, gc);
            return true;
        }

        void BackToDockKinds()
        {
            view = MenuView.DockKinds;
            SelectedRow = dockKindRow < 1 ? 1 : dockKindRow;
        }

        void PaintDockKinds(IniContext ic, GridContext gc)
        {
            Sprites spt = new Sprites(ic);
            spt.Add("Dock blocks");
            int row = 1;
            spt.Add("Dock trigger gears", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add("Dock trigger connectors", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add("Landing pad connectors", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add("Close", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.DrawTo(gc.LcdsMenu);
        }

        void PaintDockBlocks(IniContext ic, GridContext gc)
        {
            FillShown(gc, ic);
            Sprites spt = new Sprites(ic);
            spt.Add(DockKindTitle(dockKind));
            int row = 1;
            for (int i = 0; i < shownBlocks.Count; i++)
            {
                IMyTerminalBlock block = shownBlocks[i];
                string name = block == null || block.CustomName == null ? "" : block.CustomName;
                bool marked = block != null && BlockMarked(block, ic);
                spt.Add(name, BoolSpriteColor(SelectedRow == row, marked), Color.Black);
                row++;
            }
            spt.Add("Close", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.DrawTo(gc.LcdsMenu);
        }

        static string DockKindTitle(DockBlockKind kind)
        {
            if (kind == DockBlockKind.TriggerConnectors)
                return "Dock trigger connectors";
            if (kind == DockBlockKind.PadConnectors)
                return "Landing pad connectors";
            return "Dock trigger gears";
        }

        void ToggleShown(IMyTerminalBlock block, IniContext ic, GridContext gc)
        {
            if (block == null || block.Closed)
                return;

            if (dockKind == DockBlockKind.PadConnectors)
            {
                TogglePad(block, ic, gc);
                return;
            }

            string line = ic.DockTriggerTag ?? "";
            if (line.Length == 0)
                return;
            if (StaticConnectorStamp.HasExactLine(block.CustomData ?? "", line))
                StaticConnectorStamp.RemoveLine(block, line);
            else
                StaticConnectorStamp.AppendLine(block, line);
        }

        void TogglePad(IMyTerminalBlock block, IniContext ic, GridContext gc)
        {
            string pad = ic.LandingPadTag ?? "";
            string trigger = ic.DockTriggerTag ?? "";
            if (pad.Length == 0)
                return;

            bool hasPad = StaticConnectorStamp.HasExactLine(block.CustomData ?? "", pad);
            if (!hasPad)
            {
                StaticConnectorStamp.AppendLine(block, pad);
                if (trigger.Length > 0 && trigger != pad)
                    StaticConnectorStamp.RemoveLine(block, trigger);
                return;
            }

            StaticConnectorStamp.RemoveLine(block, pad);
            if (trigger.Length > 0 && ConnectorInTriggerGroup(gc, ic, block.EntityId))
                StaticConnectorStamp.AppendLine(block, trigger);
        }

        bool BlockMarked(IMyTerminalBlock block, IniContext ic)
        {
            if (dockKind == DockBlockKind.PadConnectors)
                return InConnectorGroup(block.EntityId) || StaticConnectorStamp.HasExactLine(block.CustomData ?? "", ic.LandingPadTag);
            if (dockKind == DockBlockKind.TriggerGears)
                return InGearGroup(block.EntityId) || StaticConnectorStamp.HasExactLine(block.CustomData ?? "", ic.DockTriggerTag);
            return InConnectorGroup(block.EntityId) || StaticConnectorStamp.HasExactLine(block.CustomData ?? "", ic.DockTriggerTag);
        }

        void FillShown(GridContext gc, IniContext ic)
        {
            shownBlocks.Clear();
            if (gc == null || gc.GridTS == null || gc.Me == null || ic == null)
                return;

            string ignore = ic.IgnoreTag ?? "";
            if (dockKind == DockBlockKind.TriggerGears)
            {
                gearScan.Clear();
                gc.GridTS.GetBlocksOfType(gearScan);
                FillGearGroup(gc, ic.DockTriggerGroup);
                for (int i = 0; i < gearScan.Count; i++)
                {
                    IMyLandingGear gear = gearScan[i];
                    if (!TakeBlock(gear, gc, ignore))
                        continue;
                    shownBlocks.Add(gear);
                }
                return;
            }

            connectorScan.Clear();
            gc.GridTS.GetBlocksOfType(connectorScan);
            string groupName = dockKind == DockBlockKind.PadConnectors ? ic.LandingPadGroup : ic.DockTriggerGroup;
            FillConnectorGroup(gc, groupName);
            for (int i = 0; i < connectorScan.Count; i++)
            {
                IMyShipConnector connector = connectorScan[i];
                if (!TakeBlock(connector, gc, ignore))
                    continue;
                shownBlocks.Add(connector);
            }
        }

        bool TakeBlock(IMyTerminalBlock block, GridContext gc, string ignore)
        {
            if (block == null || block.Closed || gc.Me == null)
                return false;
            if (!block.IsSameConstructAs(gc.Me))
                return false;
            return !BlockIgnored(block, ignore);
        }

        static bool BlockIgnored(IMyTerminalBlock block, string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return false;
            if (block.CustomName != null && block.CustomName.Contains(tag))
                return true;
            string data = block.CustomData;
            return data != null && data.Contains(tag);
        }

        void FillGearGroup(GridContext gc, string groupName)
        {
            gearGroup.Clear();
            if (gc.GridTS == null || string.IsNullOrEmpty(groupName))
                return;
            IMyBlockGroup group = gc.GridTS.GetBlockGroupWithName(groupName);
            if (group == null)
                return;
            group.GetBlocksOfType(gearGroup);
        }

        void FillConnectorGroup(GridContext gc, string groupName)
        {
            connectorGroup.Clear();
            if (gc.GridTS == null || string.IsNullOrEmpty(groupName))
                return;
            IMyBlockGroup group = gc.GridTS.GetBlockGroupWithName(groupName);
            if (group == null)
                return;
            group.GetBlocksOfType(connectorGroup);
        }

        bool InGearGroup(long entityId)
        {
            for (int i = 0; i < gearGroup.Count; i++)
            {
                IMyLandingGear member = gearGroup[i];
                if (member != null && !member.Closed && member.EntityId == entityId)
                    return true;
            }
            return false;
        }

        bool InConnectorGroup(long entityId)
        {
            for (int i = 0; i < connectorGroup.Count; i++)
            {
                IMyShipConnector member = connectorGroup[i];
                if (member != null && !member.Closed && member.EntityId == entityId)
                    return true;
            }
            return false;
        }

        bool ConnectorInTriggerGroup(GridContext gc, IniContext ic, long entityId)
        {
            FillConnectorGroup(gc, ic.DockTriggerGroup);
            return InConnectorGroup(entityId);
        }
    }
}
