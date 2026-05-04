using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // ================= CONFIG =================
        readonly MyIni ini = new MyIni();
        SC sc;
        readonly IMyGridTerminalSystem gridTerminalSystem;
        readonly IMyProgrammableBlock me;

        /*
         * R e a d m e
         * -----------
         * 
         * In this file you can include any instructions or other comments you want to have injected onto the 
         * top of your final script. You can safely delete this file if you do not want any such comments.
         */

        // Descent()
        int tickCount;


        double alt;
        double effectiveAlt;
        double stopYDist;
        double stopZDist;
        double mass;
        double cruiseSpeed;
        double climbRate;
        double vEffectiveYSpeed;
        double vEffectiveZSpeed;
        double maxYDecel;
        double maxZDecel;
        double gravity;
        double oldGravity;
        double gravityRatio = 1;
        Vector3D naturalGrav;
        double timeToImpact;
        double timeToStopY;
        double timeToStopZ;
        double thrust = 0;
        double forwardVelocity;
        double rightVelocity;
        double upVelocity;
        double netDecel;
        double maxThrustUp;
        double distanceToLine;
        Vector3D desiredUpVector;


        double prevSmoothedSpeed = 0.0; // persistent between ticks
        const double ALPHA = 0.2;       // 0.1-0.3 is reasonable; lower = smoother/slower response
        const int SpeedTimeTrackerMaxSize = 100;



        // Cruise Control
        // Forward Speed Limiter + Cruise Control Fields

        const double SPEED_TOLERANCE = 0.5;  // m/s deadzone
        const double OVERRIDE_STEP = 0.05;   // cruise adjustment rate
        double MinSpeed = 20; // m/s

        readonly SpeedTimeTracker speedTimeTracker;

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

        // Circunavigation
        // CNav fields


        // Docking Routine
        // Connector-based Function Block Shutdown Fields
        
        const string SectionName = "Flight Systems";

        string INI_GRID_NAME = "Grid Name";
        string INI_FS_GROUP_TAG = "Group Tag";
        string INI_OVERRIDE_BLOCKS_TAG = "Override Blocks Tag";
        string INI_IGNORE_TAG = "Ignore Tag";
        string INI_LCD1_TAG = "LCD 1";
        string INI_LCD2_TAG = "LCD 2";
        string MAX_SPEED = "Max Speed";
        string CNAV_ALTITUDE = "Cnav Altitude";
        string DISTANCE_TO_GPS = "Distance to GPS";
        string MINIMUM_ACCEPTED_FUEL = "Minimum Accepted Fuel";
        string FLIGHT_SYSTEMS = "Flight Systems";
        string DOCK_MODE = "Dock Mode";
        string CONTROL_ANTENNAS = "Control Antennas";
        string RENAME_SUBGRIDS = "Rename Subgrids";

        string __fsGroupTag = "Flight Systems";
        string __overrideBlockTag = "[FS_override]";
        string __ignoreTag = "[FS_ignore]";
        string __Lcd1Tag = "[FS_LCD1]";
        string __Lcd2Tag = "[FS_LCD2]";
        double __maxSpeed = 99; // m/s
        double __cnavAltitude = 1000; // m
        double __distanceToGPS = 500; // m
        double __minimumAcceptedFuel = 20; //%
        bool __allowFlightSystems = true;
        bool __allowDockMode = false;
        bool __controlAntennas = false;
        bool __renameSubgrids = false;


        bool isDockMode = false;
        bool lastDockState = false;

        // Info LCDs
        private const string BACKUP_TAG = "backup";

        Vector3D lastVelocity;
        double lastH2Fill = 0;
        bool firstRun = true;
        // Global config instance

        class Command
        {
            public MainStateEnum State { get; set; }
            public CommandParam Param { get; set; }

            public Command(MainStateEnum cmd, CommandParam p)
            {
                if (Enum.IsDefined(typeof(MainStateEnum), cmd)) State = cmd;
                Param = p;
            }

            public static Command Empty => new Command(MainStateEnum.Idle, CommandParam.Empty);
        }

        class CommandParam
        {
            public ParamType Type;
            public AutoLandStateEnum AutoLandState = AutoLandStateEnum.Idle;

            public double Number;
            public string Text = "";
            public Vector3D TargetCoordinates = new Vector3D();

            // ────────────────────────────────────────────────
            // Constructors — one per type
            // ────────────────────────────────────────────────

            public CommandParam(double n)
            {
                Type = ParamType.Number;
                Number = n;
            }

            public CommandParam(string t)
            {
                Type = ParamType.Text;
                Text = t ?? "";
            }
            public CommandParam(Vector3D targetCoordinates)
            {
                Type = ParamType.Vector3D;
                TargetCoordinates = targetCoordinates;
            }

            // Empty
            public static CommandParam Empty => new CommandParam(null);
        }

        public Program()
        {
            gridTerminalSystem = GridTerminalSystem; 
            me = Me;

            b = new booleans();
            speedTimeTracker = new SpeedTimeTracker(SpeedTimeTrackerMaxSize);
            
            Runtime.UpdateFrequency = UpdateFrequency.Update10;

            Reload();

            if (__allowFlightSystems)
            {
                Abort();

                // Info LCDscontroller
                if (sc.Lcds1.Count > 0) WriteInfo();
                if (sc.Lcds2.Count > 0) WriteInfo2();
            }

            if (__allowDockMode)
            {
                bool anyConnected = IsAnyConnectorConnected();
                bool isGearLocked = sc.Gears.Exists(g => g.IsLocked);
                isDockMode = anyConnected || isGearLocked;
                DockToggle(isDockMode);
            }
        }
        
        Command command = Command.Empty;

        public void Main(string argument, UpdateType updateSource)
        {
            tickCount++;
            if (tickCount % 100 == 1)
            {

                ini.Clear();

                if (!ini.TryParse(sc.Me.CustomData)) return;

                string sectionName = SectionName;

                if (!ini.ContainsSection(sectionName))
                {
                    ini.AddSection(sectionName);
                }

                //TODO find a solution without having to read Ini 2x
                bool allowFlightSystems = ini.Get(SectionName, FLIGHT_SYSTEMS).ToBoolean(__allowFlightSystems);
                bool allowDockMode = ini.Get(SectionName, DOCK_MODE).ToBoolean(__allowDockMode);
                bool controlAntennas = ini.Get(SectionName, CONTROL_ANTENNAS).ToBoolean(__controlAntennas);
                bool renameSubgrids = ini.Get(SectionName, RENAME_SUBGRIDS).ToBoolean(__renameSubgrids);

                ParseIni();

                if (!string.IsNullOrWhiteSpace(sc.GridName) && !sc.GridName.Contains(" Grid "))
                {
                    sc.Me.CubeGrid.CustomName = sc.GridName;
                }
                
                if (allowFlightSystems != __allowFlightSystems
                    || allowDockMode != __allowDockMode
                    || controlAntennas != __controlAntennas
                    || renameSubgrids != __renameSubgrids)
                {
                    Reload();
                }
            }

            FlightSystems(argument);
        }

        private void FlightSystems(string argument)
        {
            if (!string.IsNullOrEmpty(argument)) command = ParseCommand(argument);

            StringBuilder scriptInfo = new StringBuilder();

            ScriptInfoHeader(scriptInfo);
            scriptInfo.AppendLine("");
            if (__allowFlightSystems)
            {
                UpdatePhysics();
                ScriptInfoPhysics(scriptInfo);
            }

            if (__allowDockMode)
            {
                bool anyConnected = IsAnyConnectorConnected();
                bool isGearlocked = sc.Gears.Exists(g => g.IsLocked);
                isDockMode = anyConnected || isGearlocked;

                if (isDockMode != lastDockState)
                {
                    DockToggle(isDockMode);
                    lastDockState = isDockMode;
                    return;
                }
            }
            
            ScriptInfoBlocks(scriptInfo);

            Echo(scriptInfo.ToString());
            sc.Me.GetSurface(0).WriteText(scriptInfo.ToString());

            if (isDockMode) return;

            if (__controlAntennas)
            {
                sc.Antennas.ForEach(b => { if (b != null) b.Enabled = false; });
                if (sc.Antennas.Count > 0)
                {
                    var firstValid = sc.Antennas.FirstOrDefault(b => b != null && !b.Closed);
                    if (firstValid != null) firstValid.Enabled = true;
                }
            }

            MainStateEnum[] arr = {MainStateEnum.CNav, MainStateEnum.Cruise, MainStateEnum.Land, MainStateEnum.SBurn, MainStateEnum.Gps};
            List<MainStateEnum> MainStateList = new List<MainStateEnum>(arr);
            if (!__allowFlightSystems && MainStateList.Contains(command.State))
            {
                WriteEmpty();
                return;
            }

            if (gravity > 0)
            {
                if (sc.H2CapacityPercent < __minimumAcceptedFuel && sc.Controller.GetNaturalGravity().Length() / 9.81 > 0.75)
                {
                    command.State = MainStateEnum.Land;
                }
                else if (sc.H2CapacityPercent < __minimumAcceptedFuel && sc.Controller.GetNaturalGravity().Length() / 9.81 < 0.75)
                {
                    command.State = MainStateEnum.Cruise;
                    command.Param.Text = "orbit";
                }
            }

            switch (command.State)
            {
                case MainStateEnum.Reload:
                    Reload();
                    break;
                case MainStateEnum.Abort:
                    Abort();
                    break;
                case MainStateEnum.Dock:
                    DockStateSwitch(command.Param);
                    return;
                case MainStateEnum.Cruise:
                    sc.Controller.DampenersOverride = true;
                    CruiseControlStateSwitch(command.Param);
                    break;
                case MainStateEnum.CNav: // Circumnavigation
                    sc.Controller.DampenersOverride = true;
                    CircumNavigateStateSwitch(command.Param);
                    break;
                case MainStateEnum.Land: // Auto Land
                    if (gravity == 0)
                    {
                        Abort();
                        return;
                    }
                    if (command.Param.AutoLandState == AutoLandStateEnum.Idle) StartLand();
                    AutoLandStateSwitch(command.Param);
                    break;
                case MainStateEnum.SBurn: // Suicide Burn
                    if (command.Param.AutoLandState == AutoLandStateEnum.Idle) StartLand();
                    SuicideBurnStateSwitch(command.Param);
                    break;
                case MainStateEnum.Gps:
                    Runtime.UpdateFrequency = UpdateFrequency.Update1;
                    b.autoPilotToggle = true;
                    CircumNavigateStateSwitch(command.Param);
                    break;
            }

            // Stop cruise control when leaves atmosphere?

            if (b.stopCruiseWhenOutOfGrav && b.lastCheckIsOnNatGrav && gravity == 0.0)
            {
                b.stopCruiseWhenOutOfGrav = b.lastCheckIsOnNatGrav = b.cruiseToggle = false;
                Abort();
            }
            else
            {
                b.lastCheckIsOnNatGrav = gravity > 0.0;
            }

            // Info LCDs
            if (sc.Lcds1.Count > 0) WriteInfo();
            if (sc.Lcds2.Count > 0) WriteInfo2();
        }

        private void TurnOFfBreakingThrust()
        {
            foreach (IMyThrust thruster in sc.BreakingThrusters)
            {
                thruster.Enabled = false;
            }
        }

        private void DockToggle(bool anyConnected)
        {
            SetBlocks(!anyConnected);
            StockpileTanks(anyConnected);
            if (anyConnected)
            {
                ChargeBatteries();
            }
            else
            {
                AutoBatteries();
            }
        }

        Command ParseCommand(string argument)
        {
            var parts = argument.Trim().Split(
                new[] { ' ', '\t' },
                StringSplitOptions.RemoveEmptyEntries
            );

            if (parts.Length == 0)
                return command;

            // First word = command (lowercase)
            MainStateEnum cmd = TryParseArgument(parts[0].ToLowerInvariant());

            // No second part → no parameter
            if (parts.Length == 1)
                return new Command(cmd, CommandParam.Empty);

            // Second part: try number, then string
            string second = parts[1].Trim();
            string end = argument.Substring(parts[0].Length + 1);

            CommandParam param;
            double num;
            Vector3D gps;

            if (TryParseGPS(end, out gps))
            {
                param = new CommandParam(gps);
            }
            else if(double.TryParse(second, out num))
                param = new CommandParam(num);             
            else
                param = new CommandParam(second.ToLowerInvariant());

            return new Command(cmd, param);
        }
        
        // GPS parser for "GPS:name:X:Y:Z:color:" format
        bool TryParseGPS(string gps, out Vector3D result)
        {
            result = new Vector3D();
            if (string.IsNullOrWhiteSpace(gps)) return false;
            if (!gps.StartsWith("GPS:")) return false;

            var parts = gps.Split(':');
            if (parts.Length < 6) return false;

            double x, y, z;
            if (!double.TryParse(parts[2], out x)) return false;
            if (!double.TryParse(parts[3], out y)) return false;
            if (!double.TryParse(parts[4], out z)) return false;

            result = new Vector3D(x, y, z);
            return true;
        }

        public StringBuilder ScriptInfoHeader(StringBuilder scriptInfo)
        {
            scriptInfo.AppendLine(sc.GridName);
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

        public StringBuilder ScriptInfoPhysics(StringBuilder scriptInfo)
        {
            scriptInfo.AppendLine();

            if (gravity > 0)
            {
                scriptInfo.AppendLine($"Alt: {alt:F1} m");
                scriptInfo.AppendLine($"Rate of climb: {climbRate:F1} m/s");
            }

            scriptInfo.AppendLine($"Longitudinal velocity: {forwardVelocity:F1} m/s");
            scriptInfo.AppendLine($"Lateral velocity: {rightVelocity:F1} m/s");
            scriptInfo.AppendLine($"Vertical velocity: {upVelocity:F1} m/s");

            switch (command.State)
            {
                case MainStateEnum.Land:
                case MainStateEnum.SBurn:
                    scriptInfo.AppendLine($"timeToImpact: {timeToImpact:F1} s");
                    scriptInfo.AppendLine($"gravity: {gravity:F1} m²/s");
                    scriptInfo.AppendLine($"Max upward accel: {maxYDecel:F1} m²/s");
                    break;
            }

            return scriptInfo;
        }

        public StringBuilder ScriptInfoBlocks(StringBuilder scriptInfo)
        {
            scriptInfo.AppendLine();

            if (__allowFlightSystems)
            {
                scriptInfo.AppendLine("Controller: " + sc.Controller.CustomName);
                scriptInfo.AppendLine("LCDs1: " + sc.Lcds1.Count);
                scriptInfo.AppendLine("LCDs2: " + sc.Lcds2.Count);
                scriptInfo.AppendLine("Batteries: " + sc.Batteries.Count + " | Tanks: " + sc.Tanks.Count);
                scriptInfo.AppendLine("Forward thruster: " + sc.ForwardThrusters.Count);
                scriptInfo.AppendLine("Breaking thruster: " + sc.BreakingThrusters.Count);
                scriptInfo.AppendLine("Upward thruster: " + sc.UpwardThrusters.Count);
                scriptInfo.AppendLine("Gears: " + sc.Gears.Count);
            }
            if (__allowDockMode)
                scriptInfo.AppendLine("Dock Mode blocks: " + sc.ControlledBlocks.Count);

            return scriptInfo;
        }

        private static MainStateEnum TryParseArgument(string input)
        {
            MainStateEnum mainStateEnum;
            try
            {
                mainStateEnum = (MainStateEnum)Enum.Parse(typeof(MainStateEnum), input, true);
            }
            catch
            {
                mainStateEnum = MainStateEnum.Abort;
            }
            return mainStateEnum;
        }

        void DockStateSwitch(CommandParam param)
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
                    DockToggle(isDockMode);
                    break;

                case "off":
                    isDockMode = false;
                    command = Command.Empty;
                    DockToggle(isDockMode);
                    break;
            }
        }

        void CruiseControlStateSwitch(CommandParam param)
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
                    CruiseControl(cruiseSpeed);
                    break;
                case "off":
                    Abort();
                    break;
                case "orbit":
                    b.cruiseToggle = !b.cruiseToggle;
                    if (b.cruiseToggle)
                    {
                        command.Param.Text = "align";
                        b.stopCruiseWhenOutOfGrav = true;
                        CruiseControl(cruiseSpeed);
                    }
                    else
                    {
                        if (b.autoPilotToggle)
                        {
                            command.State = MainStateEnum.Gps;
                            command.Param.Text = "on";
                        } else
                        {
                            Abort();
                        }
                    }
                    break;
                case "align":
                    if (AlignToGravity())
                    {
                        desiredUpVector = RotateUpTowardForwardForNoseUp(-0.9 * GetMaxPitchAngle());
                        command.Param.Text = "climb";
                    }
                    break;
                case "climb":
                    if (b.circumnavCheckAltitude && effectiveAlt > __cnavAltitude)
                    {
                        if (b.autoPilotToggle)
                        {
                            command.State = MainStateEnum.Gps;
                            command.Param.Text = "on";
                            break;
                        }
                        else
                        {
                            Abort();
                            command.State = MainStateEnum.CNav;
                        }
                    }
                    Vector3D shipUp = sc.Controller.WorldMatrix.Up;
                    AlignToVector(desiredUpVector, false, shipUp);
                    CruiseControl(cruiseSpeed);
                    break;
                case "glide":
                    CruiseControl(cruiseSpeed);
                    if (effectiveAlt < 500 + stopYDist)
                    {
                        Abort();
                        command.State = MainStateEnum.Land;
                    }
                    break;
            }
        }

        void CircumNavigateStateSwitch(CommandParam param)
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
                    if (effectiveAlt < __cnavAltitude)
                    {
                        SoftAbort();
                        b.circumnavCheckAltitude = true;
                        command.State = MainStateEnum.Cruise;
                        command.Param.Text = "orbit";
                    }

                    CruiseControl(cruiseSpeed);
                    if (!b.autoPilotToggle)
                    {
                        AlignToGravity();
                    } 
                    else if (distanceToLine < __distanceToGPS + stopZDist)
                    {
                        command.State = MainStateEnum.Land;
                        b.autoPilotToggle = false;
                    } 
                    else if (AlignToGravity() && b.autoPilotToggle && AimYawOnlyAt(param.TargetCoordinates))
                    {
                        Runtime.UpdateFrequency = UpdateFrequency.Update10;
                    }
                    break;
                case "off":
                    Abort();
                    break;
            }
        }

        void SuicideBurnStateSwitch(CommandParam param)
        {
            switch (param.AutoLandState)
            {
                case AutoLandStateEnum.Idle:
                    break;

                case AutoLandStateEnum.Align:
                    SoftAbort();
                    if (AlignToGravity(true)) command.Param.AutoLandState = AutoLandStateEnum.Drop;
                    break;

                case AutoLandStateEnum.Drop:
                    if (SuicideBurn()) command.Param.AutoLandState = AutoLandStateEnum.LockGear;
                    break;

                case AutoLandStateEnum.LockGear:
                    if (TryLock()) Abort();
                    break;
            }
        }

        void AutoLandStateSwitch(CommandParam param)
        {
            switch (param.AutoLandState)
            {
                case AutoLandStateEnum.Idle:
                    break;

                case AutoLandStateEnum.Align:
                    SoftAbort();
                    if (AlignToGravity(true)) command.Param.AutoLandState = AutoLandStateEnum.Drop;
                    break;

                case AutoLandStateEnum.Drop:
                    if (AutoLand()) command.Param.AutoLandState = AutoLandStateEnum.LockGear;
                    break;

                case AutoLandStateEnum.LockGear:
                    if (TryLock()) Abort();
                    break;
            }
        }

        bool AimYawOnlyAt(Vector3D targetGps)
        {
            if (sc.Controller == null || sc.Gyros == null || sc.Gyros.Count == 0) return false;
            if (naturalGrav.LengthSquared() < 0.01) return false;

            // Yaw axis: away-from-gravity (up)
            Vector3D up = Vector3D.Normalize(naturalGrav);

            // Ship position and forward (use ShipContext.Controller forward in world)
            Vector3D shipPos = sc.Controller.GetPosition();
            Vector3D shipForward = sc.Controller.WorldMatrix.Forward;

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
                foreach (var g in sc.Gyros) g.GyroOverride = false;
                return true;
            }

            // Desired angular rate around up only
            const double MAX_ROT_RATE = 3.0;
            const double RESPONSE = 1.0;
            double desiredRateScalar = Math.Min(Math.Abs(yawAngle) * RESPONSE, MAX_ROT_RATE);
            Vector3D desiredRate = up * (Math.Sign(yawAngle) * desiredRateScalar);

            // PD correction (use full angular velocity but we'll only command yaw to sc.Gyros)
            Vector3D angVel = sc.Controller.GetShipVelocities().AngularVelocity;
            Vector3D correction = desiredRate - angVel;

            // Apply to sc.Gyros but zero pitch & roll commands so only yaw moves
            foreach (var g in sc.Gyros)
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

        private void Reload()
        {
            firstRun = true;

            sc = new SC(gridTerminalSystem, me, __ignoreTag);

            ParseIni();

            if (__allowFlightSystems)
            {
                sc
                    // Flight cached blocks
                    .UpdateControllers()
                    .UpdateGridHeight()
                    .UpdateThrusters()
                    .UpdateGyros()
                    .UpdateGears()
                    .UpdateLCDs(__Lcd1Tag, __Lcd2Tag);

                b.lastCheckIsOnNatGrav = sc.Controller.GetNaturalGravity().LengthSquared() > 0;
                Abort();
            }

            if (__allowDockMode)
                sc
                // Dock cached blocks
                .UpdateConnectors()
                .UpdateTanks()
                .UpdateH2Tanks()
                .UpdateBatteries(BACKUP_TAG)
                .UpdateControlledBlocks(__fsGroupTag)
                // Override group cached blocks
                .UpdateOverrideGroup(__overrideBlockTag);

            if (__controlAntennas)
                sc.UpdateAntennas(__controlAntennas);

            if (__renameSubgrids)
            {
                // Get main grid (where this PB is)
                IMyCubeGrid mainGrid = sc.Me.CubeGrid;
                if (mainGrid != null)
                {
                    RenameSubgrids.GetSubgridsAndRename(sc.GridTS, mainGrid);
                }
            }
        }

        void CruiseControl(double cruiseSpeed)
        {
            double error = cruiseSpeed - forwardVelocity;

            if (Math.Abs(error) < SPEED_TOLERANCE)
                return;

            if (error > 0)
                currentOverride += OVERRIDE_STEP;
            else
                currentOverride -= OVERRIDE_STEP;

            currentOverride = MathHelper.Clamp(currentOverride, 0f, 1f);

            // Disable braking thrusters so they don't fight cruise
            foreach (var brakingThruster in sc.BreakingThrusters)
                brakingThruster.Enabled = false;

            // Control forward thrust smoothly
            foreach (var forwardThruster in sc.ForwardThrusters)
            {
                forwardThruster.Enabled = true;
                forwardThruster.ThrustOverridePercentage = (float)currentOverride;
            }

        }

        // -------------------- Remote control helpers --------------------
        void FlyToTarget(Vector3D target)
        {
            sc.Controller.FlightMode = FlightMode.OneWay;
            sc.Controller.ClearWaypoints();
            sc.Controller.AddWaypoint(target, "Target");
            if (!sc.Controller.IsAutoPilotEnabled)
                sc.Controller.SetAutoPilotEnabled(true);
        }

        void DisableRemoteControl()
        {
            if (sc.Controller.IsAutoPilotEnabled)
                sc.Controller.SetAutoPilotEnabled(false);
            sc.Controller.ClearWaypoints();
        }

        double DistanceToGps(IMyShipController controller, Vector3D gps)
        {
            Vector3D shipPos = controller.GetPosition();
            double vertical;

            Vector3D up = -Vector3D.Normalize(naturalGrav); // up direction
            Vector3D toTarget = gps - shipPos;

            // vertical distance along up (signed): positive = target is "above" ship in up direction
            vertical = Vector3D.Dot(toTarget, up);

            // horizontal vector: component of toTarget on plane perpendicular to up
            Vector3D horizVec = toTarget - up * vertical;

            return horizVec.Length();
        }

        Vector3D TryGetPlanetPosition(IMyShipController controller)
        {
            Vector3D shipPos = controller.GetPosition();
            Vector3D planetCenter = new Vector3D();

            // Get planet center
            controller.TryGetPlanetPosition(out planetCenter);

            return planetCenter;
        }

        bool IsAnyConnectorConnected()
        {
            foreach (IMyShipConnector connector in sc.Connectors)
            {
                if (connector.Status == MyShipConnectorStatus.Connected)
                    return true;
            }
            return false;
        }

        void SetBlocks(bool enabled)
        {
            //Always turn tools OFF when dock/undock
            sc.ControlledToolBlocks.ForEach(b => b.Enabled = false);

            //Toggle other blocks when dock/undock
            foreach (IMyFunctionalBlock cachedBlock in sc.ControlledBlocks)
            {
                if (cachedBlock != null && cachedBlock.IsFunctional)
                    cachedBlock.Enabled = enabled;
            }

            isDockMode = !enabled;
        }

        void StockpileTanks(bool stockpile)
        {
            foreach (IMyGasTank tank in sc.Tanks)
            {
                if (tank != null && tank.IsFunctional)
                    tank.Stockpile = stockpile;
            }
        }

        void ChargeBatteries()
        {
            if (sc.BackupBattery != null)
                sc.BackupBattery.ChargeMode = ChargeMode.Auto;

            foreach (IMyBatteryBlock battery in sc.Batteries)
            {
                battery.ChargeMode = ChargeMode.Recharge;
            }
        }

        void AutoBatteries()
        {
            if (sc.BackupBattery != null)
                sc.BackupBattery.ChargeMode = ChargeMode.Recharge;

            foreach (IMyBatteryBlock battery in sc.Batteries)
            {
                battery.ChargeMode = ChargeMode.Auto;
            }
        }

        void WriteInfo()
        {
            // Mass
            var mass = sc.Controller.CalculateShipMass();

            // Hydrogen
            double h2Cap = 0, h2Fill = 0;
            foreach (var tank in sc.H2Tanks)
            {
                h2Cap += tank.Capacity;
                h2Fill += tank.Capacity * tank.FilledRatio;
            }

            double h2Rate = (h2Fill - lastH2Fill) / Runtime.TimeSinceLastRun.TotalSeconds;
            lastH2Fill = h2Fill;

            string h2Time = "--";
            if (Math.Abs(h2Rate) > 1e-6)
            {
                if (h2Rate >= 0)
                    h2Time = FormatTime((h2Cap - h2Fill) / h2Rate) + " /\\";
                else
                    h2Time = FormatTime(h2Fill / -h2Rate) + " \\/";
            }

            sc.H2CapacityPercent = h2Fill / h2Cap * 100;

            // Batteries
            double batCap = 0, batStored = 0;
            double batIn = 0, batOut = 0;

            foreach (var battery in sc.Batteries)
            {
                batCap += battery.MaxStoredPower;
                batStored += battery.CurrentStoredPower;
                batIn += battery.CurrentInput;
                batOut += battery.CurrentOutput;
            }

            double netPower = batIn - batOut;
            string batTime = "--";

            if (Math.Abs(netPower) > 0.01)
            {
                if (netPower > 0)
                    batTime = FormatTime(3600 * (batCap - batStored) / netPower) + " /\\";
                else
                    batTime = FormatTime(3600 * batStored / -netPower) + " \\/";
            }

            // Output
            StringBuilder stringBuilder = new StringBuilder();

            ScriptInfoHeader(stringBuilder);
            stringBuilder.AppendLine("\n");

            stringBuilder.AppendLine($"Mass: {mass.PhysicalMass / 1000:0.0} t");
            stringBuilder.AppendLine($"Empty Mass: {mass.BaseMass / 1000:0.0} t");

            stringBuilder.AppendLine($"H2: {sc.H2CapacityPercent:0}% - {h2Time}");

            stringBuilder.AppendLine($"Bat:  {batStored / batCap * 100:0}% - {batTime}");

            foreach (IMyTextSurface lcd1 in sc.Lcds1)
                lcd1.WriteText(stringBuilder.ToString());
        }

        void WriteInfo2()
        {
            // Velocity & acceleration
            Vector3D velocity = sc.Controller.GetShipVelocities().LinearVelocity;
            Vector3D accel = Vector3D.Zero;

            if (!firstRun)
                accel = (velocity - lastVelocity) / Runtime.TimeSinceLastRun.TotalSeconds;

            lastVelocity = velocity;
            firstRun = false;

            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.AppendLine(sc.GridName);
            stringBuilder.AppendLine(new string('-', 28));

            if (gravity > 0)
            {
                stringBuilder.AppendLine($"Ground level : {alt:F1} m");
                stringBuilder.AppendLine($"Rate of climb: {climbRate:F1} m/s");
                stringBuilder.AppendLine($"Accel: {accel.Length() / 9.81:F1} g");
                stringBuilder.AppendLine($"Stop Y: {stopYDist:F1} m | {timeToStopY:F1} s");
            }
            stringBuilder.AppendLine($"Stop Z: {stopZDist:F1} m | {timeToStopZ:F1} s");

            if (b.autoPilotToggle)
            {
                stringBuilder.AppendLine($"\nETA: {FormatTime(TimeToDistanceSmoothed(distanceToLine, Runtime.LastRunTimeMs))}");

            }
            else if (command.State == MainStateEnum.Land || command.State == MainStateEnum.SBurn)
            {
                stringBuilder.AppendLine($"Gravity: {gravity:F1} m²/s");
                stringBuilder.AppendLine($"Max up accel: {maxYDecel:F1} m²/s");
                stringBuilder.AppendLine($"TTI: {timeToImpact:F1} s");
            }
            else
            {
                stringBuilder.AppendLine($"Longitudinal v: {forwardVelocity:F1} m/s");
                stringBuilder.AppendLine($"Lateral v: {rightVelocity:F1} m/s");
                stringBuilder.AppendLine($"Vertical v: {upVelocity:F1} m/s");
            }

            stringBuilder.AppendLine();

            foreach (IMyTextSurface lcd2 in sc.Lcds2)
                lcd2.WriteText(stringBuilder.ToString());
        }
        void WriteEmpty()
        {
            foreach (IMyTextSurface lcd1 in sc.Lcds1)
                lcd1.WriteText("");

            foreach (IMyTextSurface lcd2 in sc.Lcds2)
                lcd2.WriteText("");
        }

            double TimeToDistanceSmoothed(double distance, double dt)
        {
            speedTimeTracker.AddValue(forwardVelocity, dt);

            if (dt <= 0) return double.PositiveInfinity;
            double avgSpeed =speedTimeTracker.GetAverageSpeed();

            if (avgSpeed <= 1e-6) avgSpeed = 0.0;

            // EMA smoothing
            prevSmoothedSpeed = (ALPHA * avgSpeed) + ((1.0 - ALPHA) * prevSmoothedSpeed);

            if (prevSmoothedSpeed <= 1e-6) return double.PositiveInfinity;
            return distance / prevSmoothedSpeed;
        }

        public class SpeedTime
        {
            public double Speed { get; set; }
            public double Time { get; set; }

            public SpeedTime(double speed, double time)
            {
                Speed = speed;
                Time = time;
            }
        }

        public class SpeedTimeTracker
        {
            private List<SpeedTime> speedTimeValues;
            private int maxSize;

            public SpeedTimeTracker(int maxSize)
            {
                this.maxSize = maxSize;
                speedTimeValues = new List<SpeedTime>();
            }

            public void AddValue(double speed, double time)
            {
                if (speedTimeValues.Count >= maxSize)
                {
                    speedTimeValues.RemoveAt(0); // Remove the oldest
                }
                speedTimeValues.Add(new SpeedTime(speed, time));
            }

            public double GetAverageSpeed()
            {
                double avgSpeed = 0;
                foreach (var value in speedTimeValues)
                {
                    avgSpeed += value.Speed;
                }
                return avgSpeed / speedTimeValues.Count;
            }
        }


        string FormatTime(double time)
        {
            if (double.IsInfinity(time) || time < 0)
                return "--";

            int intTime = (int)time;
            int days = intTime / 3600 / 24;
            int hours = (intTime % 24) / 3600;
            int minutes = (intTime % 3600) / 60;
            int seconds = intTime % 60;

            if (days > 0)
                return $"{days}d {hours}h {minutes}m";
            if (hours > 0)
                return $"{hours}h {minutes}m";
            if (minutes > 0)
                return $"{minutes}m {seconds}s";
            return $"{seconds}s";
        }

        ////////////////////////////////////////////////////////
        /// SETUP
        ////////////////////////////////////////////////////////

        void UpdatePhysics()
        {
            naturalGrav = sc.Controller.GetNaturalGravity();
            gravity = naturalGrav.Length();

            mass = sc.Controller.CalculateShipMass().PhysicalMass;
            maxYDecel = GetMaxDecel(sc.UpwardThrusters);
            maxZDecel = GetMaxDecel(sc.BreakingThrusters);

            GetShipAxisVelocities();

            tickCount++;
            if (tickCount % 10 == 0)
            {
                gravityRatio = gravity / oldGravity;
                oldGravity = gravity;
            }

            sc.Controller.TryGetPlanetElevation(MyPlanetElevation.Surface, out alt);

            var paramSpeed = command.Param.Number;
            cruiseSpeed = (paramSpeed == 0 ? __maxSpeed : MathHelper.Clamp(command.Param.Number, MinSpeed, __maxSpeed));

            climbRate = GetGravityAlignedVerticalVelocity();
            vEffectiveYSpeed = climbRate + maxYDecel * Runtime.TimeSinceLastRun.TotalSeconds;
            vEffectiveZSpeed = forwardVelocity + maxZDecel * Runtime.TimeSinceLastRun.TotalSeconds;

            stopYDist = Math.Abs((vEffectiveYSpeed * vEffectiveYSpeed) / (2 * maxYDecel));
            stopZDist = Math.Abs((vEffectiveZSpeed * vEffectiveZSpeed) / (2 * maxZDecel));

            effectiveAlt = alt - vEffectiveYSpeed * Runtime.TimeSinceLastRun.TotalSeconds - sc.GridHeight;
            effectiveAlt = effectiveAlt / gravityRatio;

            timeToImpact = alt / Math.Abs(vEffectiveYSpeed);
            timeToStopY = Math.Abs(climbRate / maxYDecel);
            timeToStopZ = Math.Abs(forwardVelocity / maxZDecel);

            netDecel = ComputeNetDecel();

            if (b.autoPilotToggle)
            {
                distanceToLine = DistanceToGps(sc.Controller, command.Param.TargetCoordinates);
            }
        }

        void StartLand()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            command.Param.AutoLandState = AutoLandStateEnum.Align;
        }

        void Abort()
        {
            b = new booleans();

            command = Command.Empty;

            sc.Controller.DampenersOverride = true;
            b.autoPilotToggle = false;

            Runtime.UpdateFrequency = UpdateFrequency.Update10;

            tickCount = 0;
            ResetGyros();
            ResetThrusters();
        }

        void SoftAbort()
        {
            sc.Controller.DampenersOverride = true;
            b.stopCruiseWhenOutOfGrav = false;

            ResetGyros();
            ResetThrusters();
        }

        void ResetGyros()
        {
            foreach (var g in sc.Gyros)
            {
                g.GyroOverride = false;
                g.Enabled = true;
            }
        }

        void ResetThrusters()
        {
            currentOverride = 0;

            foreach (var forwardThruster in sc.ForwardThrusters)
            {
                forwardThruster.ThrustOverridePercentage = 0f;
                forwardThruster.Enabled = true;
            }

            foreach (var brakingThruster in sc.BreakingThrusters)
            {
                brakingThruster.ThrustOverridePercentage = 0f;
                brakingThruster.Enabled = true;
            }

            foreach (var upThruster in sc.UpwardThrusters)
            {
                upThruster.ThrustOverridePercentage = 0f;
                upThruster.Enabled = true;
            }

        }

        ////////////////////////////////////////////////////////
        /// FLIGHT
        ////////////////////////////////////////////////////////

        bool AlignToGravity()
        {
            return AlignToGravity(false);
        }

        bool AlignToGravity(bool checkSpeed)
        {
            Vector3D desiredUp = Vector3D.Normalize(naturalGrav);
            return AlignToVector(checkSpeed, desiredUp);
        }

        bool AlignToVector(bool checkSpeed, Vector3D desiredUpVector)
        {
            Vector3D shipUp = sc.Controller.WorldMatrix.Up;

            return AlignToVector(shipUp, checkSpeed, desiredUpVector);
        }

        bool AlignToVector(Vector3D shipUp, bool checkSpeed, Vector3D desiredUpVector)
        {
            if (naturalGrav.LengthSquared() < 0.01)
                return false;

            Vector3D axis = shipUp.Cross(desiredUpVector);
            double angle = axis.Length();

            if (angle < 0.005 && (checkSpeed ? IsStopped() : true))
            {
                foreach (var g in sc.Gyros)
                    g.GyroOverride = false;

                return true;
            }

            axis /= angle;

            Vector3D angVel = sc.Controller.GetShipVelocities().AngularVelocity;

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

            foreach (var g in sc.Gyros)
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
            return threshold > upVelocity && threshold >= Math.Abs(forwardVelocity) && threshold >= Math.Abs(rightVelocity);
        }

        ////////////////////////////////////////////////////////
        /// SAFE DEscENT
        ////////////////////////////////////////////////////////
        bool SuicideBurn()
        {
            if (netDecel - 1 < 0)
            {
                Abort();
                command.State = MainStateEnum.Cruise;
                command.Param.Text = "orbit";
            }

            sc.Controller.DampenersOverride = false;
            AlignToGravity();
            MatchVerticalSpeed(-104);
            return effectiveAlt < 1.1 * stopYDist + sc.GridHeight;
        }

        bool AutoLand()
        {
            if (netDecel - 0.5 < 0)
            {
                Abort();
                command.State = MainStateEnum.Cruise;
                command.Param.Text = "orbit";
            }

            sc.Controller.DampenersOverride = false;
            AlignToGravity();

            double speedFromAlt = (100 + alt) * 0.08;
            double speedFromAccel = 20 * netDecel;
            double speedMin = -Math.Min(speedFromAlt, speedFromAccel);

            if (speedMin > -104) MatchVerticalSpeed(speedMin);
            return effectiveAlt < 10 + 2 * sc.GridHeight;
        }

        bool TryLock()
        {
            AlignToGravity();
            MatchVerticalSpeed(-2);
            sc.Controller.DampenersOverride = true;

            foreach (var g in sc.Gears)
                g.Lock();

            return sc.Gears.Exists(g => g.IsLocked);
        }

        ////////////////////////////////////////////////////////
        /// PHYSICS HELPERS
        ////////////////////////////////////////////////////////

        double GetGravityAlignedVerticalVelocity()
        {
            Vector3D gNorm = Vector3D.Normalize(naturalGrav);

            return -sc.Controller.GetShipVelocities()
                .LinearVelocity.Dot(gNorm);
        }

        void GetShipAxisVelocities()
        {
            Vector3D velocity = sc.Controller.GetShipVelocities().LinearVelocity;
            MatrixD wm = sc.Controller.WorldMatrix;

            forwardVelocity = Vector3D.Dot(velocity, wm.Forward);
            rightVelocity = Vector3D.Dot(velocity, wm.Right);
            upVelocity = Vector3D.Dot(velocity, wm.Up);
        }

        double GetMaxDecel(List<IMyThrust> thrusters)
        {
            thrust = 0;

            Vector3D up = -Vector3D.Normalize(naturalGrav);

            foreach (var t in thrusters)
            {
                double dot = t.WorldMatrix.Backward.Dot(up);

                if (dot > 0.7)
                    thrust += t.MaxEffectiveThrust * dot;
            }

            return (thrust / mass) - gravity;
        }

        void MatchVerticalSpeed(double target)
        {
            double hover = (mass * gravity) / SumThrust();

            double current = GetGravityAlignedVerticalVelocity();
            double error = target - current;

            double minThrustOverride = (climbRate < 10 ? 0.001 : 0);
            double output = MathHelper.Clamp(hover + error * 0.5, 0.01, 1);

            foreach (var t in sc.UpwardThrusters)
                t.ThrustOverridePercentage = (float)output;
        }

        double SumThrust()
        {
            double total = 0;

            foreach (var t in sc.UpwardThrusters)
                total += t.MaxEffectiveThrust;

            return total;
        }

        // Suicide Burn
        // ────────────────────────────────────────────────
        // 1. NET DECEL PREDICTION (core - ignores current g spikes)
        // Computes max possible upward accel from thrusters - current_g
        // ────────────────────────────────────────────────
        double ComputeNetDecel()
        {
            maxThrustUp = 0;
            foreach (var t in sc.UpwardThrusters) maxThrustUp += t.MaxEffectiveThrust;

            double thrustAccel = maxThrustUp / mass;

            return thrustAccel - gravity;  // positive = can decelerate
        }

        /// <summary>
        /// Rotates the ship's Up vector toward the ship's Forward vector (nose-UP pitch).
        /// Positive angleDeg = nose UP.
        /// </summary>
        Vector3D RotateUpTowardForwardForNoseUp(double angleDeg)
        {
            if (sc.Controller == null)
                return Vector3D.Up;

            Vector3D currentUp = sc.Controller.WorldMatrix.Up;
            Vector3D rightAxis = sc.Controller.WorldMatrix.Right;  // pitch axis

            double angleRad = MathHelper.ToRadians(angleDeg);
            MatrixD rotation = MatrixD.CreateFromAxisAngle(rightAxis, -angleRad);  // NEGATIVE = nose UP!

            Vector3D rotatedUp = Vector3D.TransformNormal(currentUp, rotation);
            return Vector3D.Normalize(rotatedUp);
        }

        private double GetMaxPitchAngle()
        {
            double fwdThrust = 0, upThrust = 0;
            foreach (var t in sc.ForwardThrusters)
                if (t.IsFunctional) fwdThrust += t.MaxEffectiveThrust;
            foreach (var t in sc.UpwardThrusters)
                if (t.IsFunctional) upThrust += t.MaxEffectiveThrust;

            return MathHelper.ToDegrees(Math.Atan2(fwdThrust, upThrust));
        }

        // ────────────────────────────────────────────────
        // Load config from CustomData (INI style)
        // ────────────────────────────────────────────────        
        private void ParseIni()
        {
            ini.Clear();

            if (!ini.TryParse(sc.Me.CustomData)) return;

            string sectionName = SectionName;

            if (!ini.ContainsSection(sectionName))
            {
                ini.AddSection(sectionName);
            }

            string tempGridName = ini.Get(sectionName, INI_GRID_NAME).ToString(sc.GridName);

            sc.GridName = string.IsNullOrWhiteSpace(tempGridName) ? sc.GridName : tempGridName;
            __fsGroupTag = ini.Get(sectionName, INI_FS_GROUP_TAG).ToString(__fsGroupTag);
            __overrideBlockTag = ini.Get(sectionName, INI_OVERRIDE_BLOCKS_TAG).ToString(__overrideBlockTag);
            __ignoreTag = ini.Get(sectionName, INI_IGNORE_TAG).ToString(__ignoreTag);
            __Lcd1Tag = ini.Get(sectionName, INI_LCD1_TAG).ToString(__Lcd1Tag);
            __Lcd2Tag = ini.Get(sectionName, INI_LCD2_TAG).ToString(__Lcd2Tag);
            __maxSpeed = (float)ini.Get(sectionName, MAX_SPEED).ToDouble(__maxSpeed);
            __cnavAltitude = (float)ini.Get(sectionName, CNAV_ALTITUDE).ToDouble(__cnavAltitude);
            __distanceToGPS = (float)ini.Get(sectionName, DISTANCE_TO_GPS).ToDouble(__distanceToGPS);
            __minimumAcceptedFuel = ini.Get(sectionName, MINIMUM_ACCEPTED_FUEL).ToDouble(__minimumAcceptedFuel);
            __allowFlightSystems = ini.Get(sectionName, FLIGHT_SYSTEMS).ToBoolean(__allowFlightSystems);
            __allowDockMode = ini.Get(sectionName, DOCK_MODE).ToBoolean(__allowDockMode);
            __controlAntennas = ini.Get(sectionName, CONTROL_ANTENNAS).ToBoolean(__controlAntennas);
            __renameSubgrids = ini.Get(sectionName, RENAME_SUBGRIDS).ToBoolean(__renameSubgrids);

            ini.Set(SectionName, INI_GRID_NAME, sc.GridName);
            ini.Set(SectionName, INI_FS_GROUP_TAG, __fsGroupTag);
            ini.Set(SectionName, INI_OVERRIDE_BLOCKS_TAG, __overrideBlockTag);
            ini.Set(SectionName, INI_IGNORE_TAG, __ignoreTag);
            ini.Set(SectionName, INI_LCD1_TAG, __Lcd1Tag);
            ini.Set(SectionName, INI_LCD2_TAG, __Lcd2Tag);
            ini.Set(SectionName, MAX_SPEED, __maxSpeed);
            ini.Set(SectionName, CNAV_ALTITUDE, __cnavAltitude);
            ini.Set(SectionName, DISTANCE_TO_GPS, __distanceToGPS);
            ini.Set(SectionName, MINIMUM_ACCEPTED_FUEL, __minimumAcceptedFuel);
            ini.Set(SectionName, FLIGHT_SYSTEMS, __allowFlightSystems);
            ini.Set(SectionName, DOCK_MODE, __allowDockMode);
            ini.Set(SectionName, CONTROL_ANTENNAS, __controlAntennas);
            ini.Set(SectionName, RENAME_SUBGRIDS, __renameSubgrids);

            sc.Me.CustomData = ini.ToString();
        }
    }
}
