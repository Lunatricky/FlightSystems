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
        Actions
    }

    class GpsMenu
    {
        public bool IsOpen;

        GpsView view;
        bool spacePressed;
        int row = 1;
        int selectedIndex;

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
                row = selectedIndex + 1;
            }
        }

        public void Tick(
            PlayerInput pi,
            GridContext gc,
            IniContext ic,
            Command command,
            Action lockInput,
            Action close,
            Action softAbort,
            bool spaceEdge)
        {
            spacePressed = spaceEdge;
            GpsStore store = ic.Gps;
            MoveRow(pi, lockInput);

            if (view == GpsView.Actions)
                DrawActions(ic, gc, store, command, lockInput, close, softAbort);
            else
                DrawList(ic, gc, store, lockInput);
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

        void DrawList(IniContext ic, GridContext gc, GpsStore store, Action lockInput)
        {
            int total = store.Count + 1;
            Clamp(1, total);

            if (spacePressed)
            {
                lockInput();
                if (row == total)
                    Close();
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
                string label = i < store.Count ? store.Get(i).Name : MenuScreens.BackLabel;
                spt.Add(label, RowColor(displayRow, ic.SpriteBackgroundColor), RowColor(displayRow, ic.SpriteFontColor));
            }

            spt.DrawTo(gc.LcdsMenu);
        }

        void DrawActions(IniContext ic, GridContext gc, GpsStore store, Command command, Action lockInput, Action close, Action softAbort)
        {
            GpsWaypoint wp = store.Get(selectedIndex);
            if (wp == null)
            {
                view = GpsView.List;
                return;
            }

            Clamp(1, 2);

            if (spacePressed)
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

                view = GpsView.List;
                row = selectedIndex + 1;
            }

            Sprites spt = new Sprites(ic);
            spt.Add(wp.Name);
            int r = 1;
            spt.Add("Fly to GPS", RowColor(r, ic.SpriteBackgroundColor), RowColor(r++, ic.SpriteFontColor));
            spt.Add(MenuScreens.BackLabel, RowColor(r, ic.SpriteBackgroundColor), RowColor(r++, ic.SpriteFontColor));
            spt.DrawTo(gc.LcdsMenu);
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
