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
        SettingsScreens settingsHud;

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

        double planetRadius = 0;
        PlanetType planet = PlanetType.Unknown;

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
            settingsHud = new SettingsScreens();

            CheckIni(true);
            ic.ApplyCommand(command);
            sb.RestoreMode(command.State);
            
            if (gc.LcdsSettings.Count > 0) settingsHud.FlightSystemIdle(ic, gc, sb);
        }

        public void Save()
        {
            ic.PersistCommand(command);
        }

        public void Main(string argument)
        {
            if (!string.IsNullOrEmpty(argument))
            {
                foreach (string param in ic.IniParamList)
                {
                    if (argument.Contains(param))
                    {
                        var parts = argument.Trim().Split(
                            new[] {':'},
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
            if (inputLock > 10)
            {
                inputLock = 0;
                settingsIsLocked = false;
            }

            if (settingsToggle && !settingsIsLocked && gc.LcdsSettings.Count > 0)
            {
                if (settingsHud.ShouldClose(pi))
                {
                    settingsToggle = false;
                    pi.ResetControllers(gc.Controllers);
                    task = Task.ResetControllers;
                    Echo(GetRuntimeInfo());
                    return;
                }

                settingsHud.Handle(
                    pi, gc, ic, sb, command, settingsToggle,
                    () => AbortShipContext(gc),
                    () => SoftAbort(gc),
                    () =>
                    {
                        settingsToggle = false;
                        pi.ResetControllers(gc.Controllers);
                    },
                    LockInput);
            }

            if (!settingsToggle && ic.AnalogThrotle && command.State == MainState.Idle)
            {
                AnalogThrust();
            }

            tickCount++;
            if (tickCount % 50 == 1)
            {

                isGearlocked = gc.Gears.Exists(g => g.IsLocked);
                anyConnected = gc.IsAnyConnectorConnected();
                isDocked = anyConnected || isGearlocked;
                task = Task.IsDocked;
                Echo(GetRuntimeInfo());
                return;
            }

            if (tickCount % 50 == 2 && !IsShipControlled())
            {
                if (!settingsToggle)
                    settingsHud.FlightSystemIdle(ic, gc, sb);

                Lcd1Display.Draw(gc.Lcds1, ic, gc, pc, command, planet, planetRadius);
                Lcd2Display.Draw(gc.Lcds2, ic, pc, command, sb);
            }

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

                if (pc.Gravity > 0 && (planetRadius == 0 || planet == PlanetType.Unknown))
                {
                    planetRadius = Vector3D.Distance(gc.Controller.GetPosition(), pc.PlanetCenter) - pc.SeaLevel;
                    planet = DetectPlanet(planetRadius);
                }
                else if (pc.Gravity == 0)
                {
                    planetRadius = 0;
                    planet = PlanetType.Unknown;
                }
            }

            switch (tick % tickSplit)
            {
                case 0:
                    task = Task.PhysicsUpdate;
                    pc.NewRun(timeSinceLastRun, command.Param.TargetCoordinates, command);
                    break;
                case 1:
                    task = Task.FlightSystems;
                    FlightSystems(gc, ic, pc);
                    break;
                case 2:
                    task = Task.LCDs;
                    if (IsShipControlled())
                    {
                        Lcd1Display.Draw(gc.Lcds1, ic, gc, pc, command, planet, planetRadius);
                        Lcd2Display.Draw(gc.Lcds2, ic, pc, command, sb);
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

        bool CheckIni(bool fromCtor = false)
        {
            tickCount = 0;

            if (!fromCtor)
                ic.CaptureCommand(command);

            bool hasIniChanged = ic.ParseIni(fromCtor);

            if (!string.IsNullOrWhiteSpace(gc.GridName) && !gc.GridName.Contains(" Grid "))
            {
                gc.Me.CubeGrid.CustomName = gc.GridName;
            }

            if (hasIniChanged || gc.Controller == null || gc.Controller.Closed)
            {
                if (!ic.AllowDockMode)
                {
                    AbortShipContext(gc);
                    DockToggle(gc, false);
                }
                ReloadGridContext(gc, ic);
                tick = 0;
                return true;
            }

            if (gc.SyncTaggedCockpitIni())
            {
                gc.ReloadLCDs();
                return true;
            }

            if (gc.Controller != null && !gc.Controller.Closed)
                ic.Gps.LoadFromBlock(gc.Controller);

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

            return $"Local Max\nTask: {maxTask}\nRuntime: {MaxRuntime} Ms\nInstructions: {MaxInstruction} Ms\n\nAverage\nRuntime: {avgRuntime:F4} Ms\nInstructions: {avgInstructions:0}";
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

            var allowedStates = new[] { MainState.CNav, MainState.Cruise, MainState.Orbit, MainState.Glide, MainState.Land, MainState.SBurn, MainState.Gps };

            if (!ic.AllowFlightSystems && allowedStates.Contains(command.State))
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
                    AutoLandStateSwitch(gc, command);
                    break;
                case MainState.SBurn: // Suicide Burn
                    if (command.Param.AutoLandState == AutoLandState.Idle) command.Param.AutoLandState = AutoLandState.Align;
                    SBurnStateSwitch(gc, command);
                    break;
            }
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
                        command.Param.Step = Step.Climb;
                    }
                    break;
                case Step.Climb:
                    Climb(gc, ic.CruiseSpeed);
                    break;
            }
        }

        void GlideStateSwitch(GridContext gc, IniContext ic, Command command)
        {
            double CruiseSpeed = (command.Param.Number > 0 ? command.Param.Number : ic.CruiseSpeed);

            switch (command.Param.Step)
            {
                case Step.Toggle:
                    ToggleCommand(gc, command);
                    break;
                case Step.On:
                    CruiseControl(CruiseSpeed, timeSinceLastRun);
                    if (pc.Gravity > 0 && pc.GroundLevel < ic.SafeAltitude + pc.StopYDist)
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
                    if (pc.GroundLevel > ic.SafeAltitude)
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
            switch (command.Param.Step)
            {
                case Step.Toggle:
                    ToggleCommand(gc, command);
                    break;

                case Step.On:                    
                    if (pc.Gravity == 0)
                    {
                        if (VectorAlignedOverride(gc, gc.Controller.WorldMatrix.Forward, false, gc.Controller.GetPosition() - command.Param.TargetCoordinates))
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

                    if (AimHorizonToGps(gc, command.Param.TargetCoordinates))
                    {
                        if (GetGravityRadius(planetRadius, planet) < Vector3D.Distance(pc.PlanetCenter, command.Param.TargetCoordinates) &&
                            VectorHelper.IsWithinAngle(pc.PlanetCenter, gc.Controller.GetPosition(), command.Param.TargetCoordinates, 40))
                        {
                            command.Param.Step = Step.Orbit;
                            return;
                        }
                        command.Param.Step = Step.Cruise;
                    }
                    break;

                case Step.Cruise:
                    if (sb.GpsToggle && pc.DistanceToGPS < ic.DistanceToGPS + pc.StopZDist)
                    {
                        if (pc.Gravity > 0)
                        {
                            SoftAbort(gc);
                            sb.RestoreMode(MainState.Land);
                            command.Param.Step = Step.On;
                            command.Param.AutoLandState = AutoLandState.Align;
                            previousRate = PREV_RATE;
                            command.State = MainState.Land;
                        }
                        else
                            AbortShipContext(gc);
                        return;
                    }

                    if (pc.GroundLevel < ic.SafeAltitude)
                    {
                        SoftAbort(gc);
                        command.Param.Step = Step.Preclimb;
                        return;
                    }
                    AimHorizonToGps(gc, command.Param.TargetCoordinates);
                    CruiseControl(ic.CruiseSpeed, timeSinceLastRun);
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
                    Climb(gc, ic.CruiseSpeed);
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
                    if (pc.GroundLevel > ic.SafeAltitude)
                    {
                        gc.ResetThrusters(gc.ForwardThrusters);
                        command.State = MainState.Gps;
                        command.Param.Step = Step.On;
                        return;
                    }
                    Climb(gc, ic.CruiseSpeed);
                    break;
            }
        }

        private void Climb(GridContext gc, double CruiseSpeed)
        {
            VectorAlignedOverride(gc, gc.Controller.WorldMatrix.Up, false, pc.DesiredUpVector);
            CruiseControl(CruiseSpeed, timeSinceLastRun);
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
                    if (AlignForVerticalLand(gc)) command.Param.AutoLandState = AutoLandState.Drop;
                    break;

                case AutoLandState.Drop:
                    if (AutoLand(gc, pc, command)) command.Param.AutoLandState = AutoLandState.LockGear;
                    break;

                case AutoLandState.LockGear:
                    if (TryLock(gc)) AbortShipContext(gc);
                    break;
            }
        }

        void SBurnStateSwitch(GridContext gc, Command command)
        {
            switch (command.Param.Step)
            {
                case Step.Toggle:
                    ToggleCommand(gc, command);
                    break;
                case Step.On:
                    SBurnSwitch(gc, command);
                    break;
                case Step.Off:
                    AbortShipContext(gc);
                    break;
            }
        }

        private void SBurnSwitch(GridContext gc, Command command)
        {
            switch (command.Param.AutoLandState)
            {
                case AutoLandState.Align:
                    if (AlignForVerticalLand(gc)) command.Param.AutoLandState = AutoLandState.Drop;
                    break;

                case AutoLandState.Drop:
                    if (SuicideBurn(gc, pc, command)) command.Param.AutoLandState = AutoLandState.LockGear;
                    break;

                case AutoLandState.LockGear:
                    if (pc.UpVelocity > -(ic.CruiseSpeed / 4) && 4 * pc.GroundLevel > 1 + pc.StopYDist)
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

            gc.Setup(ic);

            gc.ReloadLCDs()
                .ReloadH2Tanks()
                .ReloadBatteries();

            if (gc.ErrorMessage.Length > 0)
                return;

            // Flight cached blocks
            if (ic.AllowFlightSystems || ic.AllowLowFuelLand)
            {                
                SoftAbort(gc);

                sb.LastCheckIsOnNatGrav = pc.Gravity > 0;
            }

            // Dock cached blocks
            if (ic.AllowDockMode)
                gc.ReloadConnectors()
                    .ReloadTanks()
                    .ReloadControlledBlocks(ic.DockGroupTag, ic.OverrideBlockTag);

            if (ic.RenameSubgrids) RenameSubgrids.GetSubgridsAndRename(gc.GridTS, gc.Me.CubeGrid);

            if (ic.PaintSurfaces)
                gc.PaintAllScreens(ic);

            if (gc.Controller != null && !gc.Controller.Closed)
                ic.Gps.LoadFromBlock(gc.Controller);
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


        private void LockInput()
        {
            settingsIsLocked = true;
            inputLock = 0;
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
            if (gc.Controller != null)
                gc.Controller.DampenersOverride = true;
            sb.StopCruiseWhenOutOfGrav = false;

            gc.ResetGyros();
            gc.ResetThrusters(gc.Thrusters);
            if (pc != null)
                pc.UnlockClimbPitch();
        }

        ////////////////////////////////////////////////////////
        /// FLIGHT
        ////////////////////////////////////////////////////////

        bool AlignForVerticalLand(GridContext gc)
        {
            SoftAbort(gc);
            return GravityAlignedOverride(gc, true);
        }

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
            gc.ApplyGyroCorrection(correction, 3f);

            return false;
        }

        bool AimHorizonToGps(GridContext gc, Vector3D targetGps)
        {
            Vector3D gDown = Vector3D.Normalize(pc.NaturalGravity);
            Vector3D shipUp = gc.Controller.WorldMatrix.Up;
            Vector3D shipFwd = gc.Controller.WorldMatrix.Forward;

            Vector3D levelAxis = shipUp.Cross(gDown);
            double levelErr = levelAxis.Length();

            Vector3D toTarget = targetGps - gc.Controller.GetPosition(); // toward GPS
            Vector3D targetHoriz = VectorHelper.Reject(toTarget, gDown);
            Vector3D fwdHoriz = VectorHelper.Reject(shipFwd, gDown);

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
            gc.ApplyGyroCorrection(correction, 2f);
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

            return PlanetType.EarthFamily;
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
            if (gc.ShipType != ShipType.Atmo)
                if (pc.NetDecel - 1 < 0)
                {
                    AbortShipContext(gc);
                    this.command.State = MainState.Orbit;
                    return false;
                }
            gc.Controller.DampenersOverride = false;
            GravityAlignedOverride(gc);
            return pc.GroundLevel < 1.1 * pc.StopYDist + 2 * gc.GridHeight;
        }

        bool AutoLand(GridContext gc, PhysicsContext pc, Command command)
        {
            if (gc.ShipType != ShipType.Atmo)
                if (pc.NetDecel - 0.5 < 0)
                {
                    AbortShipContext(gc);
                    this.command.State = MainState.Orbit;
                    return false;
                }
            gc.Controller.DampenersOverride = false;
            GravityAlignedOverride(gc);

            double speedFromAlt = (ic.CruiseSpeed + pc.GroundLevel) * 0.08;

            VectorHelper.MatchVerticalSpeed(gc, pc, -speedFromAlt);
            return pc.GroundLevel < 10 + 2 * gc.GridHeight;
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
