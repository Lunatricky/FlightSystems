using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // ================= CONFIG =================
        MyIni ini = new MyIni();

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

        double centerGridHight;
        double bottomGridHight;
        double gridHight;

        double forwardVelocity;
        double rightVelocity;
        double upVelocity;

        double netDecel;
        double maxThrustUp;
        double H2CapacityPercent;
        double distanceToLine;

        double prevSmoothedSpeed = 0.0; // persistent between ticks
        const double ALPHA = 0.2;       // 0.1-0.3 is reasonable; lower = smoother/slower response
        const int SpeedTimeTrackerMaxSize = 100;

        Vector3D desiredUpVector;

        IMyProgrammableBlock me;

        List<IMyGyro> gyros = new List<IMyGyro>();
        List<IMyLandingGear> gears = new List<IMyLandingGear>();

        // Cruise Control
        // Forward Speed Limiter + Cruise Control Fields

        const double SPEED_TOLERANCE = 0.5;  // m/s deadzone
        const double OVERRIDE_STEP = 0.05;   // cruise adjustment rate
        double MinSpeed = 20; // m/s

        readonly List<IMyThrust> breakingThrusters = new List<IMyThrust>();
        readonly List<IMyThrust> forwardThrusters = new List<IMyThrust>();
        readonly List<IMyThrust> upwardThrusters = new List<IMyThrust>();

        IMyRemoteControl controller ;


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

        string INI_FS_GROUP_TAG = "Group Tag";
        string INI_OVERRIDE_BLOCKS_TAG = "Override Blocks Tag";
        string INI_IGNORE_TAG = "Ignore Tag";
        string INI_LCD1_TAG = "LCD 1";
        string INI_LCD2_TAG = "LCD 2";
        string MAX_SPEED = "Max Speed";
        string CNAV_ALTITUDE = "Cnav Altitude";
        string DISTANCE_TO_GPS = "Distance to GPS";
        string MINIMUM_ACCEPTED_FUEL = "Minimum Accepted Fuel";
        string DOCK_MODE = "Dock Mode";
        string CONTROL_ANTENNAS = "Control Antennas";

        string __fsGroupTag = "Flight Systems";
        string __overrideBlockTag = "[FS_override]";
        string __ignoreTag = "[FS_ignore]";
        string __Lcd1Tag = "[FS_LCD1]";
        string __Lcd2Tag = "[FS_LCD2]";
        double __maxSpeed = 99; // m/s
        double __cnavAltitude = 1000; // m
        double __distanceToGPS = 500; // m
        double __minimumAcceptedFuel = 20; //%
        bool __allowDockMode = false;
        bool __controlAntennas = false;

        readonly List<IMyFunctionalBlock> controlledBlocks = new List<IMyFunctionalBlock>();
        readonly List<IMyFunctionalBlock> controlledToolBlocks = new List<IMyFunctionalBlock>();
        readonly List<IMyFunctionalBlock> overrideBlocks = new List<IMyFunctionalBlock>();
        readonly List<IMyShipConnector> connectors = new List<IMyShipConnector>();
        readonly List<IMyGasTank> tanks = new List<IMyGasTank>();
        readonly List<IMyGasTank> h2Tanks = new List<IMyGasTank>();
        readonly List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
        readonly List<IMyRadioAntenna> antennas = new List<IMyRadioAntenna>();
        readonly List<IMyShipController> controllers = new List<IMyShipController>();


        IMyBatteryBlock backupBattery;

        bool isDockMode = false;
        bool lastDockState = false;

        // Info LCDs
        private const string BACKUP_TAG = "backup";
        readonly List<IMyTextSurface> lcds1 = new List<IMyTextSurface>();
        readonly List<IMyTextSurface> lcds2 = new List<IMyTextSurface>();

        string gridName;

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
            b = new booleans();
            speedTimeTracker = new SpeedTimeTracker(SpeedTimeTrackerMaxSize);

            me = Me;
            Runtime.UpdateFrequency = UpdateFrequency.Update10;

            Reload();
            Abort();

            // Info LCDs
            if (lcds1.Count > 0) WriteInfo();
            if (lcds2.Count > 0) WriteInfo2();

            if (__allowDockMode)
            {
                bool anyConnected = IsAnyConnectorConnected();
                bool isGearLocked = gears.Exists(g => g.IsLocked);
                isDockMode = anyConnected || isGearLocked;
                DockToggle(isDockMode);
            }
        }

        Command command = Command.Empty;

        public void Main(string argument, UpdateType updateSource)
        {
            tickCount++;
            if (tickCount % 100 == 0)
            {
                ParseIni();
            }

            FlightSystems(argument);
        }

        private void FlightSystems(string argument)
        {
            if (!string.IsNullOrEmpty(argument)) command = ParseCommand(argument);

            StringBuilder scriptInfo = new StringBuilder();

            ScriptInfoHeader(scriptInfo);
            scriptInfo.AppendLine("");
            UpdatePhysics();
            ScriptInfoPhysics(scriptInfo);


            if (__allowDockMode)
            {
                bool anyConnected = IsAnyConnectorConnected();
                bool isGearlocked = gears.Exists(g => g.IsLocked);
                isDockMode = anyConnected || isGearlocked;

                if (isDockMode != lastDockState)
                {
                    DockToggle(isDockMode);
                    lastDockState = isDockMode;
                    return;
                }
            }

            if (gravity > 0)
            {
                if (H2CapacityPercent < __minimumAcceptedFuel && controller.GetNaturalGravity().Length() / 9.81 > 0.75)
                {
                    command.State = MainStateEnum.Land;
                }
                else if (H2CapacityPercent < __minimumAcceptedFuel && controller.GetNaturalGravity().Length() / 9.81 < 0.75)
                {
                    command.State = MainStateEnum.Cruise;
                    command.Param.Text = "orbit";
                }
            }

                ScriptInfoBlocks(scriptInfo);

            Echo(scriptInfo.ToString());
            me.GetSurface(0).WriteText(scriptInfo.ToString());

            if (isDockMode) return;

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
                    controller.DampenersOverride = true;
                    CruiseControlStateSwitch(command.Param);
                    break;
                case MainStateEnum.CNav: // Circumnavigation
                    controller.DampenersOverride = true;
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
            if (lcds1.Count > 0) WriteInfo();
            if (lcds2.Count > 0) WriteInfo2();

            if (__controlAntennas)
            {
                antennas.ForEach(b => { if (b != null) b.Enabled = false; });
                if (antennas.Count > 0)
                {
                    var firstValid = antennas.FirstOrDefault(b => b != null && !b.Closed);
                    if (firstValid != null) firstValid.Enabled = true;
                }
            }
        }

        private void TurnOFfBreakingThrust()
        {
            foreach (IMyThrust thruster in breakingThrusters)
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
            scriptInfo.AppendLine(gridName);
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
            scriptInfo.AppendLine("Controller: " + controller.CustomName);
            scriptInfo.AppendLine("LCDs1: " + lcds1.Count);
            scriptInfo.AppendLine("LCDs2: " + lcds2.Count);
            scriptInfo.AppendLine("Batteries: " + batteries.Count + " | Tanks: " + tanks.Count);
            scriptInfo.AppendLine("Forward thruster: " + forwardThrusters.Count);
            scriptInfo.AppendLine("Breaking thruster: " + breakingThrusters.Count);
            scriptInfo.AppendLine("Upward thruster: " + upwardThrusters.Count);
            scriptInfo.AppendLine("Gears: " + gears.Count);
            scriptInfo.AppendLine("Dock Mode blocks: " + controlledBlocks.Count);

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
                    Vector3D shipUp = controller.WorldMatrix.Up;
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
            if (controller == null || gyros == null || gyros.Count == 0) return false;
            if (naturalGrav.LengthSquared() < 0.01) return false;

            // Yaw axis: away-from-gravity (up)
            Vector3D up = Vector3D.Normalize(naturalGrav);

            // Ship position and forward (use controller forward in world)
            Vector3D shipPos = controller.GetPosition();
            Vector3D shipForward = controller.WorldMatrix.Forward;

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
                foreach (var g in gyros) g.GyroOverride = false;
                return true;
            }

            // Desired angular rate around up only
            const double MAX_ROT_RATE = 3.0;
            const double RESPONSE = 1.0;
            double desiredRateScalar = Math.Min(Math.Abs(yawAngle) * RESPONSE, MAX_ROT_RATE);
            Vector3D desiredRate = up * (Math.Sign(yawAngle) * desiredRateScalar);

            // PD correction (use full angular velocity but we'll only command yaw to gyros)
            Vector3D angVel = controller.GetShipVelocities().AngularVelocity;
            Vector3D correction = desiredRate - angVel;

            // Apply to gyros but zero pitch & roll commands so only yaw moves
            foreach (var g in gyros)
            {
                MatrixD inv = MatrixD.Transpose(g.WorldMatrix);
                Vector3D local = Vector3D.TransformNormal(correction, inv);

                g.GyroOverride = true;
                g.Pitch = 0f;
                // Some gyros have inverted yaw axis; if direction is reversed, invert local.Y here
                g.Yaw = (float)MathHelper.Clamp(-local.Y / 2, -3, 3);
                g.Roll = 0f;
            }

            return false;
        }

        private void Reload()
        {
            ParseIni();
            SetupSurface(me.GetSurface(0));
            LoadOverrideGroup();
            CacheBlocksCC();
            CacheBlocksLand();
            CacheBlocksDock();
            CacheBlocksLCD();
            b.lastCheckIsOnNatGrav = controller.GetNaturalGravity().LengthSquared() > 0;

            Abort();
        }

        void GetOwnGridBlocks<T>(List<T> list) where T : class, IMyTerminalBlock
        {
            list.Clear();
            GridTerminalSystem.GetBlocksOfType(list, block =>
            (block.IsSameConstructAs(me) && !block.CustomName.Contains(__ignoreTag))
            );
        }

        void CacheBlocksCC()
        {
            forwardThrusters.Clear();
            breakingThrusters.Clear();
            upwardThrusters.Clear();
            controllers.Clear();

            List<IMyRemoteControl> remotes = new List<IMyRemoteControl>();
            List<IMyCockpit> cockpits = new List<IMyCockpit>();

            GetOwnGridBlocks(remotes);
            GetOwnGridBlocks(cockpits);

            GridTerminalSystem.GetBlocksOfType(controllers, controller =>
               controller.IsSameConstructAs(me) && controller.IsMainCockpit);

            if (controllers.Count == 0)
            {
                foreach (IMyRemoteControl remote in remotes)
                {
                    if (controller == null)
                        controller = remote;
                    controllers.Add(remote);
                }
                if (controller == null)
                    throw new Exception("No Remote Control block found!");

                foreach (IMyCockpit cockpit in cockpits)
                {
                    controllers.Add(cockpit);
                }
            }

            List<IMyThrust> allThrusters = new List<IMyThrust>();
            GetOwnGridBlocks(allThrusters);

            foreach (var thruster in allThrusters)
            {
                // Thrusters that push the ship forward
                if (thruster.Orientation.Forward == Base6Directions.GetOppositeDirection(controller.Orientation.Forward))
                    forwardThrusters.Add(thruster);

                // Thrusters that push the ship backward
                else if (thruster.Orientation.Forward == controller.Orientation.Forward)
                    breakingThrusters.Add(thruster);

                // Thrusters that push the ship upwards
                else if (thruster.Orientation.Forward == Base6Directions.GetOppositeDirection(controller.Orientation.Up))
                    upwardThrusters.Add(thruster);
            }
        }

        void CacheBlocksLand()
        {
            gyros.Clear();
            gears.Clear();

            GetOwnGridBlocks(gyros);
            GetOwnGridBlocks(gears);

            Vector3D gravityDir = Vector3D.Normalize(controller.GetNaturalGravity());

            Vector3D center = me.CubeGrid.WorldVolume.Center;
            Vector3D shipBottom = GetLowestPoint(controller);

            // project onto gravity vector
            centerGridHight = center.Dot(gravityDir);
            bottomGridHight = shipBottom.Dot(gravityDir);

            // height difference along gravity
            gridHight = Math.Abs(centerGridHight - bottomGridHight);
        }

        Vector3D GetLowestPoint(IMyShipController controller)
        {
            BoundingBoxD bb = Me.CubeGrid.WorldAABB;

            Vector3D shipDown = Base6Directions.GetVector(
                Base6Directions.GetOppositeDirection(controller.Orientation.Up)
            );

            // This gives the true lowest point of the grid in the ship's "down" direction
            Vector3D lowestPoint = bb.Center - shipDown * bb.HalfExtents.Dot(shipDown);

            return lowestPoint;
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
            foreach (var brakingThruster in breakingThrusters)
                brakingThruster.Enabled = false;

            // Control forward thrust smoothly
            foreach (var forwardThruster in forwardThrusters)
            {
                forwardThruster.Enabled = true;
                forwardThruster.ThrustOverridePercentage = (float)currentOverride;
            }

        }

        // -------------------- Remote control helpers --------------------
        void FlyToTarget(Vector3D target)
        {
            controller.FlightMode = FlightMode.OneWay;
            controller.ClearWaypoints();
            controller.AddWaypoint(target, "Target");
            if (!controller.IsAutoPilotEnabled)
                controller.SetAutoPilotEnabled(true);
        }

        void DisableRemoteControl()
        {
            if (controller.IsAutoPilotEnabled)
                controller.SetAutoPilotEnabled(false);
            controller.ClearWaypoints();
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

        //Docking Routine
        void LoadOverrideGroup()
        {
            overrideBlocks.Clear();

            var blocks = new List<IMyFunctionalBlock>();
            GridTerminalSystem.GetBlocksOfType(blocks, b =>
                b.IsSameConstructAs(me) &&
                b.CustomName.Contains(__overrideBlockTag)
            );

            foreach (IMyFunctionalBlock block in blocks)
            {
                if (block.IsSameConstructAs(me))
                    overrideBlocks.Add(block);
            }
        }

        void CacheBlocksDock()
        {
            controlledBlocks.Clear();
            connectors.Clear();
            tanks.Clear();
            h2Tanks.Clear();
            batteries.Clear();

            IMyBlockGroup group = GridTerminalSystem.GetBlockGroupWithName(__fsGroupTag);

            if (controlledBlocks.Count == 0 && group != null)
            {
                List<IMyFunctionalBlock> blocksGroup = new List<IMyFunctionalBlock>();
                group.GetBlocksOfType(blocksGroup);
                controlledBlocks.AddList(blocksGroup);
            } else
            {
                ReloadControlledBlocks();
                controlledBlocks.AddList(overrideBlocks);
                controlledBlocks.Remove(me);
            }


            // Connectors, Tanks & Batteries (own construct only)
            GetOwnGridBlocks(connectors);
            SetConnectors();

            GetOwnGridBlocks(tanks);
            GetOwnGridBlocks(batteries);

            foreach (IMyGasTank tank in tanks)
            {
                if (IsHydrogenTank(tank))
                {
                    h2Tanks.Add(tank);
                }
            }

            // Backup Battery
            if (backupBattery == null || backupBattery.Closed)
            {
                foreach (var battery in batteries)
                {
                    if (!battery.Closed && battery.CustomName.ToLower().Contains(BACKUP_TAG))
                    {
                        backupBattery = battery;
                        break;
                    }
                }
                batteries.Remove(backupBattery);
            }

        }

        private void SetConnectors()
        {
            foreach (IMyShipConnector connector in connectors)
            {
                connector.IsParkingEnabled = false;
                connector.PullStrength = 0.00005f;
            }
        }

        void ReloadControlledBlocks()
        {
            controlledBlocks.Clear();
            controlledToolBlocks.Clear();

            AddBlocks<IMyShipToolBase>(controlledToolBlocks);
            AddBlocks<IMyThrust>(controlledBlocks);
            AddBlocks<IMyMechanicalConnectionBlock>(controlledBlocks);
            AddBlocks<IMyReflectorLight>(controlledBlocks);
            AddBlocks<IMySearchlight>(controlledBlocks);
            AddBlocks<IMySensorBlock>(controlledBlocks);
            AddBlocks<IMyLaserAntenna>(controlledBlocks);
            AddBlocks<IMyRadioAntenna>(controlledBlocks);
            AddBlocks<IMyBeacon>(controlledBlocks);
            AddBlocks<IMyOreDetector>(controlledBlocks);
            AddBlocks<IMyTextPanel>(controlledBlocks);
            AddBlocks<IMyProgrammableBlock>(controlledBlocks);
        }

        void AddBlocks<T>(List<IMyFunctionalBlock> blocks) where T : class, IMyFunctionalBlock
        {
            var tempList = new List<T>();

            GridTerminalSystem.GetBlocksOfType(tempList, tempBlock =>
                tempBlock.IsSameConstructAs(me) &&
                !ContainsIgnore(tempBlock.CustomName)
            );

            foreach (var block in tempList)
                blocks.Add(block);
        }

        bool ContainsIgnore(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            return text.IndexOf("ignore", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        bool IsHydrogenTank(IMyGasTank tank)
        {
            return tank.BlockDefinition.SubtypeName
                .IndexOf("Hydrogen", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        bool IsAnyConnectorConnected()
        {
            foreach (IMyShipConnector connector in connectors)
            {
                if (connector.Status == MyShipConnectorStatus.Connected)
                    return true;
            }
            return false;
        }

        void SetBlocks(bool enabled)
        {
            //Always turn tools OFF when dock/undock
            controlledToolBlocks.ForEach(b => b.Enabled = false);

            //Toggle other blocks when dock/undock
            foreach (IMyFunctionalBlock cachedBlock in controlledBlocks)
            {
                if (cachedBlock != null && cachedBlock.IsFunctional)
                    cachedBlock.Enabled = enabled;
            }

            isDockMode = !enabled;
        }

        void StockpileTanks(bool stockpile)
        {
            foreach (IMyGasTank tank in tanks)
            {
                if (tank != null && tank.IsFunctional)
                    tank.Stockpile = stockpile;
            }
        }

        void ChargeBatteries()
        {
            if (backupBattery != null)
                backupBattery.ChargeMode = ChargeMode.Auto;

            foreach (IMyBatteryBlock battery in batteries)
            {
                battery.ChargeMode = ChargeMode.Recharge;
            }
        }

        void AutoBatteries()
        {
            if (backupBattery != null)
                backupBattery.ChargeMode = ChargeMode.Recharge;

            foreach (IMyBatteryBlock battery in batteries)
            {
                battery.ChargeMode = ChargeMode.Auto;
            }
        }

        //Info LCDs

        void CacheBlocksLCD()
        {
            lcds1.Clear();
            lcds2.Clear();
            antennas.Clear();

            gridName = me.CubeGrid.CustomName;

            AddLCDsToList(lcds1, __Lcd1Tag);
            AddLCDsToList(lcds2, __Lcd2Tag);

            GetOwnGridBlocks(antennas);
            if (__controlAntennas)
            {
                foreach (IMyRadioAntenna antenna in antennas)
                {
                    if (string.IsNullOrEmpty(antenna.HudText)) antenna.HudText = gridName;
                }
            }

            firstRun = true;
        }

        private void AddLCDsToList(List<IMyTextSurface> lcds, string LCD_TAG)
        {

            // LCDs
            var blocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyTextSurfaceProvider>(blocks, block =>
                block.IsSameConstructAs(me) &&
                block.CustomName.Contains(LCD_TAG)
            );

            foreach (IMyTextSurfaceProvider surfaceProvider in blocks)
            {
                // Only take the first surface (index 0)
                if (surfaceProvider.SurfaceCount > 0)
                {
                    var surface = surfaceProvider.GetSurface(0);

                    lcds.Add(SetupSurface(surface));
                }
            }
        }

        private static IMyTextSurface SetupSurface(IMyTextSurface surface)
        {
            surface.ContentType = ContentType.TEXT_AND_IMAGE;
            surface.Font = "DEBUG";
            surface.FontSize = 1.7f;
            surface.Alignment = TextAlignment.LEFT;
            return surface;
        }

        void WriteInfo()
        {
            // Mass
            var mass = controller.CalculateShipMass();

            // Hydrogen
            double h2Cap = 0, h2Fill = 0;
            foreach (var tank in h2Tanks)
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

            H2CapacityPercent = h2Fill / h2Cap * 100;

            // Batteries
            double batCap = 0, batStored = 0;
            double batIn = 0, batOut = 0;

            foreach (var battery in batteries)
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

            stringBuilder.AppendLine($"H2: {H2CapacityPercent:0}% - {h2Time}");

            stringBuilder.AppendLine($"Bat:  {batStored / batCap * 100:0}% - {batTime}");

            foreach (IMyTextSurface lcd1 in lcds1)
                lcd1.WriteText(stringBuilder.ToString());
        }

        void WriteInfo2()
        {
            // Velocity & acceleration
            Vector3D velocity = controller.GetShipVelocities().LinearVelocity;
            Vector3D accel = Vector3D.Zero;

            if (!firstRun)
                accel = (velocity - lastVelocity) / Runtime.TimeSinceLastRun.TotalSeconds;

            lastVelocity = velocity;
            firstRun = false;

            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.AppendLine(gridName);
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

            foreach (IMyTextSurface lcd2 in lcds2)
                lcd2.WriteText(stringBuilder.ToString());
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

            public IEnumerable<SpeedTime> GetValues()
            {
                return speedTimeValues;
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
            naturalGrav = controller.GetNaturalGravity();
            gravity = naturalGrav.Length();

            mass = controller.CalculateShipMass().PhysicalMass;
            maxYDecel = GetMaxDecel(upwardThrusters);
            maxZDecel = GetMaxDecel(breakingThrusters);

            GetShipAxisVelocities();

            tickCount++;
            if (tickCount % 10 == 0)
            {
                gravityRatio = gravity / oldGravity;
                oldGravity = gravity;
            }

            controller.TryGetPlanetElevation(MyPlanetElevation.Surface, out alt);

            var paramSpeed = command.Param.Number;
            cruiseSpeed = (paramSpeed == 0 ? __maxSpeed : MathHelper.Clamp(command.Param.Number, MinSpeed, __maxSpeed));

            climbRate = GetGravityAlignedVerticalVelocity();
            vEffectiveYSpeed = climbRate + maxYDecel * Runtime.TimeSinceLastRun.TotalSeconds;
            vEffectiveZSpeed = forwardVelocity + maxZDecel * Runtime.TimeSinceLastRun.TotalSeconds;

            stopYDist = Math.Abs((vEffectiveYSpeed * vEffectiveYSpeed) / (2 * maxYDecel));
            stopZDist = Math.Abs((vEffectiveZSpeed * vEffectiveZSpeed) / (2 * maxZDecel));

            effectiveAlt = alt - vEffectiveYSpeed * Runtime.TimeSinceLastRun.TotalSeconds - gridHight;
            effectiveAlt = effectiveAlt / gravityRatio;

            timeToImpact = alt / Math.Abs(vEffectiveYSpeed);
            timeToStopY = Math.Abs(climbRate / maxYDecel);
            timeToStopZ = Math.Abs(forwardVelocity / maxZDecel);

            netDecel = ComputeNetDecel();

            if (b.autoPilotToggle)
            {
                distanceToLine = DistanceToGps(controller, command.Param.TargetCoordinates);
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

            controller.DampenersOverride = true;
            b.autoPilotToggle = false;

            Runtime.UpdateFrequency = UpdateFrequency.Update10;

            tickCount = 0;
            ResetGyros();
            ResetThrusters();
        }

        void SoftAbort()
        {
            controller.DampenersOverride = true;
            b.stopCruiseWhenOutOfGrav = false;

            ResetGyros();
            ResetThrusters();
        }

        void ResetGyros()
        {
            foreach (var g in gyros)
            {
                g.GyroOverride = false;
                g.Enabled = true;
            }
        }

        void ResetThrusters()
        {
            currentOverride = 0;

            foreach (var forwardThruster in forwardThrusters)
            {
                forwardThruster.ThrustOverridePercentage = 0f;
                forwardThruster.Enabled = true;
            }

            foreach (var brakingThruster in breakingThrusters)
            {
                brakingThruster.ThrustOverridePercentage = 0f;
                brakingThruster.Enabled = true;
            }

            foreach (var upThruster in upwardThrusters)
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
            Vector3D shipUp = controller.WorldMatrix.Up;

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
                foreach (var g in gyros)
                    g.GyroOverride = false;

                return true;
            }

            axis /= angle;

            Vector3D angVel = controller.GetShipVelocities().AngularVelocity;

            //-----------------------------------
            // ⭐ ANGULAR RATE LIMIT
            //-----------------------------------

            const double MAX_ROT_RATE = 0.6; // radians/sec
            const double RESPONSE = 1.0;     // lower = smoother

            Vector3D desiredRate = axis * Math.Min(angle * RESPONSE, MAX_ROT_RATE);

            //-----------------------------------
            // PD controller on angular velocity
            //-----------------------------------

            Vector3D correction = desiredRate - angVel;

            //-----------------------------------

            foreach (var g in gyros)
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
        /// SAFE DESCENT
        ////////////////////////////////////////////////////////
        bool SuicideBurn()
        {
            if (netDecel - 1 < 0)
            {
                Abort();
                command.State = MainStateEnum.Cruise;
                command.Param.Text = "orbit";
            }

            controller.DampenersOverride = false;
            AlignToGravity();
            MatchVerticalSpeed(-104);
            return effectiveAlt < 1.1 * stopYDist + gridHight;
        }

        bool AutoLand()
        {
            if (netDecel - 0.5 < 0)
            {
                Abort();
                command.State = MainStateEnum.Cruise;
                command.Param.Text = "orbit";
            }

            controller.DampenersOverride = false;
            AlignToGravity();

            double speedFromAlt = (100 + alt) * 0.08;
            double speedFromAccel = 20 * netDecel;
            double speedMin = -Math.Min(speedFromAlt, speedFromAccel);

            if (speedMin > -104) MatchVerticalSpeed(speedMin);
            return effectiveAlt < 10 + 2 * gridHight;
        }

        bool TryLock()
        {
            AlignToGravity();
            MatchVerticalSpeed(-2);
            controller.DampenersOverride = true;

            foreach (var g in gears)
                g.Lock();

            return gears.Exists(g => g.IsLocked);
        }

        ////////////////////////////////////////////////////////
        /// PHYSICS HELPERS
        ////////////////////////////////////////////////////////

        double GetGravityAlignedVerticalVelocity()
        {
            Vector3D gNorm = Vector3D.Normalize(naturalGrav);

            return -controller.GetShipVelocities()
                .LinearVelocity.Dot(gNorm);
        }

        void GetShipAxisVelocities()
        {
            Vector3D velocity = controller.GetShipVelocities().LinearVelocity;
            MatrixD wm = controller.WorldMatrix;

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

            foreach (var t in upwardThrusters)
                t.ThrustOverridePercentage = (float)output;
        }

        double SumThrust()
        {
            double total = 0;

            foreach (var t in upwardThrusters)
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
            foreach (var t in upwardThrusters) maxThrustUp += t.MaxEffectiveThrust;

            double thrustAccel = maxThrustUp / mass;

            return thrustAccel - gravity;  // positive = can decelerate
        }

        /// <summary>
        /// Rotates the ship's Up vector toward the ship's Forward vector (nose-UP pitch).
        /// Positive angleDeg = nose UP.
        /// </summary>
        Vector3D RotateUpTowardForwardForNoseUp(double angleDeg)
        {
            if (controller == null)
                return Vector3D.Up;

            Vector3D currentUp = controller.WorldMatrix.Up;
            Vector3D rightAxis = controller.WorldMatrix.Right;  // pitch axis

            double angleRad = MathHelper.ToRadians(angleDeg);
            MatrixD rotation = MatrixD.CreateFromAxisAngle(rightAxis, -angleRad);  // NEGATIVE = nose UP!

            Vector3D rotatedUp = Vector3D.TransformNormal(currentUp, rotation);
            return Vector3D.Normalize(rotatedUp);
        }

        private double GetMaxPitchAngle()
        {
            double fwdThrust = 0, upThrust = 0;
            foreach (var t in forwardThrusters)
                if (t.IsFunctional) fwdThrust += t.MaxEffectiveThrust;
            foreach (var t in upwardThrusters)
                if (t.IsFunctional) upThrust += t.MaxEffectiveThrust;

            return MathHelper.ToDegrees(Math.Atan2(fwdThrust, upThrust));
        }

        // ────────────────────────────────────────────────
        // Load config from CustomData (INI style)
        // ────────────────────────────────────────────────        
        private void ParseIni()
        {
            ini.Clear();
            string customData = me.CustomData;
            bool parsed = ini.TryParse(customData);

            string sectionName = SectionName;

            if (!ini.ContainsSection(sectionName))
            {
                ini.AddSection(sectionName);
            }

            __fsGroupTag = ini.Get(sectionName, INI_FS_GROUP_TAG).ToString(__fsGroupTag);
            __overrideBlockTag = ini.Get(sectionName, INI_OVERRIDE_BLOCKS_TAG).ToString(__overrideBlockTag);
            __ignoreTag = ini.Get(sectionName, INI_IGNORE_TAG).ToString(__ignoreTag);
            __Lcd1Tag = ini.Get(sectionName, INI_LCD1_TAG).ToString(__Lcd1Tag);
            __Lcd2Tag = ini.Get(sectionName, INI_LCD2_TAG).ToString(__Lcd2Tag);
            __maxSpeed = (float)ini.Get(sectionName, MAX_SPEED).ToDouble(__maxSpeed);
            __cnavAltitude = (float)ini.Get(sectionName, CNAV_ALTITUDE).ToDouble(__cnavAltitude);
            __distanceToGPS = (float)ini.Get(sectionName, DISTANCE_TO_GPS).ToDouble(__distanceToGPS);
            __minimumAcceptedFuel = ini.Get(sectionName, MINIMUM_ACCEPTED_FUEL).ToDouble(__minimumAcceptedFuel);
            __allowDockMode = ini.Get(sectionName, DOCK_MODE).ToBoolean(__allowDockMode);
            __controlAntennas = ini.Get(sectionName, CONTROL_ANTENNAS).ToBoolean(__controlAntennas);


            ini.Set(SectionName, INI_FS_GROUP_TAG, __fsGroupTag);
            ini.Set(SectionName, INI_OVERRIDE_BLOCKS_TAG, __overrideBlockTag);
            ini.Set(SectionName, INI_IGNORE_TAG, __ignoreTag);
            ini.Set(SectionName, INI_LCD1_TAG, __Lcd1Tag);
            ini.Set(SectionName, INI_LCD2_TAG, __Lcd2Tag);
            ini.Set(SectionName, MAX_SPEED, __maxSpeed);
            ini.Set(SectionName, CNAV_ALTITUDE, __cnavAltitude);
            ini.Set(SectionName, DISTANCE_TO_GPS, __distanceToGPS);
            ini.Set(SectionName, MINIMUM_ACCEPTED_FUEL, __minimumAcceptedFuel);
            ini.Set(SectionName, DOCK_MODE, __allowDockMode);
            ini.Set(SectionName, CONTROL_ANTENNAS, __controlAntennas);

            string output = ini.ToString();
            me.CustomData = output;
            if (!string.Equals(output, me.CustomData))
            {
                me.CustomData = output;
            }
        }
    }
}
