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
        ColorChannels,
        DockKinds,
        DockBlocks,
        ScreenSlots,
        ScreenSurfaces
    }

    enum DockBlockKind
    {
        TriggerGears,
        TriggerConnectors,
        PadConnectors
    }

    enum ScreenSlot
    {
        Lcd1,
        Lcd2,
        Menu
    }

    class MenuScreens
    {
        public int SelectedRow = 1;
        public bool IsDefaultScreen;
        readonly GpsMenu gpsMenu = new GpsMenu();
        MenuView view = MenuView.Home;
        bool spaceWasDown;
        bool spaceEdge;
        bool cWasDown;
        bool cEdge;
        bool qWasDown;
        bool qEdge;
        bool eWasDown;
        bool eEdge;

        public const string BackLabel = "Back (C)";

        const int HomeRows = 7;
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
        readonly List<IMyTerminalBlock> providerScan = new List<IMyTerminalBlock>();
        readonly List<IMyTerminalBlock> surfaceBlocks = new List<IMyTerminalBlock>();
        readonly List<int> surfaceIndexes = new List<int>();
        readonly List<IMyTerminalBlock> tagScan = new List<IMyTerminalBlock>();
        ScreenSlot screenSlot = ScreenSlot.Lcd1;
        int screenSlotRow = 1;
        IMyTextSurface markedSurface;
        long markedBlockId;
        int markedIndex = -1;
        Color draftLcdBg;
        Color draftLcdFg;
        Color draftPlate;
        Color draftBar;
        Color draftText;
        int colorRow = 1;

        public void ShowHome()
        {
            ReleaseScreenMarker();
            view = MenuView.Home;
            SelectedRow = 1;
            gpsMenu.Close();
            // A new open has not seen Space down, so the first press counts.
            spaceWasDown = false;
            spaceEdge = false;
            // C already held must not close the menu on the open tick.
            cWasDown = true;
            cEdge = false;
            qWasDown = false;
            qEdge = false;
            eWasDown = false;
            eEdge = false;
        }

        public void NoteSpace(PlayerInput pi)
        {
            spaceEdge = Rising(pi.Space(), ref spaceWasDown);
            cEdge = Rising(pi.C(), ref cWasDown);
            qEdge = Rising(pi.Q(), ref qWasDown);
            eEdge = Rising(pi.E(), ref eWasDown);
        }

        static bool Rising(bool down, ref bool wasDown)
        {
            bool edge = down && !wasDown;
            wasDown = down;
            return edge;
        }

        public bool ShouldClose()
        {
            if (gpsMenu.IsOpen || view != MenuView.Home)
                return false;
            if (cEdge)
                return true;
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
                if (cEdge)
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

            // Q and E change the sibling page. They do not move the row and they do not go back.
            bool backed = false;
            if (cEdge && view != MenuView.Home)
            {
                lockInput();
                GoBack();
                backed = true;
            }
            else if (qEdge || eEdge)
            {
                if (CycleSibling(qEdge ? -1 : 1, ic))
                {
                    lockInput();
                    backed = true;
                }
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
                if (SelectedRow == 6)
                {
                    view = MenuView.ScreenSlots;
                    SelectedRow = 1;
                    return true;
                }
                return false;
            }

            if (view == MenuView.ScreenSlots)
                return ActivateScreenSlots();

            if (view == MenuView.ScreenSurfaces)
                return ActivateScreenSurface(ic, gc);

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

            if (view == MenuView.ColorChannels)
                return ActivateColorChannel();

            if (view == MenuView.Colors)
                return ActivateColors(ic);

            return false;
        }

        bool ActivateColors(IniContext ic)
        {
            if (SelectedRow >= 1 && SelectedRow <= ColorValueRows)
            {
                colorRow = SelectedRow;
                view = MenuView.ColorChannels;
                SelectedRow = 1;
                return true;
            }

            if (SelectedRow == ColorValueRows + 1)
            {
                ApplyColors(ic);
                ReturnHome();
                return true;
            }

            if (SelectedRow == ColorValueRows + 2 || SelectedRow == ColorValueRows + 3)
            {
                ReturnHome();
                return true;
            }

            return false;
        }

        bool ActivateColorChannel()
        {
            if (SelectedRow != 4)
                return false;

            view = MenuView.Colors;
            SelectedRow = colorRow < 1 ? 1 : colorRow;
            return true;
        }

        void ApplyColors(IniContext ic)
        {
            ic.LcdBackgroundColor = draftLcdBg;
            ic.LcdFontColor = draftLcdFg;
            ic.SpriteMenuColor = draftPlate;
            ic.SpriteBackgroundColor = draftBar;
            ic.SpriteFontColor = draftText;
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
            else if (view == MenuView.ColorChannels)
                AdjustColorChannel(pi, lockInput);
        }

        void AdjustColorChannel(PlayerInput pi, Action lockInput)
        {
            if (SelectedRow < 1 || SelectedRow > 3)
                return;

            int delta = 0;
            if (pi.D())
                delta += 15;
            if (pi.A())
                delta -= 15;
            if (delta == 0)
                return;

            lockInput();
            Color edited = DraftColor();
            int r = edited.R;
            int g = edited.G;
            int b = edited.B;
            if (SelectedRow == 1)
                r = ClampChannel(r + delta);
            else if (SelectedRow == 2)
                g = ClampChannel(g + delta);
            else
                b = ClampChannel(b + delta);
            SetDraft(new Color(r, g, b));
        }

        static int ClampChannel(int value)
        {
            if (value < 0)
                return 0;
            if (value > 255)
                return 255;
            return value;
        }

        Color DraftColor()
        {
            if (colorRow == 2)
                return draftLcdFg;
            if (colorRow == 3)
                return draftPlate;
            if (colorRow == 4)
                return draftBar;
            if (colorRow == 5)
                return draftText;
            return draftLcdBg;
        }

        void SetDraft(Color color)
        {
            if (colorRow == 2)
                draftLcdFg = color;
            else if (colorRow == 3)
                draftPlate = color;
            else if (colorRow == 4)
                draftBar = color;
            else if (colorRow == 5)
                draftText = color;
            else
                draftLcdBg = color;
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
            else if (view == MenuView.ColorChannels)
                PaintColorChannels(ic, gc);
            else if (view == MenuView.DockKinds)
                PaintDockKinds(ic, gc);
            else if (view == MenuView.DockBlocks)
                PaintDockBlocks(ic, gc);
            else if (view == MenuView.ScreenSlots)
                PaintScreenSlots(ic, gc);
            else if (view == MenuView.ScreenSurfaces)
                PaintScreenSurfaces(ic, gc);
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
            spt.Add("Screens", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
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
                spt.Add(BackLabel, RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
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
            spt.Add(BackLabel, RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
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
            spt.Add(BackLabel, RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.DrawTo(gc.LcdsMenu);
        }

        void PaintColors(IniContext ic, GridContext gc)
        {
            Sprites spt = new Sprites(ic);
            int row = 1;
            spt.Add(IniContext.SurfaceColorsSection);
            AddColorRow(spt, ic, ref row, IniContext.LCDBACKGROUNDCOLOR, draftLcdBg);
            AddColorRow(spt, ic, ref row, IniContext.LCDFONTCOLOR, draftLcdFg);
            AddColorRow(spt, ic, ref row, IniContext.SPRITEMENUCOLOR, draftPlate);
            AddColorRow(spt, ic, ref row, IniContext.SPRITEBACKGROUNDCOLOR, draftBar);
            AddColorRow(spt, ic, ref row, IniContext.SPRITEFONTCOLOR, draftText);
            spt.Add("Confirm", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add("Cancel", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add(BackLabel, RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.DrawTo(gc.LcdsMenu);
        }

        void PaintColorChannels(IniContext ic, GridContext gc)
        {
            Color edited = DraftColor();
            Color sampleBg;
            Color sampleFg;
            SamplePair(out sampleBg, out sampleFg);

            Sprites spt = new Sprites(ic);
            spt.Add(ColorKey());
            int row = 1;
            spt.Add("R: " + edited.R, RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add("G: " + edited.G, RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add("B: " + edited.B, RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add("Sample", sampleBg, sampleFg);
            spt.Add(BackLabel, RowColor(row, ic.SpriteBackgroundColor), RowColor(row, ic.SpriteFontColor));
            spt.DrawTo(gc.LcdsMenu);
        }

        void SamplePair(out Color background, out Color text)
        {
            if (colorRow <= 2)
            {
                background = draftLcdBg;
                text = draftLcdFg;
                return;
            }

            if (colorRow == 3)
            {
                background = draftPlate;
                text = draftText;
                return;
            }

            background = draftBar;
            text = draftText;
        }

        string ColorKey()
        {
            if (colorRow == 2)
                return IniContext.LCDFONTCOLOR;
            if (colorRow == 3)
                return IniContext.SPRITEMENUCOLOR;
            if (colorRow == 4)
                return IniContext.SPRITEBACKGROUNDCOLOR;
            if (colorRow == 5)
                return IniContext.SPRITEFONTCOLOR;
            return IniContext.LCDBACKGROUNDCOLOR;
        }

        void CopyColors(IniContext ic)
        {
            draftLcdBg = ic.LcdBackgroundColor;
            draftLcdFg = ic.LcdFontColor;
            draftPlate = ic.SpriteMenuColor;
            draftBar = ic.SpriteBackgroundColor;
            draftText = ic.SpriteFontColor;
        }

        void AddColorRow(Sprites spt, IniContext ic, ref int row, string key, Color color)
        {
            spt.Add(key + ": " + IniContext.FormatRgb(color), RowColor(row, ic.SpriteBackgroundColor), RowColor(row, ic.SpriteFontColor));
            row++;
        }

        void GoBack()
        {
            if (view == MenuView.DockBlocks)
                BackToDockKinds();
            else if (view == MenuView.ScreenSurfaces)
                BackToScreenSlots();
            else if (view == MenuView.ColorChannels)
            {
                view = MenuView.Colors;
                SelectedRow = colorRow < 1 ? 1 : colorRow;
            }
            else
                ReturnHome();
        }

        bool CycleSibling(int dir, IniContext ic)
        {
            if (view == MenuView.ColorChannels)
            {
                colorRow += dir;
                if (colorRow < 1)
                    colorRow = ColorValueRows;
                else if (colorRow > ColorValueRows)
                    colorRow = 1;
                return true;
            }

            if (view == MenuView.DockBlocks)
            {
                CycleDockKind(dir);
                return true;
            }

            if (view == MenuView.ScreenSurfaces)
            {
                CycleScreenSlot(dir);
                return true;
            }

            int index = SectionIndex();
            if (index < 0)
                return false;

            index += dir;
            if (index < 0)
                index = 5;
            else if (index > 5)
                index = 0;
            OpenSection(index, ic);
            return true;
        }

        int SectionIndex()
        {
            if (view == MenuView.Flight) return 0;
            if (view == MenuView.Toggles) return 1;
            if (view == MenuView.Numbers) return 2;
            if (view == MenuView.Colors) return 3;
            if (view == MenuView.DockKinds) return 4;
            if (view == MenuView.ScreenSlots) return 5;
            return -1;
        }

        void OpenSection(int index, IniContext ic)
        {
            if (index == 0)
                view = MenuView.Flight;
            else if (index == 1)
                view = MenuView.Toggles;
            else if (index == 2)
                view = MenuView.Numbers;
            else if (index == 3)
            {
                CopyColors(ic);
                view = MenuView.Colors;
            }
            else if (index == 4)
                view = MenuView.DockKinds;
            else
                view = MenuView.ScreenSlots;
        }

        void CycleDockKind(int dir)
        {
            int index = 0;
            if (dockKind == DockBlockKind.TriggerConnectors)
                index = 1;
            else if (dockKind == DockBlockKind.PadConnectors)
                index = 2;

            index += dir;
            if (index < 0)
                index = 2;
            else if (index > 2)
                index = 0;

            if (index == 0)
                dockKind = DockBlockKind.TriggerGears;
            else if (index == 1)
                dockKind = DockBlockKind.TriggerConnectors;
            else
                dockKind = DockBlockKind.PadConnectors;
            dockKindRow = index + 1;
        }

        void CycleScreenSlot(int dir)
        {
            int index = 0;
            if (screenSlot == ScreenSlot.Lcd2)
                index = 1;
            else if (screenSlot == ScreenSlot.Menu)
                index = 2;

            index += dir;
            if (index < 0)
                index = 2;
            else if (index > 2)
                index = 0;

            if (index == 0)
                screenSlot = ScreenSlot.Lcd1;
            else if (index == 1)
                screenSlot = ScreenSlot.Lcd2;
            else
                screenSlot = ScreenSlot.Menu;
            screenSlotRow = index + 1;
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
            else if (view == MenuView.ScreenSlots)
                SelectedRow = 6;
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
                max = ColorValueRows + 3;
            else if (view == MenuView.ColorChannels)
                max = 4;
            else if (view == MenuView.DockKinds)
                max = DockKindRows + 1;
            else if (view == MenuView.DockBlocks)
            {
                FillShown(gc, ic);
                max = shownBlocks.Count + 1;
            }
            else if (view == MenuView.ScreenSlots)
                max = 4;
            else if (view == MenuView.ScreenSurfaces)
            {
                FillSurfaces(gc);
                max = surfaceBlocks.Count + 1;
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
            spt.Add(BackLabel, RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
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
            spt.Add(BackLabel, RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.KeepVisible(SelectedRow);
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

        public void ReleaseScreenMarker()
        {
            IMyTextSurface previous = markedSurface;
            markedSurface = null;
            markedBlockId = 0;
            markedIndex = -1;
            Sprites.SetMarked(null);
            if (previous == null)
                return;
            Sprites.RestoreUnmarked(previous);
        }

        bool ActivateScreenSlots()
        {
            if (SelectedRow == 4)
            {
                ReturnHome();
                return true;
            }

            if (SelectedRow == 1)
                screenSlot = ScreenSlot.Lcd1;
            else if (SelectedRow == 2)
                screenSlot = ScreenSlot.Lcd2;
            else if (SelectedRow == 3)
                screenSlot = ScreenSlot.Menu;
            else
                return false;

            screenSlotRow = SelectedRow;
            view = MenuView.ScreenSurfaces;
            SelectedRow = 1;
            return true;
        }

        bool ActivateScreenSurface(IniContext ic, GridContext gc)
        {
            FillSurfaces(gc);
            int index = SelectedRow - 1;
            if (index < 0 || index >= surfaceBlocks.Count)
            {
                BackToScreenSlots();
                return true;
            }

            AssignSurface(surfaceBlocks[index], surfaceIndexes[index], ic, gc);
            return true;
        }

        void BackToScreenSlots()
        {
            ReleaseScreenMarker();
            view = MenuView.ScreenSlots;
            SelectedRow = screenSlotRow < 1 ? 1 : screenSlotRow;
        }

        void PaintScreenSlots(IniContext ic, GridContext gc)
        {
            Sprites spt = new Sprites(ic);
            spt.Add("Screens");
            int row = 1;
            spt.Add("LCD 1", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add("LCD 2", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add("Menu", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add(BackLabel, RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.DrawTo(gc.LcdsMenu);
        }

        void PaintScreenSurfaces(IniContext ic, GridContext gc)
        {
            FillSurfaces(gc);
            Sprites spt = new Sprites(ic);
            spt.Add(ScreenSlotTitle(screenSlot));
            int row = 1;
            for (int i = 0; i < surfaceBlocks.Count; i++)
            {
                IMyTerminalBlock block = surfaceBlocks[i];
                string name = block == null || block.CustomName == null ? "" : block.CustomName;
                spt.Add(SurfaceLabel(name, surfaceIndexes[i]), RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            }
            spt.Add(BackLabel, RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.KeepVisible(SelectedRow);

            IMyTextSurface current = null;
            long blockId = 0;
            int surfaceIndex = -1;
            int picked = SelectedRow - 1;
            if (picked >= 0 && picked < surfaceBlocks.Count)
            {
                IMyTerminalBlock block = surfaceBlocks[picked];
                surfaceIndex = surfaceIndexes[picked];
                if (block != null && !block.Closed)
                {
                    blockId = block.EntityId;
                    current = SurfaceAt(block, surfaceIndex);
                }
            }

            bool moved = markedIndex != surfaceIndex || markedBlockId != blockId;
            IMyTextSurface previous = markedSurface;
            markedSurface = current;
            markedBlockId = blockId;
            markedIndex = surfaceIndex;
            Sprites.SetMarked(current);
            if (moved && previous != null && previous != current)
                Sprites.RestoreUnmarked(previous);

            spt.DrawTo(gc.LcdsMenu);
            if (current != null)
                spt.DrawPlainMarker(current);
        }

        static string ScreenSlotTitle(ScreenSlot slot)
        {
            if (slot == ScreenSlot.Lcd2)
                return "LCD 2";
            if (slot == ScreenSlot.Menu)
                return "Menu";
            return "LCD 1";
        }

        static string SurfaceLabel(string name, int index)
        {
            string num = index.ToString();
            if (string.IsNullOrEmpty(name))
                return num;
            return name + " " + num;
        }

        void AssignSurface(IMyTerminalBlock block, int surfaceIndex, IniContext ic, GridContext gc)
        {
            if (block == null || block.Closed || ic == null || gc == null)
                return;

            IMyCockpit cockpit = block as IMyCockpit;
            if (cockpit != null)
            {
                gc.WriteCockpitSlot(cockpit, CockpitKey(screenSlot), surfaceIndex);
                gc.ReloadLCDs();
                return;
            }

            IMyTextPanel panel = block as IMyTextPanel;
            if (panel == null)
                return;

            string tag = SlotTag(ic, screenSlot);
            if (string.IsNullOrEmpty(tag))
                return;
            StripTag(gc, panel.EntityId, tag);
            AppendTag(panel, tag);
            gc.ReloadLCDs();
        }

        static string CockpitKey(ScreenSlot slot)
        {
            if (slot == ScreenSlot.Lcd2)
                return CockpitSpriteIni.LCD2;
            if (slot == ScreenSlot.Menu)
                return CockpitSpriteIni.LCDSETTINGS;
            return CockpitSpriteIni.LCD1;
        }

        static string SlotTag(IniContext ic, ScreenSlot slot)
        {
            if (slot == ScreenSlot.Lcd2)
                return ic.Lcd2Tag;
            if (slot == ScreenSlot.Menu)
                return ic.LcdMenuTag;
            return ic.Lcd1Tag;
        }

        void StripTag(GridContext gc, long keepId, string tag)
        {
            if (gc.GridTS == null || gc.Me == null || string.IsNullOrEmpty(tag))
                return;

            tagScan.Clear();
            gc.GridTS.GetBlocksOfType(tagScan);
            for (int i = 0; i < tagScan.Count; i++)
            {
                IMyTerminalBlock block = tagScan[i];
                if (block == null || block.Closed || block.EntityId == keepId)
                    continue;
                if (block is IMyCockpit)
                    continue;
                if (!block.IsSameConstructAs(gc.Me))
                    continue;
                RemoveTag(block, tag);
            }
        }

        static void AppendTag(IMyTerminalBlock block, string tag)
        {
            string name = block.CustomName ?? "";
            if (name.Contains(tag))
                return;
            if (name.Length == 0)
                block.CustomName = tag;
            else
                block.CustomName = name + " " + tag;
        }

        static void RemoveTag(IMyTerminalBlock block, string tag)
        {
            string name = block.CustomName ?? "";
            bool changed = false;
            int at = name.IndexOf(tag);
            while (at >= 0)
            {
                int cut = at;
                int len = tag.Length;
                if (cut > 0 && name[cut - 1] == ' ')
                {
                    cut--;
                    len++;
                }
                name = name.Remove(cut, len);
                changed = true;
                at = name.IndexOf(tag);
            }
            if (changed)
                block.CustomName = name;
        }

        void FillSurfaces(GridContext gc)
        {
            surfaceBlocks.Clear();
            surfaceIndexes.Clear();
            if (gc == null || gc.GridTS == null || gc.Me == null)
                return;

            providerScan.Clear();
            gc.GridTS.GetBlocksOfType<IMyTextSurfaceProvider>(providerScan);
            for (int i = 0; i < providerScan.Count; i++)
            {
                IMyTerminalBlock block = providerScan[i];
                if (block == null || block.Closed || !block.IsSameConstructAs(gc.Me))
                    continue;
                IMyTextSurfaceProvider provider = block as IMyTextSurfaceProvider;
                if (provider == null)
                    continue;
                for (int s = 0; s < provider.SurfaceCount; s++)
                {
                    if (provider.GetSurface(s) == null)
                        continue;
                    surfaceBlocks.Add(block);
                    surfaceIndexes.Add(s);
                }
            }
        }

        static IMyTextSurface SurfaceAt(IMyTerminalBlock block, int surfaceIndex)
        {
            IMyTextSurfaceProvider provider = block as IMyTextSurfaceProvider;
            if (provider == null || surfaceIndex < 0 || surfaceIndex >= provider.SurfaceCount)
                return null;
            return provider.GetSurface(surfaceIndex);
        }
    }
}
