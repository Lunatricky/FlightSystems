using IngameScript.Domain;
using IngameScript.Enums;
using IngameScript.UseCases;
using IngameScript.Utils;
using System;
using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    class SettingsScreens
    {
        public int SelectedRow;
        public int SelectedPage = 1;
        public bool IsDefaultScreen;
        public Dictionary<string, Vector3D> GpsList = new Dictionary<string, Vector3D>();

        public bool ShouldClose(PlayerInput pi)
        {
            return SelectedRow == 0 && pi.Space();
        }

        public void Handle(
            PlayerInput pi,
            GridContext gc,
            IniContext ic,
            SystemBools sb,
            Command command,
            bool settingsToggle,
            Action abort,
            Action softAbort,
            Action close,
            Action lockInput)
        {
            Navigate(pi, lockInput);

            if (sb.GpsMenuToggle)
            {
                GpsSection(ic, gc, pi, close);
                return;
            }

            switch (SelectedPage)
            {
                case 1:
                    ClampRow(1, 7);
                    if (settingsToggle)
                        FlightSystemSectionEdit(ic, gc, sb, pi, command, abort, softAbort, close);
                    else
                        FlightSystemIdle(ic, gc, sb);
                    break;
                case 2:
                    ClampRow(1, 8);
                    ToggleSectionEdit(ic, gc, pi, lockInput);
                    break;
                case 3:
                    ClampRow(1, 5);
                    ParamSectionEdit(ic, gc, pi, lockInput);
                    break;
            }
        }

        public void FlightSystemIdle(IniContext ic, GridContext gc, SystemBools sb)
        {
            if (IsDefaultScreen)
                return;

            Sprites spt = new Sprites(ic);
            int row = 1;

            if (sb.CruiseToggle) SelectedRow = row++;
            else if (gc.ShipType != ShipType.Atmo && sb.OrbitToggle) SelectedRow = row++;
            else if (sb.CNavToggle) SelectedRow = row++;
            else if (sb.LandToggle) SelectedRow = row++;
            else if (sb.GlideToggle) SelectedRow = row++;
            else if (sb.SBurnToggle) SelectedRow = row++;
            else if (sb.GpsMenuToggle) SelectedRow = row++;
            else SelectedRow = 0;

            AddFlightRows(spt, ic, gc);
            spt.DrawTo(gc.LcdsSettings);
            IsDefaultScreen = true;
        }

        void Navigate(PlayerInput pi, Action lockInput)
        {
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

            if (pi.Q())
            {
                lockInput();
                SelectedPage--;
            }

            if (pi.E())
            {
                lockInput();
                SelectedPage++;
            }

            if (SelectedPage < 1) SelectedPage = 3;
            else if (SelectedPage > 3) SelectedPage = 1;
        }

        void ClampRow(int min, int max)
        {
            if (SelectedRow < 0) SelectedRow = max;
            else if (SelectedRow > max) SelectedRow = min;
        }

        void FlightSystemSectionEdit(
            IniContext ic,
            GridContext gc,
            SystemBools sb,
            PlayerInput pi,
            Command command,
            Action abort,
            Action softAbort,
            Action close)
        {
            Sprites spt = new Sprites(ic);
            int row = 1;

            if (pi.Space())
            {
                close();
                MainState ms = MainState.Idle;

                if (SelectedRow == row++) ms = MainState.Cruise;
                else if (gc.ShipType != ShipType.Atmo && SelectedRow == row++) ms = MainState.Orbit;
                else if (SelectedRow == row++) ms = MainState.CNav;
                else if (SelectedRow == row++) ms = MainState.Land;
                else if (SelectedRow == row++) ms = MainState.Glide;
                else if (SelectedRow == row++) ms = MainState.SBurn;
                else if (SelectedRow == row++) ms = MainState.Gps;

                if (ms != MainState.Idle)
                {
                    if (sb.GetModeState(ms))
                    {
                        abort();
                        FlightSystemIdle(ic, gc, sb);
                        return;
                    }

                    softAbort();
                    command.Empty(ms);
                }
            }

            AddFlightRows(spt, ic, gc);
            spt.DrawTo(gc.LcdsSettings);
        }

        void AddFlightRows(Sprites spt, IniContext ic, GridContext gc)
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
        }

        void ToggleSectionEdit(IniContext ic, GridContext gc, PlayerInput pi, Action lockInput)
        {
            IsDefaultScreen = false;
            Sprites spt = new Sprites(ic);
            int row = 1;

            if (pi.A() || pi.D())
            {
                lockInput();
                if (SelectedRow == row++) ic.AllowFlightSystems = !ic.AllowFlightSystems;
                else if (SelectedRow == row++) ic.AnalogThrotle = !ic.AnalogThrotle;
                else if (SelectedRow == row++) ic.AllowLowFuelLand = !ic.AllowLowFuelLand;
                else if (SelectedRow == row++) ic.AllowDockMode = !ic.AllowDockMode;
                else if (SelectedRow == row++) ic.ControlAntennas = !ic.ControlAntennas;
                else if (SelectedRow == row++) ic.RenameSubgrids = !ic.RenameSubgrids;
                else if (SelectedRow == row++) ic.PaintSurfaces = !ic.PaintSurfaces;
                else if (SelectedRow == row++) ic.TransparentLCD = !ic.TransparentLCD;
            }

            row = 1;
            spt.Add($"{IniContext.ToggleSection}");
            spt.Add($"{IniContext.FLIGHT_SYSTEMS}", BoolSpriteColor(SelectedRow == row++, ic.AllowFlightSystems), Color.Black);
            spt.Add($"{IniContext.ANALOG_THROTLE}", BoolSpriteColor(SelectedRow == row++, ic.AnalogThrotle), Color.Black);
            spt.Add($"{IniContext.LOW_FUEL_LAND}", BoolSpriteColor(SelectedRow == row++, ic.AllowLowFuelLand), Color.Black);
            spt.Add($"{IniContext.DOCK_MODE}", BoolSpriteColor(SelectedRow == row++, ic.AllowDockMode), Color.Black);
            spt.Add($"{IniContext.CONTROL_ANTENNAS}", BoolSpriteColor(SelectedRow == row++, ic.ControlAntennas), Color.Black);
            spt.Add($"{IniContext.RENAME_SUBGRIDS}", BoolSpriteColor(SelectedRow == row++, ic.RenameSubgrids), Color.Black);
            spt.Add($"{IniContext.PAINT_SURFACES}", BoolSpriteColor(SelectedRow == row++, ic.PaintSurfaces), Color.Black);
            spt.Add($"{IniContext.TRANSPARENTLCD}", BoolSpriteColor(SelectedRow == row++, ic.TransparentLCD), Color.Black);

            spt.DrawTo(gc.LcdsSettings);
        }

        void ParamSectionEdit(IniContext ic, GridContext gc, PlayerInput pi, Action lockInput)
        {
            IsDefaultScreen = false;
            Sprites spt = new Sprites(ic);
            int row = 1;

            if (SelectedRow == row++) ic.MaxSpeed = IncrementedValue(ic.MaxSpeed, pi, lockInput);
            else if (SelectedRow == row++) ic.CruiseSpeed = IncrementedValue(ic.CruiseSpeed, pi, lockInput);
            else if (SelectedRow == row++) ic.SafeAltitude = IncrementedValue(ic.SafeAltitude, pi, lockInput);
            else if (SelectedRow == row++) ic.DistanceToGPS = IncrementedValue(ic.DistanceToGPS, pi, lockInput);
            else if (SelectedRow == row++) ic.MinimumAcceptedFuel = IncrementedValue(ic.MinimumAcceptedFuel, pi, lockInput);

            row = 1;
            spt.Add($"{IniContext.ParamsSection}");
            spt.Add($"{IniContext.MAX_SPEED}: {ic.MaxSpeed}", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add($"{IniContext.CRUISE_SPEED}: {ic.CruiseSpeed}", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add($"{IniContext.CNAV_ALTITUDE}: {ic.SafeAltitude}", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add($"{IniContext.DISTANCE_TO_GPS}: {ic.DistanceToGPS}", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add($"{IniContext.MINIMUM_ACCEPTED_FUEL}: {ic.MinimumAcceptedFuel}", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));

            spt.DrawTo(gc.LcdsSettings);
        }

        void GpsSection(IniContext ic, GridContext gc, PlayerInput pi, Action close)
        {
            IsDefaultScreen = false;
            Sprites spt = new Sprites(ic);

            if (pi.Space())
                GpsSectionEdit(ic, gc, pi, close);

            int row = 1;
            spt.Add("GPS");

            foreach (KeyValuePair<string, Vector3D> kvp in GpsList)
            {
                spt.Add("Select", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            }
            spt.Add("Add", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add("Edit", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add("Delete", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));

            spt.DrawTo(gc.LcdsSettings);
        }

        void GpsSectionEdit(IniContext ic, GridContext gc, PlayerInput pi, Action close)
        {
            IsDefaultScreen = false;
            Sprites spt = new Sprites(ic);

            if (pi.Space())
                close();

            int row = 1;
            spt.Add("GPS");
            spt.Add("Select", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add("Add", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add("Edit", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add("Delete", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));

            spt.DrawTo(gc.LcdsSettings);
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
