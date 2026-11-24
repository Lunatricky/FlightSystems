using IngameScript.Domain;
using IngameScript.Enums;
using IngameScript.Physics;
using IngameScript.UseCases;
using IngameScript.Utils;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        GridContext gc;
        IniContext ic;
        PhysicsContext pc;
        SpeedTimeTracker stt;
        PlayerInput pi;

        Command command;
        MenuScreens menuHud;
        Sprites hudStack1;
        Sprites hudStack2;
        Sprites menuStack;

        int inputLock = 0;
        int tickSplit = 3;
        int tick;
        int tickCount;
        double timeSinceLastRun;

        //Dock Mode
        bool isDocked;
        bool isDockMode;
        bool lastDockMode;
        bool anyConnected;
        int connectorSafePasses;
        int connectorSafeMisses;
        bool knownGridStatic;
        bool dockWarning;
        int dockWarningTicks;
        int dockWarningRow = 1;
        int dockPressCount;
        int ticksSinceDockPress;
        bool dockSeatsHeld;
        bool dockLatch;
        bool autoDockSuppress;
        bool suppressSawUnsafe;
        readonly List<IMyLandingGear> constructGears = new List<IMyLandingGear>();
        readonly List<IMyLandingGear> dockTriggerGroup = new List<IMyLandingGear>();
        readonly List<IMyShipConnector> constructConnectors = new List<IMyShipConnector>();
        readonly List<IMyShipConnector> dockTriggerConnectors = new List<IMyShipConnector>();
        readonly List<IMyShipConnector> landingPadConnectors = new List<IMyShipConnector>();
        readonly List<IMyShipConnector> ownGridConnectors = new List<IMyShipConnector>();
        readonly List<long> lastTriggerGearIds = new List<long>();
        readonly List<long> lastTriggerConnectorIds = new List<long>();
        readonly List<long> lastPadConnectorIds = new List<long>();
        readonly List<long> sampleTriggerGearIds = new List<long>();
        readonly List<long> sampleTriggerConnectorIds = new List<long>();
        readonly List<long> samplePadConnectorIds = new List<long>();
        int groupSyncTicks;
        bool menuToggle;
        bool menuIsLocked;
        bool menuSeatHold;
        int seatHoldKeys;
        string textModeEcho;

        double planetRadius = 0;
        bool haveFall;
        double fallAlt;
        double fallDecel;

        SystemBools sb;

        Task task;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;

            gc = new GridContext(GridTerminalSystem, Me);
            ic = new IniContext(gc);
            sb = new SystemBools();
            stt = new SpeedTimeTracker();
            pi = new PlayerInput(gc.Controllers);
            command = new Command();
            menuHud = new MenuScreens();
            hudStack1 = new Sprites(ic);
            hudStack2 = new Sprites(ic);
            menuStack = new Sprites(ic);

            CheckIni();

            if (gc.LcdsMenu.Count > 0) menuHud.FlightSystemIdle(ic, gc, sb);
        }

        public void Main(string argument)
        {
            Sprites.NoteSpriteTick();

            // Stop at 121 so a long idle cannot wrap the gap back into the 120-tick window.
            if (dockPressCount > 0 && ticksSinceDockPress <= 120)
                ticksSinceDockPress++;

            if (!string.IsNullOrEmpty(argument))
            {
                foreach (string param in ic.IniParamList)
                {
                    if (argument.Contains(param))
                    {
                        var parts = argument.Trim().Split(
                            new[] { ':' },
                            StringSplitOptions.RemoveEmptyEntries
                        );

                        double value;
                        try
                        {
                            value = double.Parse(parts[1]);
                        }
                        catch (Exception)
                        {
                            return;
                        }
                        switch (parts[0])
                        {
                            case IniContext.MAX_SPEED:
                                ic.MaxSpeed = value;
                                break;
                            case IniContext.CRUISE_SPEED:
                                ic.CruiseSpeed = value;
                                break;
                            case IniContext.CNAV_ALTITUDE:
                                ic.SafeAltitude = value;
                                break;
                            case IniContext.DISTANCE_TO_GPS:
                                ic.DistanceToGPS = value;
                                break;
                            case IniContext.MINIMUM_ACCEPTED_FUEL:
                                ic.MinimumAcceptedFuel = value;
                                break;
                        }
                        return;
                    }
                }

                string dockArg = argument.Trim().ToLowerInvariant();
                if (dockArg == "dock" || dockArg == "dock confirm" || dockArg == "dock cancel")
                {
                    if (dockArg == "dock")
                        OnDock();
                    else if (dockArg == "dock confirm")
                        OnDockConfirm();
                    else
                        CancelDockWarning();
                }
                else if (dockArg == "text")
                {
                    ic.TextPanels = !ic.TextPanels;
                    textModeEcho = ic.TextPanels ? "Text" : "Sprites";
                    Echo(textModeEcho);
                }
                else if (argument.ToLowerInvariant() == "menu" && (gc.Cockpits.Count > 1 || isDocked))
                {
                    menuToggle = !menuToggle;

                    if (menuToggle)
                    {
                        menuSeatHold = false;
                        menuHud.ShowHome();
                        HoldMenuSeat();
                    }
                    else
                    {
                        menuHud.ReleaseScreenMarker();
                        menuSeatHold = false;
                        pi.ResetControllers(gc.Controllers);
                    }
                }
                else if (!DroppedArg(argument))
                    command = new Command(argument);
            }

            inputLock++;
            if (inputLock > 10)
            {
                inputLock = 0;
                menuIsLocked = false;
            }

            ReleaseMenuSeatHold();

            if (dockWarning)
            {
                dockWarningTicks++;
                if (dockWarningTicks >= 900)
                {
                    // Timeout is its own reset. A dock after it starts at 1 even inside 120 ticks.
                    dockPressCount = 0;
                    ticksSinceDockPress = 0;
                    CancelDockWarning();
                }
            }

            if (dockWarning)
            {
                HandleDockWarningInput();
                if (dockWarning)
                    DrawDockWarning();
            }
            else if (menuToggle && gc.LcdsMenu.Count > 0)
            {
                // Sample Space while the menu is open, including a locked tick that does not read keys.
                menuHud.NoteSpace(pi);
                if (!menuIsLocked)
                {
                    RefreshHudLines();
                    if (menuHud.ShouldClose(ic))
                    {
                        CloseMenuFromC();
                        menuHud.FlightSystemIdle(ic, gc, sb, menuStack);
                        // menuToggle is false now. Rebind so the shared panel is the flight HUD, not the idle list.
                        RefreshHudLines();
                        Sprites.PaintHud();
                        task = Task.ResetControllers;
                        Echo(GetRuntimeInfo());
                        return;
                    }

                    menuHud.Handle(
                        pi, gc, ic, sb, command, menuToggle,
                        () => { ic.ClearRestoreHold(); AbortShipContext(gc); },
                        () => SoftAbort(gc),
                        CloseMenuFromSpace,
                        LockInput);
                    if (!menuToggle)
                    {
                        RefreshHudLines();
                        Sprites.PaintHud();
                    }
                }
            }

            if (!menuToggle && ic.AnalogThrotle && command.State == MainState.Idle)
            {
                AnalogThrust();
            }

            tickCount++;
            groupSyncTicks++;
            if (groupSyncTicks >= 100)
            {
                groupSyncTicks = 0;
                SyncDockGroupsIfChanged();
            }
            if (tickCount % 50 == 1)
            {
                // Station and ship write different connector ini. Do not wait for the 500-tick parse.
                bool gridStatic = GridIsStatic();
                if (gridStatic != knownGridStatic)
                {
                    ic.ParseIni();
                    ReloadGridContext(gc, ic);
                    gridStatic = GridIsStatic();
                }

                anyConnected = gc.IsAnyConnectorConnected();
                // Two connector passes to enter. One failed read does not dock and does not undock.
                // A locked gear whose line matches Dock Trigger Tag joins connectorOpen as one hold.
                bool connectorOpen = ConnectorSafeToDock();
                bool gearDock = GearDocking();
                bool held = connectorOpen || gearDock;
                bool connectorSafe = ic.AllowDockMode && !gridStatic && connectorOpen;
                if (connectorSafe)
                {
                    connectorSafeMisses = 0;
                    if (connectorSafePasses < 2)
                        connectorSafePasses++;
                }
                else if (connectorSafePasses >= 2)
                {
                    connectorSafeMisses++;
                    if (connectorSafeMisses >= 2)
                    {
                        connectorSafePasses = 0;
                        connectorSafeMisses = 0;
                    }
                }
                else
                {
                    connectorSafePasses = 0;
                    connectorSafeMisses = 0;
                }

                if (autoDockSuppress)
                {
                    if (!held)
                        suppressSawUnsafe = true;
                    else if (suppressSawUnsafe)
                        autoDockSuppress = false;
                }

                if (dockLatch)
                    isDocked = true;
                else if (autoDockSuppress || gridStatic)
                    isDocked = false;
                else
                    isDocked = connectorSafePasses >= 2 || gearDock;
                task = Task.IsDocked;
                Echo(GetRuntimeInfo());
                return;
            }

            if (tickCount % 50 == 2 && !IsShipControlled())
                DrawFlightLcds();

            if (dockLatch)
                isDocked = true;

            if (ic.AllowDockMode)
            {
                if (!isDocked) isDockMode = false;
                else if (isDocked && !isDockMode) isDockMode = true;

                if (lastDockMode != isDockMode)
                {
                    AbortShipContext(gc);
                    DockToggle(gc, isDockMode, anyConnected);
                    lastDockMode = isDockMode;
                }
            }

            if (isDockMode)
            {
                Echo(GetRuntimeInfo());
                return;
            }

            if (gc.ErrorMessage.Length > 0)
            {
                ic.SaveCommand(command);
                Echo("ErrorMessage: \n" + gc.ErrorMessage.ToString());
                return;
            }

            timeSinceLastRun = Runtime.TimeSinceLastRun.TotalSeconds;

            if (tickCount > 500)
            {
                if (CheckIni())
                {
                    task = Task.CheckIni;
                    Echo(GetRuntimeInfo());
                    return;
                }

                if (pc.Gravity > 0 && planetRadius == 0)
                    planetRadius = Vector3D.Distance(gc.Controller.GetPosition(), pc.PlanetCenter) - pc.SeaLevel;
                else if (pc.Gravity == 0)
                    planetRadius = 0;
            }

            switch (tick % tickSplit)
            {
                case 0:
                    task = Task.PhysicsUpdate;
                    Vector3D aimAt = command.Param.TargetCoordinates;
                    pc.NewRun(timeSinceLastRun, aimAt, command);
                    break;
                case 1:
                    task = Task.FlightSystems;
                    FlightSystems(gc, ic, pc);
                    break;
                case 2:
                    task = Task.LCDs;
                    if (IsShipControlled())
                        DrawFlightLcds();
                    pc.CacheValues();
                    break;
            }

            tick++;

            Echo(GetRuntimeInfo());
        }

        private bool IsShipControlled()
        {
            foreach (IMyShipController controller in gc.Controllers)
            {
                if (controller.IsUnderControl) return true;
            }
            return false;
        }

        bool CheckIni()
        {
            tickCount = 0;

            bool hasIniChanged = ic.ParseIni();

            if (!string.IsNullOrWhiteSpace(gc.GridName) && !gc.GridName.Contains(" Grid "))
            {
                gc.Me.CubeGrid.CustomName = gc.GridName;
            }
            else
            {
                gc.Me.CubeGrid.CustomName = gc.ShipType.ToString();
            }

            if (hasIniChanged || gc.Controller == null || gc.Controller.Closed)
            {
                if (!ic.AllowDockMode && !dockLatch)
                {
                    AbortShipContext(gc);
                    DockToggle(gc, false);
                    lastDockMode = isDockMode;
                }
                ReloadGridContext(gc, ic);
                ic.ApplySavedCommand(command, ref sb);
                tick = 0;
                RedrawSpriteLcds();
                return true;
            }

            ic.ApplySavedCommand(command, ref sb);

            if (gc.SyncTaggedCockpitIni())
            {
                gc.ReloadLCDs();
                RedrawSpriteLcds();
                return true;
            }

            return false;
        }

        void RefreshHudLines()
        {
            if (pc == null)
            {
                hudStack1.Clear();
                hudStack2.Clear();
                Sprites.BindFlight(gc.Lcds1, gc.Lcds2, gc.LcdsMenu, menuToggle, null, null, menuStack);
                return;
            }

            Lcd1Display.Fill(hudStack1, ic, gc, pc, command);
            Lcd2Display.Fill(hudStack2, pc, command, sb, isDockMode);
            Sprites.BindFlight(gc.Lcds1, gc.Lcds2, gc.LcdsMenu, menuToggle, hudStack1, hudStack2, menuStack);
        }

        void DrawFlightLcds()
        {
            if (dockWarning)
            {
                DrawDockWarning();
                return;
            }

            RefreshHudLines();
            if (!menuToggle)
                menuHud.FlightSystemIdle(ic, gc, sb, menuStack);
            Sprites.PaintHud();
        }

        void RedrawSpriteLcds()
        {
            menuHud.IsDefaultScreen = false;
            DrawFlightLcds();
        }

        void AnalogThrust()
        {
            pi.OcupiedController(gc.Controllers);
            if (gc.ForwardThrusters.Count == 0)
                return;

            if (pi.W())
            {
                foreach (IMyThrust t in gc.ForwardThrusters)
                {
                    t.ThrustOverridePercentage = t.ThrustOverridePercentage + 0.01f;
                }
            }

            if (pi.S())
            {
                foreach (IMyThrust t in gc.ForwardThrusters)
                {
                    t.ThrustOverridePercentage = t.ThrustOverridePercentage - 0.01f;
                }
            }

            // No W/S this tick leaves the last override. Abort is what clears it.
            if (gc.ForwardThrusters[0].ThrustOverridePercentage > 0) gc.KillThrusters(gc.BreakingThrusters);
            else gc.ResetThrusters(gc.BreakingThrusters);
        }

        double TickCounter;
        double MaxRuntime;
        double MaxInstruction;
        double RunTimeSum;
        double InstructionSum;
        int SumCounter;
        string maxTask;

        private String GetRuntimeInfo()
        {
            if (++TickCounter % 200 == 1)
            {
                MaxRuntime = MaxInstruction = RunTimeSum = InstructionSum = 0;
                SumCounter = 0;
            }

            double lastRunTimeMs = Runtime.LastRunTimeMs;
            double currentInstructions = Runtime.CurrentInstructionCount;

            RunTimeSum += lastRunTimeMs;
            InstructionSum += currentInstructions;
            SumCounter++;

            if (lastRunTimeMs > MaxRuntime)
            {
                MaxRuntime = lastRunTimeMs;
                maxTask = EnumLabels.TaskName(task);
            }

            if (currentInstructions > MaxInstruction)
                MaxInstruction = currentInstructions;

            double avgRuntime = RunTimeSum / SumCounter;
            double avgInstructions = InstructionSum / SumCounter;

            ic.SaveCommand(command);
            string report = $"Local Max\nTask: {maxTask}\nRuntime: {MaxRuntime} Ms\nInstructions: {MaxInstruction} Ms\n\nAverage\nRuntime: {avgRuntime:F4} Ms\nInstructions: {avgInstructions:0}";
            if (gearLockNote != null)
                report = report + "\n" + gearLockNote;
            if (textModeEcho != null)
                report = report + "\n" + textModeEcho;
            return report;
        }


        private void FlightSystems(GridContext gc, IniContext ic, PhysicsContext pc)
        {
            if (ic.ControlAntennas)
            {
                gc.Antennas.ForEach(b => { if (b != null) b.Enabled = false; });
                if (gc.Antennas.Count > 0)
                {
                    var firstValid = gc.Antennas.FirstOrDefault(b => b != null && !b.Closed);
                    if (firstValid != null) firstValid.Enabled = true;
                }
            }

            // The toggle is forced off while static. Low fuel must not start a landing from Idle before the next parse.
            if (GridIsStatic())
            {
                if (command.State == MainState.Reload)
                    ReloadGridContext(gc, ic);
                else if (command.State == MainState.Abort)
                    AbortShipContext(gc);
                return;
            }

            var allowedStates = new[] { MainState.CNav, MainState.Cruise, MainState.Orbit, MainState.Land, MainState.Gps };

            if (!ic.AllowFlightSystems && allowedStates.Contains(command.State))
            {
                return;
            }

            ic.SaveCommand(command);
            if (gc.Controller == null || gc.Controller.Closed)
            {
                if (command.State == MainState.Reload)
                    ReloadGridContext(gc, ic);
                else if (command.State == MainState.Abort)
                    AbortShipContext(gc);
                return;
            }
            ic.ApplySavedCommand(command, ref sb);
            ic.ClearRestoreHold();

            if (ic.AllowLowFuelLand && pc.Gravity > 0)
            {
                if (pc.H2Cache.Percent < ic.MinimumAcceptedFuel && gc.Controller.GetNaturalGravity().Length() / 9.81 > 0.75)
                {
                    command.State = MainState.Land;
                }
                else if (pc.H2Cache.Percent < ic.MinimumAcceptedFuel && gc.Controller.GetNaturalGravity().Length() / 9.81 < 0.75)
                {
                    command.State = MainState.Orbit;
                }
            }

            // Stop cruise control when leaves gravity well
            if (sb.StopCruiseWhenOutOfGrav && sb.LastCheckIsOnNatGrav && pc.Gravity == 0.0)
            {
                AbortShipContext(gc);
                return;
            }
            else sb.LastCheckIsOnNatGrav = pc.Gravity > 0.0;

            switch (command.State)
            {
                case MainState.Reload:
                    ReloadGridContext(gc, ic);
                    break;
                case MainState.Abort:
                    AbortShipContext(gc);
                    break;
                case MainState.Cruise:
                    gc.Controller.DampenersOverride = true;
                    CruiseControlStateSwitch(gc, ic, command);
                    break;
                case MainState.Orbit:
                    gc.Controller.DampenersOverride = true;
                    OrbitStateSwitch(gc, ic, command);
                    break;
                case MainState.CNav: // Circumnavigation
                    if (pc.Gravity > 0)
                    {
                        gc.Controller.DampenersOverride = true;
                        CircumNavigateStateSwitch(gc, ic, command);
                    }
                    else AbortShipContext(gc);
                    break;
                case MainState.Gps: // Fly to GPS
                    gc.Controller.DampenersOverride = true;
                    GPSStateSwitch(gc, ic, command);
                    break;
                case MainState.Land: // Auto Land
                    if (pc.Gravity == 0)
                    {
                        AbortShipContext(gc);
                        return;
                    }
                    if (command.Param.AutoLandState == AutoLandState.Idle) command.Param.AutoLandState = AutoLandState.Align;
                    AutoLandStateSwitch(gc, command);
                    break;
            }
        }

        static bool DroppedArg(string argument)
        {
            string cmd = argument.Trim().ToLowerInvariant();
            return cmd == "glide" || cmd == "sburn" || cmd.StartsWith("glide ") || cmd.StartsWith("sburn ");
        }

        private void DockToggle(GridContext gc, bool isDocked, bool anyConnected = false)
        {
            gc.SetBlocks(!isDocked, out isDockMode);
            gc.StockpileTanks(isDocked);
            if (anyConnected)
            {
                gc.ChargeBatteries();
            }
            else
            {
                gc.AutoBatteries();
            }
        }

        void OnDock()
        {
            if (isDockMode)
            {
                ManualExitDock();
                return;
            }

            if (dockPressCount > 0 && ticksSinceDockPress <= 120)
                dockPressCount++;
            else
                dockPressCount = 1;
            ticksSinceDockPress = 0;

            if (dockPressCount >= 3)
            {
                ManualEnterDock();
                return;
            }

            ArmDockWarning();
        }

        void OnDockConfirm()
        {
            if (!dockWarning || isDockMode)
                return;
            ManualEnterDock();
        }

        void ArmDockWarning()
        {
            dockWarning = true;
            dockWarningTicks = 0;
            dockWarningRow = 1;
            HoldDockSeats();
            DrawDockWarning();
        }

        void CancelDockWarning()
        {
            if (!dockWarning)
                return;
            ClearDockWarning();
            DrawFlightLcds();
        }

        void ClearDockWarning()
        {
            dockWarning = false;
            dockWarningTicks = 0;
            dockWarningRow = 1;
            ReleaseDockSeats();
        }

        void HoldDockSeats()
        {
            if (dockSeatsHeld || gc.Cockpits.Count <= 1 || !CockpitOccupied())
                return;
            pi.HoldSeat(gc.Controllers);
            dockSeatsHeld = true;
        }

        void ReleaseDockSeats()
        {
            if (!dockSeatsHeld)
                return;
            dockSeatsHeld = false;
            // Menu or a key close still owns the seat. Resetting here would thrust up.
            if (menuToggle || menuSeatHold)
                return;
            if (ControlHeld())
            {
                menuSeatHold = true;
                seatHoldKeys = ReadHeldKeys();
                HoldMenuSeat();
                return;
            }
            pi.ResetControllers(gc.Controllers);
        }

        void ManualEnterDock()
        {
            dockPressCount = 0;
            ticksSinceDockPress = 0;
            ClearDockWarning();
            dockLatch = true;
            autoDockSuppress = false;
            suppressSawUnsafe = false;
            isDocked = true;
            AbortShipContext(gc);
            DockToggle(gc, true);
            lastDockMode = isDockMode;
            DrawFlightLcds();
        }

        void ManualExitDock()
        {
            dockPressCount = 0;
            ticksSinceDockPress = 0;
            ClearDockWarning();
            dockLatch = false;
            autoDockSuppress = true;
            // Same hold as the 50-tick check. A gear that is still locked must not count as already clear.
            suppressSawUnsafe = !(ConnectorSafeToDock() || GearDocking());
            isDocked = false;
            AbortShipContext(gc);
            DockToggle(gc, false);
            lastDockMode = isDockMode;
            DrawFlightLcds();
        }

        void HandleDockWarningInput()
        {
            if (menuIsLocked || !CockpitOccupied())
                return;

            // The only seat still flies. Leave W, S, Space, and Q on the thrusters.
            if (gc.Cockpits.Count <= 1)
                return;

            pi.OcupiedController(gc.Controllers);
            HoldDockSeats();
            if (menuToggle)
                menuHud.NoteSpace(pi);
            if (menuToggle && menuHud.ShouldClose(ic))
            {
                LockInput();
                CancelDockWarning();
                return;
            }

            if (pi.Q())
            {
                LockInput();
                CancelDockWarning();
                return;
            }

            if (pi.W())
            {
                dockWarningRow = 1;
                LockInput();
            }
            else if (pi.S())
            {
                dockWarningRow = 2;
                LockInput();
            }

            if (gc.Cockpits.Count > 1 && pi.Space())
            {
                LockInput();
                if (dockWarningRow == 2)
                    CancelDockWarning();
                else
                    OnDockConfirm();
            }
        }

        bool CockpitOccupied()
        {
            for (int i = 0; i < gc.Controllers.Count; i++)
            {
                IMyShipController seat = gc.Controllers[i];
                if (seat != null && !seat.Closed && seat.IsUnderControl)
                    return true;
            }
            return false;
        }

        void DrawDockWarning()
        {
            Sprites.BindFlight(null, null, null, false, null, null, null);

            hudStack1.Clear();
            hudStack2.Clear();
            hudStack1.Add("DOCK UNSAFE", Color.DarkRed, Color.White);
            hudStack1.Add("Thrusters and tools will turn off");
            hudStack1.Add("No safe connector");
            hudStack1.Add("Send dock confirm");
            if (gc.Cockpits.Count <= 1)
                AddDockPressCount(hudStack1);
            hudStack1.DrawTo(gc.Lcds1);
            hudStack1.DrawTo(gc.Lcds2);

            menuStack.Clear();
            menuStack.Add("DOCK UNSAFE", Color.DarkRed, Color.White);
            menuStack.Add("Thrusters and tools will turn off");
            menuStack.Add("No safe connector");
            menuStack.Add("Send dock confirm");
            if (gc.Cockpits.Count <= 1)
                AddDockPressCount(menuStack);
            else
            {
                menuStack.Add("Confirm", WarningRowColor(1), Color.White);
                menuStack.Add("Cancel", WarningRowColor(2), Color.White);
            }
            menuStack.DrawTo(gc.LcdsMenu);
        }

        void AddDockPressCount(Sprites stack)
        {
            stack.Add("Press dock 3 times");
            // Read on every draw. A saved "1/3" would stay up after the second press.
            // Literals stay intact after MDK minify.
            if (dockPressCount >= 2)
                stack.Add("2/3");
            else
                stack.Add("1/3");
        }

        Color WarningRowColor(int row)
        {
            if (dockWarningRow == row)
                return Color.DarkOrange;
            return ic.SpriteBackgroundColor;
        }

        void CruiseControlStateSwitch(GridContext gc, IniContext ic, Command command)
        {
            double CruiseSpeed = (command.Param.Number > 0 ? command.Param.Number : ic.CruiseSpeed);

            switch (command.Param.Step)
            {
                case Step.Toggle:
                    ToggleCommand(gc, command);
                    break;
                case Step.On:
                    CruiseControl(CruiseSpeed, timeSinceLastRun);
                    break;
                case Step.Off:
                    AbortShipContext(gc);
                    break;
            }
        }

        void OrbitStateSwitch(GridContext gc, IniContext ic, Command command)
        {
            double CruiseSpeed = (command.Param.Number > 0 ? command.Param.Number : ic.CruiseSpeed);

            switch (command.Param.Step)
            {
                case Step.Toggle:
                    ToggleCommand(gc, command);
                    break;
                case Step.On:
                    if (GravityAlignedOverride(gc))
                    {
                        command.Param.Step = Step.Preclimb;
                        return;
                    }
                    break;
                case Step.Off:
                    AbortShipContext(gc);
                    break;
                case Step.Preclimb:
                    if (GravityAlignedOverride(gc, pc.ForwardVelocity == 0))
                    {
                        SoftAbort(gc);
                        BeginClimbOrLift(gc, command);
                    }
                    break;
                case Step.Lift:
                    LiftClearance(gc, command);
                    break;
                case Step.Climb:
                    if (pc.Gravity > 0 && ForwardTerrainClose(gc, ic.CruiseSpeed))
                        ArmClimbBoost();
                    Climb(gc, ic.CruiseSpeed);
                    break;
            }
        }

        void CircumNavigateStateSwitch(GridContext gc, IniContext ic, Command command)
        {
            double CruiseSpeed = (command.Param.Number > 0 ? command.Param.Number : ic.CruiseSpeed);
            switch (command.Param.Step)
            {
                case Step.Toggle:
                    ToggleCommand(gc, command);
                    break;
                case Step.On:
                    if (pc.GroundLevel < ic.SafeAltitude)
                    {
                        SoftAbort(gc);
                        command.Param.Step = Step.Preclimb;
                    }
                    else
                    {
                        if (pc.Gravity > 0 && ForwardTerrainClose(gc, CruiseSpeed))
                        {
                            SoftAbort(gc);
                            command.Param.Step = Step.Preclimb;
                            break;
                        }
                        GravityAlignedOverride(gc);
                        CruiseControl(CruiseSpeed, timeSinceLastRun);
                    }
                    break;
                case Step.Off:
                    AbortShipContext(gc);
                    break;
                case Step.Preclimb:
                    if (GravityAlignedOverride(gc, pc.ForwardVelocity == 0))
                        BeginClimbOrLift(gc, command);
                    break;
                case Step.Lift:
                    LiftClearance(gc, command);
                    break;
                case Step.Climb:
                    if (pc.Gravity > 0 && ForwardTerrainClose(gc, CruiseSpeed))
                        ArmClimbBoost();
                    if (pc.GroundLevel > ic.SafeAltitude && !climbBoost && !rayBlocksLevel)
                    {
                        gc.ResetThrusters(gc.ForwardThrusters);
                        command.State = MainState.CNav;
                        command.Param.Step = Step.On;
                    }
                    Climb(gc, CruiseSpeed);
                    break;
            }
        }

        void GPSStateSwitch(GridContext gc, IniContext ic, Command command)
        {
            Vector3D gps = command.Param.TargetCoordinates;
            double reach = ic.DistanceToGPS + pc.StopZDist;

            switch (command.Param.Step)
            {
                case Step.Toggle:
                    ToggleCommand(gc, command);
                    break;

                case Step.On:
                    if (pc.Gravity == 0)
                    {
                        if (VectorAlignedOverride(gc, gc.Controller.WorldMatrix.Forward, false, gc.Controller.GetPosition() - gps))
                            command.Param.Step = Step.Cruise;
                    }
                    else if (GravityAlignedOverride(gc))
                    {
                        command.Param.Step = Step.AimToGPS;
                    }
                    break;

                case Step.AimToGPS:
                    if (pc.Gravity == 0)
                    {
                        command.Param.Step = Step.On;
                        return;
                    }

                    if (AimHorizonToGps(gc, gps))
                    {
                        if (GetGravityRadius(planetRadius) < Vector3D.Distance(pc.PlanetCenter, gps) &&
                            VectorHelper.IsWithinAngle(pc.PlanetCenter, gc.Controller.GetPosition(), gps, 40))
                        {
                            command.Param.Step = Step.Orbit;
                            return;
                        }
                        command.Param.Step = Step.Cruise;
                    }
                    break;

                case Step.Cruise:
                    if (sb.GpsToggle && pc.DistanceToGPS < reach)
                    {
                        AbortShipContext(gc);
                        if (pc.Gravity > 0)
                        {
                            command.State = MainState.Land;
                            sb.GpsToggle = false;
                            previousRate = PREV_RATE;
                        }
                        return;
                    }

                    if (pc.GroundLevel < ic.SafeAltitude)
                    {
                        SoftAbort(gc);
                        command.Param.Step = Step.Preclimb;
                        return;
                    }
                    if (pc.Gravity > 0 && ForwardTerrainClose(gc, ic.CruiseSpeed))
                    {
                        SoftAbort(gc);
                        command.Param.Step = Step.Preclimb;
                        return;
                    }
                    AimHorizonToGps(gc, command.Param.TargetCoordinates);
                    CruiseControl(ic.CruiseSpeed, timeSinceLastRun);
                    break;

                case Step.Orbit:
                    if (sb.GpsToggle && pc.DistanceToGPS < reach)
                    {
                        SoftAbort(gc);
                        return;
                    }
                    if (pc.Gravity == 0)
                    {
                        gc.ResetThrusters(gc.ForwardThrusters);
                        command.State = MainState.Gps;
                        command.Param.Step = Step.On;
                        return;
                    }
                    if (ForwardTerrainClose(gc, ic.CruiseSpeed))
                        ArmClimbBoost();
                    Climb(gc, ic.CruiseSpeed);
                    break;

                case Step.Off:
                    AbortShipContext(gc);
                    break;

                case Step.Preclimb:
                    if (GravityAlignedOverride(gc, pc.ForwardVelocity == 0))
                        BeginClimbOrLift(gc, command);
                    break;

                case Step.Lift:
                    LiftClearance(gc, command);
                    break;

                case Step.Climb:
                    if (pc.Gravity > 0 && ForwardTerrainClose(gc, ic.CruiseSpeed))
                        ArmClimbBoost();
                    if (pc.GroundLevel > ic.SafeAltitude && !climbBoost && !rayBlocksLevel)
                    {
                        gc.ResetThrusters(gc.ForwardThrusters);
                        command.State = MainState.Gps;
                        command.Param.Step = Step.On;
                        ClearClimbBoost(gc);
                        return;
                    }
                    Climb(gc, ic.CruiseSpeed);
                    break;
            }
        }

        const double ClearanceLiftRate = 8;
        const double RayLookCap = 2000;
        const double RayMinCast = 20;
        int lastRayTick = -10;
        int rayCacheTick = -1;
        bool rayDidScan;
        bool rayHitClose;
        bool rayBlocksLevel;
        int lastDownTick = -10;
        bool downHave;
        Vector3D downPoint;
        IMyCameraBlock downCam;

        double ThreatRange(double cruiseSpeed)
        {
            double threat = pc.StopZDist * 1.5;
            double bySpeed = cruiseSpeed * 4;
            if (bySpeed > threat)
                threat = bySpeed;
            if (threat > RayLookCap || double.IsInfinity(threat) || double.IsNaN(threat))
                threat = RayLookCap;
            if (threat < 50)
                threat = 50;
            return threat;
        }

        bool ForwardTerrainClose(GridContext grid, double cruiseSpeed)
        {
            if (rayCacheTick == tick)
                return rayHitClose;

            rayCacheTick = tick;
            rayDidScan = false;
            rayHitClose = false;

            if (pc == null || pc.Gravity <= 0 || grid == null || grid.Controller == null)
                return false;
            if (command.Param.Step == Step.Lift || command.Param.Step == Step.Preclimb)
                return false;

            IMyCameraBlock cam = grid.RayCamera;
            if (cam == null || cam.Closed)
                return false;

            if (!cam.Enabled)
                cam.Enabled = true;
            // Leave raycast enabled so the camera keeps charging between casts.
            cam.EnableRaycast = true;

            if (tick - lastRayTick < 10)
            {
                rayHitClose = rayBlocksLevel;
                return rayHitClose;
            }

            double threat = ThreatRange(cruiseSpeed);
            double charged = cam.AvailableScanRange;
            double cast = charged < threat ? charged : threat;
            if (cast < RayMinCast || !cam.CanScan(cast))
            {
                rayHitClose = rayBlocksLevel;
                return rayHitClose;
            }

            lastRayTick = tick;
            rayDidScan = true;
            MyDetectedEntityInfo hit = cam.Raycast(cast);

            if (hit.IsEmpty() || hit.Type == MyDetectedEntityType.None || !hit.HitPosition.HasValue)
            {
                rayBlocksLevel = false;
                return false;
            }
            if (grid.IsOwnGrid(hit.EntityId))
            {
                rayBlocksLevel = false;
                return false;
            }

            double distance = Vector3D.Distance(grid.Controller.GetPosition(), hit.HitPosition.Value);
            rayHitClose = distance < threat;
            rayBlocksLevel = rayHitClose;
            return rayHitClose;
        }

        void DisarmForwardRay(GridContext grid)
        {
            if (grid == null)
                return;
            IMyCameraBlock cam = grid.RayCamera;
            if (cam == null || cam.Closed)
                return;
            cam.EnableRaycast = false;
        }

        void BeginClimbOrLift(GridContext gc, Command command)
        {
            command.Param.Step = NeedsTailClearance(gc) ? Step.Lift : Step.Climb;
        }

        bool NeedsTailClearance(GridContext gc)
        {
            if (pc == null || pc.Gravity <= 0)
                return false;
            if (pc.GroundLevel >= ic.SafeAltitude)
                return false;

            double thetaDeg = pc.GetMaxPitchAngle(gc);
            if (thetaDeg < 1.0)
                return false;

            return pc.GroundLevel < TailClearance(gc, thetaDeg);
        }

        double TailClearance(GridContext gc, double thetaDeg)
        {
            double theta = thetaDeg * (Math.PI / 180.0);
            double pad = gc.IsLG ? 2.5 : 0.5;
            double sweep = gc.GridLength * 0.5 * Math.Sin(theta);
            return gc.GridHeight + sweep + pad;
        }

        void LiftClearance(GridContext gc, Command command)
        {
            // Stay level until the tail can swing through the pitch Climb is about to use.
            GravityAlignedOverride(gc);
            gc.ResetThrusters(gc.ForwardThrusters);
            VectorHelper.MatchVerticalSpeed(gc, pc, ClearanceLiftRate, false);
            if (!NeedsTailClearance(gc))
                command.Param.Step = Step.Climb;
        }

        bool climbBoost;
        int climbDropStreak;
        int climbRiseStreak;
        int climbSampleTick = -1;
        bool climbReleasedThisTick;

        private void Climb(GridContext gc, double CruiseSpeed)
        {
            bool inClimb = command.Param.Step == Step.Climb || command.Param.Step == Step.Orbit;
            if (!inClimb)
            {
                climbReleasedThisTick = false;
                if (climbBoost || climbDropStreak != 0 || climbRiseStreak != 0)
                    ClearClimbBoost(gc);
            }
            else
                SampleClimbBoost(gc, CruiseSpeed);

            VectorAlignedOverride(gc, gc.Controller.WorldMatrix.Up, false, pc.DesiredUpVector);

            if (inClimb && climbBoost)
            {
                BoostClimb(gc);
                return;
            }

            // The tick that ends the boost clears the up override. Cruise starts on the next climb tick.
            if (climbReleasedThisTick)
                return;

            CruiseControl(CruiseSpeed, timeSinceLastRun);
        }

        void ArmClimbBoost()
        {
            climbBoost = true;
            climbDropStreak = 0;
            climbRiseStreak = 0;
        }

        void SampleClimbBoost(GridContext grid, double cruiseSpeed)
        {
            if (climbSampleTick == tick)
                return;

            climbSampleTick = tick;
            climbReleasedThisTick = false;

            if (climbBoost)
            {
                ForwardTerrainClose(grid, cruiseSpeed);
                if (rayDidScan)
                {
                    if (rayHitClose || pc.ClimbRate <= 1)
                        climbRiseStreak = 0;
                    else
                        climbRiseStreak++;

                    if (climbRiseStreak >= 2)
                    {
                        ClearClimbBoost(grid);
                        climbReleasedThisTick = true;
                    }
                }
                return;
            }

            bool dropping = pc.Gravity > 0 && pc.ClimbRate < -1 && pc.ForwardVelocity > 5;
            if (dropping)
                climbDropStreak++;
            else
                climbDropStreak = 0;

            if (climbDropStreak >= 5)
                ArmClimbBoost();
        }

        void BoostClimb(GridContext grid)
        {
            for (int i = 0; i < grid.UpwardThrusters.Count; i++)
            {
                IMyThrust up = grid.UpwardThrusters[i];
                if (up == null || up.Closed)
                    continue;
                up.Enabled = true;
                up.ThrustOverridePercentage = 1f;
            }

            currentOverride = 0;
            grid.ResetThrusters(grid.ForwardThrusters);
        }

        void ClearClimbBoost(GridContext grid)
        {
            bool wasBoost = climbBoost;
            climbBoost = false;
            climbDropStreak = 0;
            climbRiseStreak = 0;
            if (wasBoost && grid != null)
                grid.ResetThrusters(grid.UpwardThrusters);
        }

        private void ToggleCommand(GridContext gc, Command command)
        {
            SoftAbort(gc);
            sb.SetActiveMode(command.State);
            if (sb.GetModeState(command.State)) command.Param.Step = Step.On;
            else command.Param.Step = Step.Off;
        }

        void AutoLandStateSwitch(GridContext gc, Command command)
        {

            switch (command.Param.Step)
            {
                case Step.Toggle:
                    ToggleCommand(gc, command);
                    break;
                case Step.On:
                    AutoLandSwitch(gc, command);
                    break;
                case Step.Off:
                    AbortShipContext(gc);
                    break;
            }
        }

        private void AutoLandSwitch(GridContext gc, Command command)
        {
            switch (command.Param.AutoLandState)
            {
                case AutoLandState.Align:
                    SoftAbort(gc);
                    if (GravityAlignedOverride(gc, true)) command.Param.AutoLandState = AutoLandState.Drop;
                    break;

                case AutoLandState.Drop:
                    if (AutoLand(gc, pc)) command.Param.AutoLandState = AutoLandState.LockGear;
                    break;

                case AutoLandState.LockGear:
                    HandleLockGear(gc);
                    break;
            }
        }

        private void ReloadGridContext(GridContext gc, IniContext ic)
        {
            gc.Setup(ic);
            // Setup turns thrust on for every cockpit. The occupied seat is the menu keyboard.
            // The remote in gc.Controller keeps its thrust.
            if (menuToggle || menuSeatHold)
                HoldMenuSeat();
            SyncDockTriggerGears();
            SyncDockConnectors();
            RememberSyncedGroupIds();

            gc.ReloadLCDs()
                .ReloadH2Tanks()
                .ReloadBatteries();

            if (gc.ErrorMessage.Length > 0)
            {
                knownGridStatic = GridIsStatic();
                return;
            }

            pc = new PhysicsContext(gc, stt, timeSinceLastRun);

            // Flight cached blocks. A static grid skips this even when low-fuel land is on.
            if (!GridIsStatic() && (ic.AllowFlightSystems || ic.AllowLowFuelLand))
            {
                SoftAbort(gc);

                sb.LastCheckIsOnNatGrav = pc.Gravity > 0;
            }

            // Dock cached blocks. Tag sync above already ran with Dock Mode off.
            if (ic.AllowDockMode)
                gc.ReloadConnectors()
                    .ReloadTanks()
                    .ReloadControlledBlocks(ic.DockGroupTag, ic.OverrideBlockTag);

            if (ic.RenameSubgrids) RenameSubgrids.GetSubgridsAndRename(gc.GridTS, gc.Me.CubeGrid);

            if (ic.PaintSurfaces)
                gc.PaintAllScreens(ic);

            knownGridStatic = GridIsStatic();
        }

        double currentOverride = 0.0;   // 0..1 forward thrust command
        double currentBrake = 0.0;      // 0..1 braking command
        double integral = 0.0;
        double lastError = 0.0;

        // tuning
        const double Kp = 0.4;
        const double Ki = 0.03;
        const double Kd = 0.5;

        const double SPEED_TOLERANCE = 0.25;   // deadzone while cruising
        double OVERRIDE_STEP = 0.01;                  // max absolute change per tick (smoothness)
        const double MAX_INTEGRAL = 1.0;       // anti-windup clamp

        void CruiseControl(double cruiseSpeed, double dt)
        {
            if (pc.ForwardVelocity >= ic.CruiseSpeed)
            {
                currentOverride = 0;
                currentBrake = 0;
                integral = 0;
                lastError = 0;
                gc.ResetThrusters(gc.ForwardThrusters);
                gc.ResetThrusters(gc.BreakingThrusters);
                return;
            }

            // error: positive => need more forward thrust
            double error = cruiseSpeed - pc.ForwardVelocity;

            // small deadzone: don't integrate or react strongly inside it
            if (Math.Abs(error) < SPEED_TOLERANCE)
            {
                // gently decay integral to avoid wind-up and reduce chatter
                integral *= 0.9;
                lastError = error;
                return;
            }

            // PID terms
            integral += error * dt;
            integral = Math.Max(-MAX_INTEGRAL, Math.Min(MAX_INTEGRAL, integral));
            double derivative = (error - lastError) / dt;

            double pid = Kp * error + Ki * integral + Kd * derivative;

            // map pid to desired forward/brake targets (complementary)
            double desiredForward = Math.Max(0.0, Math.Min(1.0, pid));   // if pid>0 -> forward
            double desiredBrake = Math.Max(0.0, Math.Min(1.0, -pid));  // if pid<0 -> brake

            // step limiter per tick to keep smooth visuals (OVERRIDE_STEP controls smoothness)
            // The step is applied independently to forward and brake, but we keep them complementary.
            double step = OVERRIDE_STEP * tickSplit; // fixed per tick step (tune for desired smoothness)

            // move currentOverride toward desiredForward by at most step
            double diffF = desiredForward - currentOverride;
            if (diffF > step) diffF = step;
            else if (diffF < -step) diffF = -step;
            currentOverride += diffF;

            // move currentBrake toward desiredBrake by at most step
            double diffB = desiredBrake - currentBrake;
            if (diffB > step) diffB = step;
            else if (diffB < -step) diffB = -step;
            currentBrake += diffB;

            // Prevent both fighting: if both non-zero, reduce them proportionally so they don't sum >1
            if (currentOverride > 0 && currentBrake > 0)
            {
                double sum = currentOverride + currentBrake;
                if (sum > 1.0)
                {
                    currentOverride /= sum;
                    currentBrake /= sum;
                }
            }

            if (ic.MaxSpeed != double.PositiveInfinity && cruiseSpeed < ic.MaxSpeed)
            {
                // Apply thrusters: enable brake thrusters only when brake significant
                bool useBrakes = currentBrake > 1e-4;

                foreach (var bt in gc.BreakingThrusters)
                {
                    bt.Enabled = useBrakes;
                    bt.ThrustOverridePercentage = (float)currentBrake;
                }
            }
            else gc.KillThrusters(gc.BreakingThrusters);

            // Apply forward thrusters
            bool useForward = currentOverride > 1e-4;
            foreach (var ft in gc.ForwardThrusters)
            {
                ft.Enabled = useForward;
                ft.ThrustOverridePercentage = (float)currentOverride;
            }

            lastError = error;
        }


        private void LockInput()
        {
            menuIsLocked = true;
            inputLock = 0;
        }

        const int HoldW = 1;
        const int HoldA = 2;
        const int HoldS = 4;
        const int HoldD = 8;
        const int HoldQ = 16;
        const int HoldE = 32;
        const int HoldC = 64;
        const int HoldSpace = 128;

        void HoldMenuSeat()
        {
            pi.HoldSeat(gc.Controllers);
        }

        void CloseMenuFromC()
        {
            menuHud.ReleaseScreenMarker();
            menuToggle = false;
            BeginSeatHold(HoldC);
        }

        void CloseMenuFromSpace()
        {
            menuHud.ReleaseScreenMarker();
            menuToggle = false;
            BeginSeatHold(HoldSpace);
        }

        // The other cockpit stays main while the exit key is down. It is cleared when that key is up.
        void BeginSeatHold(int exitKey)
        {
            if ((ReadHeldKeys() & exitKey) != 0)
            {
                menuSeatHold = true;
                seatHoldKeys = exitKey;
                HoldMenuSeat();
                return;
            }

            menuSeatHold = false;
            seatHoldKeys = 0;
            pi.ResetControllers(gc.Controllers);
        }

        void ReleaseMenuSeatHold()
        {
            if (!menuSeatHold)
                return;
            if ((ReadHeldKeys() & seatHoldKeys) != 0)
            {
                HoldMenuSeat();
                return;
            }

            menuSeatHold = false;
            seatHoldKeys = 0;
            pi.ResetControllers(gc.Controllers);
        }

        int ReadHeldKeys()
        {
            int keys = 0;
            if (pi.W()) keys |= HoldW;
            if (pi.A()) keys |= HoldA;
            if (pi.S()) keys |= HoldS;
            if (pi.D()) keys |= HoldD;
            if (pi.Q()) keys |= HoldQ;
            if (pi.E()) keys |= HoldE;
            if (pi.C()) keys |= HoldC;
            if (pi.Space()) keys |= HoldSpace;
            return keys;
        }

        bool ControlHeld()
        {
            return ReadHeldKeys() != 0;
        }

        void AbortShipContext(GridContext gc)
        {
            sb = new SystemBools();

            command.Empty();
            previousRate = PREV_RATE;

            SoftAbort(gc);
        }

        void SoftAbort(GridContext gc)
        {
            currentOverride = 0;
            currentBrake = 0;
            integral = 0;
            lastError = 0;
            gearLockNote = null;
            haveFall = false;
            ClearClimbBoost(gc);

            if (gc.Controller != null)
                gc.Controller.DampenersOverride = true;
            sb.StopCruiseWhenOutOfGrav = false;

            gc.ResetGyros();
            gc.ResetThrusters(gc.Thrusters);
            DisarmForwardRay(gc);
            DisarmDown();
            if (pc != null)
                pc.UnlockClimbPitch();
        }

        ////////////////////////////////////////////////////////
        /// FLIGHT
        ////////////////////////////////////////////////////////

        bool GravityAlignedOverride(GridContext gc)
        {
            return GravityAlignedOverride(gc, false);
        }

        bool GravityAlignedOverride(GridContext gc, bool checkSpeed)
        {
            Vector3D shipUp = gc.Controller.WorldMatrix.Up;

            return VectorAlignedOverride(gc, shipUp, checkSpeed, Vector3D.Normalize(pc.NaturalGravity));
        }

        double PREV_RATE = 0.5;
        double previousRate;

        bool VectorAlignedOverride(GridContext gc, Vector3D shipUp, bool checkSpeed, Vector3D desiredUpVector)
        {
            if (previousRate == 0) previousRate = PREV_RATE;

            Vector3D axis = shipUp.Cross(desiredUpVector);
            double angle = axis.Length(); double maxRate; double RESPONSE;
            double angleThreshold;

            if (pc.Gravity > 0)
            {
                angleThreshold = 0.005;
                maxRate = 1;
                RESPONSE = 1;
            }
            else
            {
                angleThreshold = 1;
                maxRate = PREV_RATE * (1.0 - Math.Exp(-angle / 40));
                maxRate = Math.Min(maxRate, previousRate);
                RESPONSE = 0.05;
            }

            if (angle < angleThreshold && (!checkSpeed || pc.IsStopped))
            {
                gc.ResetGyros();
                return true;
            }

            previousRate = maxRate;

            axis /= angle;

            Vector3D angVel = gc.Controller.GetShipVelocities().AngularVelocity;

            //-----------------------------------
            // ⭐ ANGULAR RATE LIMIT
            //-----------------------------------

            Vector3D desiredRate = axis * Math.Min(angle * RESPONSE, maxRate);

            //-----------------------------------
            // PD ShipContext.Controller on angular velocity
            //-----------------------------------

            Vector3D correction = desiredRate - angVel;
            //-----------------------------------
            foreach (var g in gc.Gyros)
            {
                MatrixD inv = MatrixD.Transpose(g.WorldMatrix);
                Vector3D local = Vector3D.TransformNormal(correction, inv);

                g.GyroOverride = true;

                g.Pitch = (float)MathHelper.Clamp(local.X / 2, -3, 3);
                g.Yaw = (float)MathHelper.Clamp(local.Y / 2, -3, 3);
                g.Roll = (float)MathHelper.Clamp(local.Z / 2, -3, 3);
            }

            return false;
        }

        bool AimHorizonToGps(GridContext gc, Vector3D targetGps)
        {
            Vector3D gDown = Vector3D.Normalize(pc.NaturalGravity);
            Vector3D shipUp = gc.Controller.WorldMatrix.Up;
            Vector3D shipFwd = gc.Controller.WorldMatrix.Forward;

            Vector3D levelAxis = shipUp.Cross(gDown);
            double levelErr = levelAxis.Length();

            Vector3D toTarget = targetGps - gc.Controller.GetPosition();
            Vector3D targetHoriz = toTarget - gDown * toTarget.Dot(gDown);
            Vector3D fwdHoriz = shipFwd - gDown * shipFwd.Dot(gDown);

            double yawErr = 0;
            double yawSign = 0;
            if (targetHoriz.LengthSquared() > 1e-6 && fwdHoriz.LengthSquared() > 1e-6)
            {
                targetHoriz.Normalize();
                fwdHoriz.Normalize();
                double cosA = MathHelper.Clamp(fwdHoriz.Dot(targetHoriz), -1.0, 1.0);
                yawErr = Math.Acos(cosA);
                yawSign = -Math.Sign(fwdHoriz.Cross(targetHoriz).Dot(gDown));
            }

            const double LEVEL_EPS = 0.04; // ~2°
            const double YAW_EPS = 0.08; // ~4.5° — do not use 0.01
            if (levelErr < LEVEL_EPS && yawErr < YAW_EPS)
            {
                gc.ResetGyros();
                return true;
            }

            Vector3D angVel = gc.Controller.GetShipVelocities().AngularVelocity;
            Vector3D desiredRate = Vector3D.Zero;

            if (levelErr > LEVEL_EPS)
            {
                levelAxis /= levelErr;
                desiredRate += levelAxis * Math.Min(levelErr * 0.8, 0.6);
            }

            if (yawErr > YAW_EPS)
            {
                // cap ~20°/s, and bleed off existing yaw rate so it doesn't flip
                double yawRate = Math.Min(yawErr * 0.5, 0.35);
                desiredRate += gDown * (yawSign * yawRate);
            }

            Vector3D correction = desiredRate - 1.8 * angVel; // heavier damp than before

            foreach (var g in gc.Gyros)
            {
                Vector3D local = Vector3D.TransformNormal(correction, MatrixD.Transpose(g.WorldMatrix));
                g.GyroOverride = true;
                g.Pitch = (float)MathHelper.Clamp(local.X / 2, -2, 2);
                g.Yaw = (float)MathHelper.Clamp(local.Y / 2, -2, 2);
                g.Roll = (float)MathHelper.Clamp(local.Z / 2, -2, 2);
            }
            return false;
        }

        double GetGravityRadius(double radius)
        {
            return radius * 1.32;
        }

        ////////////////////////////////////////////////////////
        /// SAFE DEscENT
        ////////////////////////////////////////////////////////
        double SafeFall(double decel, double altitude)
        {
            if (decel <= 0 || altitude <= 0)
                return 0;
            return Math.Sqrt(2.0 * decel * altitude);
        }

        double LandFall(double decel, double altitude)
        {
            double safe = SafeFall(decel, altitude);
            if (safe <= 0)
                return 0;
            double speed = 0.60 * safe;
            if (speed < 2)
                speed = 2;
            if (speed > ic.CruiseSpeed)
                speed = ic.CruiseSpeed;
            if (speed > safe)
                speed = safe;
            return -speed;
        }

        bool FallSettled(GridContext gc, double target)
        {
            return VectorHelper.GetGravityAlignedVerticalVelocity(gc, pc) >= target;
        }

        void ProjectFall(double decel, double altitude, out double projected)
        {
            projected = decel;
            if (haveFall && altitude < fallAlt && decel > fallDecel)
            {
                double drop = fallAlt - altitude;
                if (drop > 1)
                {
                    double rise = (decel - fallDecel) / drop * altitude;
                    if (rise > 0)
                        projected = decel + rise;
                }
            }
            fallAlt = altitude;
            fallDecel = decel;
            haveFall = true;
        }

        bool AutoLand(GridContext gc, PhysicsContext pc)
        {
            double projected;
            ProjectFall(pc.MaxYDecel, SurfaceLeft(gc, pc.GroundLevel), out projected);
            double gate = projected;
            if (gc.ShipType != ShipType.Atmo)
                if (gate - 0.25 < 0)
                {
                    AbortShipContext(gc);
                    this.command.State = MainState.Orbit;
                    return false;
                }
            gc.Controller.DampenersOverride = false;
            GravityAlignedOverride(gc);

            double agl = DescentAgl(gc);
            double above = SurfaceLeft(gc, agl);
            double stop = pc.StopYDist;
            double stopReach = Math.Max(2.0 * gc.GridHeight, 0.5 * stop + gc.GridHeight);
            double target = LandFall(pc.MaxYDecel, above);
            if (above <= SlowToTwo(gc))
                target = -2;
            VectorHelper.MatchVerticalSpeed(gc, pc, target, false, 0.5);
            return agl < stopReach && FallSettled(gc, target);
        }

        // Remote altitude minus the block that touches first. Not written down.
        double SurfaceLeft(GridContext grid, double agl)
        {
            double above = agl - TouchDrop(grid);
            if (above < 0)
                return 0;
            return above;
        }

        double TouchDrop(GridContext grid)
        {
            if (pc == null || pc.Gravity <= 0 || grid == null || grid.Controller == null)
                return 0;
            Vector3D down = Vector3D.Normalize(pc.NaturalGravity);
            Vector3D remote = grid.Controller.GetPosition();
            IMyShipConnector home = IniContext.HomeConnector(grid);
            if (home != null && !home.Closed)
            {
                double drop = (remote - home.GetPosition()).Dot(down);
                if (drop < 0)
                    return 0;
                return drop;
            }

            double lowest = 0;
            for (int i = 0; i < grid.Gears.Count; i++)
            {
                IMyLandingGear gear = grid.Gears[i];
                if (gear == null || gear.Closed)
                    continue;
                double drop = (remote - gear.GetPosition()).Dot(down);
                if (drop > lowest)
                    lowest = drop;
            }
            return lowest;
        }

        double SlowToTwo(GridContext grid)
        {
            double early = 0.25 * grid.GridHeight;
            double speed = -pc.ClimbRate;
            if (speed <= 2)
                return early;
            double net = pc.NetDecel;
            if (double.IsNaN(net) || net <= 1e-6)
                return double.PositiveInfinity;
            double need = (speed * speed - 4.0) / (2.0 * net);
            if (need > early)
                return need;
            return early;
        }

        // Land and the GPS handoff into Land. No camera leaves planet altitude.
        double DescentAgl(GridContext grid)
        {
            double planet = pc.GroundLevel;
            if (pc == null || pc.Gravity <= 0 || grid == null || grid.Controller == null)
                return planet;

            IMyCameraBlock cam = grid.FindDownCamera(ic.RayCameraTag, pc.NaturalGravity);
            if (cam == null)
            {
                DisarmDown();
                return planet;
            }

            if (downCam != null && downCam != cam)
                DisarmDown();
            downCam = cam;
            cam.EnableRaycast = true;

            if (tick - lastDownTick >= 10)
            {
                double reach = cam.AvailableScanRange;
                double cast = reach < planet ? reach : planet;
                if (cast >= 1 && cam.CanScan(cast))
                {
                    lastDownTick = tick;
                    ReadDownHit(grid, cam.Raycast(cast), planet);
                }
            }

            if (!downHave)
                return planet;

            double agl = (downPoint - grid.Controller.GetPosition()).Dot(Vector3D.Normalize(pc.NaturalGravity));
            if (agl <= 0 || agl >= planet)
            {
                downHave = false;
                return planet;
            }
            return agl;
        }

        void ReadDownHit(GridContext grid, MyDetectedEntityInfo hit, double planet)
        {
            if (hit.IsEmpty() || hit.Type == MyDetectedEntityType.None || !hit.HitPosition.HasValue)
                return;
            if (hit.Type == MyDetectedEntityType.Planet || hit.Type == MyDetectedEntityType.Asteroid)
                return;
            if (hit.Type != MyDetectedEntityType.LargeGrid && hit.Type != MyDetectedEntityType.SmallGrid)
                return;
            if (grid.IsOwnGrid(hit.EntityId))
                return;

            Vector3D point = hit.HitPosition.Value;
            double agl = (point - grid.Controller.GetPosition()).Dot(Vector3D.Normalize(pc.NaturalGravity));
            if (agl <= 0 || agl >= planet)
                return;
            downHave = true;
            downPoint = point;
        }

        void DisarmDown()
        {
            downHave = false;
            lastDownTick = -10;
            if (downCam == null || downCam.Closed)
            {
                downCam = null;
                return;
            }
            downCam.EnableRaycast = false;
            downCam = null;
        }

        string gearLockNote;

        void SyncDockTriggerGears()
        {
            if (gc == null || gc.GridTS == null || ic == null)
                return;

            string current = ic.DockTriggerTag ?? "";
            string pad = ic.LandingPadTag ?? "";

            // Group membership is not written onto the gear. A menu tag stays when the gear leaves the group.
            gc.GetSameConstructBlocks(constructGears, "");
            for (int i = 0; i < constructGears.Count; i++)
            {
                IMyLandingGear gear = constructGears[i];
                if (gear == null || gear.Closed)
                    continue;
            }
        }

        // Previous tag is still the Restore value. ReloadGridContext writes Restore once after gears and connectors.
        void SyncDockConnectors()
        {
            if (gc == null || gc.GridTS == null || ic == null)
                return;

            string current = ic.DockTriggerTag ?? "";
            string pad = ic.LandingPadTag ?? "";;

            FillConnectorGroup(dockTriggerConnectors, ic.DockTriggerGroup);
            FillConnectorGroup(landingPadConnectors, ic.LandingPadGroup);
            gc.GetSameConstructBlocks(constructConnectors, "");

            for (int i = 0; i < constructConnectors.Count; i++)
            {
                IMyShipConnector connector = constructConnectors[i];
                if (connector == null || connector.Closed)
                    continue;
                StaticConnectorStamp.StripLegacyLines(connector);
            }

            WriteOwnConnectorStatic();
        }

        // Group ids only. Custom data is read later, and only when a list changed.
        void SyncDockGroupsIfChanged()
        {
            if (gc == null || gc.GridTS == null || ic == null)
                return;

            FillGearGroup(dockTriggerGroup, ic.DockTriggerGroup);
            FillConnectorGroup(dockTriggerConnectors, ic.DockTriggerGroup);
            FillConnectorGroup(landingPadConnectors, ic.LandingPadGroup);
            CopyBlockIds(sampleTriggerGearIds, dockTriggerGroup);
            CopyBlockIds(sampleTriggerConnectorIds, dockTriggerConnectors);
            CopyBlockIds(samplePadConnectorIds, landingPadConnectors);

            if (SameIds(lastTriggerGearIds, sampleTriggerGearIds)
                && SameIds(lastTriggerConnectorIds, sampleTriggerConnectorIds)
                && SameIds(lastPadConnectorIds, samplePadConnectorIds))
                return;

            SyncDockTriggerGears();
            SyncDockConnectors();
            RememberSyncedGroupIds();
        }

        void RememberSyncedGroupIds()
        {
            CopyBlockIds(lastTriggerGearIds, dockTriggerGroup);
            CopyBlockIds(lastTriggerConnectorIds, dockTriggerConnectors);
            CopyBlockIds(lastPadConnectorIds, landingPadConnectors);
        }

        void FillGearGroup(List<IMyLandingGear> dest, string groupName)
        {
            dest.Clear();
            if (string.IsNullOrEmpty(groupName) || gc.GridTS == null)
                return;
            IMyBlockGroup group = gc.GridTS.GetBlockGroupWithName(groupName);
            if (group == null)
                return;
            group.GetBlocksOfType(dest);
        }

        static void CopyBlockIds<T>(List<long> dest, List<T> blocks) where T : class, IMyTerminalBlock
        {
            dest.Clear();
            for (int i = 0; i < blocks.Count; i++)
            {
                T block = blocks[i];
                if (block == null || block.Closed)
                    continue;
                dest.Add(block.EntityId);
            }
        }

        static bool SameIds(List<long> last, List<long> current)
        {
            if (last.Count != current.Count)
                return false;
            for (int i = 0; i < current.Count; i++)
            {
                long id = current[i];
                bool found = false;
                for (int j = 0; j < last.Count; j++)
                {
                    if (last[j] == id)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    return false;
            }
            return true;
        }

        void FillConnectorGroup(List<IMyShipConnector> dest, string groupName)
        {
            dest.Clear();
            if (string.IsNullOrEmpty(groupName))
                return;
            IMyBlockGroup group = gc.GridTS.GetBlockGroupWithName(groupName);
            if (group == null)
                return;
            group.GetBlocksOfType(dest);
        }

        static bool ConnectorInList(List<IMyShipConnector> list, long entityId)
        {
            for (int i = 0; i < list.Count; i++)
            {
                IMyShipConnector member = list[i];
                if (member != null && !member.Closed && member.EntityId == entityId)
                    return true;
            }
            return false;
        }

        // Own grid only. A rotor or hinge arm is not this grid, so it keeps whatever the player wrote.
        void WriteOwnConnectorStatic()
        {
            if (gc.Me == null || gc.Me.CubeGrid == null || gc.Me.CubeGrid.Closed)
                return;

            bool on = gc.Me.CubeGrid.IsStatic;
            gc.GetOwnGridBlocks(ownGridConnectors, "");
            for (int i = 0; i < ownGridConnectors.Count; i++)
            {
                IMyShipConnector connector = ownGridConnectors[i];
                if (connector == null || connector.Closed)
                    continue;
                StaticConnectorStamp.WriteStatic(connector, on);
            }
        }

        bool GridIsStatic()
        {
            return gc != null && gc.Me != null && gc.Me.CubeGrid != null && !gc.Me.CubeGrid.Closed && gc.Me.CubeGrid.IsStatic;
        }

        static void RetagExactLine(IMyTerminalBlock block, string previous, string current)
        {
            if (block == null || block.Closed || string.IsNullOrEmpty(previous) || previous == current)
                return;
            string data = block.CustomData ?? "";
            if (!StaticConnectorStamp.HasExactLine(data, previous))
                return;
            RemoveExactLine(block, previous);
            if (!string.IsNullOrEmpty(current))
                AppendExactLine(block, current);
        }

        static void AppendExactLine(IMyTerminalBlock block, string line)
        {
            string data = block.CustomData ?? "";
            if (StaticConnectorStamp.HasExactLine(data, line))
                return;
            block.CustomData = StaticConnectorStamp.AppendExactLine(data, line);
        }

        static void RemoveExactLine(IMyTerminalBlock block, string line)
        {
            if (string.IsNullOrEmpty(line))
                return;
            string data = block.CustomData ?? "";
            if (!StaticConnectorStamp.HasExactLine(data, line))
                return;
            string stripped = data;
            while (StaticConnectorStamp.HasExactLine(stripped, line))
                stripped = StaticConnectorStamp.RemoveExactLine(stripped, line);
            if (stripped != data)
                block.CustomData = stripped;
        }

        bool GearIgnored(IMyLandingGear gear)
        {
            string tag = ic.IgnoreTag;
            if (string.IsNullOrEmpty(tag))
                return false;
            if (gear.CustomName != null && gear.CustomName.Contains(tag))
                return true;
            string data = gear.CustomData;
            return data != null && data.Contains(tag);
        }

        // A locked gear counts when it is in the Dock trigger group, or it is not in that group and still has the tag.
        // A static grid does not auto-dock from its own gears.
        bool GearDocking()
        {
            if (ic == null || !ic.AllowDockMode || gc == null || GridIsStatic())
                return false;

            string line = ic.DockTriggerTag ?? "";
            FillGearGroup(dockTriggerGroup, ic.DockTriggerGroup);
            for (int i = 0; i < gc.Gears.Count; i++)
            {
                IMyLandingGear gear = gc.Gears[i];
                if (gear == null || gear.Closed || !gear.IsLocked || GearIgnored(gear))
                    continue;
                if (GearInTriggerGroup(gear.EntityId))
                    return true;
                if (line.Length > 0 && StaticConnectorStamp.HasExactLine(gear.CustomData, line))
                    return true;
            }
            return false;
        }

        bool GearInTriggerGroup(long entityId)
        {
            for (int g = 0; g < dockTriggerGroup.Count; g++)
            {
                IMyLandingGear member = dockTriggerGroup[g];
                if (member != null && !member.Closed && member.EntityId == entityId)
                    return true;
            }
            return false;
        }

        // Own connector is a trigger by group or by tag. A pad group wins over the trigger group.
        // The other connector is a pad by its group, its tag, or Static=true.
        bool ConnectorSafeToDock()
        {
            if (gc == null || ic == null)
                return false;

            string trigger = ic.DockTriggerTag ?? "";
            string pad = ic.LandingPadTag ?? "";
            FillConnectorGroup(dockTriggerConnectors, ic.DockTriggerGroup);
            FillConnectorGroup(landingPadConnectors, ic.LandingPadGroup);
            for (int i = 0; i < gc.Connectors.Count; i++)
            {
                IMyShipConnector connector = gc.Connectors[i];
                if (connector == null || connector.Closed)
                    continue;
                if (connector.Status != MyShipConnectorStatus.Connected)
                    continue;
                if (ConnectorIsTrigger(connector, trigger, pad))
                    return true;

                IMyShipConnector other = connector.OtherConnector;
                if (other == null || other.Closed)
                    continue;
                if (ConnectorIsPad(other, pad))
                    return true;
            }
            return false;
        }

        bool ConnectorIsTrigger(IMyShipConnector connector, string trigger, string pad)
        {
            bool inPad = ConnectorInList(landingPadConnectors, connector.EntityId);
            bool inTrigger = ConnectorInList(dockTriggerConnectors, connector.EntityId);
            if (inPad)
                return false;
            if (inTrigger)
                return true;
            if (pad.Length > 0 && StaticConnectorStamp.HasExactLine(connector.CustomData, pad))
                return false;
            return trigger.Length > 0 && StaticConnectorStamp.HasExactLine(connector.CustomData, trigger);
        }

        bool ConnectorIsPad(IMyShipConnector connector, string pad)
        {
            if (ConnectorInList(landingPadConnectors, connector.EntityId))
                return true;
            string data = connector.CustomData;
            if (pad.Length > 0 && StaticConnectorStamp.HasExactLine(data, pad))
                return true;
            return StaticConnectorStamp.ReadsStatic(data);
        }

        void HandleLockGear(GridContext gc)
        {
            if (!GearHolding(gc) && pc.GroundLevel > 1.5 * pc.StopZDist)
            {
                command.Param.AutoLandState = AutoLandState.Drop;
                return;
            }

            if (TryLock(gc))
                AbortShipContext(gc);
        }

        bool GearHolding(GridContext gc)
        {
            for (int i = 0; i < gc.Gears.Count; i++)
            {
                IMyLandingGear g = gc.Gears[i];
                if (g == null || g.Closed)
                    continue;
                if (g.IsLocked || g.LockMode == LandingGearMode.ReadyToLock)
                    return true;
            }
            return false;
        }

        bool TryLock(GridContext gc)
        {
            GravityAlignedOverride(gc);
            if (gc.Controller != null)
                gc.Controller.DampenersOverride = true;

            if (gc.Gears.Count == 0)
            {
                // No gear to grab. Dampeners only — do not keep the -2 descent.
                gearLockNote = "No landing gear";
                return false;
            }

            gearLockNote = null;
            VectorHelper.MatchVerticalSpeed(gc, pc, -2, true);

            bool grabbed = false;
            for (int i = 0; i < gc.Gears.Count; i++)
            {
                IMyLandingGear g = gc.Gears[i];
                if (g == null || g.Closed)
                    continue;

                g.Enabled = true;
                g.AutoLock = true;
                bool ready = g.LockMode == LandingGearMode.ReadyToLock;
                g.Lock();
                if (g.IsLocked || ready || g.LockMode == LandingGearMode.ReadyToLock)
                    grabbed = true;
            }

            return grabbed;
        }
    }
}
