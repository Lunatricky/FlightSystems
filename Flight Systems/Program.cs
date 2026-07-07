using IngameScript.Domain;
using IngameScript.Enums;
using IngameScript.Physics;
using IngameScript.UseCases;
using IngameScript.Utils;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game.ModAPI.Ingame;
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

        Command command = Command.Empty;

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
        bool isGearlocked;
        bool settingsToggle;
        bool settingsIsLocked;

        Vector3D desiredUp;

        SystemBools sb;

        struct SystemBools
        {
            public bool CruiseToggle;
            public bool OrbitToggle;
            public bool GlideToggle;
            public bool CNavToggle;
            public bool LandToggle;
            public bool SBurnToggle;
            public bool GpsToggle;
            public bool LastCheckIsOnNatGrav;
            public bool StopCruiseWhenOutOfGrav;

            public void SetActiveMode(MainState modeName)
            {
                // Get current state of the target mode
                bool currentState = GetModeState(modeName);

                // Clear all modes
                CruiseToggle = false;
                OrbitToggle = false;
                GlideToggle = false;
                CNavToggle = false;
                LandToggle = false;
                SBurnToggle = false;
                GpsToggle = false;

                // Toggle the target mode (if it was true, now false; if false, now true)
                SetModeState(modeName, !currentState);
            }

            private bool GetModeState(MainState modeName)
            {
                switch (modeName)
                {
                    case MainState.Cruise: return CruiseToggle;
                    case MainState.Orbit: return OrbitToggle;
                    case MainState.Glide: return GlideToggle;
                    case MainState.CNav: return CNavToggle;
                    case MainState.Land: return LandToggle;
                    case MainState.SBurn: return SBurnToggle;
                    case MainState.Gps: return GpsToggle;
                    default: return false;
                }
            }

            private void SetModeState(MainState modeName, bool value)
            {
                switch (modeName)
                {
                    case MainState.Cruise: CruiseToggle = value; break;
                    case MainState.Orbit: OrbitToggle = value; break;
                    case MainState.Glide: GlideToggle = value; break;
                    case MainState.CNav: CNavToggle = value; break;
                    case MainState.Land: LandToggle = value; break;
                    case MainState.SBurn: SBurnToggle = value; break;
                    case MainState.Gps: GpsToggle = value; break;
                }
            }
        }
                
        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;

            gc = new GridContext(GridTerminalSystem, Me);
            ic = new IniContext(gc);
            sb = new SystemBools();
            stt = new SpeedTimeTracker();
            pi = new PlayerInput(gc.Controllers);

            CheckIni();

            if (gc.LcdsSettings.Count > 0) FlightSystemSection();
        }

        string task;
        string maxTask;

        public void Main(string argument)
        {
            if (!string.IsNullOrEmpty(argument))
            {
                if (argument.ToLowerInvariant() == "settings" && (gc.Cockpits.Count > 1 || isDocked))
                {
                    settingsToggle = !settingsToggle;

                    if (settingsToggle)
                    {
                        pi.OcupiedController(gc.Controllers);
                        pi.PrepareController();
                    }
                    else pi.ResetControllers(gc.Controllers);
                }
                else command = new Command(argument);
            }

            inputLock++;
            if (inputLock > 25)
            {
                inputLock = 0;
                settingsIsLocked = false;
            }

            if (settingsToggle && !settingsIsLocked && gc.LcdsSettings.Count > 0)
            {

                if (selectedRow == 0 && pi.Space())
                {
                    settingsToggle = false; 
                    pi.ResetControllers(gc.Controllers);
                    task = "Reset Controllers";
                    Echo(GetRuntimeInfo());
                    return;
                }

                EditSettingsSprite();

                switch (selectedPage)
                {
                    case 1:
                        if (selectedRow < 0) selectedRow = 7;
                        else if (selectedRow > 7) selectedRow = 1;

                        (settingsToggle ? (Action)FlightSystemSectionEdit : FlightSystemSection)(); 
                        break;
                    case 2:
                        if (selectedRow < 0) selectedRow = 8;
                        else if (selectedRow > 8) selectedRow = 1;

                        (settingsToggle ? (Action)ToggleSectionEdit : ToggleSection)(); 
                        break;
                    case 3:
                        if (selectedRow < 0) selectedRow = 5;
                        else if (selectedRow > 5) selectedRow = 1;

                        (settingsToggle ? (Action)ParamSectionEdit : ParamSection)(); 
                        break;
                }
            }

            if (!settingsToggle && ic.AnalogThrotle && command.State == MainState.Idle)
            {
                AnalogThrust();
            }

            tickCount++;
            if (tickCount % 50 == 1)
            {
                if (!settingsToggle)
                {
                    FlightSystemSection();
                }

                isGearlocked = gc.Gears.Exists(g => g.IsLocked);
                anyConnected = gc.IsAnyConnectorConnected();
                isDocked = anyConnected || isGearlocked;
                task = "IsDocked";
                Echo(GetRuntimeInfo());
                return;
            }

            if (ic.AllowDockMode)
            {
                if (!isDocked) isDockMode = false;
                else if (isDocked && !isDockMode) isDockMode = true;

                if (lastDockMode != isDockMode)
                {
                    AbortShipContext(gc);
                    DockToggle(gc, isDockMode);
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
                Echo("ErrorMessage: \n" + gc.ErrorMessage.ToString());
                return;
            }

            timeSinceLastRun = Runtime.TimeSinceLastRun.TotalSeconds;

            if (tickCount > 500)
            {
                if (CheckIni())
                {
                    task = "CheckIni";
                    Echo(GetRuntimeInfo());
                    return;
                }
            }

            switch (tick % tickSplit)
            {
                case 0:
                    task = "Physics Update";
                    pc.NewRun(timeSinceLastRun, command.Param.TargetCoordinates);
                    break;
                case 1:
                    task = "Flight Systems";
                    FlightSystems(gc, ic, pc);
                    break;
                case 2:
                    task = "LCDs";
                    if (IsShipControlled())
                    {
                        if (gc.Lcds1.Count > 0) LCD1Sprite();
                        if (gc.Lcds2.Count > 0) LCD2Sprite();
                    }
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
            gc.IgnoreTag = ic.IgnoreTag;

            if (!string.IsNullOrWhiteSpace(gc.GridName) && !gc.GridName.Contains(" Grid "))
            {
                gc.Me.CubeGrid.CustomName = gc.GridName;
            }

            if (hasIniChanged || gc.Controller == null || gc.Controller.Closed)
            {
                ReloadGridContext(gc, ic);
                tick = 0;
                return true;
            }
            return false;
        }

        void AnalogThrust()
        {
            pi.OcupiedController(gc.Controllers);
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

            if (gc.ForwardThrusters.First().ThrustOverridePercentage > 0) gc.KillThrusters(gc.BreakingThrusters);
            else gc.ResetThrusters(gc.BreakingThrusters);
        }

        StringBuilder ScriptInfo()
        {
            StringBuilder scriptInfo = new StringBuilder();
            ScriptInfoHeader(scriptInfo);
            scriptInfo.AppendLine("");
            ScriptInfoBlocks(ic, scriptInfo);
            return scriptInfo;
        }

        double tickCounter = 0;
        double maxRuntimeMs = 0;

        private String GetRuntimeInfo()
        {
            tickCounter++;

            if (tickCounter % 100 == 1)
            {
                maxRuntimeMs = 0;
            }

            StringBuilder m_echoBuilder = new StringBuilder(512);
            m_echoBuilder.AppendLine($"Runtime: {Math.Round(Runtime.LastRunTimeMs, 5)} Ms");

            double newRuntimeMs = Math.Round(Runtime.LastRunTimeMs, 5);
            if (newRuntimeMs > maxRuntimeMs)
            {
                maxTask = task;
            }
            maxRuntimeMs = Math.Max(newRuntimeMs, maxRuntimeMs);


            m_echoBuilder.AppendLine($"Max Runtime: {maxRuntimeMs} Ms");
            m_echoBuilder.AppendLine($"Task: {maxTask}");
            return m_echoBuilder.ToString();
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

            MainState[] arr = {MainState.CNav, MainState.Cruise, MainState.Orbit, MainState.Glide, MainState.Land, MainState.SBurn, MainState.Gps};
            List<MainState> MainStateList = new List<MainState>(arr);
            if (!ic.AllowFlightSystems && MainStateList.Contains(command.State))
            {
                return;
            }

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
                case MainState.Glide:
                    gc.Controller.DampenersOverride = true;
                    GlideStateSwitch(gc, ic, command);
                    break;
                case MainState.CNav: // Circumnavigation
                    if (pc.Gravity > 0)
                    {
                        gc.Controller.DampenersOverride = true;
                        CircumNavigateStateSwitch(gc, ic, command);
                    } else AbortShipContext(gc);
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
                    AutoLandStateSwitch(gc, command.Param);
                    break;
                case MainState.SBurn: // Suicide Burn
                    if (command.Param.AutoLandState == AutoLandState.Idle) command.Param.AutoLandState = AutoLandState.Align;
                    SuicideBurnStateSwitch(gc, command.Param);
                    break;
            }
        }

        private void DockToggle(GridContext gc, bool isDocked)
        {
            gc.SetBlocks(!isDocked, out isDockMode);
            gc.StockpileTanks(isDocked);
            if (isDocked)
            {
                gc.ChargeBatteries();
            }
            else
            {
                gc.AutoBatteries();
            }
        }

        public StringBuilder ScriptInfoHeader(StringBuilder scriptInfo)
        {
            scriptInfo.AppendLine("Flight systems - " + gc.GridName);
            scriptInfo.Append("    State: " + command.State);


            if (command.Param.Step != Step.Toggle)
                scriptInfo.Append(" - " + command.Param.Step);
            if (command.Param.Number != 0)
                scriptInfo.Append(" - " + command.Param.Number);
            if (command.Param.AutoLandState != AutoLandState.Idle)
                scriptInfo.Append(" - " + command.Param.AutoLandState);

            return scriptInfo;
        }

        public StringBuilder ScriptInfoBlocks(IniContext ic, StringBuilder scriptInfo)
        {
            scriptInfo.AppendLine("Toggles");
            scriptInfo.AppendLine("    " + IniContext.FLIGHT_SYSTEMS + ": " + ic.AllowFlightSystems);
            scriptInfo.AppendLine("    " + IniContext.ANALOG_THROTLE + ": " + ic.AnalogThrotle);
            scriptInfo.AppendLine("    " + IniContext.LOW_FUEL_LAND + ": " + ic.AllowLowFuelLand);
            scriptInfo.AppendLine("    " + IniContext.DOCK_MODE + ": " + ic.AllowDockMode);
            scriptInfo.AppendLine("    " + IniContext.CONTROL_ANTENNAS + ": " + ic.ControlAntennas);
            scriptInfo.AppendLine("    " + IniContext.RENAME_SUBGRIDS + ": " + ic.RenameSubgrids);
            scriptInfo.AppendLine("    " + IniContext.PAINT_SURFACES + ": " + ic.PaintSurfaces);
            scriptInfo.AppendLine("    " + IniContext.TRANSPARENTLCD + ": " + ic.TransparentLCD);
            scriptInfo.AppendLine("Blocks");
            scriptInfo.AppendLine("    Controller: " + gc.Controller.CustomName);

            if (ic.AllowFlightSystems)
            {
                scriptInfo.AppendLine("    Batteries: " + gc.Batteries.Count + " | Tanks: " + gc.Tanks.Count);
                scriptInfo.AppendLine("    Forward thruster: " + gc.ForwardThrusters.Count);
                scriptInfo.AppendLine("    Breaking thruster: " + gc.BreakingThrusters.Count);
                scriptInfo.AppendLine("    Upward thruster: " + gc.UpwardThrusters.Count);
                scriptInfo.AppendLine("    Gyros: " + gc.Gyros.Count);
            }

            if (ic.AllowDockMode || ic.AllowFlightSystems)
                scriptInfo.AppendLine("    Gears: " + gc.Gears.Count);

            if (ic.AllowDockMode)
            {
                scriptInfo.AppendLine("    Dock Mode blocks: " + gc.ControlledBlocks.Count);
            }

            scriptInfo.AppendLine("    LCDs1: " + gc.Lcds1.Count);
            scriptInfo.AppendLine("    LCDs2: " + gc.Lcds2.Count);
            scriptInfo.AppendLine("    Lcds Settings: " + gc.LcdsSettings.Count);
            scriptInfo.AppendLine("    Surfaces: " + gc.Surfaces.Count);

            return scriptInfo;
        }

        void CruiseControlStateSwitch(GridContext gc, IniContext ic, Command command)
        {
            double CruiseSpeed = (command.Param.Number > 0 ? command.Param.Number : ic.CruiseSpeed);

            switch (command.Param.Step)
            {
                case Step.Toggle:
                    sb.SetActiveMode(MainState.Cruise);
                    if (sb.CruiseToggle) command.Param.Step = Step.On;
                    else command.Param.Step = Step.Off;
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
                    sb.SetActiveMode(MainState.Orbit);
                    if (sb.OrbitToggle) command.Param.Step = Step.On;
                    else command.Param.Step = Step.Off;
                    break;
                case Step.On:
                    if (GravityAlignedOverride(gc))
                    {
                        command.Param.Step = Step.Preclimb;
                        desiredUp = pc.DesiredUpVector;
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
                        command.Param.Step = Step.Climb;
                    }
                    break;
                case Step.Climb:
                    Climb(gc, ic.CruiseSpeed, desiredUp);
                    break;
            }
        }

        void GlideStateSwitch(GridContext gc, IniContext ic, Command command)
        {
            double CruiseSpeed = (command.Param.Number > 0 ? command.Param.Number : ic.CruiseSpeed);

            switch (command.Param.Step)
            {
                case Step.Toggle:
                    sb.SetActiveMode(MainState.Glide);
                    if (sb.GlideToggle) command.Param.Step = Step.On;
                    else command.Param.Step = Step.Off;
                    break;
                case Step.On:
                    CruiseControl(CruiseSpeed, timeSinceLastRun);
                    if (pc.EffectiveAlt < ic.safeAltitude + pc.StopYDist)
                    {
                        AbortShipContext(gc);
                        command.State = MainState.Land;
                    }
                    break;
                case Step.Off:
                    AbortShipContext(gc);
                    break;
            }
        }

        void CircumNavigateStateSwitch(GridContext gc, IniContext ic, Command command)
        {
            double CruiseSpeed = (command.Param.Number > 0 ? command.Param.Number : ic.CruiseSpeed);
            switch (command.Param.Step)
            {
                case Step.Toggle:
                    sb.SetActiveMode(MainState.CNav);
                    if (sb.CNavToggle) command.Param.Step = Step.On;
                    else command.Param.Step = Step.Off;
                    break;
                case Step.On:
                    if (pc.EffectiveAlt < ic.safeAltitude)
                    {
                        SoftAbort(gc);
                        command.Param.Step = Step.Preclimb;
                        desiredUp = pc.DesiredUpVector;
                    }
                    else
                    {
                        GravityAlignedOverride(gc);
                        CruiseControl(CruiseSpeed, timeSinceLastRun); 
                    }
                    break;
                case Step.Off:
                    AbortShipContext(gc);
                    break;
                case Step.Preclimb:
                    if (GravityAlignedOverride(gc, pc.ForwardVelocity == 0))
                    {
                        command.Param.Step = Step.Climb;
                    }
                    break;
                case Step.Climb:
                    if (pc.EffectiveAlt > ic.safeAltitude)
                    {
                        gc.ResetThrusters(gc.ForwardThrusters);
                        command.State = MainState.CNav;
                        command.Param.Step = Step.On;
                    }
                    Climb(gc, CruiseSpeed, desiredUp);
                    break;
            }
        }

        void GPSStateSwitch(GridContext gc, IniContext ic, Command command)
        {
            switch (command.Param.Step)
            {
                case Step.Toggle:
                    sb.SetActiveMode(MainState.Gps);
                    if (sb.GpsToggle) command.Param.Step = Step.On;
                    else command.Param.Step = Step.Off;
                    break;

                case Step.On:
                    if (sb.GpsToggle && pc.DistanceToGPS < ic.DistanceToGPS + pc.StopZDist)
                    {
                        if (pc.Gravity > 0)
                        {
                            command.State = MainState.Land;
                            sb.GpsToggle = false;
                            previousRate = PREV_RATE;
                        }
                        else AbortShipContext(gc);
                        return;
                    }

                    if (pc.EffectiveAlt < ic.safeAltitude)
                    {
                        SoftAbort(gc);
                        command.Param.Step = Step.Preclimb;
                        desiredUp = pc.DesiredUpVector;
                        return;
                    }

                    double planetRadius = Vector3D.Distance(gc.Controller.GetPosition(), pc.PlanetCenter) - pc.SeaLevel;

                    PlanetType planet = DetectPlanet(planetRadius);
                    
                    if (pc.Gravity == 0)
                    {
                        if (VectorAlignedOverride(gc, gc.Controller.WorldMatrix.Forward, false, gc.Controller.GetPosition() - command.Param.TargetCoordinates))
                            CruiseControl(ic.CruiseSpeed, timeSinceLastRun);
                    }
                    else if (GravityAlignedOverride(gc) && GravAlignedYawOverride(gc, command.Param.TargetCoordinates))
                    {
                        if (GetGravityRadius(planetRadius, planet) < Vector3D.Distance(pc.PlanetCenter, command.Param.TargetCoordinates) &&
                            VectorHelper.IsWithinAngle(pc.PlanetCenter, gc.Controller.GetPosition(), command.Param.TargetCoordinates, 40))
                        {
                            desiredUp = pc.DesiredUpVector;
                            command.Param.Step = Step.Orbit;
                        }
                        CruiseControl(ic.CruiseSpeed, timeSinceLastRun);
                    }
                    break;

                case Step.Orbit:
                    if (sb.GpsToggle && pc.DistanceToGPS < ic.DistanceToGPS + pc.StopZDist)
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
                    Climb(gc, ic.CruiseSpeed, desiredUp);
                    break;

                case Step.Off:
                    AbortShipContext(gc);
                    break;

                case Step.Preclimb:
                    if (GravityAlignedOverride(gc, pc.ForwardVelocity == 0))
                    {
                        command.Param.Step = Step.Climb;
                    }
                    break;

                case Step.Climb:
                    if (pc.EffectiveAlt > ic.safeAltitude)
                    {
                        gc.ResetThrusters(gc.ForwardThrusters);
                        command.State = MainState.Gps;
                        command.Param.Step = Step.On;
                        return;
                    }
                    Climb(gc, ic.CruiseSpeed, desiredUp);
                    break;
            }
        }

        private void Climb(GridContext gc, double CruiseSpeed, Vector3D desiredUp)
        {
            VectorAlignedOverride(gc, gc.Controller.WorldMatrix.Up, false, desiredUp);
            CruiseControl(CruiseSpeed, timeSinceLastRun);
        }

        void AutoLandStateSwitch(GridContext gc, CommandParam param)
        {
            sb.SetActiveMode(MainState.Land);
            if (!sb.LandToggle) AbortShipContext(gc);
            switch (param.AutoLandState)
            {
                case AutoLandState.Idle:
                    break;

                case AutoLandState.Align:
                    SoftAbort(gc);
                    if (GravityAlignedOverride(gc, true)) command.Param.AutoLandState = AutoLandState.Drop;
                    break;

                case AutoLandState.Drop:
                    if (AutoLand(gc, pc, command)) command.Param.AutoLandState = AutoLandState.LockGear;
                    break;

                case AutoLandState.LockGear:
                    if (TryLock(gc)) AbortShipContext(gc);
                    break;
            }
        }

        void SuicideBurnStateSwitch(GridContext gc, CommandParam param)
        {
            sb.SetActiveMode(MainState.SBurn);
            if (!sb.SBurnToggle) AbortShipContext(gc);
            switch (param.AutoLandState)
            {
                case AutoLandState.Idle:
                    break;

                case AutoLandState.Align:
                    SoftAbort(gc);
                    if (GravityAlignedOverride(gc, true)) command.Param.AutoLandState = AutoLandState.Drop;
                    break;

                case AutoLandState.Drop:
                    if (SuicideBurn(gc, pc, command)) command.Param.AutoLandState = AutoLandState.LockGear;
                    break;

                case AutoLandState.LockGear:
                    if (pc.UpVelocity > -(ic.CruiseSpeed / 4) && 4 * pc.EffectiveAlt > 1 + pc.StopZDist)
                    {
                        command.State = MainState.Land;
                        command.Param.AutoLandState = AutoLandState.Drop;
                    }
                    else if (TryLock(gc)) AbortShipContext(gc);
                    break;
            }
        }

        private void ReloadGridContext(GridContext gc, IniContext ic)
        {
            pc = new PhysicsContext(gc, stt, timeSinceLastRun);

            gc.ReloadLCDs(ic.Lcd1Tag, ic.Lcd2Tag, ic.LcdSettingsTag)
                    .ReloadH2Tanks()
                    .ReloadBatteries(ic.BackupBatteryTag);

            // Flight cached blocks
            if (ic.AllowFlightSystems)
            {
                gc.ReloadControllers(ic.ControllerTag);

                if (gc.ErrorMessage.Length > 0)
                    return;

                gc.ReloadGridHeight()
                    .ReloadThrusters()
                    .ReloadGyros()
                    .ReloadGears();

                SoftAbort(gc);

                sb.LastCheckIsOnNatGrav = pc.Gravity > 0;
            }

                // Dock cached blocks
                if (ic.AllowDockMode)
                    gc.ReloadConnectors()
                    .ReloadGears()
                    .ReloadTanks()
                    .ReloadControlledBlocks(ic.DockGroupTag, ic.OverrideBlockTag);

                if (ic.ControlAntennas)
                    gc.ReloadAntennas(ic.ControlAntennas);

                if (ic.RenameSubgrids)
                {
                    IMyCubeGrid mainGrid = gc.Me.CubeGrid;
                    if (mainGrid != null)
                    {
                        RenameSubgrids.GetSubgridsAndRename(gc.GridTS, mainGrid);
                    }
                }

                if (ic.PaintSurfaces)
                {
                    gc.ReloadSurfaces();

                    GridContext.PaintSurfaces(ic, gc.Surfaces);
                }
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
                gc.ResetThrusters(gc.ForwardThrusters);
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

        private void LCD1Sprite()
        {
            Sprites spt = new Sprites(ic);
            spt.Add(gc.GridName);

            StringBuilder state = new StringBuilder();
            state.Append("State: " + command.State);


            if (command.Param.Step != Step.Toggle)
                state.Append(" - " + command.Param.Step);
            if (command.Param.Number != 0)
                state.Append(" - " + command.Param.Number);
            if (command.Param.AutoLandState != AutoLandState.Idle)
                state.Append(" - " + command.Param.AutoLandState);

            spt.Add(state.ToString());

            spt.Add($"Mass: {pc.Mass.PhysicalMass / 1000:0.0} t");
            spt.Add($"Empty Mass: {pc.Mass.BaseMass / 1000:0.0} t");

            Color color;
            color = pc.PrevH2Fill < pc.H2Cache.Filled ? Color.LightBlue
                : pc.H2Cache.Percent < ic.MinimumAcceptedFuel / 2 ? Color.DarkRed
                : pc.H2Cache.Percent < ic.MinimumAcceptedFuel ? Color.DarkOrange
                : new Color();

            if (!color.Equals(new Color()))
                spt.AddB($"H2: {pc.H2Cache.Percent:0}% - {pc.H2Cache.Time}", color);
            else spt.Add($"H2: {pc.H2Cache.Percent:0}% - {pc.H2Cache.Time}");

            color = pc.PrevBatFill < pc.BatCache.Filled ? Color.LightBlue
                : pc.BatCache.Percent < ic.MinimumAcceptedFuel / 2 ? Color.DarkRed
                : pc.BatCache.Percent < ic.MinimumAcceptedFuel ? Color.DarkOrange
                : new Color();

            if (!color.Equals(new Color()))
                spt.AddB($"Bat:  {pc.BatCache.Percent:0}% - {pc.BatCache.Time}", color);
            else spt.Add($"Bat:  {pc.BatCache.Percent:0}% - {pc.BatCache.Time}");

            DrawSprites(spt, gc.Lcds1);
        }

        void LCD2Sprite()
        {
            Sprites spt = new Sprites(ic);

            if (pc.Gravity > 0)
            {
                Color color = new Color();
                if (pc.ClimbRate < 0)
                {
                    color = pc.GroundLevel < 2 * pc.StopYDist
                        ? Color.DarkRed : pc.GroundLevel < 4 * pc.StopYDist
                        ? Color.DarkOrange : new Color();
                }

                if (!color.Equals(new Color()))
                {
                    spt.AddB($"GL: {pc.GroundLevel:F1} m | SL: {pc.SeaLevel:F1} m", color);
                    spt.Add($"Rate of climb: {pc.ClimbRate:F1} m/s");
                    spt.AddB($"Stop Y: {pc.StopYDist:F1} m | {pc.TimeToStopY:F1} s", color);
                }
                else
                {
                    spt.Add($"GL: {pc.GroundLevel:F1} m | SL: {pc.SeaLevel:F1} m");
                    spt.Add($"Rate of climb: {pc.ClimbRate:F1} m/s");
                    spt.Add($"Stop Y: {pc.StopYDist:F1} m | {pc.TimeToStopY:F1} s");
                }
            }

            spt.Add($"Stop Z: {pc.StopZDist:F1} m | {pc.TimeToStopZ:F1} s");
            if (pc.Gravity > 0) spt.Add($"Accel: {pc.Accel.Length() / 9.81:F1} g | Grav: {pc.Gravity:F2} g ");
            else spt.Add($"Accel: {pc.Accel.Length() / 9.81:F1} g");

            if (sb.GpsToggle)
            {
                spt.Add($"ETA: {UtilsHelpder.FormatTime(pc.TimeToDistanceSmoothed)}");
            }
            else if (command.State == MainState.Land || command.State == MainState.SBurn)
            {
                spt.Add("TTI: " + (pc.TimeToImpact == 0 ? "--" : $"{pc.TimeToImpact:F0}") + " s");
            }
            else
            {
                spt.Add($"Longitudinal v: {pc.ForwardVelocity:F1} m/s");
                spt.Add($"Lateral v: {pc.RightVelocity:F1} m/s");
                spt.Add($"Vertical v: {pc.UpVelocity:F1} m/s");
            }

            DrawSprites(spt, gc.Lcds2);
        }

        int selectedRow;
        int selectedPage = 1;

        void EditSettingsSprite()
        {
            if (pi.W())
            {
                LockInput();
                selectedRow--;
            }

            if (pi.S())
            {
                LockInput();
                selectedRow++;
            }

            if (pi.A())
            {
                LockInput();
                selectedPage--;
            }

            if (pi.D())
            {
                LockInput();
                selectedPage++;
            }

            if (selectedPage < 1) selectedPage = 3;
            else if (selectedPage > 3) selectedPage = 1;
        }

        private void LockInput()
        {
            settingsIsLocked = true;
            inputLock = 0;
        }

        void FlightSystemSectionEdit()
        {
            Sprites spt = new Sprites(ic);
            int row = 1;

            if (pi.Space())
            {
                settingsToggle = false;
                pi.ResetControllers(gc.Controllers);
                AbortShipContext(gc);

                if (selectedRow == row++) command = new Command(MainState.Cruise);
                else if (selectedRow == row++) command = new Command(MainState.Orbit);
                else if (selectedRow == row++) command = new Command(MainState.CNav);
                else if (selectedRow == row++) command = new Command(MainState.Land);
                else if (selectedRow == row++) command = new Command(MainState.Glide);
                else if (selectedRow == row++) command = new Command(MainState.SBurn);
                else if (selectedRow == row++) command = new Command(MainState.Gps);
            }

            row = 1;

            spt.Add($"Flight Systems");
            spt.Add($"Cruise control", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add($"Fly to orbit", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add($"Circumnavigate", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add($"Vertical land", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add($"Glide to surface", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add($"Suicide burn", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add($"Fly to GPS", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));

            DrawSprites(spt, gc.LcdsSettings);
        }

        void ToggleSectionEdit()
        {
            Sprites spt = new Sprites(ic);
            int row = 1;

            if (pi.Space())
            {
                LockInput();
                if (selectedRow == row++) ic.AllowFlightSystems = !ic.AllowFlightSystems;
                else if (selectedRow == row++) ic.AnalogThrotle = !ic.AnalogThrotle;
                else if (selectedRow == row++) ic.AllowLowFuelLand = !ic.AllowLowFuelLand;
                else if (selectedRow == row++) ic.AllowDockMode = !ic.AllowDockMode;
                else if (selectedRow == row++) ic.ControlAntennas = !ic.ControlAntennas;
                else if (selectedRow == row++) ic.RenameSubgrids = !ic.RenameSubgrids;
                else if (selectedRow == row++) ic.PaintSurfaces = !ic.PaintSurfaces;
                else if (selectedRow == row++) ic.TransparentLCD = !ic.TransparentLCD;
            }

            row = 1;

            spt.Add($"{IniContext.ToggleSection}");
            spt.Add($"{IniContext.FLIGHT_SYSTEMS}", BoolSpriteColor(selectedRow == row++, ic.AllowFlightSystems), Color.Black);
            spt.Add($"{IniContext.ANALOG_THROTLE}", BoolSpriteColor(selectedRow == row++, ic.AnalogThrotle), Color.Black);
            spt.Add($"{IniContext.LOW_FUEL_LAND}", BoolSpriteColor(selectedRow == row++, ic.AllowLowFuelLand), Color.Black);
            spt.Add($"{IniContext.DOCK_MODE}", BoolSpriteColor(selectedRow == row++, ic.AllowDockMode), Color.Black);
            spt.Add($"{IniContext.CONTROL_ANTENNAS}", BoolSpriteColor(selectedRow == row++, ic.ControlAntennas), Color.Black);
            spt.Add($"{IniContext.RENAME_SUBGRIDS}", BoolSpriteColor(selectedRow == row++, ic.RenameSubgrids), Color.Black);
            spt.Add($"{IniContext.PAINT_SURFACES}", BoolSpriteColor(selectedRow == row++, ic.PaintSurfaces), Color.Black);
            spt.Add($"{IniContext.TRANSPARENTLCD}", BoolSpriteColor(selectedRow == row++, ic.TransparentLCD), Color.Black);

            DrawSprites(spt, gc.LcdsSettings);
        }

        void ParamSectionEdit()
        {
            Sprites spt = new Sprites(ic);
            int row = 1;

            if (selectedRow == row++) ic.MaxSpeed = IncrementedValue(ic.MaxSpeed);
            else if (selectedRow == row++) ic.CruiseSpeed = IncrementedValue(ic.CruiseSpeed);
            else if (selectedRow == row++) ic.safeAltitude = IncrementedValue(ic.safeAltitude);
            else if (selectedRow == row++) ic.DistanceToGPS = IncrementedValue(ic.DistanceToGPS);
            else if (selectedRow == row++) ic.MinimumAcceptedFuel = IncrementedValue(ic.MinimumAcceptedFuel);

            row = 1;

            spt.Add($"{IniContext.ParamsSection}");
            spt.Add($"{IniContext.MAX_SPEED}: {ic.MaxSpeed}", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add($"{IniContext.CRUISE_SPEED}: {ic.CruiseSpeed}", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add($"{IniContext.CNAV_ALTITUDE}: {ic.safeAltitude}", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add($"{IniContext.DISTANCE_TO_GPS}: {ic.DistanceToGPS}", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add($"{IniContext.MINIMUM_ACCEPTED_FUEL}: {ic.MinimumAcceptedFuel}", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));

            DrawSprites(spt, gc.LcdsSettings);
        }

        void FlightSystemSection()
        {
            Sprites spt = new Sprites(ic);

            if (sb.CruiseToggle) selectedRow = 1;
            else if (sb.OrbitToggle) selectedRow = 2;
            else if (sb.CNavToggle) selectedRow = 3;
            else if (sb.LandToggle) selectedRow = 4;
            else if (sb.GlideToggle) selectedRow = 5;
            else if (sb.SBurnToggle) selectedRow = 6;
            else if (sb.GpsToggle) selectedRow = 7;

            int row = 1;

            spt.Add($"Flight Systems");
            spt.Add($"Cruise control", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add($"Fly to orbit", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add($"Circumnavigate",  RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add($"Vertical land",  RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add($"Glide to surface",  RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add($"Suicide burn",  RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));
            spt.Add($"Fly to GPS", RowColor(row, ic.SpriteBackgroundColor), RowColor(row++, ic.SpriteFontColor));


            DrawSprites(spt, gc.LcdsSettings);
        }

        void ToggleSection()
        {
            Sprites spt = new Sprites(ic);

            spt.Add($"{IniContext.ToggleSection}");
            spt.Add($"{IniContext.FLIGHT_SYSTEMS}", BoolSpriteColor(ic.AllowFlightSystems), Color.Black);
            spt.Add($"{IniContext.ANALOG_THROTLE}", BoolSpriteColor(ic.AnalogThrotle), Color.Black);
            spt.Add($"{IniContext.LOW_FUEL_LAND}", BoolSpriteColor(ic.AllowLowFuelLand), Color.Black);
            spt.Add($"{IniContext.DOCK_MODE}", BoolSpriteColor(ic.AllowDockMode), Color.Black);
            spt.Add($"{IniContext.CONTROL_ANTENNAS}", BoolSpriteColor(ic.ControlAntennas), Color.Black);
            spt.Add($"{IniContext.RENAME_SUBGRIDS}", BoolSpriteColor(ic.RenameSubgrids), Color.Black);
            spt.Add($"{IniContext.PAINT_SURFACES}", BoolSpriteColor(ic.PaintSurfaces), Color.Black);
            spt.Add($"{IniContext.TRANSPARENTLCD}", BoolSpriteColor(ic.TransparentLCD), Color.Black);

            DrawSprites(spt, gc.LcdsSettings);
        }

        void ParamSection()
        {
            Sprites spt = new Sprites(ic);

            spt.Add($"{IniContext.ParamsSection}");
            spt.Add($"{IniContext.MAX_SPEED}: {ic.MaxSpeed}");
            spt.Add($"{IniContext.CRUISE_SPEED}: {ic.CruiseSpeed}");
            spt.Add($"{IniContext.CNAV_ALTITUDE}: {ic.safeAltitude}");
            spt.Add($"{IniContext.DISTANCE_TO_GPS}: {ic.DistanceToGPS}");
            spt.Add($"{IniContext.MINIMUM_ACCEPTED_FUEL}: {ic.MinimumAcceptedFuel}");


            DrawSprites(spt, gc.LcdsSettings);
        }

        Color RowColor(int row, Color color)
        {
            double DarkenFactor = 0.2;
            return selectedRow == row ? ColorMap.SelectedColor(color, DarkenFactor) : color;
        }

        double IncrementedValue(double value)
        {
            double increment;
            if (value < 1) increment = 0.1;
            else if(value < 10) increment = 1;
            else if (value < 50) increment = 5;
            else if (value < 100) increment = 10;
            else if (value < 500) increment = 50;
            else if (value < 1000) increment = 100;
            else if (value < 5000) increment = 500;
            else increment = 1000;

            if (pi.Space())
            {
                LockInput();
                value += increment;
            }

            if (pi.C())
            {
                LockInput();
                value -= increment;
            }

            return value < 0 ? 0 : value;
        }

        Color BoolSpriteColor(bool isSelected, bool toggle)
        {
            return (isSelected ? 
                toggle ? Color.Green : Color.Red : 
                toggle ? Color.LightGreen : Color.OrangeRed);
        }

        Color BoolSpriteColor(bool toggle)
        {
            return (toggle ? Color.LightGreen : Color.OrangeRed);
        }
                
        void DrawSprites(Sprites spt, List<IMyTextSurface> surfaces, int col = 1)
        {
            foreach (IMyTextSurface surface in surfaces)
            {
                spt.DrawInfoPanel(surface, col);
            }
        }

        void AbortShipContext(GridContext gc)
        {
            sb = new SystemBools();

            command = Command.Empty;
            previousRate = PREV_RATE;

            SoftAbort(gc);
        }

        void SoftAbort(GridContext gc)
        {
            gc.Controller.DampenersOverride = true;
            sb.StopCruiseWhenOutOfGrav = false;

            gc.ResetGyros();
            gc.ResetThrusters(gc.Thrusters);
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

        bool GravAlignedYawOverride(GridContext gc, Vector3D targetGps)
        {   
            if (gc.Controller == null || gc.Gyros == null || gc.Gyros.Count == 0) return false;
            if (pc.NaturalGravity.LengthSquared() < 0.01) return false;

            Vector3D up = Vector3D.Normalize(pc.NaturalGravity);
            Vector3D shipPos = gc.Controller.GetPosition();
            Vector3D shipForward = gc.Controller.WorldMatrix.Forward;

            Vector3D toTarget = targetGps - shipPos;

            // **NEW: Check if we're on the wrong side of the planet**
            // If the ship is moving away from the target (dot product is negative),
            // it means the target is behind/opposite relative to ship's current position
            // on the gravity plane. Reject in this case.
            Vector3D targetProj = toTarget - up * Vector3D.Dot(toTarget, up);

            if (targetProj.LengthSquared() < 1e-6)
            {
                // Target is nearly vertical (pole case)
                // Check: is the target in the same hemisphere as the ship?
                // Compare altitude-adjusted positions
                double shipAltitude = Vector3D.Dot(shipPos, up);
                double targetAltitude = Vector3D.Dot(targetGps, up);

                if (Math.Sign(shipAltitude) != Math.Sign(targetAltitude))
                {
                    // Target is on opposite pole — don't fly there
                    return true;
                }

                // Target is directly above/below on same side — no yaw needed
                return true;
            }

            targetProj = Vector3D.Normalize(targetProj);

            Vector3D forwardProj = shipForward - up * Vector3D.Dot(shipForward, up);
            if (forwardProj.LengthSquared() < 1e-9)
            {
                forwardProj = Vector3D.Cross(up, Math.Abs(up.X) < 0.9 ? Vector3D.UnitX : Vector3D.UnitY);
            }
            forwardProj = Vector3D.Normalize(forwardProj);

            // Signed yaw angle
            double cosA = Vector3D.Dot(forwardProj, targetProj);
            cosA = Math.Max(-1.0, Math.Min(1.0, cosA));
            double angleMag = Math.Acos(cosA);
            double sign = Math.Sign(Vector3D.Dot(forwardProj.Cross(targetProj), up));
            double yawAngle = sign * angleMag;

            const double ANGLE_EPS = 0.01;
            if (Math.Abs(yawAngle) < ANGLE_EPS)
            {
                gc.ResetGyros();
                return true;
            }

            const double MAX_ROT_RATE = 6.0;
            const double RESPONSE = 2.0;
            double desiredRateScalar = Math.Min(Math.Abs(yawAngle) * RESPONSE, MAX_ROT_RATE);
            Vector3D desiredRate = up * (Math.Sign(yawAngle) * desiredRateScalar);

            Vector3D angVel = gc.Controller.GetShipVelocities().AngularVelocity;
            Vector3D correction = desiredRate - angVel;

            foreach (var g in gc.Gyros)
            {
                MatrixD inv = MatrixD.Transpose(g.WorldMatrix);
                Vector3D local = Vector3D.TransformNormal(correction, inv);

                g.GyroOverride = true;
                g.Pitch = 0f;
                g.Yaw = (float)MathHelper.Clamp(-local.Y / 2, -6, 6);
                g.Roll = 0f;
            }

            return false;
        }

        PlanetType DetectPlanet(double radius)
        {
            if (radius < 12000)
                return PlanetType.MoonFamily;

            if (radius < 35000)
                return PlanetType.Pertam;

            if (radius < 50000)
                return PlanetType.Triton;

            return PlanetType.Earth;
        }

        double GetGravityRadius(double radius, PlanetType type)
        {
            switch (type)
            {
                case PlanetType.Triton:
                    return radius * 1.847800623;

                case PlanetType.Pertam:
                    return radius * 1.620035921;

                case PlanetType.MoonFamily:
                    return radius * 1.319403509;

                default:
                    return radius * 1.701333333;
            }
        }

        ////////////////////////////////////////////////////////
        /// SAFE DEscENT
        ////////////////////////////////////////////////////////
        bool SuicideBurn(GridContext gc, PhysicsContext pc, Command command)
        {
            if (pc.NetDecel - 1 < 0)
            {
                AbortShipContext(gc);
                command.State = MainState.Orbit;
            }

            gc.Controller.DampenersOverride = false;
            GravityAlignedOverride(gc);
            return pc.EffectiveAlt < 1.3 * pc.StopYDist + gc.GridHeight;
        }

        bool AutoLand(GridContext gc, PhysicsContext pc, Command command)
        {
            if (pc.NetDecel - 0.5 < 0)
            {
                AbortShipContext(gc);
                command.State = MainState.Orbit;
            }

            gc.Controller.DampenersOverride = false;
            GravityAlignedOverride(gc);

            double speedFromAlt = (100 + pc.GroundLevel) * 0.08;
            double speedFromAccel = 20 * pc.NetDecel;
            double speedMin = -Math.Min(speedFromAlt, speedFromAccel);

            if (speedMin > -104) VectorHelper.MatchVerticalSpeed(gc, pc, speedMin);
            return pc.EffectiveAlt < 10 + 2 * gc.GridHeight;
        }

        bool TryLock(GridContext gc)
        {
            GravityAlignedOverride(gc);
            VectorHelper.MatchVerticalSpeed(gc, pc, -2);
            gc.Controller.DampenersOverride = true;

            foreach (var g in gc.Gears)
                g.Lock();

            return gc.Gears.Exists(g => g.IsLocked);
        }
    }
}
