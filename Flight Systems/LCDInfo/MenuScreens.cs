using IngameScript.Domain;
using IngameScript.Enums;
using IngameScript.UseCases;
using IngameScript.Utils;
using System;
using VRageMath;

namespace IngameScript
{
    enum MenuView
    {
        Home,
        Flight,
        Toggles,
        Numbers
    }

    class MenuScreens
    {
        public int SelectedRow = 1;
        public bool IsDefaultScreen;
        readonly GpsMenu gpsMenu = new GpsMenu();
        MenuView view = MenuView.Home;
        bool spaceWasDown;
        bool spaceEdge;

        const int HomeRows = 4;
        const int ToggleValueRows = 8;
        const int NumberValueRows = 5;

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

                ClampFor(gc);
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
                ReturnHome();
                backed = true;
            }

            ClampFor(gc);

            bool acted = false;
            bool closed = false;
            if (!backed && spaceEdge)
                acted = Activate(gc, sb, command, abort, softAbort, close, ref closed);

            if (closed)
            {
                FlightSystemIdle(ic, gc, sb);
                return;
            }

            if (gpsMenu.IsOpen)
                return;

            if (!backed && !acted)
                AdjustValue(ic, pi, lockInput);

            ClampFor(gc);
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

            int row = 1;

            if (sb.CruiseToggle) SelectedRow = row++;
            else if (gc.ShipType != ShipType.Atmo && sb.OrbitToggle) SelectedRow = row++;
            else if (sb.CNavToggle) SelectedRow = row++;
            else if (sb.LandToggle) SelectedRow = row++;
            else if (sb.GlideToggle) SelectedRow = row++;
            else if (sb.SBurnToggle) SelectedRow = row++;
            else SelectedRow = 0;

            AddFlightRows(spt, ic, gc, false);
            spt.DrawTo(gc.LcdsMenu);
        }

        bool Activate(
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
                    SelectedRow = 1;
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
                return false;
            }

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
            else
                PaintNumbers(ic, gc);
        }

        void PaintHome(IniContext ic, GridContext gc)
        {
            Sprites spt = new Sprites(ic);
            spt.Add("Menu");
            int row = 1;
            spt.Add("Flight", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add("Toggles", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add("Numbers", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
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

        void ReturnHome()
        {
            if (view == MenuView.Flight)
                SelectedRow = 1;
            else if (view == MenuView.Toggles)
                SelectedRow = 2;
            else if (view == MenuView.Numbers)
                SelectedRow = 3;
            else
                SelectedRow = 1;
            view = MenuView.Home;
        }

        void ClampFor(GridContext gc)
        {
            int max = HomeRows;
            if (view == MenuView.Flight)
                max = FlightGpsRow(gc) + 1;
            else if (view == MenuView.Toggles)
                max = ToggleValueRows + 1;
            else if (view == MenuView.Numbers)
                max = NumberValueRows + 1;
            ClampRow(1, max);
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
    }
}
