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
        double vSpeed;
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

        // safety buffer (VERY important)
        const double SAFETY = 1.2;

        List<IMyGyro> gyros = new List<IMyGyro>();
        List<IMyLandingGear> gears = new List<IMyLandingGear>();

        // Cruise Control
        // Forward Speed Limiter + Cruise Control Fields

		const double SPEED_TOLERANCE = 0.5;  // m/s deadzone
		const double OVERRIDE_STEP = 0.05;   // cruise adjustment rate

        double MinSpeed = 20; // m/s
        double MaxSpeed = 99; // m/s

        readonly List<IMyThrust> brakingThrusters = new List<IMyThrust>();
        readonly List<IMyThrust> forwardThrusters = new List<IMyThrust>();
        readonly List<IMyThrust> upThrusters = new List<IMyThrust>();

		IMyShipController controller;

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

        const string LCD_TAG = "[FS_LCD]";
        readonly List<IMyTextSurface> lcds = new List<IMyTextSurface>();

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
            public static CommandParam Empty => new CommandParam((string)null);

            // ────────────────────────────────────────────────
            // Single unified "TryGet" style — extensible
            // ────────────────────────────────────────────────
            public bool TryGetNumber(out double value)
            {
                value = Number;
                return Type == ParamType.Number;
            }

            public bool TryGetText(out string value)
            {
                value = Text;
                return Type == ParamType.Text;
            }

            public bool TryGetSuicideState(out SuicideBurnStateEnum value)
            {
                value = SuicideBurnState;
                return Type == ParamType.SuicideBurnState;
            }

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

        string arg = "";
        Command command = Command.Empty;

        public void Main(string argument, UpdateType updateSource)
        {
            if (!string.IsNullOrEmpty(argument)) command = ParseCommand(argument);

            if (!isDockMode) UpdatePhysics();

            arg = (argument != null && argument != "" ? argument : arg);
            Echo("state: " + command.State);
            Echo("SuicideBurnState: " + command.Param.SuicideBurnState);
            Echo($"stopDist: {(SAFETY * (Math.Abs(stopDist) + 2 * gridHight)):F2}");
            Echo($"effectiveAlt < stopDist: {(effectiveAlt < SAFETY * (Math.Abs(stopDist) + 2 * gridHight)):F2}");
            Echo($"alt: {alt:F2}");
            Echo($"stopDist: {stopDist:F2}");
            Echo($"gravity: {gravity:F2}");
            Echo($"maxDecel: {maxDecel:F2}");
            Echo($"mass: {mass:F2}");
            Echo($"thrust: {thrust:F2}");
            Echo($"cruiseSpeed: {cruiseSpeed:F2}");
            Echo($"vSpeed: {vSpeed:F2}");
            Echo($"timeToImpact: {timeToImpact:F2}");
            Echo($"timeToStop: {timeToStop:F2}");

            tickCount++;
            if (tickCount % 10 == 0)
            {
                gravityRatio = gravity / oldGravity;
                oldGravity = gravity;
            }

            Echo("\ngyros: " + gyros.Count);
            Echo("upThrusters: " + upThrusters.Count);
            Echo("forwardThrusters: " + forwardThrusters.Count);
            Echo("brakingThrusters: " + brakingThrusters.Count);
            Echo("gears: " + gears.Count);

            if (isDockMode)
            {
                return;
            }

            bool anyConnected = IsAnyConnectorConnected();
            isDockMode = anyConnected;

            if (anyConnected != lastConnectedState)
            {
                DockToggle(anyConnected);
                lastConnectedState = anyConnected;
            }

            if (isDockMode)
            {
                return;
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
                    break;
                case MainStateEnum.Cruise:
                    CruiseControlStateSwitch(command.Param);
                    break;
                case MainStateEnum.CNav: // Circumnavigation
                    CircumNavigateStateSwitch(command.Param);
                    break;
                case MainStateEnum.SBurn: // Suicide Burn
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

            string stringInfo = ScriptInfo();

            Echo(stringInfo);
            Me.GetSurface(0).WriteText(stringInfo);

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

            if (controller != null && lcds.Count != 0)
            {
                WriteInfo();
            }
        }

        private void DockToggle(bool anyConnected)
        {
            SetBlocks(!anyConnected);
            StockpileTanks(anyConnected);
            if (anyConnected)
            {
                ChargeBatteries();
            } else
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

        public string ScriptInfo()
        {
            StringBuilder scriptInfo = new StringBuilder();
            scriptInfo.Clear();
            scriptInfo.AppendLine("Flight Systems");
            scriptInfo.AppendLine(gridName);
            scriptInfo.AppendLine(new string('-', 28));
            scriptInfo.AppendLine("Dock Mode: " + isDockMode);
            scriptInfo.AppendLine("Cruise Mode: " + cruiseToggle);
            scriptInfo.AppendLine("CircumNavigate Mode: " + circumnavToggle);
            scriptInfo.AppendLine("LCDs: " + lcds.Count);
            scriptInfo.AppendLine("Batteries: " + batteries.Count + " | Tanks: " + tanks.Count);
            scriptInfo.AppendLine("Dock Mode blocks: " + controlledBlocks.Count);

            return scriptInfo.ToString();
        }

        public string CustomDataInfo()
        {
            StringBuilder customDataInfo = new StringBuilder();
            customDataInfo.AppendLine("LCD Tag: " + LCD_TAG);
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
                    DockToggle(isDockMode);
                    break;

                case "off":
                    isDockMode = false;
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
                    command.Param.Text = "on";
                    stopCruiseWhenOutOfGrav = true;
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
                    if (AlignToGravity()) command.Param.SuicideBurnState = SuicideBurnStateEnum.Drop;
                    break;

                case SuicideBurnStateEnum.Drop:
                    if (SuicideBurn()) command.Param.SuicideBurnState = SuicideBurnStateEnum.Cushion;
                    break;

                case SuicideBurnStateEnum.Cushion:
                    if (Cushion()) command.Param.SuicideBurnState = SuicideBurnStateEnum.LockGear;
                    break;

                case SuicideBurnStateEnum.LockGear:
                    if (TryLock()) Abort(); // finished
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
                block.IsSameConstructAs(Me)
            );
        }

        // Cruise Control
        void CacheBlocksCC()
        {
            forwardThrusters.Clear();
			brakingThrusters.Clear();
            upThrusters.Clear();

			var allThrusters = new List<IMyThrust>();
			GridTerminalSystem.GetBlocksOfType(allThrusters, thruster =>
               thruster.IsSameConstructAs(Me) &&
               thruster.IsFunctional);

			foreach (var thruster in allThrusters)
			{
				// Thrusters that push the ship forward
				if (thruster.Orientation.Forward == Base6Directions.Direction.Backward)
					forwardThrusters.Add(thruster);

                // Thrusters that push the ship backward
                else if (thruster.Orientation.Forward == Base6Directions.Direction.Forward)
					brakingThrusters.Add(thruster);

                // Thrusters that push the ship upwards
                else if (thruster.Orientation.Forward == Base6Directions.Direction.Up)
                    upThrusters.Add(thruster);
			}


			var controllers = new List<IMyShipController>();
			GridTerminalSystem.GetBlocksOfType(controllers, controller =>
               controller.IsSameConstructAs(Me) && controller.IsMainCockpit);

			if (controllers.Count == 0)
                GetOwnGridBlocks(controllers);

            controller = controllers.Count > 0 ? controllers[0] : null;
        }

        //CircumNavigate
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

        private double ForwardSpeed()
        {
            Vector3D velocity = controller.GetShipVelocities().LinearVelocity;
            Vector3D forward = controller.WorldMatrix.Forward;

            double speed = Vector3D.Dot(velocity, forward);
            return speed;
        }

        void GlidingEntry(CommandParam param)
        {
            double targeAltitude = param.Number;
            CruiseControl(MaxSpeed);
            if (alt < targeAltitude)
            {

            }

        }

        void ManualLimiter()
        {
            bool allowThrust = ForwardSpeed() < MaxSpeed;

            // Normal forward thrust behavior
            foreach (var forwardThruster in forwardThrusters)
            {
                forwardThruster.ThrustOverridePercentage = 0f;
                forwardThruster.Enabled = allowThrust;
            }

        }

        void CruiseControl(double cruiseSpeed)
        {
            double error = cruiseSpeed - ForwardSpeed();

			if (Math.Abs(error) < SPEED_TOLERANCE)
				return;

			if (error > 0)
				currentOverride += OVERRIDE_STEP;
			else
				currentOverride -= OVERRIDE_STEP;

			currentOverride = MathHelper.Clamp(currentOverride, 0f, 1f);

			// Disable braking thrusters so they don't fight cruise
			foreach (var brakingThruster in brakingThrusters)
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

            foreach(IMyGasTank tank in tanks)
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
            lcds.Clear();

            gridName = Me.CubeGrid.CustomName;

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

            firstRun = true;
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
            var stringBuilder = new StringBuilder();

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
            stringBuilder.AppendLine(gridName);
            stringBuilder.AppendLine(new string('-', 28));
            
            stringBuilder.AppendLine($"Alt (Sea): {seaAltText}");
            stringBuilder.AppendLine($"Alt (Ground): {groundAltText}");
            stringBuilder.AppendLine($"Accel: {accel.Length() / 9.81:F2} g");
            stringBuilder.AppendLine($"Mass: {mass.PhysicalMass / 1000:0.0} t");
            stringBuilder.AppendLine($"Empty Mass: {mass.BaseMass / 1000:0.0} t");
            
            stringBuilder.AppendLine($"H2: {H2CapacityPercent:0}%");
            stringBuilder.AppendLine($"H2 Time: {h2Time}"); 

            stringBuilder.AppendLine($"Bat:  {batStored / batCap * 100:0} %");
            stringBuilder.AppendLine($"Bat Time: {batTime}");
            
            string text = stringBuilder.ToString();

            foreach (var lcd in lcds)
                lcd.WriteText(text);
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


            controller.TryGetPlanetElevation(MyPlanetElevation.Surface, out alt);

            var paramSpeed = command.Param.Number;
            cruiseSpeed = (paramSpeed == 0 ? MaxSpeed : MathHelper.Clamp(command.Param.Number, MinSpeed, MaxSpeed));

            vSpeed = GetVerticalSpeed();
            vEffectiveSpeed = vSpeed + maxDecel * Runtime.TimeSinceLastRun.TotalSeconds;

            stopDist = Math.Abs((vEffectiveSpeed * vEffectiveSpeed) / (2 * maxDecel));

            effectiveAlt = alt - vEffectiveSpeed * Runtime.TimeSinceLastRun.TotalSeconds - gridHight;
            effectiveAlt = effectiveAlt / gravityRatio;

            timeToImpact = alt / Math.Abs(vEffectiveSpeed);
            timeToStop = Math.Abs(vSpeed) / maxDecel;
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

            foreach (var brakingThruster in brakingThrusters)
            {
                brakingThruster.ThrustOverridePercentage = 0f;
                brakingThruster.Enabled = true;
            }

            foreach (var upThruster in upThrusters)
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

            foreach (var t in upThrusters)
                t.ThrustOverridePercentage = 0;
        }

        ////////////////////////////////////////////////////////
        /// FLIGHT
        ////////////////////////////////////////////////////////

        bool AlignToGravity()
        {
            if (naturalGrav.LengthSquared() < 0.01)
                return false;

            Vector3D desiredUp = Vector3D.Normalize(naturalGrav);
            Vector3D shipUp = controller.WorldMatrix.Up;

            Vector3D axis = shipUp.Cross(desiredUp);
            double angle = axis.Length();

            if (angle < 0.01 && !HasSpeed())
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
            const double RESPONSE = 3.0;     // lower = smoother

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
        /// Aligns ship's Up mostly to anti-gravity, with optional forward/down pitch tilt for glide.
        /// Returns true when alignment is good enough.
        /// </summary>
        /// <param name="tiltAngleDeg">Desired pitch-down angle in degrees (0 = perfect vertical, 5–12 typical for glide)</param>
        /// <param name="toleranceDeg">When angle error is below this → consider aligned (default 1.5°)</param>
        bool AlignWithGlideTilt(double tiltAngleDeg = 0, double toleranceDeg = 1.5)
        {
            if (naturalGrav.LengthSquared() < 0.01)
                return false;

            Vector3D gravNorm = Vector3D.Normalize(naturalGrav);          // points down
            Vector3D desiredUp = -gravNorm;                                // ship up should oppose gravity

            // Add forward/down tilt for glide (positive tiltAngleDeg = nose down)
            if (Math.Abs(tiltAngleDeg) > 0.01)
            {
                Vector3D shipForward = controller.WorldMatrix.Forward;
                // Rotate desired up vector around local right axis (pitch down)
                MatrixD pitchRot = MatrixD.CreateFromAxisAngle(controller.WorldMatrix.Right, MathHelper.ToRadians(tiltAngleDeg));
                desiredUp = Vector3D.TransformNormal(desiredUp, pitchRot);
                desiredUp = Vector3D.Normalize(desiredUp);
            }

            Vector3D shipUp = controller.WorldMatrix.Up;
            Vector3D axis = Vector3D.Cross(shipUp, desiredUp);
            double angleRad = axis.Length();

            // Early exit if already well aligned and not moving much
            if (angleRad < MathHelper.ToRadians(toleranceDeg) && !HasSpeed())
            {
                foreach (var g in gyros) g.GyroOverride = false;
                return true;
            }

            if (angleRad > 1e-6) axis /= angleRad;   // normalize rotation axis

            Vector3D angVel = controller.GetShipVelocities().AngularVelocity;

            // ────────────────────────────────────────────────
            // PD + rate limit (your existing logic – very good)
            // ────────────────────────────────────────────────
            const double MAX_ROT_RATE = 0.6;     // rad/s
            const double RESPONSE = 3.0;     // lower = smoother

            Vector3D desiredRate = axis * Math.Min(angleRad * RESPONSE, MAX_ROT_RATE);
            Vector3D correction = desiredRate - angVel;

            foreach (var g in gyros)
            {
                if (!g.IsFunctional) continue;

                // Local correction in gyro frame
                MatrixD gyroToLocal = MatrixD.Transpose(g.WorldMatrix);
                Vector3D localCorr = Vector3D.TransformNormal(correction, gyroToLocal);

                g.GyroOverride = true;
                g.Pitch = (float)MathHelper.Clamp(localCorr.X / 2, -3, 3);
                g.Yaw = (float)MathHelper.Clamp(localCorr.Y / 2, -3, 3);
                g.Roll = (float)MathHelper.Clamp(localCorr.Z / 2, -3, 3);
            }

            return false;
        }

        bool HasSpeed(double threshold = 1)
        {
            return controller.GetShipVelocities().LinearVelocity.LengthSquared() >= threshold;
        }

        ////////////////////////////////////////////////////////
        /// SAFE DESCENT
        ////////////////////////////////////////////////////////

        bool SuicideBurn()
        {
            controller.DampenersOverride = false;
            return effectiveAlt < SAFETY * (Math.Abs(stopDist) + 2 * gridHight);
        }

        bool Cushion()
        {
            MatchVerticalSpeed(-10);

            return effectiveAlt < 0;
        }

        bool TryLock()
        {
            AlignToGravity();
            MatchVerticalSpeed(-3);
            controller.DampenersOverride = true;

            foreach (var g in gears)
                g.Lock();

            return gears.Exists(g => g.IsLocked);
        }

        ////////////////////////////////////////////////////////
        /// PHYSICS HELPERS
        ////////////////////////////////////////////////////////

        double GetVerticalSpeed()
        {
            Vector3D gNorm = Vector3D.Normalize(naturalGrav);
            
            return -controller.GetShipVelocities()
                .LinearVelocity.Dot(gNorm);
        }

        double GetMaxDecel()
        {
            thrust = 0;

            Vector3D up = -Vector3D.Normalize(naturalGrav);

            foreach (var t in upThrusters)
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

            double current = GetVerticalSpeed();
            double error = target - current;

            double output = MathHelper.Clamp(hover + error * 0.5, 0, 1);

            foreach (var t in upThrusters)
                t.ThrustOverridePercentage = (float)output;
        }

        double SumThrust()
        {
            double total = 0;

            foreach (var t in upThrusters)
                total += t.MaxEffectiveThrust;

            return total;
        }
    }
}
