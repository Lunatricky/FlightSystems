using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // DockPad — landing-gear / magnetic-plate status on Sci-Fi button LCDs
        // Paste into a Programmable Block. Runtime.UpdateFrequency = Update10.
        //
        // What the script CAN do
        //   - Find IMyLandingGear (landing gear + magnetic plates + large plates)
        //     whose name contains the tag (default "dock")
        //   - Find Sci-Fi button panels that have LCD surfaces
        //   - Pair gears to button slots (INI override, then auto-fill)
        //   - Color each slot LCD: green locked, yellow ready, white on, red off
        //   - Write a short state label on the LCD
        //   - SetCustomButtonName once the player has assigned an action
        //   - Write the resolved map back into the panel Custom Data so you can edit it
        //
        // What the script CANNOT do
        //   - Assign toolbar actions to buttons. Keen never exposed that to PBs.
        //     You still drag Switch Lock (or On/Off) onto each slot yourself.
        //
        // Setup
        //   1. Name gears / plates with "dock" (e.g. "Dock A", "Hangar Dock 2").
        //   2. Place Sci-Fi Four-Button Panel and/or Sci-Fi One-Button Terminal.
        //      Name those panels with "dock" too, OR put a [DockPad] section in
        //      their Custom Data (auto-written on first scan).
        //   3. Recompile. Check Echo for the map.
        //   4. On each panel: Setup Actions → drag the matching gear → Switch Lock.
        //   5. Next tick the button tooltip name is filled from the gear name.
        //
        // Panel Custom Data (auto-created, then yours to edit)
        //   [DockPad]
        //   Button1=Dock A
        //   Button2=Dock B
        //   Button3=
        //   Button4=
        //   WriteLcdText=true
        //   NameButtons=true
        //
        // PB Custom Data (optional)
        //   [DockPad]
        //   Tag=dock
        //   SameConstruct=true
        //   RescanEvery=30
        //
        // Arguments: scan | once
        // In-game editor already supplies the usual usings. MDK: add
        // System, Collections.Generic, Text, Sandbox.ModAPI.Ingame,
        // SpaceEngineers.Game.ModAPI.Ingame, VRage.Game.GUI.TextPanel,
        // VRage.Game.ModAPI.Ingame.Utilities, VRageMath.

        const string Section = "DockPad";
        const string DefaultTag = "dock";

        static readonly Color ColLocked = new Color(32, 170, 40);
        static readonly Color ColReady = new Color(210, 175, 16);
        static readonly Color ColOn = new Color(210, 210, 210);
        static readonly Color ColOff = new Color(170, 28, 28);
        static readonly Color ColEmpty = new Color(16, 16, 16);
        static readonly Color ColMiss = new Color(90, 20, 20);
        static readonly Color InkDark = new Color(8, 8, 8);
        static readonly Color InkLight = new Color(240, 240, 240);

        readonly List<IMyLandingGear> _gears = new List<IMyLandingGear>();
        readonly List<IMyButtonPanel> _panels = new List<IMyButtonPanel>();
        readonly List<Binding> _binds = new List<Binding>();
        readonly MyIni _ini = new MyIni();
        readonly StringBuilder _echo = new StringBuilder();

        string _tag = DefaultTag;
        bool _sameConstruct = true;
        int _rescanEvery = 30;
        int _tick;
        bool _scanNow = true;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            LoadPbIni();
        }

        public void Save() { }

        public void Main(string argument, UpdateType updateType)
        {

            if (!string.IsNullOrEmpty(argument))
            {
                var a = argument.Trim().ToLowerInvariant();
                if (a == "scan" || a == "reload" || a == "once")
                {
                    LoadPbIni();
                    _scanNow = true;
                }
            }

            _tick++;
            if (_scanNow || (_rescanEvery > 0 && (_tick % _rescanEvery) == 0))
            {
                Scan();
                _scanNow = false;
            }

            for (int i = 0; i < _binds.Count; i++)
                TickBind(_binds[i]);

            String echo = EchoStatus();
            WriteMeText(echo);
            Echo(echo);
        }

        private void WriteMeText(string text1, string text2 = "")
        {
            IMyTextSurface surface = Me.GetSurface(0);
            IMyTextSurface surface2 = Me.GetSurface(1);

            surface.ContentType = ContentType.TEXT_AND_IMAGE;
            surface2.ContentType = ContentType.TEXT_AND_IMAGE;

            surface.FontSize = 0.7f;
            surface2.FontSize = 0.7f;

            if (text1 != "") surface.WriteText(text1);
            if (text2 != "") surface2.WriteText(text2);
        }

        void LoadPbIni()
        {
            _ini.Clear();
            _ini.TryParse(Me.CustomData);
            _tag = _ini.Get(Section, "Tag").ToString(DefaultTag);
            if (string.IsNullOrEmpty(_tag)) _tag = DefaultTag;
            _sameConstruct = _ini.Get(Section, "SameConstruct").ToBoolean(true);
            _rescanEvery = _ini.Get(Section, "RescanEvery").ToInt32(30);
            if (_rescanEvery < 1) _rescanEvery = 1;
        }

        void Scan()
        {
            _gears.Clear();
            _panels.Clear();
            _binds.Clear();

            GridTerminalSystem.GetBlocksOfType(_gears, AcceptGear);
            GridTerminalSystem.GetBlocksOfType(_panels, AcceptPanel);
            if (_panels.Count == 0)
                GridTerminalSystem.GetBlocksOfType(_panels, AcceptAnyLcdPanel);
            SortByName(_gears);
            SortByName(_panels);

            var used = new HashSet<long>();

            for (int p = 0; p < _panels.Count; p++)
                BindPanel(_panels[p], used);
        }

        bool AcceptGear(IMyLandingGear g)
        {
            if (_sameConstruct && !g.IsSameConstructAs(Me)) return false;
            return ContainsTag(g.CustomName, _tag);
        }

        bool AcceptPanel(IMyButtonPanel p)
        {
            if (_sameConstruct && !p.IsSameConstructAs(Me)) return false;
            var sp = p as IMyTextSurfaceProvider;
            if (sp == null || sp.SurfaceCount < 1) return false;
            if (ContainsTag(p.CustomName, _tag)) return true;
            return HasDockPadSection(p.CustomData);
        }

        bool AcceptAnyLcdPanel(IMyButtonPanel p)
        {
            if (_sameConstruct && !p.IsSameConstructAs(Me)) return false;
            var sp = p as IMyTextSurfaceProvider;
            return sp != null && sp.SurfaceCount > 0;
        }

        bool HasDockPadSection(string data)
        {
            if (string.IsNullOrEmpty(data)) return false;
            _ini.Clear();
            if (!_ini.TryParse(data)) return false;
            return _ini.ContainsSection(Section);
        }

        void BindPanel(IMyButtonPanel panel, HashSet<long> used)
        {
            var sp = (IMyTextSurfaceProvider)panel;
            int slots = sp.SurfaceCount;
            if (slots > 8) slots = 8;

            _ini.Clear();
            _ini.TryParse(panel.CustomData);
            bool writeText = _ini.Get(Section, "WriteLcdText").ToBoolean(true);
            bool nameBtns = _ini.Get(Section, "NameButtons").ToBoolean(true);

            var names = new string[slots];
            for (int i = 0; i < slots; i++)
                names[i] = (_ini.Get(Section, "Button" + (i + 1)).ToString() ?? "").Trim();

            var gears = new IMyLandingGear[slots];

            for (int i = 0; i < slots; i++)
            {
                if (string.IsNullOrEmpty(names[i])) continue;
                var g = FindGearByName(names[i], used);
                if (g == null) continue;
                gears[i] = g;
                used.Add(g.EntityId);
            }

            for (int i = 0; i < slots; i++)
            {
                if (gears[i] != null) continue;
                var g = NextFreeGear(used);
                if (g == null) break;
                gears[i] = g;
                used.Add(g.EntityId);
                names[i] = g.CustomName;
            }

            WritePanelIni(panel, names, writeText, nameBtns);

            for (int i = 0; i < slots; i++)
            {
                var b = new Binding();
                b.Panel = panel;
                b.Index = i;
                b.Gear = gears[i];
                b.Surface = sp.GetSurface(i);
                b.WriteText = writeText;
                b.NameButtons = nameBtns;
                b.LastState = -1;
                _binds.Add(b);
            }
        }

        IMyLandingGear FindGearByName(string name, HashSet<long> used)
        {
            for (int i = 0; i < _gears.Count; i++)
            {
                var g = _gears[i];
                if (used.Contains(g.EntityId)) continue;
                if (string.Equals(g.CustomName, name, StringComparison.OrdinalIgnoreCase))
                    return g;
            }
            for (int i = 0; i < _gears.Count; i++)
            {
                var g = _gears[i];
                if (used.Contains(g.EntityId)) continue;
                if (ContainsTag(g.CustomName, name))
                    return g;
            }
            return null;
        }

        IMyLandingGear NextFreeGear(HashSet<long> used)
        {
            for (int i = 0; i < _gears.Count; i++)
            {
                var g = _gears[i];
                if (!used.Contains(g.EntityId)) return g;
            }
            return null;
        }

        void WritePanelIni(IMyButtonPanel panel, string[] names, bool writeText, bool nameBtns)
        {
            _ini.Clear();
            _ini.TryParse(panel.CustomData);

            for (int i = 0; i < names.Length; i++)
                _ini.Set(Section, "Button" + (i + 1), names[i] ?? "");

            if (!_ini.ContainsKey(Section, "WriteLcdText"))
                _ini.Set(Section, "WriteLcdText", writeText);
            if (!_ini.ContainsKey(Section, "NameButtons"))
                _ini.Set(Section, "NameButtons", nameBtns);

            string next = _ini.ToString();
            if (next != panel.CustomData)
                panel.CustomData = next;
        }

        void TickBind(Binding b)
        {
            int state;
            string label;
            Color bg;
            Color ink;

            if (b.Gear == null)
            {
                state = 0;
                label = "";
                bg = ColEmpty;
                ink = InkLight;
            }
            else if (!b.Gear.IsFunctional)
            {
                state = 1;
                label = "DEAD";
                bg = ColMiss;
                ink = InkLight;
            }
            else if (b.Gear.LockMode == LandingGearMode.Locked)
            {
                state = 2;
                label = "LOCK";
                bg = ColLocked;
                ink = InkDark;
            }
            else if (b.Gear.LockMode == LandingGearMode.ReadyToLock)
            {
                state = 3;
                label = "RDY";
                bg = ColReady;
                ink = InkDark;
            }
            else if (b.Gear.Enabled)
            {
                state = 4;
                label = "ON";
                bg = ColOn;
                ink = InkDark;
            }
            else
            {
                state = 5;
                label = "OFF";
                bg = ColOff;
                ink = InkLight;
            }

            if (state != b.LastState && b.Surface != null)
            {
                var s = b.Surface;
                if (s.ContentType != ContentType.TEXT_AND_IMAGE)
                    s.ContentType = ContentType.TEXT_AND_IMAGE;
                s.BackgroundColor = bg;
                s.FontColor = ink;
                s.Alignment = TextAlignment.CENTER;
                s.TextPadding = 0f;
                s.FontSize = 6f;
                s.TextPadding = 20f;
                if (b.WriteText)
                    s.WriteText(label);
                else if (state == 0)
                    s.WriteText("");
                b.LastState = state;
            }

            if (!b.NameButtons || b.Gear == null) return;
            if (!b.Panel.IsButtonAssigned(b.Index)) return;

            string want = ShortName(b.Gear.CustomName);
            if (b.LastName == want) return;
            if (b.Panel.HasCustomButtonName(b.Index) && b.Panel.GetButtonName(b.Index) == want)
            {
                b.LastName = want;
                return;
            }
            b.Panel.SetCustomButtonName(b.Index, want);
            b.LastName = want;
        }

        string EchoStatus()
        {
            _echo.Clear();
            _echo.Append("DockPad  tag=").Append(_tag).Append('\n');
            _echo.Append("gears ").Append(_gears.Count);
            _echo.Append("  panels ").Append(_panels.Count);
            _echo.Append("  slots ").Append(_binds.Count).Append('\n');

            string lastPanel = null;
            for (int i = 0; i < _binds.Count; i++)
            {
                var b = _binds[i];
                if (b.Panel.CustomName != lastPanel)
                {
                    lastPanel = b.Panel.CustomName;
                    _echo.Append('\n').Append(lastPanel).Append('\n');
                }
                _echo.Append("  ").Append(b.Index + 1).Append("  ");
                if (b.Gear == null)
                {
                    _echo.Append("(empty)");
                }
                else
                {
                    _echo.Append(b.Gear.CustomName).Append("  ");
                    _echo.Append(StateWord(b.LastState));
                    if (!b.Panel.IsButtonAssigned(b.Index))
                        _echo.Append("  [assign Switch Lock]");
                }
                _echo.Append('\n');
            }

            return _echo.ToString();
        }

        static string StateWord(int s)
        {
            switch (s)
            {
                case 2: return "LOCKED";
                case 3: return "READY";
                case 4: return "ON";
                case 5: return "OFF";
                case 1: return "DEAD";
                default: return "";
            }
        }

        static string ShortName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Dock";
            string n = name.Trim();
            if (n.Length <= 24) return n;
            return n.Substring(0, 24);
        }

        static bool ContainsTag(string name, string tag)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(tag)) return false;
            return name.IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static void SortByName<T>(List<T> list) where T : IMyTerminalBlock
        {
            for (int i = 1; i < list.Count; i++)
            {
                var cur = list[i];
                int j = i - 1;
                while (j >= 0 && string.Compare(list[j].CustomName, cur.CustomName, StringComparison.OrdinalIgnoreCase) > 0)
                {
                    list[j + 1] = list[j];
                    j--;
                }
                list[j + 1] = cur;
            }
        }

        class Binding
        {
            public IMyButtonPanel Panel;
            public int Index;
            public IMyLandingGear Gear;
            public IMyTextSurface Surface;
            public bool WriteText;
            public bool NameButtons;
            public int LastState;
            public string LastName;
        }

    }
}
