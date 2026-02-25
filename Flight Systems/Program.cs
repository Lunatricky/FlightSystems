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
using IngameScript;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
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
        double stopDist;
        double mass;
        double cruiseSpeed;
        double climbRate;
        double vEffectiveSpeed;
        double maxDecel;
        double gravity;
        double oldGravity;
        double gravityRatio = 1;
        Vector3D naturalGrav;
        double timeToImpact;
        double timeToStop;
        double thrust = 0;

        double ctrlGridHight;
        double gearGridHight;
        double gridHight;

        double forwardVelocity;
        double rightVelocity;
        double upVelocity;

        double netDecel;
        double maxThrustUp;

        Vector3D desiredUpVector;

        List<IMyGyro> gyros = new List<IMyGyro>();
        List<IMyLandingGear> gears = new List<IMyLandingGear>();

        // Cruise Control
        // Forward Speed Limiter + Cruise Control Fields

        const double SPEED_TOLERANCE = 0.5;  // m/s deadzone
        const double OVERRIDE_STEP = 0.05;   // cruise adjustment rate

        double MinSpeed = 20; // m/s
        double MaxSpeed = 99; // m/s

        readonly List<IMyThrust> breakingThrusters = new List<IMyThrust>();
        readonly List<IMyThrust> forwardThrusters = new List<IMyThrust>();
        readonly List<IMyThrust> upwardThrusters = new List<IMyThrust>();

        IMyRemoteControl controller;

        bool cruiseToggle = false;
        bool circumnavToggle = false;
        bool lastCheckIsOnNatGrav = false;
        bool stopCruiseWhenOutOfGrav = false;

        double currentOverride = 0.0;

        // Circunavigation
        // CNav fields


        // Docking Routine
        // Connector-based Function Block Shutdown Fields

        const string OVERRIDE_BLOCKS = "[FS_override]";
        const string IGNORE_TAG = "[FS_ignore]";

        readonly List<IMyFunctionalBlock> cachedBlocks = new List<IMyFunctionalBlock>();
        readonly List<IMyFunctionalBlock> controlledBlocks = new List<IMyFunctionalBlock>();
        readonly HashSet<long> overrideBlocks = new HashSet<long>();
        readonly List<IMyShipConnector> connectors = new List<IMyShipConnector>();
        readonly List<IMyGasTank> tanks = new List<IMyGasTank>();
        readonly List<IMyGasTank> h2Tanks = new List<IMyGasTank>();
        readonly List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();


        IMyBatteryBlock backupBattery;

        bool isDockMode = false;
        bool lastConnectedState = false;

        // Info LCDs
        // Info LCD Telemetry Script Fields

        const string LCD1_TAG = "[FS_LCD1]";
        const string LCD2_TAG = "[FS_LCD2]";
        readonly List<IMyTextSurface> lcds1 = new List<IMyTextSurface>();
        readonly List<IMyTextSurface> lcds2 = new List<IMyTextSurface>();

        string gridName;

        Vector3D lastVelocity;
        double lastH2Fill = 0;
        bool firstRun = true;

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
            public double Number;
            public string Text;
            public SuicideBurnStateEnum SuicideBurnState;

            // ────────────────────────────────────────────────
            // Constructors — one per type
            // ────────────────────────────────────────────────
            public CommandParam(double n)
            {
                Type = ParamType.Number;
                Number = n;
                Text = null;
                SuicideBurnState = SuicideBurnStateEnum.Idle;
            }

            public CommandParam(string t)
            {
                Type = ParamType.Text;
                Number = 0;
                Text = t ?? "";
                SuicideBurnState = SuicideBurnStateEnum.Idle;
            }

            public CommandParam(SuicideBurnStateEnum s)
            {
                Type = ParamType.SuicideBurnState;
                Number = 0;
                Text = null;
                SuicideBurnState = s;
            }

            // Empty
            public static CommandParam Empty => new CommandParam(null);

            // Optional: fallback helpers (still clean)
            public double GetNumberOr(double fallback) => Type == ParamType.Number ? Number : fallback;
            public string GetTextOr(string fallback) => Type == ParamType.Text ? Text : fallback;
            public SuicideBurnStateEnum GetSuicideStateOr(SuicideBurnStateEnum fallback)
                => Type == ParamType.SuicideBurnState ? SuicideBurnState : fallback;
        }

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            Reload();

            Me.CustomData = CustomDataInfo();

            bool anyConnected = IsAnyConnectorConnected();
            isDockMode = anyConnected;
            DockToggle(anyConnected);
        }

        Command command = Command.Empty;

        public void Main(string argument, UpdateType updateSource)
        {
            if (!string.IsNullOrEmpty(argument)) command = ParseCommand(argument);

            StringBuilder scriptInfo = new StringBuilder();

            ScriptInfoHeader(scriptInfo);

            if (!isDockMode)
            {
                UpdatePhysics();
                ScriptInfoPhysics(scriptInfo);

                bool anyConnected = IsAnyConnectorConnected();
                isDockMode = anyConnected;

                if (anyConnected != lastConnectedState)
                {
                    DockToggle(anyConnected);
                    lastConnectedState = anyConnected;
                }
            }
            ScriptInfoBlocks(scriptInfo);

            Echo(scriptInfo.ToString());
            Me.GetSurface(0).WriteText(scriptInfo.ToString());

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
                    break;
                case MainStateEnum.Cruise:
                    CruiseControlStateSwitch(command.Param);
                    break;
                case MainStateEnum.CNav: // Circumnavigation
                    CircumNavigateStateSwitch(command.Param);
                    break;
                case MainStateEnum.SBurn: // Suicide Burn
                    if (gravity == 0)
                    {
                        Abort();
                        return;
                    }
                    if (command.Param.SuicideBurnState == SuicideBurnStateEnum.Idle) StartSuicideBurn();
                    SuicideBurnStateSwitch(command.Param);
                    break;
                case MainStateEnum.GEntry: // Gliding Entry
                    GlidingEntry(command.Param);
                    break;
                default:
                    ManualLimiter();
                    break;
            }

            if (isDockMode) return;

            // Stop cruise control when leaves atmosphere?

            if (stopCruiseWhenOutOfGrav && lastCheckIsOnNatGrav && gravity == 0.0)
            {
                stopCruiseWhenOutOfGrav = lastCheckIsOnNatGrav = cruiseToggle = false;
                Abort();
            }
            else
            {
                lastCheckIsOnNatGrav = gravity > 0.0;
            }

            // Info LCDs

            if (lcds1.Count > 0) WriteInfo();
            if (lcds2.Count > 0) WriteInfo2();
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

            CommandParam param;
            double num;
            if (double.TryParse(second, out num))
                param = new CommandParam(num);
            else
                param = new CommandParam(second.ToLowerInvariant());

            return new Command(cmd, param);
        }

        public StringBuilder ScriptInfoHeader(StringBuilder scriptInfo)
        {
            scriptInfo.Clear();
            scriptInfo.AppendLine(gridName);
            scriptInfo.AppendLine(new string('-', 28));
            scriptInfo.AppendLine("State: " + command.State);

            if (command.Param.Text != "")
                scriptInfo.AppendLine("Param: " + command.Param.Text);
            if (command.Param.Number != 0)
                scriptInfo.AppendLine("Param: " + command.Param.Number);
            if (command.Param.SuicideBurnState != SuicideBurnStateEnum.Idle)
                scriptInfo.AppendLine("Param: " + command.Param.SuicideBurnState);

            return scriptInfo;
        }

        public StringBuilder ScriptInfoPhysics(StringBuilder scriptInfo)
        {
            scriptInfo.AppendLine();

            if (gravity > 0)
            {
                scriptInfo.AppendLine($"Alt: {alt:F2} m");
                scriptInfo.AppendLine($"Rate of climb: {climbRate:F2} m/s");
            }

            scriptInfo.AppendLine($"Longitudinal velocity: {forwardVelocity:F2} m/s");
            scriptInfo.AppendLine($"Lateral velocity: {rightVelocity:F2} m/s");
            scriptInfo.AppendLine($"Vertical velocity: {upVelocity:F2} m/s");

            switch (command.State)
            {
                case MainStateEnum.SBurn:
                case MainStateEnum.GEntry:
                    scriptInfo.AppendLine($"timeToImpact: {timeToImpact:F2} s");
                    scriptInfo.AppendLine($"gravity: {gravity:F2} m²/s");
                    scriptInfo.AppendLine($"Max upward accel: {maxDecel:F2} m²/s");
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

        public string CustomDataInfo()
        {
            StringBuilder customDataInfo = new StringBuilder();
            customDataInfo.AppendLine("LCD 1 Tag: " + LCD1_TAG);
            customDataInfo.AppendLine("LCD 2 Tag: " + LCD2_TAG);
            customDataInfo.AppendLine("Dock Mode Ignore Tag: " + IGNORE_TAG);
            customDataInfo.AppendLine("Dock Mode Override Tag: " + OVERRIDE_BLOCKS);
            return customDataInfo.ToString();
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
                    cruiseToggle = !cruiseToggle;
                    if (cruiseToggle) command.Param.Text = "on";
                    else command.Param.Text = "off";
                    break;
                case "on":
                    CruiseControl(cruiseSpeed);
                    break;
                case "off":
                    Abort();
                    break;
                case "orbit":
                    cruiseToggle = !cruiseToggle;
                    if (cruiseToggle)
                    {
                        command.Param.Text = "align";
                        stopCruiseWhenOutOfGrav = true;
                        CruiseControl(cruiseSpeed);
                    }
                    else
                    {
                        Abort();
                    }
                    break;
                case "align":
                    if (AlignToGravity())
                    {
                        double pitchAngle = CalculateOptimalClimbPitch(gravity, mass, GetUpThrust(), GetForwardThrust());
                        desiredUpVector = RotateUpTowardForward(controller, 180 + pitchAngle);
                        command.Param.Text = "climb";
                    }
                    break;
                case "climb":
                    AlignToGravity(desiredUpVector);
                    CruiseControl(cruiseSpeed);
                    break;
            }
        }

        void CircumNavigateStateSwitch(CommandParam param)
        {
            switch (param.Text.ToLowerInvariant())
            {
                case "toggle":
                case "":
                    circumnavToggle = !circumnavToggle;
                    if (circumnavToggle) command.Param.Text = "on";
                    else command.Param.Text = "off";
                    break;
                case "on":
                    CircumNav(cruiseSpeed);
                    break;
                case "off":
                    Abort();
                    break;
            }
        }

        void SuicideBurnStateSwitch(CommandParam param)
        {
            switch (param.SuicideBurnState)
            {
                case SuicideBurnStateEnum.Idle:
                    break;

                case SuicideBurnStateEnum.Align:
                    SoftAbort();
                    if (AlignToGravity(true)) command.Param.SuicideBurnState = SuicideBurnStateEnum.Drop;
                    break;

                case SuicideBurnStateEnum.Drop:
                    if (SuicideBurn()) command.Param.SuicideBurnState = SuicideBurnStateEnum.LockGear;
                    break;

                case SuicideBurnStateEnum.LockGear:
                    if (TryLock()) Abort();
                    break;
            }
        }

        private void Reload()
        {
            SetupSurface(Me.GetSurface(0));
            LoadOverrideGroup();
            CacheBlocksCC();
            CacheBlocksLand();
            CacheBlocksDock();
            CacheBlocksLCD();
            lastCheckIsOnNatGrav = controller.GetNaturalGravity().LengthSquared() > 0;

            KillGyroOverride();
            KillThrustOverride();
            controller.DampenersOverride = true;
        }

        void GetOwnGridBlocks<T>(List<T> list) where T : class, IMyTerminalBlock
        {
            list.Clear();
            GridTerminalSystem.GetBlocksOfType(list, block => 
            (block.IsSameConstructAs(Me) && !block.CustomName.Contains(IGNORE_TAG))
            );
        }

        void CacheBlocksCC()
        {
            forwardThrusters.Clear();
            breakingThrusters.Clear();
            upwardThrusters.Clear();

            var controllers = new List<IMyRemoteControl>();
            GridTerminalSystem.GetBlocksOfType(controllers, controller =>
               controller.IsSameConstructAs(Me) && controller.IsMainCockpit);

            if (controllers.Count == 0)
                GetOwnGridBlocks(controllers);

            controller = controllers.Count > 0 ? controllers[0] : null;

            var allThrusters = new List<IMyThrust>();
            GridTerminalSystem.GetBlocksOfType(allThrusters, thruster =>
               thruster.IsSameConstructAs(Me) &&
               thruster.IsFunctional);

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

            // world positions
            Vector3D ctrlPos = controller.GetPosition();
            Vector3D gearPos = gears[0].GetPosition();

            // project onto gravity vector
            ctrlGridHight = ctrlPos.Dot(gravityDir);
            gearGridHight = gearPos.Dot(gravityDir);

            // height difference along gravity
            gridHight = Math.Abs(ctrlGridHight - gearGridHight);
        }

        void GlidingEntry(CommandParam param)
        {
            double targeAltitude = param.Number;
            CruiseControl(MaxSpeed);
            if (effectiveAlt < targeAltitude)
            {

            }

        }

        void ManualLimiter()
        {
            bool allowThrust = forwardVelocity < MaxSpeed;

            // Normal forward thrust behavior
            foreach (var forwardThruster in forwardThrusters)
            {
                forwardThruster.ThrustOverridePercentage = 0f;
                forwardThruster.Enabled = allowThrust;
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
            foreach (var brakingThruster in breakingThrusters)
                brakingThruster.Enabled = false;

            // Control forward thrust smoothly
            foreach (var forwardThruster in forwardThrusters)
            {
                forwardThruster.Enabled = true;
                forwardThruster.ThrustOverridePercentage = (float)currentOverride;
            }

        }

        void CircumNav(double speed)
        {
            AlignToGravity();
            CruiseControl(speed);
        }

        //Docking Routine

        void LoadOverrideGroup()
        {
            overrideBlocks.Clear();

            var blocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks, b =>
                b.IsSameConstructAs(Me) &&
                b.CustomName.Contains(OVERRIDE_BLOCKS)
            );

            foreach (var block in blocks)
            {
                if (block.IsSameConstructAs(Me))
                    overrideBlocks.Add(block.EntityId);
            }
        }

        void CacheBlocksDock()
        {
            cachedBlocks.Clear();
            controlledBlocks.Clear();
            connectors.Clear();
            tanks.Clear();
            h2Tanks.Clear();
            batteries.Clear();

            // Functional blocks to power on/off
            var temp = new List<IMyFunctionalBlock>();
            GridTerminalSystem.GetBlocksOfType(temp, b =>
                b.IsSameConstructAs(Me) &&
                !ContainsIgnore(b.CustomName) &&
                !ContainsIgnore(b.CustomData) &&
                b != Me &&
                !(b is IMyShipConnector) &&
                !(b is IMyBatteryBlock) &&
                !(b is IMyDoor) &&
                !(b is IMyAirVent) &&
                !(b is IMyCryoChamber) &&
                !(b is IMyMedicalRoom) &&
                !(b is IMyGasTank) &&
                !(b is IMyTimerBlock) &&
                !(b is IMyEventControllerBlock) &&
                !(b is IMyInteriorLight) &&
                !(b is IMyLandingGear) &&
                !IsSurvivalKit(b)
            );

            cachedBlocks.AddRange(temp);


            IMyBlockGroup group = GridTerminalSystem.GetBlockGroupWithName("Auto Managed");

            if (group != null)
                group.GetBlocksOfType(controlledBlocks, block =>
                    block.IsSameConstructAs(Me));

            if (controlledBlocks.Count == 0)
            {
                ReloadControlledBlocks();
                controlledBlocks.Remove(Me);
            }


            // Connectors, Tanks & Batteries (own construct only)
            GetOwnGridBlocks(connectors);
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
                    if (!battery.Closed && battery.CustomName.ToLower().Contains("backup"))
                    {
                        backupBattery = battery;
                        break;
                    }
                }
                batteries.Remove(backupBattery);
            }

        }

        void ReloadControlledBlocks()
        {
            controlledBlocks.Clear();

            AddBlocks<IMyThrust>();
            AddBlocks<IMyMechanicalConnectionBlock>();
            AddBlocks<IMyShipToolBase>();
            AddBlocks<IMyReflectorLight>();
            AddBlocks<IMySearchlight>();
            AddBlocks<IMySensorBlock>();
            AddBlocks<IMyLaserAntenna>();
            AddBlocks<IMyRadioAntenna>();
            AddBlocks<IMyBeacon>();
            AddBlocks<IMyOreDetector>();
            AddBlocks<IMyTextPanel>();
            AddBlocks<IMyProgrammableBlock>();
        }

        void AddBlocks<T>() where T : class, IMyFunctionalBlock
        {
            var tempList = new List<T>();

            GridTerminalSystem.GetBlocksOfType(tempList, tempBlock =>
                tempBlock.IsSameConstructAs(Me) &&
                !ContainsIgnore(tempBlock.CustomName)
            );

            foreach (var block in tempList)
                controlledBlocks.Add(block);
        }

        bool ContainsIgnore(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            return text.IndexOf("ignore", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        bool IsSurvivalKit(IMyFunctionalBlock b)
        {
            return b.BlockDefinition.SubtypeName
                .IndexOf("SurvivalKit", StringComparison.OrdinalIgnoreCase) >= 0;
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
            backupBattery.ChargeMode = ChargeMode.Auto;

            foreach (IMyBatteryBlock battery in batteries)
            {
                battery.ChargeMode = ChargeMode.Recharge;
            }
        }

        void AutoBatteries()
        {
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

            gridName = Me.CubeGrid.CustomName;

            AddLCDsToList(lcds1, LCD1_TAG);
            AddLCDsToList(lcds2, LCD2_TAG);

            firstRun = true;
        }

        private void AddLCDsToList(List<IMyTextSurface> lcds, string LCD_TAG)
        {

            // LCDs
            var blocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyTextSurfaceProvider>(blocks, block =>
                block.IsSameConstructAs(Me) &&
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
            surface.FontSize = 1.5f;
            surface.Alignment = TextAlignment.LEFT;
            return surface;
        }

        void WriteInfo()
        {
            // Velocity & acceleration
            Vector3D velocity = controller.GetShipVelocities().LinearVelocity;
            Vector3D accel = Vector3D.Zero;

            if (!firstRun)
                accel = (velocity - lastVelocity) / Runtime.TimeSinceLastRun.TotalSeconds;

            lastVelocity = velocity;
            firstRun = false;

            // Mass
            var mass = controller.CalculateShipMass();

            // Hydrogen
            double H2CapacityPercent;
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
                    batTime = FormatTime((batCap - batStored) / netPower) + " /\\";
                else
                    batTime = FormatTime(batStored / -netPower) + " \\/";
            }

            // Output
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(gridName);
            stringBuilder.AppendLine(new string('-', 28));

            stringBuilder.AppendLine($"stopDist: {stopDist:F2} m");
            stringBuilder.AppendLine($"Accel: {accel.Length() / 9.81:F2} g");
            stringBuilder.AppendLine($"Mass: {mass.PhysicalMass / 1000:0.0} t");
            stringBuilder.AppendLine($"Empty Mass: {mass.BaseMass / 1000:0.0} t");

            stringBuilder.AppendLine($"H2: {H2CapacityPercent:0}%");
            stringBuilder.AppendLine($"H2 Time: {h2Time}");

            stringBuilder.AppendLine($"Bat:  {batStored / batCap * 100:0} %");
            stringBuilder.AppendLine($"Bat Time: {batTime}");

            foreach(IMyTextSurface lcd1 in lcds1)
                lcd1.WriteText(stringBuilder.ToString());
        }

        void WriteInfo2()
        {
            // Altitude
            string seaAltText = "No g";
            string groundAltText = "No g";

            double sea, ground;
            if (controller.GetNaturalGravity().LengthSquared() > 1e-3)
            {
                if (controller.TryGetPlanetElevation(MyPlanetElevation.Sealevel, out sea))
                    seaAltText = $"{sea:0} m";

                if (controller.TryGetPlanetElevation(MyPlanetElevation.Surface, out ground))
                    groundAltText = $"{ground:0} m";
            }

            StringBuilder stringBuilder = new StringBuilder();

            ScriptInfoHeader(stringBuilder);

            if (gravity > 0)
            {
                stringBuilder.AppendLine($"Rate of climb: {climbRate:F2}");
            }


            if (command.State == MainStateEnum.SBurn || command.State == MainStateEnum.GEntry)
            {
                stringBuilder.AppendLine($"gravity: {gravity:F2} m²/s");
                stringBuilder.AppendLine($"Max upward accel: {maxDecel:F2} m²/s");
                stringBuilder.AppendLine($"timeToStop: {timeToStop:F2} s");
                stringBuilder.AppendLine($"timeToImpact: {timeToImpact:F2} s");
            }
            else
            {
                stringBuilder.AppendLine($"Longitudinal velocity: {forwardVelocity:F2} m/s");
                stringBuilder.AppendLine($"Lateral velocity: {rightVelocity:F2} m/s");
                stringBuilder.AppendLine($"Vertical velocity: {upVelocity:F2} m/s");
            }

            stringBuilder.AppendLine();

            foreach (IMyTextSurface lcd2 in lcds2)
                lcd2.WriteText(stringBuilder.ToString());
        }


        string FormatTime(double time)
        {
            if (double.IsInfinity(time) || time < 0)
                return "--";

            int intTime = (int)time;
            int hours = intTime / 3600;
            int minutes = (intTime % 3600) / 60;
            int seconds = intTime % 60;

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
            mass = controller.CalculateShipMass().PhysicalMass;
            gravity = naturalGrav.Length();
            maxDecel = GetMaxDecel();

            GetShipAxisVelocities();

            tickCount++;
            if (tickCount % 10 == 0)
            {
                gravityRatio = gravity / oldGravity;
                oldGravity = gravity;
            }

            controller.TryGetPlanetElevation(MyPlanetElevation.Surface, out alt);

            var paramSpeed = command.Param.Number;
            cruiseSpeed = (paramSpeed == 0 ? MaxSpeed : MathHelper.Clamp(command.Param.Number, MinSpeed, MaxSpeed));

            climbRate = GetGravityAlignedVerticalVelocity();
            vEffectiveSpeed = climbRate + maxDecel * Runtime.TimeSinceLastRun.TotalSeconds;

            stopDist = Math.Abs((vEffectiveSpeed * vEffectiveSpeed) / (2 * maxDecel));

            effectiveAlt = alt - vEffectiveSpeed * Runtime.TimeSinceLastRun.TotalSeconds - gridHight;
            effectiveAlt = effectiveAlt / gravityRatio;

            timeToImpact = alt / Math.Abs(vEffectiveSpeed);
            timeToStop = Math.Abs(climbRate) / maxDecel;

            netDecel = ComputeNetDecel();
        }

        void StartSuicideBurn()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            command.Param.SuicideBurnState = SuicideBurnStateEnum.Align;
        }

        void Abort()
        {
            command = Command.Empty;

            controller.DampenersOverride = true;
            stopCruiseWhenOutOfGrav = false;

            Runtime.UpdateFrequency = UpdateFrequency.Update10;

            tickCount = 0;
            ResetGyros();
            ResetThrusters();
        }

        void SoftAbort()
        {
            controller.DampenersOverride = true;
            stopCruiseWhenOutOfGrav = false;

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

        private void KillGyroOverride()
        {
            foreach (var g in gyros)
                g.GyroOverride = false;
        }

        private void KillThrustOverride()
        {

            foreach (var t in upwardThrusters)
                t.ThrustOverridePercentage = 0;
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
            return AlignToGravity(checkSpeed, desiredUp);
        }

        bool AlignToGravity(Vector3D desiredUp)
        {
            return AlignToGravity(false, desiredUp);
        }

        bool AlignToGravity(bool checkSpeed, Vector3D desiredUp)
        {
            if (naturalGrav.LengthSquared() < 0.01)
                return false;

            
            Vector3D shipUp = controller.WorldMatrix.Up;

            Vector3D axis = shipUp.Cross(desiredUp);
            double angle = axis.Length();

            if (angle < 0.002 && (checkSpeed ? IsStopped() : true))
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
        
        /// <summary>
         /// Rotates the input vector (typically ship's Forward) upward by pitch angle around ship's RIGHT axis.
         /// Positive angleDeg = nose UP (climb attitude).
         /// Output: rotated direction vector (normalized).
         /// </summary>
        Vector3D RotatePitchUp(Vector3D inputVector, double angleDeg)
        {
            double angleRad = MathHelper.ToRadians(angleDeg);
            Vector3D pitchAxis = controller.WorldMatrix.Right;  // RIGHT axis for pitch (standard)
            MatrixD rotationMatrix = MatrixD.CreateFromAxisAngle(pitchAxis, angleRad);
            Vector3D rotated = Vector3D.TransformNormal(inputVector, rotationMatrix);
            return Vector3D.Normalize(rotated);  // ensure unit vector
        }

        bool IsStopped(double threshold = 0.1)
        {
            return upVelocity <= threshold && Math.Abs(forwardVelocity) <= threshold && Math.Abs(rightVelocity) <= threshold;
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
                command.Param.Text= "orbit";
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

        double GetMaxDecel()
        {
            thrust = 0;

            Vector3D up = -Vector3D.Normalize(naturalGrav);

            foreach (var t in upwardThrusters)
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

        // Enhanced Suicide Burn Algorithm - C#6 SE PB Compatible
        // Handles varying gravity (Pertam atm/low well): Thrust-based net decel prediction
        // Adaptive target descent V (0-110 m/s): Drops to 0 as net_decel -> 0 (safety!)
        // Recovery: Optimal climb angle from fwd/up thrust ratio (e.g. 45° if equal)
        // Drop-in methods: ComputeNetDecel(), GetSafeDescentTargetV(), GetRecoveryClimbAngle()

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

        Vector3D GetOptimalAntiGravUp()
        {
            double fwdThrust = 0, upThrust = 0;
            foreach (var t in forwardThrusters)
                if (t.IsFunctional) fwdThrust += t.MaxEffectiveThrust;
            foreach (var t in upwardThrusters)
                if (t.IsFunctional) upThrust += t.MaxEffectiveThrust;
            if (upThrust <= 0) return controller.WorldMatrix.Up;  // can't climb
                                                                  // Fixed optimal pitch angle (nose up)
            double optimalPitchRad = Math.Atan(fwdThrust / upThrust);
            double optimalPitchDeg = MathHelper.ToDegrees(optimalPitchRad);
            // Anti-gravity up direction
            Vector3D gravity = controller.GetNaturalGravity();
            if (gravity.LengthSquared() < 0.01) return controller.WorldMatrix.Up;
            Vector3D antiGravUp = Vector3D.Normalize(-gravity);
            // Rotate antiGravUp by -optimalPitchDeg around ship RIGHT (nose up)
            Vector3D rightAxis = controller.WorldMatrix.Right;
            MatrixD rotation = MatrixD.CreateFromAxisAngle(rightAxis, -optimalPitchRad);  // negative for up
            Vector3D desiredUp = Vector3D.TransformNormal(antiGravUp, rotation);
            return Vector3D.Normalize(desiredUp);
        }

        // Usage in descent loop:
        // double netDecel = ComputeNetDecel(upThrusters, mass, -Vector3D.Normalize(naturalGrav));
        // double targetVdown = GetSafeDescentTargetV(netDecel, alt - gridHeight, vSpeed);

        // Then: MatchVerticalSpeed(-targetVdown);  // your PID

        // ────────────────────────────────────────────────
        // 3. RECOVERY CLIMB ANGLE (auto 0-90°)
        // When v_down <=0: atan2(fwd_thrust_max, up_thrust_max)
        // e.g. equal = 45° (max accel up-forward)
        // ────────────────────────────────────────────────
        Vector3D GetMaxThrustVector()
        {
            double fwdThrust = 0, upThrust = 0;

            // Sum max fwd thrust (project fwd if needed)
            foreach (var t in forwardThrusters)
                if (t.IsFunctional) fwdThrust += t.MaxEffectiveThrust;

            // Sum max up thrust
            foreach (var t in upwardThrusters)
                if (t.IsFunctional) upThrust += t.MaxEffectiveThrust;

            Vector3D forward = fwdThrust * controller.WorldMatrix.Forward;
            Vector3D up = upThrust * controller.WorldMatrix.Up;

            return forward + up;
        }

        /// <summary>
        /// Computes the frozen max thrust direction (fwd + up) once, relative to anti-gravity.
        /// Subtracts to get the true offset vector for pitch adjustment.
        /// Returns the desired ship Up vector (pitched nose up for optimal fwd lift).
        /// Call once after aligning to gravity.
        /// </summary>
        /// <param name="fwdThrusters">Forward thrusters list</param>
        /// <param name="upThrusters">Up thrusters list</param>
        /// <param name="controller">Ship controller</param>
        /// <param name="gravity">Current gravity vector</param>
        /// <param name="blendStrength">How much to apply the pitch offset (0 = pure anti-grav, 1 = full offset, default 0.8)</param>
        Vector3D GetDesiredUpForOptimalPitch(double blendStrength = 0.8)
        {
            if (naturalGrav.LengthSquared() < 0.01)
                return controller.WorldMatrix.Up;  // no gravity → no change

            Vector3D antiGravUp = Vector3D.Normalize(-naturalGrav);

            // Compute frozen max thrust direction (once, in current alignment)
            double fwdThrust = 0, upThrust = 0;
            foreach (var t in forwardThrusters)
                if (t.IsFunctional) fwdThrust += t.MaxEffectiveThrust;
            foreach (var t in upwardThrusters)
                if (t.IsFunctional) upThrust += t.MaxEffectiveThrust;

            if (fwdThrust <= 0 && upThrust <= 0)
                return antiGravUp;  // no thrust → pure anti-grav

            Vector3D fwdVec = fwdThrust * controller.WorldMatrix.Forward;
            Vector3D upVec = upThrust * controller.WorldMatrix.Up;

            Vector3D frozenThrustDir = Vector3D.Normalize(fwdVec + upVec);

            // Up direction of frozen thrust = projection on antiGravUp
            double thrustUpProjScalar = Vector3D.Dot(frozenThrustDir, antiGravUp);
            Vector3D thrustUpComponent = antiGravUp * thrustUpProjScalar;

            // Subtract: antiGravUp - thrustUpComponent = the "true offset vector" (horizontal adjustment)
            Vector3D trueOffset = antiGravUp - thrustUpComponent;

            // Normalize offset and blend with antiGravUp
            if (trueOffset.LengthSquared() < 1e-6)
                return antiGravUp;  // no meaningful offset

            Vector3D normOffset = Vector3D.Normalize(trueOffset);
            Vector3D desiredUp = Vector3D.Normalize(antiGravUp + normOffset * blendStrength);

            return desiredUp;
        }

        private Vector3D ComputeFrozenMaxThrustDirection()
        {
            double fwdThrust = 0, upThrust = 0;

            foreach (var t in forwardThrusters)
                if (t.IsFunctional) fwdThrust += t.MaxEffectiveThrust;

            foreach (var t in upwardThrusters)
                if (t.IsFunctional) upThrust += t.MaxEffectiveThrust;

            if (fwdThrust <= 0 && upThrust <= 0)
                return controller.WorldMatrix.Up;  // fallback

            Vector3D fwdVec = fwdThrust * controller.WorldMatrix.Forward;
            Vector3D upVec = upThrust * controller.WorldMatrix.Up;

            Vector3D combined = fwdVec + upVec;

            return Vector3D.Normalize(combined);  // now a pure direction
        }

        /// <summary>
        /// Returns the desired ship Up vector that rotates the current Up toward the average thrust direction,
        /// so forward + up thrusters together provide the best lift against gravity.
        /// The rotation amount is proportional to how much the average thrust deviates from current anti-gravity.
        /// </summary>
        /// <param name="gravity">Gravity vector from controller.GetNaturalGravity()</param>
        /// <param name="controller">The ship controller (for current orientation)</param>
        /// <param name="maxThrustVector">Result from your GetMaxThrustVector() — combined fwd+up thrust direction</param>
        /// <param name="strength">Blending factor: 0 = no rotation (keep current Up), 1 = full rotation to thrust vector (default 0.8)</param>
        Vector3D GetDesiredAntiGravityUp(Vector3D maxThrustVector, double strength = 0.8)
        {
            // Desired anti-gravity direction (upward = opposite gravity)
            Vector3D antiGrav = Vector3D.Normalize(-naturalGrav);

            // Normalize the max thrust vector (it's already a direction after your averaging)
            Vector3D thrustDir = Vector3D.Normalize(maxThrustVector);

            // Blend: how much we want to rotate current Up toward the thrust direction
            Vector3D currentUp = controller.WorldMatrix.Up;
            Vector3D blendedUp = Vector3D.Lerp(currentUp, thrustDir, strength);

            // Project blendedUp onto the plane perpendicular to gravity (to keep it "level-ish")
            // This avoids tilting too far sideways when thrust vector is off-plane
            blendedUp -= antiGrav * Vector3D.Dot(blendedUp, antiGrav);

            // Final normalization
            if (blendedUp.LengthSquared() < 1e-6)
                return antiGrav;  // fallback to pure anti-gravity if degenerate

            return Vector3D.Normalize(blendedUp);
        }

        double AngleBetweenLessThanFast(Vector3D v1, Vector3D v2, double maxAngleRad = 0.02)
        {
            Vector3D n1 = Vector3D.Normalize(v1);
            Vector3D n2 = Vector3D.Normalize(v2);

            double dot = Vector3D.Dot(n1, n2);
            dot = MathHelper.Clamp(dot, -1.0, 1.0);

            // cos(maxAngleRad) is the minimum dot product we accept
            double minCos = Math.Cos(maxAngleRad);

            //return dot >= minCos;
            return dot;
        }

        Vector3D GetMaxClimbVector()
        {
            Vector3D grav = controller.GetNaturalGravity();

            if (gravity < 0.0001)
                return controller.WorldMatrix.Up;

            double weight = mass * gravity;

            double Fu = GetUpThrust();       // aligned to gravity
            double Ff = GetForwardThrust();  // forward thrusters only

            // Case: cannot climb
            if (Fu + Ff <= weight)
                return controller.WorldMatrix.Up;

            // Case: up thrust alone can hover
            if (Fu >= weight)
                return controller.WorldMatrix.Forward;

            double ratio = (weight - Fu) / Ff;
            ratio = MathHelper.Clamp(ratio, 0, 1);

            double theta = Math.Asin(ratio);

            Vector3D up = controller.WorldMatrix.Up;
            Vector3D forward = controller.WorldMatrix.Forward;

            Vector3D desiredUp = (up + forward) * Math.Sin(theta);

            return Vector3D.Normalize(desiredUp);
        }

        double GetUpThrust()
        {
            Vector3D gravDir = Vector3D.Normalize(controller.GetNaturalGravity());
            double total = 0;

            foreach (var t in upwardThrusters)
            {
                double dot = t.WorldMatrix.Backward.Dot(-gravDir);
                if (dot > 0)
                    total += t.MaxEffectiveThrust * dot;
            }

            return total;
        }

        double GetForwardThrust()
        {
            Vector3D forward = controller.WorldMatrix.Forward;
            double total = 0;

            foreach (var t in forwardThrusters)
            {
                double dot = t.WorldMatrix.Backward.Dot(forward);
                if (dot > 0.7)
                    total += t.MaxEffectiveThrust * dot;
            }

            return total;
        }

        public double CalculateOptimalClimbPitch(
            double gravityMagnitude,           // from physics.GravityMagnitude
            double shipMass,                   // controller.CalculateShipMass().PhysicalMass
            double maxThrustUp,                 // precomputed strong upward thrust (N)
            double maxThrustForward)            // precomputed weak forward thrust (N)
        {
            if (gravityMagnitude < 0.05) return 90f;                    // zero-g → go vertical
            if (shipMass <= 0 || maxThrustUp <= 0) return 0f;           // safety

            double requiredVertical = shipMass * gravityMagnitude;      // N needed to balance g

            double a = maxThrustUp;      // strong upward
            double b = maxThrustForward; // weak forward
            double R = Math.Sqrt(a * a + b * b);

            if (requiredVertical > R + 1e-4)
            {
                return 0f;               // can't hover even at best angle → stay level, full power
            }

            double phi = Math.Atan2(b, a);                              // angle of resultant thrust vector
            double acosArg = requiredVertical / R;
            acosArg = Math.Max(-1.0, Math.Min(1.0, acosArg));          // floating-point safety

            double delta = Math.Acos(acosArg);

            // Two mathematical solutions – pick the one in [0, 90°]
            double theta1 = phi + delta;
            double theta2 = phi - delta;

            double thetaRad = (theta1 >= 0 && theta1 <= Math.PI / 2) ? theta1 : theta2;
            if (thetaRad < 0 || thetaRad > Math.PI / 2)
                thetaRad = Math.Max(0, Math.Min(Math.PI / 2, theta1)); // fallback

            double pitchDeg = (thetaRad * 180.0 / Math.PI);

            // Safety cap: never allow net horizontal thrust to go negative
            double sinT = Math.Sin(thetaRad);
            double cosT = Math.Cos(thetaRad);
            double netHorizontal = b * cosT - a * sinT;
            if (netHorizontal < 0)
            {
                // max safe pitch where netHorizontal == 0
                double safeTheta = Math.Atan2(b, a);   // = atan(T_forward / T_up)
                pitchDeg = (float)(safeTheta * 180.0 / Math.PI);
            }

            return pitchDeg;
        }

        /// <summary>
        /// Rotates the ship's Up vector toward the ship's Forward vector (nose-up pitch)
        /// by the specified angle in degrees.
        /// Positive angleDeg = nose UP (Up rotates toward Forward).
        /// Returns the rotated Up vector (normalized).
        /// </summary>
        Vector3D RotateUpTowardForward(IMyShipController controller, double angleDeg)
        {
            if (controller == null)
                return Vector3D.Up;  // fallback

            Vector3D currentUp = controller.WorldMatrix.Up;
            Vector3D rightAxis = controller.WorldMatrix.Right;  // pitch axis

            double angleRad = MathHelper.ToRadians(angleDeg);
            MatrixD rotation = MatrixD.CreateFromAxisAngle(rightAxis, angleRad);

            Vector3D rotatedUp = Vector3D.TransformNormal(currentUp, rotation);
            return Vector3D.Normalize(rotatedUp);
        }
    }
}
