using IngameScript.Physics;
using IngameScript.Utils;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game.ModAPI.Ingame;
using VRageMath;
using IngameScript.UseCases;
using IngameScript.Domain;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        GridContext gc;
        IniContext ic;
        PhysicsContext pc;

        readonly IMyGridTerminalSystem gridTerminalSystem;
        readonly IMyProgrammableBlock me;

        Command command = Command.Empty;
        int tickCount;
        bool firstRun = true;

        readonly SpeedTimeTracker speedTimeTracker;

        //Dock Mode
        bool isDockMode = false;
        bool lastDockState = false;

        struct booleans
        {
            public bool cruiseToggle;
            public bool circumnavToggle;
            public bool circumnavCheckAltitude;
            public bool lastCheckIsOnNatGrav;
            public bool stopCruiseWhenOutOfGrav;
            public bool autoPilotToggle;
        }

        booleans b;

        double currentOverride = 0.0;
                
        public Program()
        {
            gridTerminalSystem = GridTerminalSystem;
            me = Me;

            b = new booleans();
            speedTimeTracker = new SpeedTimeTracker();

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            InicializeContexts();
        }

        private void InicializeContexts()
        {
            firstRun = true;

            gc = new GridContext(gridTerminalSystem, me);
            ic = new IniContext(gc);
            pc = new PhysicsContext(gc, ic, speedTimeTracker, Runtime.TimeSinceLastRun.TotalSeconds);

            ic.ParseIni();
            gc.IgnoreTag = ic.IgnoreTag;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            pc.ResetTransientPhysicsContext(gc, ic, speedTimeTracker, Runtime.TimeSinceLastRun.TotalSeconds);

            if (ic.IniAnyChanged
                || gc.Controller == null
                || gc.Controller.Closed
                || gc.ErrorMessage.Length > 0)
            {
                ReloadGridContext(gc, ic);
            }



            tickCount++;
            if (tickCount % 100 == 1)
            {
                ic.ParseIni();

                if (!string.IsNullOrWhiteSpace(gc.GridName) && !gc.GridName.Contains(" Grid "))
                {
                    gc.Me.CubeGrid.CustomName = gc.GridName;
                }
            }

            if (gc.ErrorMessage.Length > 0)
            {
                Echo("ErrorMessage: \n" + gc.ErrorMessage.ToString());
                return;
            }

            FlightSystems(gc, ic, pc, argument);
        }

        private void FlightSystems(GridContext gc, IniContext ic, PhysicsContext pc, string argument)
        {
            if (!string.IsNullOrEmpty(argument)) command = new Command(argument);

            StringBuilder scriptInfo = new StringBuilder();

            ScriptInfoHeader(scriptInfo);
            scriptInfo.AppendLine("");
            if (ic.AllowFlightSystems)
            {
                double timeSinceLastRun = Runtime.TimeSinceLastRun.TotalSeconds;
                pc.Transient.UpdatePhysics(gc, ic, command, tickCount);
            }

            if (ic.AllowDockMode)
            {
                bool anyConnected = GridManager.IsAnyConnectorConnected(gc);
                bool isGearlocked = gc.Gears.Exists(g => g.IsLocked);
                isDockMode = anyConnected || isGearlocked;

                if (isDockMode != lastDockState)
                {
                    DockToggle(gc, isDockMode);
                    lastDockState = isDockMode;
                    return;
                }
            }
            
            ScriptInfoBlocks(ic, scriptInfo);

            Echo(scriptInfo.ToString());
            gc.Me.GetSurface(0).WriteText(scriptInfo.ToString());

            if (isDockMode) return;

            if (ic.ControlAntennas)
            {
                gc.Antennas.ForEach(b => { if (b != null) b.Enabled = false; });
                if (gc.Antennas.Count > 0)
                {
                    var firstValid = gc.Antennas.FirstOrDefault(b => b != null && !b.Closed);
                    if (firstValid != null) firstValid.Enabled = true;
                }
            }

            MainStateEnum[] arr = {MainStateEnum.CNav, MainStateEnum.Cruise, MainStateEnum.Land, MainStateEnum.SBurn, MainStateEnum.Gps};
            List<MainStateEnum> MainStateList = new List<MainStateEnum>(arr);
            if (!ic.AllowFlightSystems && MainStateList.Contains(command.State))
            {
                return;
            }

            if (pc.Transient.Gravity > 0)
            {
                if (gc.H2CapacityPercent < ic.MinimumAcceptedFuel && gc.Controller.GetNaturalGravity().Length() / 9.81 > 0.75)
                {
                    command.State = MainStateEnum.Land;
                }
                else if (gc.H2CapacityPercent < ic.MinimumAcceptedFuel && gc.Controller.GetNaturalGravity().Length() / 9.81 < 0.75)
                {
                    command.State = MainStateEnum.Cruise;
                    command.Param.Text = "orbit";
                }
            }

            switch (command.State)
            {
                case MainStateEnum.Reload:
                    ReloadGridContext(gc, ic);
                    break;
                case MainStateEnum.Abort:
                    AbortShipContext(gc);
                    break;
                case MainStateEnum.Dock:
                    DockStateSwitch(gc, command.Param);
                    return;
                case MainStateEnum.Cruise:
                    gc.Controller.DampenersOverride = true;
                    CruiseControlStateSwitch(gc, ic, command.Param);
                    break;
                case MainStateEnum.CNav: // Circumnavigation
                    gc.Controller.DampenersOverride = true;
                    CircumNavigateStateSwitch(gc, ic, command.Param);
                    break;
                case MainStateEnum.Land: // Auto Land
                    if (pc.Transient.Gravity == 0)
                    {
                        AbortShipContext(gc);
                        return;
                    }
                    if (command.Param.AutoLandState == AutoLandStateEnum.Idle) StartLand();
                    AutoLandStateSwitch(gc, command.Param);
                    break;
                case MainStateEnum.SBurn: // Suicide Burn
                    if (command.Param.AutoLandState == AutoLandStateEnum.Idle) StartLand();
                    SuicideBurnStateSwitch(gc, command.Param);
                    break;
                case MainStateEnum.Gps:
                    Runtime.UpdateFrequency = UpdateFrequency.Update1;
                    b.autoPilotToggle = true;
                    CircumNavigateStateSwitch(gc, ic, command.Param);
                    break;
            }

            // Stop cruise control when leaves atmosphere?

            if (b.stopCruiseWhenOutOfGrav && b.lastCheckIsOnNatGrav && pc.Transient.Gravity == 0.0)
            {
                b.stopCruiseWhenOutOfGrav = b.lastCheckIsOnNatGrav = b.cruiseToggle = false;
                AbortShipContext(gc);
            }
            else
            {
                b.lastCheckIsOnNatGrav = pc.Transient.Gravity > 0.0;
            }

            // Info LCDs
            if (gc.Lcds1.Count > 0) WriteInfo();
            if (gc.Lcds2.Count > 0) WriteInfo2();
        }

        private void DockToggle(GridContext gc, bool anyConnected)
        {
            GridManager.SetBlocks(gc, !anyConnected, out isDockMode);
            GridManager.StockpileTanks(gc, anyConnected);
            if (anyConnected)
            {
                GridManager.ChargeBatteries(gc);
            }
            else
            {
                GridManager.AutoBatteries(gc);
            }
        }

        public StringBuilder ScriptInfoHeader(StringBuilder scriptInfo)
        {
            scriptInfo.AppendLine(gc.GridName);
            scriptInfo.AppendLine(new string('-', 28));
            scriptInfo.Append("State: " + command.State);

            if (!string.IsNullOrEmpty(command.Param.Text))
                scriptInfo.Append(" - " + command.Param.Text);
            if (command.Param.Number != 0)
                scriptInfo.Append(" - " + command.Param.Number);
            if (command.Param.AutoLandState != AutoLandStateEnum.Idle)
                scriptInfo.Append(" - " + command.Param.AutoLandState);

            return scriptInfo;
        }

        public StringBuilder ScriptInfoBlocks(IniContext ic, StringBuilder scriptInfo)
        {
            scriptInfo.AppendLine();
            scriptInfo.AppendLine("Toggles");
            scriptInfo.AppendLine(IniContext.FLIGHT_SYSTEMS + ": " + ic.AllowFlightSystems);
            scriptInfo.AppendLine(IniContext.DOCK_MODE + ": " + ic.AllowDockMode);
            scriptInfo.AppendLine(IniContext.CONTROL_ANTENNAS + ": " + ic.ControlAntennas);
            scriptInfo.AppendLine(IniContext.RENAME_SUBGRIDS + ": " + ic.RenameSubgrids);
            scriptInfo.AppendLine(IniContext.PAINT_SURFACES + ": " + ic.PaintSurfaces);
            scriptInfo.AppendLine();
            scriptInfo.AppendLine("Blocks");
            scriptInfo.AppendLine("Controller: " + gc.Controller.CustomName);

            if (ic.AllowFlightSystems)
            {
                scriptInfo.AppendLine("Batteries: " + gc.Batteries.Count + " | Tanks: " + gc.Tanks.Count);
                scriptInfo.AppendLine("Forward thruster: " + gc.ForwardThrusters.Count);
                scriptInfo.AppendLine("Breaking thruster: " + gc.BreakingThrusters.Count);
                scriptInfo.AppendLine("Upward thruster: " + gc.UpwardThrusters.Count);
            }

            if (ic.AllowDockMode || ic.AllowFlightSystems)
                scriptInfo.AppendLine("Gears: " + gc.Gears.Count);

            if (ic.AllowDockMode)
                scriptInfo.AppendLine("Dock Mode blocks: " + gc.ControlledBlocks.Count);

            scriptInfo.AppendLine("LCDs1: " + gc.Lcds1.Count);
            scriptInfo.AppendLine("LCDs2: " + gc.Lcds2.Count);

            return scriptInfo;
        }

        void DockStateSwitch(GridContext gc, CommandParam param)
        {
            switch (param.Text.ToLowerInvariant())
            {
                case "toggle":
                case "":
                    isDockMode = !isDockMode;
                    if (isDockMode) command.Param.Text = "on";
                    else command.Param.Text = "off";
                    break;
                case "on":
                    isDockMode = true;
                    command = Command.Empty;
                    DockToggle(gc, isDockMode);
                    break;

                case "off":
                    isDockMode = false;
                    command = Command.Empty;
                    DockToggle(gc, isDockMode);
                    break;
            }
        }

        void CruiseControlStateSwitch(GridContext gc, IniContext ic, CommandParam param)
        {
            switch (param.Text.ToLowerInvariant())
            {
                case "toggle":
                case "":
                    b.cruiseToggle = !b.cruiseToggle;
                    if (b.cruiseToggle) command.Param.Text = "on";
                    else command.Param.Text = "off";
                    break;
                case "on":
                    CruiseControl(pc.Transient.CruiseSpeed);
                    break;
                case "off":
                    AbortShipContext(gc);
                    break;
                case "orbit":
                    b.cruiseToggle = !b.cruiseToggle;
                    if (b.cruiseToggle)
                    {
                        command.Param.Text = "align";
                        b.stopCruiseWhenOutOfGrav = true;
                        CruiseControl(pc.Transient.CruiseSpeed);
                    }
                    else
                    {
                        if (b.autoPilotToggle)
                        {
                            command.State = MainStateEnum.Gps;
                            command.Param.Text = "on";
                        } else
                        {
                            AbortShipContext(gc);
                        }
                    }
                    break;
                case "align":
                    if (AlignToGravity(gc))
                    {
                        command.Param.Text = "climb";
                    }
                    break;
                case "climb":
                    if (b.circumnavCheckAltitude && pc.Transient.EffectiveAlt > ic.CnavAltitude)
                    {
                        if (b.autoPilotToggle)
                        {
                            command.State = MainStateEnum.Gps;
                            command.Param.Text = "on";
                            break;
                        }
                        else
                        {
                            AbortShipContext(gc);
                            command.State = MainStateEnum.CNav;
                        }
                    }
                    Vector3D shipUp = gc.Controller.WorldMatrix.Up;
                    AlignToVector(gc, pc.Transient.DesiredUpVector, false, shipUp);
                    CruiseControl(pc.Transient.CruiseSpeed);
                    break;
                case "glide":
                    CruiseControl(pc.Transient.CruiseSpeed);
                    if (pc.Transient.EffectiveAlt < 500 + pc.Transient.StopYDist)
                    {
                        AbortShipContext(gc);
                        command.State = MainStateEnum.Land;
                    }
                    break;
            }
        }

        void CircumNavigateStateSwitch(GridContext gc, IniContext ic, CommandParam param)
        {
            switch (param.Text.ToLowerInvariant())
            {
                case "toggle":
                case "":
                    b.circumnavToggle = !b.circumnavToggle;
                    if (b.circumnavToggle) command.Param.Text = "on";
                    else command.Param.Text = "off";
                    break;
                case "on":
                    if (pc.Transient.EffectiveAlt < ic.CnavAltitude)
                    {
                        SoftAbort(gc);
                        b.circumnavCheckAltitude = true;
                        command.State = MainStateEnum.Cruise;
                        command.Param.Text = "orbit";
                    }

                    CruiseControl(pc.Transient.CruiseSpeed);
                    if (!b.autoPilotToggle)
                    {
                        AlignToGravity(gc);
                    } 
                    else if (pc.Transient.DistanceToLine < ic.DistanceToGPS + pc.Transient.StopZDist)
                    {
                        command.State = MainStateEnum.Land;
                        b.autoPilotToggle = false;
                    } 
                    else if (AlignToGravity(gc) && b.autoPilotToggle && AimYawOnlyAt(gc, param.TargetCoordinates))
                    {
                        Runtime.UpdateFrequency = UpdateFrequency.Update10;
                    }
                    break;
                case "off":
                    AbortShipContext(gc);
                    break;
            }
        }

        void SuicideBurnStateSwitch(GridContext gc, CommandParam param)
        {
            switch (param.AutoLandState)
            {
                case AutoLandStateEnum.Idle:
                    break;

                case AutoLandStateEnum.Align:
                    SoftAbort(gc);
                    if (AlignToGravity(gc, true)) command.Param.AutoLandState = AutoLandStateEnum.Drop;
                    break;

                case AutoLandStateEnum.Drop:
                    if (SuicideBurn(gc)) command.Param.AutoLandState = AutoLandStateEnum.LockGear;
                    break;

                case AutoLandStateEnum.LockGear:
                    if (TryLock(gc)) AbortShipContext(gc);
                    break;
            }
        }

        void AutoLandStateSwitch(GridContext gc, CommandParam param)
        {
            switch (param.AutoLandState)
            {
                case AutoLandStateEnum.Idle:
                    break;

                case AutoLandStateEnum.Align:
                    SoftAbort(gc);
                    if (AlignToGravity(gc, true)) command.Param.AutoLandState = AutoLandStateEnum.Drop;
                    break;

                case AutoLandStateEnum.Drop:
                    if (AutoLand(gc)) command.Param.AutoLandState = AutoLandStateEnum.LockGear;
                    break;

                case AutoLandStateEnum.LockGear:
                    if (TryLock(gc)) AbortShipContext(gc);
                    break;
            }
        }

        bool AimYawOnlyAt(GridContext gc, Vector3D targetGps)
        {
            if (gc.Controller == null || gc.Gyros == null || gc.Gyros.Count == 0) return false;
            if (pc.Transient.NaturalGravity.LengthSquared() < 0.01) return false;

            // Yaw axis: away-from-gravity (up)
            Vector3D up = Vector3D.Normalize(pc.Transient.NaturalGravity);

            // Ship position and forward (use ShipContext.Controller forward in world)
            Vector3D shipPos = gc.Controller.GetPosition();
            Vector3D shipForward = gc.Controller.WorldMatrix.Forward;

            // Vector to target
            Vector3D toTarget = targetGps - shipPos;
            if (toTarget.LengthSquared() < 1e-6) return true; // target at ship

            // Project both onto plane perpendicular to up (horizon plane)
            Vector3D targetProj = toTarget - up * Vector3D.Dot(toTarget, up);
            if (targetProj.LengthSquared() < 1e-9) return true; // target exactly above/below — no yaw defined
            targetProj = Vector3D.Normalize(targetProj);

            Vector3D forwardProj = shipForward - up * Vector3D.Dot(shipForward, up);
            if (forwardProj.LengthSquared() < 1e-9)
            {
                // degenerate forward: pick any perp on plane
                forwardProj = Vector3D.Cross(up, Math.Abs(up.X) < 0.9 ? Vector3D.UnitX : Vector3D.UnitY);
            }
            forwardProj = Vector3D.Normalize(forwardProj);

            // Signed yaw angle from forwardProj -> targetProj around up
            double cosA = Vector3D.Dot(forwardProj, targetProj);
            cosA = Math.Max(-1.0, Math.Min(1.0, cosA));
            double angleMag = Math.Acos(cosA);
            double sign = Math.Sign(Vector3D.Dot(forwardProj.Cross(targetProj), up));
            double yawAngle = sign * angleMag; // radians; + = rotate around 'up' by right-hand rule

            // Finished if small
            const double ANGLE_EPS = 0.01; // ~0.57 deg
            if (Math.Abs(yawAngle) < ANGLE_EPS)
            {
                foreach (var g in gc.Gyros) g.GyroOverride = false;
                return true;
            }

            // Desired angular rate around up only
            const double MAX_ROT_RATE = 3.0;
            const double RESPONSE = 1.0;
            double desiredRateScalar = Math.Min(Math.Abs(yawAngle) * RESPONSE, MAX_ROT_RATE);
            Vector3D desiredRate = up * (Math.Sign(yawAngle) * desiredRateScalar);

            // PD correction (use full angular velocity but we'll only command yaw to sc.Gyros)
            Vector3D angVel = gc.Controller.GetShipVelocities().AngularVelocity;
            Vector3D correction = desiredRate - angVel;

            // Apply to sc.Gyros but zero pitch & roll commands so only yaw moves
            foreach (var g in gc.Gyros)
            {
                MatrixD inv = MatrixD.Transpose(g.WorldMatrix);
                Vector3D local = Vector3D.TransformNormal(correction, inv);

                g.GyroOverride = true;
                g.Pitch = 0f;
                // Some sc.Gyros have inverted yaw axis; if direction is reversed, invert local.Y here
                g.Yaw = (float)MathHelper.Clamp(-local.Y / 2, -3, 3);
                g.Roll = 0f;
            }

            return false;
        }

        private void ReloadGridContext(GridContext gc, IniContext ic)
        {
            InicializeContexts();

            gc.ReloadLCDs(ic.Lcd1Tag, ic.Lcd2Tag);

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

                b.lastCheckIsOnNatGrav = gc.Controller.GetNaturalGravity().LengthSquared() > 0;
                AbortShipContext(gc);
            }

            // Dock cached blocks
            if (ic.AllowDockMode)
                gc.ReloadConnectors()
                .ReloadGears()
                .ReloadTanks()
                .ReloadH2Tanks()
                .ReloadBatteries(ic.BackupBatteryTag)
                .ReloadControlledBlocks(ic.DockGroupTag)
                .ReloadOverrideGroup(ic.OverrideBlockTag);

            if (ic.ControlAntennas)
                gc.ReloadAntennas(ic.ControlAntennas);

            if (ic.RenameSubgrids)
            {
                // Get main grid (where this PB is)
                IMyCubeGrid mainGrid = gc.Me.CubeGrid;
                if (mainGrid != null)
                {
                    RenameSubgrids.GetSubgridsAndRename(gc.GridTS, mainGrid);
                }
            }

            if (ic.PaintSurfaces)
            {
                gc.ReloadSurfaces();

                foreach(IMyTextSurface surface in gc.Surfaces)
                {
                    Color backgroundColor = ColorMap.GetColorFromString(ic.BackgroundColor);
                    Color fontColor = ColorMap.GetColorFromString(ic.FontColor);

                    GridContext.PaintSurface(surface, backgroundColor, fontColor);
                }
            }
        }

        void CruiseControlOld(double cruiseSpeed)
        {
            const double SPEED_TOLERANCE = 0.5;  // m/s deadzone
            const double OVERRIDE_STEP = 0.05;   // cruise adjustment rate
            double error = cruiseSpeed - pc.Transient.ForwardVelocity;

            if (Math.Abs(error) < SPEED_TOLERANCE)
                return;

            if (error > 0)
                currentOverride += OVERRIDE_STEP;
            else
                currentOverride -= OVERRIDE_STEP;

            currentOverride = MathHelper.Clamp(currentOverride, 0f, 1f);

            // Disable braking thrusters so they don't fight cruise
            foreach (var brakingThruster in gc.BreakingThrusters)
                brakingThruster.Enabled = false;

            // Control forward thrust smoothly
            foreach (var forwardThruster in gc.ForwardThrusters)
            {
                forwardThruster.Enabled = true;
                forwardThruster.ThrustOverridePercentage = (float)currentOverride;
            }

        }

        double lastError = 0.0;
        // tune these
        const double Kp = 0.8;    // proportional gain
        const double Kd = 0.3;    // derivative gain
        const double dt = 0.1;    // seconds per tick (10 ticks/sec)

        void CruiseControl(double cruiseSpeed)
        {
            const double SPEED_TOLERANCE = 0.5;
            double error = cruiseSpeed - pc.Transient.ForwardVelocity;

            if (Math.Abs(error) < SPEED_TOLERANCE)
            {
                // small deadzone: optionally zero derivative memory to reduce oscillation
                lastError = 0.0;
                return;
            }

            // PD controller: compute change
            double derivative = (error - lastError) / dt;
            double delta = Kp * error + Kd * derivative;

            // scale delta to a reasonable per-tick change (tune Kp/Kd instead of extra scaling)
            // here we treat delta as direct override increment; divide by cruiseSpeed to normalize if needed
            currentOverride += delta * dt; // integrate change over dt

            currentOverride = Math.Max(0.0, Math.Min(1.0, currentOverride));
            lastError = error;

            foreach (var brakingThruster in gc.BreakingThrusters)
                brakingThruster.Enabled = false;

            foreach (var forwardThruster in gc.ForwardThrusters)
            {
                forwardThruster.Enabled = true;
                forwardThruster.ThrustOverridePercentage = (float)currentOverride;
            }
        }


        // -------------------- Remote control helpers --------------------
        void FlyToTarget(Vector3D target)
        {
            gc.Controller.FlightMode = FlightMode.OneWay;
            gc.Controller.ClearWaypoints();
            gc.Controller.AddWaypoint(target, "Target");
            if (!gc.Controller.IsAutoPilotEnabled)
                gc.Controller.SetAutoPilotEnabled(true);
        }

        void DisableRemoteControl()
        {
            if (gc.Controller.IsAutoPilotEnabled)
                gc.Controller.SetAutoPilotEnabled(false);
            gc.Controller.ClearWaypoints();
        }

        Vector3D TryGetPlanetPosition(IMyShipController controller)
        {
            Vector3D shipPos = controller.GetPosition();
            Vector3D planetCenter = new Vector3D();

            // Get planet center
            controller.TryGetPlanetPosition(out planetCenter);

            return planetCenter;
        }

        void WriteInfo()
        {
            // Output
            StringBuilder stringBuilder = new StringBuilder();

            ScriptInfoHeader(stringBuilder);
            stringBuilder.AppendLine("\n");

            stringBuilder.AppendLine($"Mass: {pc.Transient.Mass.PhysicalMass / 1000:0.0} t");
            stringBuilder.AppendLine($"Empty Mass: {mass.BaseMass / 1000:0.0} t");

            stringBuilder.AppendLine($"H2: {gc.H2CapacityPercent:0}% - {pc.Transient.H2Time}");

            stringBuilder.AppendLine($"Bat:  {batStored / batCap * 100:0}% - {batTime}");

            foreach (IMyTextSurface lcd1 in gc.Lcds1)
                lcd1.WriteText(stringBuilder.ToString());
        }

        void WriteInfo2()
        {
            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.AppendLine(gc.GridName);
            stringBuilder.AppendLine(new string('-', 28));

            if (pc.Transient.Gravity > 0)
            {
                stringBuilder.AppendLine($"Ground level : {pc.Transient.Alt:F1} m");
                stringBuilder.AppendLine($"Rate of climb: {pc.Transient.ClimbRate:F1} m/s");
                stringBuilder.AppendLine($"Accel: {pc.Transient.Accel.Length() / 9.81:F1} g");
                stringBuilder.AppendLine($"Stop Y: {pc.Transient.StopZDist:F1} m | {pc.Transient.TimeToStopY:F1} s");
            }
            stringBuilder.AppendLine($"Stop Z: {pc.Transient.StopZDist:F1} m | {pc.Transient.TimeToStopZ:F1} s");
            stringBuilder.AppendLine($"maxZDecel: {pc.Transient.MaxZDecel:F1} s");

            if (b.autoPilotToggle)
            {
                stringBuilder.AppendLine($"\nETA: {UtilsHelpder.FormatTime(pc.Transient.TimeToDistanceSmoothed(pc.Transient.DistanceToLine, Runtime.LastRunTimeMs, speedTimeTracker))}");
            }
            else if (command.State == MainStateEnum.Land || command.State == MainStateEnum.SBurn)
            {
                stringBuilder.AppendLine($"Gravity: {pc.Transient.Gravity:F1} m²/s");
                stringBuilder.AppendLine($"Max up accel: {pc.Transient.MaxYDecel:F1} m²/s");
                stringBuilder.AppendLine($"TTI: {pc.Transient.TimeToImpact:F1} s");
            }
            else
            {
                stringBuilder.AppendLine($"Longitudinal v: {pc.Transient.ForwardVelocity:F1} m/s");
                stringBuilder.AppendLine($"Lateral v: {pc.Transient.RightVelocity:F1} m/s");
                stringBuilder.AppendLine($"Vertical v: {pc.Transient.UpVelocity:F1} m/s");
            }

            stringBuilder.AppendLine();

            foreach (IMyTextSurface lcd2 in gc.Lcds2)
                lcd2.WriteText(stringBuilder.ToString());
        }

        ////////////////////////////////////////////////////////
        /// SETUP
        ////////////////////////////////////////////////////////

        

        void StartLand()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            command.Param.AutoLandState = AutoLandStateEnum.Align;
        }

        void AbortShipContext(GridContext gc)
        {
            b = new booleans();

            command = Command.Empty;

            gc.Controller.DampenersOverride = true;
            b.autoPilotToggle = false;

            Runtime.UpdateFrequency = UpdateFrequency.Update10;

            tickCount = 0;
            GridManager.ResetGyros(gc);
            GridManager.ResetThrusters(gc);
        }

        void SoftAbort(GridContext gc)
        {
            gc.Controller.DampenersOverride = true;
            b.stopCruiseWhenOutOfGrav = false;

            GridManager.ResetGyros(gc);
            GridManager.ResetThrusters(gc);
        }

        ////////////////////////////////////////////////////////
        /// FLIGHT
        ////////////////////////////////////////////////////////

        bool AlignToGravity(GridContext gc)
        {
            return AlignToGravity(gc, false);
        }

        bool AlignToGravity(GridContext sc, bool checkSpeed)
        {
            Vector3D desiredUp = Vector3D.Normalize(pc.Transient.NaturalGravity);
            return AlignToVector(sc, checkSpeed, desiredUp);
        }

        bool AlignToVector(GridContext gc, bool checkSpeed, Vector3D desiredUpVector)
        {
            Vector3D shipUp = gc.Controller.WorldMatrix.Up;

            return AlignToVector(gc, shipUp, checkSpeed, desiredUpVector);
        }

        bool AlignToVector(GridContext gc, Vector3D shipUp, bool checkSpeed, Vector3D desiredUpVector)
        {
            if (pc.Transient.NaturalGravity.LengthSquared() < 0.01)
                return false;

            Vector3D axis = shipUp.Cross(desiredUpVector);
            double angle = axis.Length();

            if (angle < 0.005 && (checkSpeed ? IsStopped() : true))
            {
                foreach (var g in gc.Gyros)
                    g.GyroOverride = false;

                return true;
            }

            axis /= angle;

            Vector3D angVel = gc.Controller.GetShipVelocities().AngularVelocity;

            //-----------------------------------
            // ⭐ ANGULAR RATE LIMIT
            //-----------------------------------

            const double MAX_ROT_RATE = 0.6; // radians/sec
            const double RESPONSE = 1.0;     // lower = smoother

            Vector3D desiredRate = axis * Math.Min(angle * RESPONSE, MAX_ROT_RATE);

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

        bool IsStopped(double threshold = 0.1)
        {
            return threshold > pc.Transient.UpVelocity && threshold >= Math.Abs(pc.Transient.ForwardVelocity) && threshold >= Math.Abs(pc.Transient.RightVelocity);
        }

        ////////////////////////////////////////////////////////
        /// SAFE DEscENT
        ////////////////////////////////////////////////////////
        bool SuicideBurn(GridContext gc)
        {
            if (pc.Transient.NetDecel - 1 < 0)
            {
                AbortShipContext(gc);
                command.State = MainStateEnum.Cruise;
                command.Param.Text = "orbit";
            }

            gc.Controller.DampenersOverride = false;
            AlignToGravity(gc);
            pc.Transient.MatchVerticalSpeed(gc, -104);
            return pc.Transient.EffectiveAlt < 1.1 * pc.Transient.StopYDist + gc.GridHeight;
        }

        bool AutoLand(GridContext gc)
        {
            if (pc.Transient.NetDecel - 0.5 < 0)
            {
                AbortShipContext(gc);
                command.State = MainStateEnum.Cruise;
                command.Param.Text = "orbit";
            }

            gc.Controller.DampenersOverride = false;
            AlignToGravity(gc);

            double speedFromAlt = (100 + pc.Transient.Alt) * 0.08;
            double speedFromAccel = 20 * pc.Transient.NetDecel;
            double speedMin = -Math.Min(speedFromAlt, speedFromAccel);

            if (speedMin > -104) pc.Transient.MatchVerticalSpeed(gc, speedMin);
            return pc.Transient.EffectiveAlt < 10 + 2 * gc.GridHeight;
        }

        bool TryLock(GridContext gc)
        {
            AlignToGravity(gc);
            pc.Transient.MatchVerticalSpeed(gc, -2);
            gc.Controller.DampenersOverride = true;

            foreach (var g in gc.Gears)
                g.Lock();

            return gc.Gears.Exists(g => g.IsLocked);
        }
    }
}
