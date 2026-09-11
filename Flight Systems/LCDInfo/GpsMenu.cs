using IngameScript.Domain;
using IngameScript.Enums;
using IngameScript.UseCases;
using IngameScript.Utils;
using System;
using VRageMath;

namespace IngameScript
{
    enum GpsView
    {
        List,
        Actions,
        AddKind,
        AddFields,
        ConfirmDelete
    }

    class GpsMenu
    {
        public bool IsOpen;

        GpsView view;
        int row = 1;
        int selectedIndex;
        bool editing;
        GpsWaypoint draft = new GpsWaypoint();

        public void Open()
        {
            IsOpen = true;
            view = GpsView.List;
            row = 1;
        }

        public void Close()
        {
            IsOpen = false;
            view = GpsView.List;
        }

        public void Back()
        {
            if (view == GpsView.List)
                Close();
            else
            {
                view = GpsView.List;
                row = 1;
            }
        }

        public void Tick(
            PlayerInput pi,
            GridContext gc,
            IniContext ic,
            Command command,
            Action lockInput,
            Action close,
            Action softAbort)
        {
            GpsStore store = ic.Gps;
            MoveRow(pi, lockInput);

            switch (view)
            {
                case GpsView.List:
                    DrawList(ic, gc, store, pi, lockInput);
                    break;
                case GpsView.Actions:
                    DrawActions(ic, gc, store, pi, command, lockInput, close, softAbort);
                    break;
                case GpsView.AddKind:
                    DrawAddKind(ic, gc, pi, lockInput);
                    break;
                case GpsView.AddFields:
                    DrawAddFields(ic, gc, store, pi, lockInput);
                    break;
                case GpsView.ConfirmDelete:
                    DrawConfirmDelete(ic, gc, store, pi, lockInput);
                    break;
            }
        }

        void MoveRow(PlayerInput pi, Action lockInput)
        {
            if (pi.W())
            {
                lockInput();
                row--;
            }
            if (pi.S())
            {
                lockInput();
                row++;
            }
        }

        int ListTotal(GpsStore store)
        {
            return store.Count + 2;
        }

        void DrawList(IniContext ic, GridContext gc, GpsStore store, PlayerInput pi, Action lockInput)
        {
            int total = ListTotal(store);
            Clamp(1, total);

            if (pi.Space())
            {
                lockInput();
                if (row == total)
                    Close();
                else if (row == total - 1)
                    StartAdd();
                else
                {
                    selectedIndex = row - 1;
                    view = GpsView.Actions;
                    row = 1;
                }
            }

            Sprites spt = new Sprites(ic);
            spt.Add("GPS");

            int window = 6;
            int start = row - 1 - window / 2;
            if (start < 0) start = 0;
            if (start > total - window) start = total - window;
            if (start < 0) start = 0;

            int end = start + window;
            if (end > total) end = total;

            for (int i = start; i < end; i++)
            {
                int displayRow = i + 1;
                string label;
                if (i < store.Count)
                    label = store.Get(i).Name;
                else if (i == store.Count)
                    label = "Add GPS";
                else
                    label = "Back";

                spt.Add(label, RowColor(displayRow, ic.SpriteBackgroundColor), RowColor(displayRow, ic.SpriteFontColor));
            }

            spt.DrawTo(gc.LcdsSettings);
        }

        void DrawActions(IniContext ic, GridContext gc, GpsStore store, PlayerInput pi, Command command, Action lockInput, Action close, Action softAbort)
        {
            GpsWaypoint wp = store.Get(selectedIndex);
            if (wp == null)
            {
                view = GpsView.List;
                return;
            }

            Clamp(1, 4);

            if (pi.Space())
            {
                lockInput();
                if (row == 1)
                {
                    softAbort();
                    command.Empty(MainState.Gps);
                    command.Param.TargetCoordinates = wp.Position;
                    command.Param.Type = ParamType.Vector3D;
                    Close();
                    close();
                    return;
                }
                if (row == 2)
                {
                    editing = true;
                    draft = wp.Copy();
                    view = GpsView.AddKind;
                    row = 1;
                }
                else if (row == 3)
                {
                    view = GpsView.ConfirmDelete;
                    row = 2;
                }
                else
                {
                    view = GpsView.List;
                    row = selectedIndex + 1;
                }
            }

            Sprites spt = new Sprites(ic);
            spt.Add(wp.Name);
            int r = 1;
            spt.Add("Fly to GPS", RowColor(r, ic.SpriteBackgroundColor), RowColor(r++, ic.SpriteFontColor));
            spt.Add("Edit", RowColor(r, ic.SpriteBackgroundColor), RowColor(r++, ic.SpriteFontColor));
            spt.Add("Delete", RowColor(r, ic.SpriteBackgroundColor), RowColor(r++, ic.SpriteFontColor));
            spt.Add("Back", RowColor(r, ic.SpriteBackgroundColor), RowColor(r++, ic.SpriteFontColor));
            spt.DrawTo(gc.LcdsSettings);
        }

        void DrawAddKind(IniContext ic, GridContext gc, PlayerInput pi, Action lockInput)
        {
            Clamp(1, 2);

            if (pi.A() || pi.D())
            {
                lockInput();
                if (row == 1)
                    draft.Location = CycleEnum(draft.Location, pi.D());
            }

            if (pi.Space())
            {
                lockInput();
                if (row == 2)
                {
                    view = editing ? GpsView.Actions : GpsView.List;
                    row = 1;
                }
                else
                {
                    view = GpsView.AddFields;
                    row = 1;
                }
            }

            Sprites spt = new Sprites(ic);
            spt.Add(editing ? "Edit GPS" : "Add GPS");
            int r = 1;
            spt.Add("Type: " + draft.KindLabel(), RowColor(r, ic.SpriteBackgroundColor), RowColor(r++, ic.SpriteFontColor));
            spt.Add("Back", RowColor(r, ic.SpriteBackgroundColor), RowColor(r++, ic.SpriteFontColor));
            spt.DrawTo(gc.LcdsSettings);
        }

        void DrawAddFields(IniContext ic, GridContext gc, GpsStore store, PlayerInput pi, Action lockInput)
        {
            int fieldCount = FieldCount(draft.Location);
            int extra = 2;
            int total = fieldCount + extra;
            Clamp(1, total);

            if (pi.A() || pi.D())
            {
                lockInput();
                CycleField(draft, row, pi.D());
            }

            if (pi.Space())
            {
                lockInput();
                if (row == total)
                {
                    view = GpsView.AddKind;
                    row = 1;
                }
                else if (row == fieldCount + 1)
                    SaveDraft(gc, ic, store, editing);
            }

            Sprites spt = new Sprites(ic);
            spt.Add(editing ? "Edit GPS" : "Add GPS");
            int r = 1;
            AddFieldRows(spt, ic, draft, ref r);
            spt.Add("Save", RowColor(r, ic.SpriteBackgroundColor), RowColor(r++, ic.SpriteFontColor));
            spt.Add("Back", RowColor(r, ic.SpriteBackgroundColor), RowColor(r++, ic.SpriteFontColor));
            spt.DrawTo(gc.LcdsSettings);
        }

        void DrawConfirmDelete(IniContext ic, GridContext gc, GpsStore store, PlayerInput pi, Action lockInput)
        {
            GpsWaypoint wp = store.Get(selectedIndex);
            if (wp == null)
            {
                view = GpsView.List;
                return;
            }

            Clamp(1, 2);
            if (pi.Space())
            {
                lockInput();
                if (row == 1)
                {
                    store.RemoveAt(selectedIndex);
                    ic.FlushGps(gc.Controller);
                    view = GpsView.List;
                    row = 1;
                }
                else
                {
                    view = GpsView.Actions;
                    row = 1;
                }
            }

            Sprites spt = new Sprites(ic);
            spt.Add("Delete " + wp.Name + "?");
            int r = 1;
            spt.Add("Yes", RowColor(r, ic.SpriteBackgroundColor), RowColor(r++, ic.SpriteFontColor));
            spt.Add("No", RowColor(r, ic.SpriteBackgroundColor), RowColor(r++, ic.SpriteFontColor));
            spt.DrawTo(gc.LcdsSettings);
        }

        void StartAdd()
        {
            editing = false;
            draft = new GpsWaypoint();
            draft.Location = LocationType.Planet;
            view = GpsView.AddKind;
            row = 1;
        }

        void SaveDraft(GridContext gc, IniContext ic, GpsStore store, bool keepPosition)
        {
            if (!keepPosition || !editing)
            {
                if (gc.Controller == null)
                    return;
                draft.Position = gc.Controller.GetPosition();
            }

            draft.Name = draft.BuildName();
            if (editing)
                store.Replace(selectedIndex, draft);
            else
                store.Add(draft);

            ic.FlushGps(gc.Controller);
            view = GpsView.List;
            row = 1;
            editing = false;
        }

        static int FieldCount(LocationType kind)
        {
            switch (kind)
            {
                case LocationType.Planet: return 2;
                case LocationType.Asteroid: return 1;
                case LocationType.Station: return 3;
                case LocationType.Vein: return 3;
                default: return 2;
            }
        }

        void AddFieldRows(Sprites spt, IniContext ic, GpsWaypoint wp, ref int r)
        {
            switch (wp.Location)
            {
                case LocationType.Planet:
                    Field(spt, ic, ref r, "Planet: " + EnumLabels.Planet(wp.Planet));
                    Field(spt, ic, ref r, "Region: " + EnumLabels.RegionName(wp.Region));
                    break;
                case LocationType.Asteroid:
                    Field(spt, ic, ref r, "Region: " + EnumLabels.RegionName(wp.Region));
                    break;
                case LocationType.Station:
                    Field(spt, ic, ref r, "Planet: " + EnumLabels.Planet(wp.Planet));
                    Field(spt, ic, ref r, "Station: " + EnumLabels.Station(wp.Station));
                    Field(spt, ic, ref r, "Faction: " + EnumLabels.FactionName(wp.Faction));
                    break;
                case LocationType.Vein:
                    Field(spt, ic, ref r, "Planet: " + EnumLabels.Planet(wp.Planet));
                    Field(spt, ic, ref r, "Ore: " + EnumLabels.Ore(wp.Ore));
                    Field(spt, ic, ref r, "Region: " + EnumLabels.RegionName(wp.Region));
                    break;
                default:
                    Field(spt, ic, ref r, "Planet: " + EnumLabels.Planet(wp.Planet));
                    Field(spt, ic, ref r, "Region: " + EnumLabels.RegionName(wp.Region));
                    break;
            }
        }

        void Field(Sprites spt, IniContext ic, ref int r, string label)
        {
            spt.Add(label, RowColor(r, ic.SpriteBackgroundColor), RowColor(r, ic.SpriteFontColor));
            r++;
        }

        static void CycleField(GpsWaypoint wp, int fieldRow, bool next)
        {
            switch (wp.Location)
            {
                case LocationType.Planet:
                    if (fieldRow == 1) wp.Planet = CycleEnum(wp.Planet, next);
                    else if (fieldRow == 2) wp.Region = CycleEnum(wp.Region, next);
                    break;
                case LocationType.Asteroid:
                    if (fieldRow == 1) wp.Region = CycleEnum(wp.Region, next);
                    break;
                case LocationType.Station:
                    if (fieldRow == 1) wp.Planet = CycleEnum(wp.Planet, next);
                    else if (fieldRow == 2) wp.Station = CycleEnum(wp.Station, next);
                    else if (fieldRow == 3) wp.Faction = CycleEnum(wp.Faction, next);
                    break;
                case LocationType.Vein:
                    if (fieldRow == 1) wp.Planet = CycleEnum(wp.Planet, next);
                    else if (fieldRow == 2) wp.Ore = CycleEnum(wp.Ore, next);
                    else if (fieldRow == 3) wp.Region = CycleEnum(wp.Region, next);
                    break;
                default:
                    if (fieldRow == 1) wp.Planet = CycleEnum(wp.Planet, next);
                    else if (fieldRow == 2) wp.Region = CycleEnum(wp.Region, next);
                    break;
            }
        }

        static T CycleEnum<T>(T current, bool next) where T : struct
        {
            Array values = Enum.GetValues(typeof(T));
            int i = 0;
            for (; i < values.Length; i++)
            {
                if (values.GetValue(i).Equals(current))
                    break;
            }
            i += next ? 1 : -1;
            if (i < 0) i = values.Length - 1;
            if (i >= values.Length) i = 0;
            return (T)values.GetValue(i);
        }

        void Clamp(int min, int max)
        {
            if (max < min) max = min;
            if (row < 1) row = max;
            else if (row > max) row = min;
        }

        Color RowColor(int r, Color color)
        {
            return row == r ? ColorMap.SelectedColor(color, 0.2) : color;
        }
    }
}
