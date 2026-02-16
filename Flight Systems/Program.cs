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

        // Cruise Control
        // Forward Speed Limiter + Cruise Control Fields

        const double MAX_SPEED = 99.0;      // m/s
		const double SPEED_TOLERANCE = 0.5;  // m/s deadzone
		const double OVERRIDE_STEP = 0.05;   // cruise adjustment rate

        readonly List<IMyThrust> brakingThrusters = new List<IMyThrust>();
        readonly List<IMyThrust> forwardThrusters = new List<IMyThrust>();

		IMyShipController controller;

		bool cruiseMode = false;
		bool circumnav = false;
        bool lastCheckIsOnNatGrav = false;
        bool stopCruiseWhenOutOfGrav = false;

        double currentOverride = 0.0;
        double cruiseSpeed = 99;

        // Circunavigation
        // CNav fields

        Vector3D naturalGrav;
        List<IMyGyro> gyros = new List<IMyGyro>();

        // Docking Routine
        // Connector-based Function Block Shutdown Fields

        const string OVERRIDE_BLOCKS = "[FS_override]";
        const string IGNORE_TAG = "[FS_ignore]";

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

        public Program()
		{
			Runtime.UpdateFrequency = UpdateFrequency.Update10;
            Reload();

            Me.CustomData = CustomDataInfo();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            UpdatePhysics();
            string stringInfo = ScriptInfo();

            Echo(stringInfo);
            Me.GetSurface(0).WriteText(stringInfo);

            // Stop cruise control when leaves atmosphere?

            if (stopCruiseWhenOutOfGrav && lastCheckIsOnNatGrav && controller.GetNaturalGravity().LengthSquared() == 0)
            {
                stopCruiseWhenOutOfGrav = lastCheckIsOnNatGrav = cruiseMode = false;
            }
            else
            {
                lastCheckIsOnNatGrav = controller.GetNaturalGravity().LengthSquared() > 0;
                argument = (argument ?? "").ToLower();
            }


            // Docking Routine

            if (!string.IsNullOrEmpty(argument))
            {
                HandleArgumentDock(argument);
            }

            bool anyConnected = IsAnyConnectorConnected();

            if (anyConnected != lastConnectedState)
            {
                SetBlocks(!anyConnected);
                StockpileTanks(anyConnected);
                ChargeBatteries(anyConnected);
                lastConnectedState = anyConnected;
            }

            if (isDockMode)
                return;

            // Cruise Control

            if (!string.IsNullOrEmpty(argument))
            {
                HandleArgumentCC(argument);
                ResetThrusters();
            }

            if (controller == null) return;

			Vector3D velocity = controller.GetShipVelocities().LinearVelocity;
			Vector3D forward = controller.WorldMatrix.Forward;
			double forwardSpeed = Vector3D.Dot(velocity, forward);

            if (cruiseMode)
                CruiseControl(forwardSpeed);
            else if (circumnav)
                CircumNav(forwardSpeed);
            else
                ManualLimiter(forwardSpeed);

            // Info LCDs

            if (controller != null && lcds.Count != 0)
            {
                WriteInfo();
            }
        }

        void UpdatePhysics()
        {
            naturalGrav = controller.GetNaturalGravity();
        }

            public string ScriptInfo()
        {
            StringBuilder scriptInfo = new StringBuilder();
            scriptInfo.Clear();
            scriptInfo.AppendLine("Flight Systems");
            scriptInfo.AppendLine(gridName);
            scriptInfo.AppendLine(new string('-', 28));
            scriptInfo.AppendLine("Dock Mode: " + isDockMode);
            scriptInfo.AppendLine("Cruise Mode: " + cruiseMode);
            scriptInfo.AppendLine("CircumNavigate Mode: " + circumnav);
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
        
        void HandleArgumentDock(string argument)
        {
            switch (argument.ToLower().Trim())
            {
                case "reload":
                    Reload();
                    break;

                case "toggle":
                    SetBlocks(isDockMode);
                    break;

                case "on":
                    SetBlocks(true);
                    break;

                case "off":
                    SetBlocks(false);
                    break;
            }
        }

        void HandleArgumentCC(string argument)
        {
            switch (argument.ToLower().Trim())
            {
                case "reload":
                    Reload();
                    break;
                case "cruise":
                    cruiseSpeed = 98;
                    cruiseMode = !cruiseMode;
                    circumnav = false;
                    stopCruiseWhenOutOfGrav = false;
                    break;
                case "cruiseon":
                    cruiseSpeed = 98;
                    cruiseMode = true;
                    circumnav = false;
                    stopCruiseWhenOutOfGrav = false;
                    break;
                case "cruiseoff":
                    cruiseMode = false;
                    stopCruiseWhenOutOfGrav = false;
                    break;
                case "cnav":
                    cruiseSpeed = 98;
                    circumnav = !circumnav;
                    cruiseMode = false;
                    break;
                case "cnavon":
                    cruiseSpeed = 98;
                    circumnav = true;
                    cruiseMode = false;
                    break;
                case "cnavoff":
                    circumnav = false;
                    break;
                case "cruiseorbit":
                    cruiseSpeed = 98;
                    cruiseMode = true;
                    circumnav = false;
                    stopCruiseWhenOutOfGrav = true;
                    break;
                default:
                    break;
            }
            
            if (argument.Contains("cruise") && ParseDouble("cruise", argument, ref cruiseSpeed))
            {
                cruiseMode = true;
            }
        }

        private void Reload()
        {
            SetupSurface(Me.GetSurface(0));
            LoadOverrideGroup();
            CacheBlocksCC();
            CacheBlocksCNav();
            CacheBlocksDR();
            CacheBlocksLCD();
            lastCheckIsOnNatGrav = controller.GetNaturalGravity().LengthSquared() > 0;
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

			var allThrusters = new List<IMyThrust>();
			GridTerminalSystem.GetBlocksOfType(allThrusters, thruster =>
               thruster.IsSameConstructAs(Me) &&
               thruster.IsFunctional);

			foreach (var thruster in allThrusters)
			{
				// Thrusters pushing ship forward
				if (thruster.Orientation.Forward == Base6Directions.Direction.Backward)
					forwardThrusters.Add(thruster);

				// Thrusters that brake forward motion
				else if (thruster.Orientation.Forward == Base6Directions.Direction.Forward)
					brakingThrusters.Add(thruster);
			}


			var controllers = new List<IMyShipController>();
			GridTerminalSystem.GetBlocksOfType(controllers, controller =>
               controller.IsSameConstructAs(Me) && controller.IsMainCockpit);

			if (controllers.Count == 0)
                GetOwnGridBlocks(controllers);

            controller = controllers.Count > 0 ? controllers[0] : null;
        }

        //CircumNavigate
        void CacheBlocksCNav()
        {
            gyros.Clear();
            GetOwnGridBlocks(gyros);
        }

        void ManualLimiter(double speed)
		{
			bool allowThrust = speed < MAX_SPEED;

			// Restore braking thrusters
			foreach (var brakingThruster in brakingThrusters)
			{
				brakingThruster.Enabled = true;
				brakingThruster.ThrustOverridePercentage = 0f;
			}

			// Normal forward thrust behavior
			foreach (var forwardThruster in forwardThrusters)
			{
				forwardThruster.ThrustOverridePercentage = 0f;
				forwardThruster.Enabled = allowThrust;
			}

        }

        bool AlignToGravity()
        {
            if (naturalGrav.LengthSquared() < 0.01)
                return false;

            Vector3D desiredUp = Vector3D.Normalize(naturalGrav);
            Vector3D shipUp = controller.WorldMatrix.Up;

            Vector3D axis = shipUp.Cross(desiredUp);
            double angle = axis.Length();

            if (angle < 0.01)
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

        void CircumNav(double speed)
        {
            if (circumnav)
            {
                AlignToGravity();
                CruiseControl(speed);
            } else
            {
                KillGyroOverride();
            }
        }

        void CruiseControl(double speed)
		{
			double error = cruiseSpeed - speed;

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

        void CacheBlocksDR()
        {
            controlledBlocks.Clear();
            connectors.Clear();
            tanks.Clear();
            h2Tanks.Clear();
            batteries.Clear();


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

        void ChargeBatteries(bool charge)
        {
            foreach (IMyBatteryBlock battery in batteries)
            {
                if (battery == backupBattery)
                {
                    battery.ChargeMode = charge ? ChargeMode.Auto : ChargeMode.Recharge;
                    continue;
                }

                if (battery != null && battery.IsFunctional)
                    battery.ChargeMode = charge ? ChargeMode.Recharge : ChargeMode.Auto;
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

        bool ParseDouble(string subString, string argument, ref double value)
        {
            double oldValue = value;

            if (string.IsNullOrWhiteSpace(argument))
                return false;

            string[] parts = argument.Split(' ');

            if (parts.Length >= 2 && parts[0].Equals(subString, StringComparison.OrdinalIgnoreCase))
            {
                double.TryParse(parts[1], out value);
            }

            return value != oldValue;
        }

        private void KillGyroOverride()
        {
            foreach (var g in gyros)
                g.GyroOverride = false;
        }
    }
}
