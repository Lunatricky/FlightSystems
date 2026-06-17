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
using VRage.Game.GUI.TextPanel;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        GridContext gc;
        IniContext ic;
        PhysicsContext pc;
        SpeedTimeTracker stt;

        readonly IMyGridTerminalSystem gridTerminalSystem;
        readonly IMyProgrammableBlock me;

        Command command = Command.Empty;
        int tickCount;
        double timeSinceLastRun;
        StringBuilder scriptInfo;
        string argument;

        //Dock Mode
        bool isDockMode;
        bool lastDockState;
        Vector3D desiredUp;

        struct Booleans
        {
            public bool cruiseToggle;
            public bool circumnavToggle;
            public bool circumnavCheckAltitude;
            public bool lastCheckIsOnNatGrav;
            public bool stopCruiseWhenOutOfGrav;
            public bool autoPilotToggle;
        }

        Booleans b;
                
        public Program()
        {
            scriptInfo = new StringBuilder();
            gridTerminalSystem = GridTerminalSystem;
            me = Me;

            b = new Booleans();
            stt = new SpeedTimeTracker();

            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            ReloadGridContext(ref gc, ref ic);
        }

        private void InicializeContexts()
        {
            gc = new GridContext(gridTerminalSystem, me);
            ic = new IniContext(gc);
            pc = new PhysicsContext(gc, stt, command, timeSinceLastRun);

            ic.ParseIni();
            gc.IgnoreTag = ic.IgnoreTag;
        }

        int tick = 0;

        public void Main(string argument)
        {
            if (gc.ErrorMessage.Length > 0)
            {
                Echo("ErrorMessage: \n" + gc.ErrorMessage.ToString());
                return;
            }

            timeSinceLastRun = Runtime.TimeSinceLastRun.TotalSeconds;

            if (!string.IsNullOrEmpty(argument)) this.argument = argument;

            Echo(scriptInfo.ToString());
            gc.Me.GetSurface(0).WriteText(scriptInfo.ToString());

            tickCount++;
            if (tickCount % 1000 == 1)
            {
                ic.ParseIni();

                if (!string.IsNullOrWhiteSpace(gc.GridName) && !gc.GridName.Contains(" Grid "))
                {
                    gc.Me.CubeGrid.CustomName = gc.GridName;
                }
            }

            if (ic.IniAnyChanged || gc.Controller == null || gc.Controller.Closed)
            {
                ReloadGridContext(ref gc, ref ic);
                tick = 0;
                return;
            }

            switch (tick % 3)
            {
                case 0:
                    pc.NewRun(timeSinceLastRun);
                    scriptInfo = ScriptInfo();
                    break;
                case 1:
                    FlightSystems(gc, ic, pc);
                    break;
                case 2:
                    if (gc.Lcds1.Count > 0) LCD1Sprite();
                    if (gc.Lcds2.Count > 0) LCD2Sprite();
                    pc.CacheValues();
                    break;
            }

            tick++;

            Echo(GetRuntimeInfo());
        }

        private StringBuilder ScriptInfo()
        {
            StringBuilder scriptInfo = new StringBuilder();
            ScriptInfoHeader(scriptInfo);
            scriptInfo.AppendLine("");
            ScriptInfoBlocks(ic, scriptInfo);
            return scriptInfo;
        }

        private double tickCounter = 0;
        private double maxRuntimeMs = 0;

        private String GetRuntimeInfo()
        {
            tickCounter++;

            if (tickCounter % 20 == 1)
            {
                maxRuntimeMs = 0;
            }

            StringBuilder m_echoBuilder = new StringBuilder(512);
            m_echoBuilder.Append($"Runtime: {Math.Round(Runtime.LastRunTimeMs, 5)} Ms\n");

            double newRuntimeMs = Math.Round(Runtime.LastRunTimeMs, 5);
            maxRuntimeMs = Math.Max(newRuntimeMs, maxRuntimeMs);

            m_echoBuilder.Append($"Max Runtime: {maxRuntimeMs} Ms\n");
            m_echoBuilder.Append($"Instruction Count: {Runtime.CurrentInstructionCount}\n");
            m_echoBuilder.Append($"Complexity: {Math.Round((double)Runtime.CurrentInstructionCount / Runtime.MaxInstructionCount, 5)}%\n");
            return m_echoBuilder.ToString();
        }

        private void FlightSystems(GridContext gc, IniContext ic, PhysicsContext pc)
        {
            if (!string.IsNullOrEmpty(argument))
            {
                command = new Command(argument);
                argument = "";
            }
                        
            if (ic.AllowDockMode)
            {
                bool anyConnected = GridContext.IsAnyConnectorConnected(gc);
                bool isGearlocked = gc.Gears.Exists(g => g.IsLocked);
                isDockMode = anyConnected || isGearlocked;

                if (isDockMode != lastDockState)
                {
                    AbortShipContext(gc);
                    DockToggle(gc, isDockMode);
                    lastDockState = isDockMode;
                    return;
                }
            }

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

            MainState[] arr = {MainState.CNav, MainState.Cruise, MainState.Land, MainState.SBurn, MainState.Gps};
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
                    command.State = MainState.Cruise;
                    command.Param.Text = "orbit";
                }
            }

            switch (command.State)
            {
                case MainState.Reload:
                    ReloadGridContext(ref gc, ref ic);
                    break;
                case MainState.Abort:
                    AbortShipContext(gc);
                    break;
                case MainState.Dock:
                    DockStateSwitch(gc, command.Param);
                    return;
                case MainState.Cruise:
                    gc.Controller.DampenersOverride = true;
                    CruiseControlStateSwitch(gc, ic, command.Param);
                    break;
                case MainState.CNav: // Circumnavigation
                    gc.Controller.DampenersOverride = true;
                    CircumNavigateStateSwitch(gc, ic, command.Param);
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
                case MainState.Gps:
                    b.autoPilotToggle = true;
                    CircumNavigateStateSwitch(gc, ic, command.Param);
                    break;
            }

            // Stop cruise control when leaves gravity well
            if (b.stopCruiseWhenOutOfGrav && b.lastCheckIsOnNatGrav && pc.Gravity == 0.0)
            {
                b.stopCruiseWhenOutOfGrav = b.lastCheckIsOnNatGrav = b.cruiseToggle = false;
                AbortShipContext(gc);
            }
            else
            {
                b.lastCheckIsOnNatGrav = pc.Gravity > 0.0;
            }
        }

        private void DockToggle(GridContext gc, bool anyConnected)
        {
            GridContext.SetBlocks(gc, !anyConnected, out isDockMode);
            GridContext.StockpileTanks(gc, anyConnected);
            if (anyConnected)
            {
                GridContext.ChargeBatteries(gc);
            }
            else
            {
                GridContext.AutoBatteries(gc);
            }
        }

        public StringBuilder ScriptInfoHeader(StringBuilder scriptInfo)
        {
            scriptInfo.AppendLine("Flight systems - " + gc.GridName);
            scriptInfo.Append("    State: " + command.State);

            if (!string.IsNullOrEmpty(command.Param.Text))
                scriptInfo.Append(" - " + command.Param.Text);
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
            scriptInfo.AppendLine("    " + IniContext.LOW_FUEL_LAND + ": " + ic.AllowLowFuelLand);
            scriptInfo.AppendLine("    " + IniContext.DOCK_MODE + ": " + ic.AllowDockMode);
            scriptInfo.AppendLine("    " + IniContext.CONTROL_ANTENNAS + ": " + ic.ControlAntennas);
            scriptInfo.AppendLine("    " + IniContext.RENAME_SUBGRIDS + ": " + ic.RenameSubgrids);
            scriptInfo.AppendLine("    " + IniContext.PAINT_SURFACES + ": " + ic.PaintSurfaces);
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
            scriptInfo.AppendLine("    Surfaces: " + gc.Surfaces.Count);

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
            double CruiseSpeed = (command.Param.Number > 0 ? command.Param.Number : ic.MaxSpeed);

            switch (param.Text.ToLowerInvariant())
            {
                case "toggle":
                case "":
                    b.cruiseToggle = !b.cruiseToggle;
                    if (b.cruiseToggle) command.Param.Text = "on";
                    else command.Param.Text = "off";
                    break;
                case "on":
                    CruiseControl(CruiseSpeed, timeSinceLastRun);
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
                        CruiseControl(CruiseSpeed, timeSinceLastRun);
                    }
                    else
                    {
                        if (b.autoPilotToggle)
                        {
                            command.State = MainState.Gps;
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
                        desiredUp = pc.DesiredUpVector;
                    }
                    break;
                case "climb":
                    if (b.circumnavCheckAltitude && pc.EffectiveAlt > ic.CnavAltitude)
                    {
                        if (b.autoPilotToggle)
                        {
                            command.State = MainState.Gps;
                            command.Param.Text = "on";
                            break;
                        }
                        else
                        {
                            AbortShipContext(gc);
                            command.State = MainState.CNav;
                        }
                    }
                    Vector3D shipUp = gc.Controller.WorldMatrix.Up;
                    AlignToVector(gc, shipUp, false, desiredUp);
                    CruiseControl(CruiseSpeed, timeSinceLastRun);
                    break;
                case "glide":
                    CruiseControl(CruiseSpeed, timeSinceLastRun);
                    if (pc.EffectiveAlt < 500 + pc.StopYDist)
                    {
                        AbortShipContext(gc);
                        command.State = MainState.Land;
                    }
                    break;
            }
        }

        void CircumNavigateStateSwitch(GridContext gc, IniContext ic, CommandParam param)
        {
            double CruiseSpeed = (command.Param.Number > 0 ? command.Param.Number : ic.MaxSpeed);
            switch (param.Text.ToLowerInvariant())
            {
                case "toggle":
                case "":
                    b.circumnavToggle = !b.circumnavToggle;
                    if (b.circumnavToggle) command.Param.Text = "on";
                    else command.Param.Text = "off";
                    break;
                case "on":
                    if (pc.EffectiveAlt < ic.CnavAltitude)
                    {
                        SoftAbort(gc);
                        b.circumnavCheckAltitude = true;
                        command.State = MainState.Cruise;
                        command.Param.Text = "orbit";
                    }

                    CruiseControl(CruiseSpeed, timeSinceLastRun);
                    if (!b.autoPilotToggle)
                    {
                        AlignToGravity(gc);
                    }
                    else if (pc.DistanceToLine < ic.DistanceToGPS + pc.StopZDist)
                    {
                        command.State = MainState.Land;
                        b.autoPilotToggle = false;
                    }
                    else if (AlignToGravity(gc) && b.autoPilotToggle && AimYawOnlyAt(gc, param.TargetCoordinates)) ;
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
                case AutoLandState.Idle:
                    break;

                case AutoLandState.Align:
                    SoftAbort(gc);
                    if (AlignToGravity(gc, true)) command.Param.AutoLandState = AutoLandState.Drop;
                    break;

                case AutoLandState.Drop:
                    if (SuicideBurn(gc)) command.Param.AutoLandState = AutoLandState.LockGear;
                    break;

                case AutoLandState.LockGear:
                    if (pc.UpVelocity > -(ic.MaxSpeed / 4) && 4 * pc.EffectiveAlt > 1 + pc.StopZDist)
                    {
                        command.State = MainState.Land;
                        command.Param.AutoLandState = AutoLandState.Drop;
                    }
                    else if (TryLock(gc)) AbortShipContext(gc);
                    break;
            }
        }

        void AutoLandStateSwitch(GridContext gc, CommandParam param)
        {
            switch (param.AutoLandState)
            {
                case AutoLandState.Idle:
                    break;

                case AutoLandState.Align:
                    SoftAbort(gc);
                    if (AlignToGravity(gc, true)) command.Param.AutoLandState = AutoLandState.Drop;
                    break;

                case AutoLandState.Drop:
                    if (AutoLand(gc)) command.Param.AutoLandState = AutoLandState.LockGear;
                    break;

                case AutoLandState.LockGear:
                    if (TryLock(gc)) AbortShipContext(gc);
                    break;
            }
        }

        bool AimYawOnlyAt(GridContext gc, Vector3D targetGps)
        {
            if (gc.Controller == null || gc.Gyros == null || gc.Gyros.Count == 0) return false;
            if (pc.NaturalGravity.LengthSquared() < 0.01) return false;

            // Yaw axis: away-from-gravity (up)
            Vector3D up = Vector3D.Normalize(pc.NaturalGravity);

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

        private void ReloadGridContext(ref GridContext gc, ref IniContext ic)
        {
            InicializeContexts();

            gc.ReloadLCDs(ic.Lcd1Tag, ic.Lcd2Tag)
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

                GridContext.ResetThrusters(gc.Thrusters);
                GridContext.ResetGyros(gc.Gyros);

                b.lastCheckIsOnNatGrav = gc.Controller.GetNaturalGravity().LengthSquared() > 0;
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
        const double OVERRIDE_STEP = 0.01;     // max absolute change per tick (smoothness)
        const double MAX_INTEGRAL = 1.0;       // anti-windup clamp

        void CruiseControl(double cruiseSpeed, double dt)
        {
            if (pc.ForwardVelocity > ic.MaxSpeed)
            {
                GridContext.ResetThrusters(gc.Thrusters);
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
            double step = OVERRIDE_STEP; // fixed per tick step (tune for desired smoothness)

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

            // Apply thrusters: enable brake thrusters only when brake significant
            bool useBrakes = currentBrake > 1e-4;
            foreach (var bt in gc.BreakingThrusters)
            {
                bt.Enabled = useBrakes;
                bt.ThrustOverridePercentage = (float)currentBrake;
            }

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
            Sprites spt = new Sprites();
            spt.Add(gc.GridName);

            StringBuilder state = new StringBuilder();
            state.Append("State: " + command.State);

            if (!string.IsNullOrEmpty(command.Param.Text))
                state.Append(" - " + command.Param.Text);
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
                spt.Add($"H2: {pc.H2Cache.Percent:0}% - {pc.H2Cache.Time}", ColorMap.GetStringFromColor(color));
            else spt.Add($"H2: {pc.H2Cache.Percent:0}% - {pc.H2Cache.Time}");

            color = pc.PrevBatFill < pc.BatCache.Filled ? Color.LightBlue
                : pc.BatCache.Percent < ic.MinimumAcceptedFuel / 2 ? Color.DarkRed
                : pc.BatCache.Percent < ic.MinimumAcceptedFuel ? Color.DarkOrange
                : new Color();

            if (!color.Equals(new Color()))
                spt.Add($"Bat:  {pc.BatCache.Percent:0}% - {pc.BatCache.Time}", ColorMap.GetStringFromColor(color));
            else spt.Add($"Bat:  {pc.BatCache.Percent:0}% - {pc.BatCache.Time}");

            DrawSprites(spt, gc.Lcds1);
        }

        void LCD2Sprite()
        {
            Sprites spt = new Sprites();

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
                    spt.Add($"Ground level: {pc.GroundLevel:F1} m", ColorMap.GetStringFromColor(color));
                    spt.Add($"Rate of climb: {pc.ClimbRate:F1} m/s");
                    spt.Add($"Stop Y: {pc.StopYDist:F1} m | {pc.TimeToStopY:F1} s", ColorMap.GetStringFromColor(color));
                }
                else
                {
                    spt.Add($"Ground level: {pc.GroundLevel:F1} m");
                    spt.Add($"Rate of climb: {pc.ClimbRate:F1} m/s");
                    spt.Add($"Stop Y: {pc.StopYDist:F1} m | {pc.TimeToStopY:F1} s");
                }
            }

            spt.Add($"Stop Z: {pc.StopZDist:F1} m | {pc.TimeToStopZ:F1} s");
            spt.Add($"Accel: {pc.Accel.Length() / 9.81:F1} g");

            if (b.autoPilotToggle)
            {
                spt.Add($"\nETA: {UtilsHelpder.FormatTime(pc.TimeToDistanceSmoothed)}");
            }
            else if (command.State == MainState.Land || command.State == MainState.SBurn)
            {
                spt.Add($"TTI: {pc.TimeToImpact:F1} s");
            }
            else
            {
                spt.Add($"Longitudinal v: {pc.ForwardVelocity:F1} m/s");
                spt.Add($"Lateral v: {pc.RightVelocity:F1} m/s");
                spt.Add($"Vertical v: {pc.UpVelocity:F1} m/s");
            }

            foreach (IMyTextSurface lcd in gc.Lcds2)
            {
                DrawSprites(spt, gc.Lcds2);
            }
        }

        private void DrawSprites(Sprites spt, List<IMyTextSurface> surfaces)
        {
            foreach (IMyTextSurface surface in surfaces)
            {
                Color backgroundColor;
                if (ic.TransparentLCD && surface.Name.ToLower().Contains("transparent")) backgroundColor = Color.Black;
                else backgroundColor = ColorMap.GetColorFromString(ic.SpriteBackgroundColor);
                spt.DrawInfoPanel(gc.IsLG, surface, ColorMap.GetColorFromString(ic.SpriteFontColor), backgroundColor);
            }
        }

        void AbortShipContext(GridContext gc)
        {
            b = new Booleans();

            command = Command.Empty;

            gc.Controller.DampenersOverride = true;
            b.autoPilotToggle = false;

            tickCount = 0;
            GridContext.ResetGyros(gc.Gyros);
            GridContext.ResetThrusters(gc.Thrusters);
        }

        void SoftAbort(GridContext gc)
        {
            gc.Controller.DampenersOverride = true;
            b.stopCruiseWhenOutOfGrav = false;

            GridContext.ResetGyros(gc.Gyros);
            GridContext.ResetThrusters(gc.Thrusters);
        }

        ////////////////////////////////////////////////////////
        /// FLIGHT
        ////////////////////////////////////////////////////////

        bool AlignToGravity(GridContext gc)
        {
            return AlignToGravity(gc, false);
        }

        bool AlignToGravity(GridContext gc, bool checkSpeed)
        {
            Vector3D shipUp = gc.Controller.WorldMatrix.Up;

            return AlignToVector(gc, shipUp, checkSpeed, Vector3D.Normalize(pc.NaturalGravity));
        }

        bool AlignToVector(GridContext gc, Vector3D shipUp, bool checkSpeed, Vector3D desiredUpVector)
        {
            if (pc.Gravity < 0.01)
                return false;

            Vector3D axis = shipUp.Cross(desiredUpVector);
            double angle = axis.Length();

            if (angle < 0.005 && (!checkSpeed || pc.IsStopped))
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

        ////////////////////////////////////////////////////////
        /// SAFE DEscENT
        ////////////////////////////////////////////////////////
        bool SuicideBurn(GridContext gc)
        {
            if (pc.NetDecel - 1 < 0)
            {
                AbortShipContext(gc);
                command.State = MainState.Cruise;
                command.Param.Text = "orbit";
            }

            gc.Controller.DampenersOverride = false;
            AlignToGravity(gc);
            VectorHelper.MatchVerticalSpeed(gc, pc, -ic.MaxSpeed - 10 );
            return pc.EffectiveAlt < 1.3 * pc.StopYDist + gc.GridHeight;
        }

        bool AutoLand(GridContext gc)
        {
            if (pc.NetDecel - 0.5 < 0)
            {
                AbortShipContext(gc);
                command.State = MainState.Cruise;
                command.Param.Text = "orbit";
            }

            gc.Controller.DampenersOverride = false;
            AlignToGravity(gc);

            double speedFromAlt = (100 + pc.GroundLevel) * 0.08;
            double speedFromAccel = 20 * pc.NetDecel;
            double speedMin = -Math.Min(speedFromAlt, speedFromAccel);

            if (speedMin > -104) VectorHelper.MatchVerticalSpeed(gc, pc, speedMin);
            return pc.EffectiveAlt < 10 + 2 * gc.GridHeight;
        }

        bool TryLock(GridContext gc)
        {
            AlignToGravity(gc);
            VectorHelper.MatchVerticalSpeed(gc, pc, -2);
            gc.Controller.DampenersOverride = true;

            foreach (var g in gc.Gears)
                g.Lock();

            return gc.Gears.Exists(g => g.IsLocked);
        }
    }
}
