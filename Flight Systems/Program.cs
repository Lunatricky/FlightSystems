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
		double currentOverride = 0.0;
        bool lastCheckIsOnNatGrav = false;

        // Docking Routine
        // Connector-based Function Block Shutdown Fields

        const string OVERRIDE_BLOCKS = "[FS_override]";
        const string IGNORE_TAG = "[FS_ignore]";

        readonly HashSet<long> overrideBlocks = new HashSet<long>();
        readonly List<IMyFunctionalBlock> cachedBlocks = new List<IMyFunctionalBlock>();
        readonly List<IMyShipConnector> connectors = new List<IMyShipConnector>();
        readonly List<IMyGasTank> tanks = new List<IMyGasTank>();
        readonly List<IMyGasTank> h2Tanks = new List<IMyGasTank>();
        readonly List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();

        IMyBatteryBlock backupBattery;

        bool runningDockMode = true;
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

            Me.CustomData = CustomDataInfo().ToString();
        }
        StringBuilder debug = new StringBuilder();
        public void Main(string argument, UpdateType updateSource)
        {
            Echo("Flight Systems");
            Echo(gridName);
            Echo("------------------------");
            Echo("Dock Mode: " + isDockMode);
            Echo("Cruise Mode: " + cruiseMode);
            Echo("------------------------");
            Echo("LCDs count: " + lcds.Count);
            Echo("Tanks count: " + tanks.Count);
            Echo("Batteries count: " + batteries.Count);
            Echo("Dock Mode block count: " + cachedBlocks.Count);
            Echo("------------------------");

            // Docking Routine

            if (lastCheckIsOnNatGrav == true && controller.GetNaturalGravity().LengthSquared() == 0)
            {
                lastCheckIsOnNatGrav = cruiseMode = false;
            }
            else
            {
                lastCheckIsOnNatGrav = controller.GetNaturalGravity().LengthSquared() > 0;
                argument = (argument ?? "").ToLower();
            }


            if (!string.IsNullOrEmpty(argument))
            {
                HandleArgumentDock(argument);
            }

            if (runningDockMode)
                return;

            bool anyConnected = IsAnyConnectorConnected();

            if (anyConnected != lastConnectedState)
            {
                SetBlocks(!anyConnected);
                StockpileTanks(anyConnected);
                ChargeBatteries(anyConnected);
                lastConnectedState = anyConnected;
            }

            // Cruise Control

            if (!string.IsNullOrEmpty(argument))
            {
                HandleArgument(argument);
                ResetThrusters();
            }

            if (controller == null) return;

			Vector3D velocity = controller.GetShipVelocities().LinearVelocity;
			Vector3D forward = controller.WorldMatrix.Forward;
			double forwardSpeed = Vector3D.Dot(velocity, forward);

            if (cruiseMode)
				CruiseControl(forwardSpeed);
			else
				ManualLimiter(forwardSpeed);

            // Info LCDs

            if (controller != null && lcds.Count != 0)
            {
                WriteInfo();
            }
        }

        public StringBuilder CustomDataInfo()
        {
            StringBuilder customDataInfo = new StringBuilder();
            customDataInfo.AppendLine("LCD Tag: " + LCD_TAG);
            customDataInfo.AppendLine("Dock Mode Ignore Tag: " + IGNORE_TAG);
            customDataInfo.AppendLine("Dock Mode Override Tag: " + OVERRIDE_BLOCKS);
            return customDataInfo;
        }
        
        void HandleArgumentDock(string argument)
        {
            switch (argument.ToLower().Trim())
            {
                case "reload":
                    Reload();
                    break;

                case "toggle":
                    SetBlocks(runningDockMode);
                    break;

                case "on":
                    SetBlocks(true);
                    break;

                case "off":
                    SetBlocks(false);
                    break;
            }
        }

        void HandleArgument(string argument)
        {
            switch (argument.ToLower().Trim())
            {
                case "reload":
                    Reload();
                    break;
                case "cruise":
                    cruiseMode = !cruiseMode;
                    break;
                case "cruiseon":
                    cruiseMode = true;
                    break;
                case "cruiseoff":
                    cruiseMode = false;
                    break;
            }
        }

        private void Reload()
        {
            LoadOverrideGroup();
            CacheBlocksCC();
            CacheBlocksDR();
            CacheBlocksLCD();
            lastCheckIsOnNatGrav = controller.GetNaturalGravity().LengthSquared() > 0;
        }

        void GetOwnGridBlocks<T>(List<T> list) where T : class, IMyTerminalBlock
        {
            list.Clear();
            GridTerminalSystem.GetBlocksOfType(list, b =>
                b.IsSameConstructAs(Me)
            );
        }

        // Cruise Control

        void CacheBlocksCC()
		{
			forwardThrusters.Clear();
			brakingThrusters.Clear();

			var allThrusters = new List<IMyThrust>();
			GridTerminalSystem.GetBlocksOfType(allThrusters, t =>
               t.IsSameConstructAs(Me) &&
               t.IsFunctional);

			foreach (var t in allThrusters)
			{
				// Thrusters pushing ship forward
				if (t.Orientation.Forward == Base6Directions.Direction.Backward)
					forwardThrusters.Add(t);

				// Thrusters that brake forward motion
				else if (t.Orientation.Forward == Base6Directions.Direction.Forward)
					brakingThrusters.Add(t);
			}


			var controllers = new List<IMyShipController>();
			GridTerminalSystem.GetBlocksOfType(controllers, c =>
               c.IsSameConstructAs(Me) && c.IsMainCockpit);

			if (controllers.Count == 0)
                GetOwnGridBlocks(controllers);

            controller = controllers.Count > 0 ? controllers[0] : null;
		}

		void ManualLimiter(double speed)
		{
			bool allowThrust = speed < MAX_SPEED;

			// Restore braking thrusters
			foreach (var t in brakingThrusters)
			{
				t.Enabled = true;
				t.ThrustOverridePercentage = 0f;
			}

			// Normal forward thrust behavior
			foreach (var t in forwardThrusters)
			{
				t.ThrustOverridePercentage = 0f;
				t.Enabled = allowThrust;
			}

		}

		void CruiseControl(double speed)
		{
			double error = MAX_SPEED - speed;

			if (Math.Abs(error) < SPEED_TOLERANCE)
				return;

			if (error > 0)
				currentOverride += OVERRIDE_STEP;
			else
				currentOverride -= OVERRIDE_STEP;

			currentOverride = MathHelper.Clamp(currentOverride, 0f, 1f);

			// Disable braking thrusters so they don't fight cruise
			foreach (var t in brakingThrusters)
				t.Enabled = false;

			// Control forward thrust smoothly
			foreach (var t in forwardThrusters)
			{
				t.Enabled = true;
				t.ThrustOverridePercentage = (float)currentOverride;
			}

		}

		void ResetThrusters()
		{
			currentOverride = 0;

			foreach (var t in forwardThrusters)
			{
				t.ThrustOverridePercentage = 0f;
				t.Enabled = true;
			}

			foreach (var t in brakingThrusters)
			{
				t.ThrustOverridePercentage = 0f;
				t.Enabled = true;
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

            foreach (var b in blocks)
            {
                if (b.IsSameConstructAs(Me))
                    overrideBlocks.Add(b.EntityId);
            }
        }

        void CacheBlocksDR()
        {
            cachedBlocks.Clear();
            connectors.Clear();
            tanks.Clear();
            h2Tanks.Clear();
            batteries.Clear();

            // Functional blocks to power on/off
            var temp = new List<IMyFunctionalBlock>();
            GridTerminalSystem.GetBlocksOfType(temp, b =>
            {
                if (!b.IsSameConstructAs(Me) || b == Me)
                    return false;

                bool isOverride = overrideBlocks.Contains(b.EntityId);

                if (!isOverride)
                {
                    if (b.CustomName.Contains(IGNORE_TAG)) return false;

                    if (b is IMyShipConnector) return false;
                    if (b is IMyBatteryBlock) return false;
                    if (b is IMyDoor) return false;
                    if (b is IMyAirVent) return false;
                    if (b is IMyCryoChamber) return false;
                    if (b is IMyMedicalRoom) return false;
                    if (b is IMyGasTank) return false;
                    if (b is IMyTimerBlock) return false;
                    if (b is IMyEventControllerBlock) return false;
                    if (b is IMyInteriorLight) return false;
                    if (b is IMyLandingGear) return false;
                    if (b is IMyButtonPanel) return false;
                    if (b is IMyLargeTurretBase) return false;
                    if (b is IMyOffensiveCombatBlock) return false;
                    if (b is IMyDefensiveCombatBlock) return false;
                    if (IsSurvivalKit(b)) return false;
                }

                return true;
            });

            cachedBlocks.AddRange(temp);

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
            if (backupBattery == null)
            {
                foreach (var b in batteries)
                {
                    if (b.CustomName.ToLower().Contains("backup"))
                    {
                        backupBattery = b;
                        break;
                    }
                }
            }
        }
        bool IsHydrogenTank(IMyGasTank tank)
        {
            return tank.BlockDefinition.SubtypeName
                .IndexOf("Hydrogen", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        bool IsSurvivalKit(IMyFunctionalBlock b)
        {
            return b.BlockDefinition.SubtypeName
                .IndexOf("SurvivalKit", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        bool IsAnyConnectorConnected()
        {
            foreach (IMyShipConnector c in connectors)
            {
                if (c.Status == MyShipConnectorStatus.Connected)
                    return true;
            }
            return false;
        }

        void SetBlocks(bool enabled)
        {
            runningDockMode = !enabled;
            foreach (IMyFunctionalBlock cachedBlock in cachedBlocks)
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
            GridTerminalSystem.GetBlocksOfType<IMyTextSurfaceProvider>(blocks, b =>
                b.IsSameConstructAs(Me) &&
                b.CustomName.Contains(LCD_TAG)
            );

            foreach (IMyTextSurfaceProvider surfaceProvider in blocks)
            {
                // Only take the first surface (index 0)
                if (surfaceProvider.SurfaceCount > 0)
                {
                    var s = surfaceProvider.GetSurface(0);
                    s.ContentType = ContentType.TEXT_AND_IMAGE;
                    s.Font = "DEBUG";
                    s.FontSize = 1.5f;
                    s.Alignment = TextAlignment.LEFT;

                    lcds.Add(s);
                }
            }

            firstRun = true;
        }

        void WriteInfo()
        {
            var sb = new StringBuilder();

            // Altitude
            string seaAltText = "No gravity";
            string groundAltText = "No gravity";

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
            foreach (var t in h2Tanks)
            {
                h2Cap += t.Capacity;
                h2Fill += t.Capacity * t.FilledRatio;
            }

            double h2Rate = (h2Fill - lastH2Fill) / Runtime.TimeSinceLastRun.TotalSeconds;
            lastH2Fill = h2Fill;

            string h2Time = "--";
            if (Math.Abs(h2Rate) > 1e-6)
            {
                if (h2Rate < 0)
                    h2Time = FormatTime(h2Fill / -h2Rate) + " \\/";
                else
                    h2Time = FormatTime((h2Cap - h2Fill) / h2Rate) + " /\\";
            }

            H2CapacityPercent = h2Fill / h2Cap * 100;

            // Batteries
            double batCap = 0, batStored = 0;
            double batIn = 0, batOut = 0;

            foreach (var b in batteries)
            {
                batCap += b.MaxStoredPower;
                batStored += b.CurrentStoredPower;
                batIn += b.CurrentInput;
                batOut += b.CurrentOutput;
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
            sb.AppendLine(gridName);
            sb.AppendLine(new string('-', 28));

            sb.AppendLine($"Alt (Sea): {seaAltText}");
            sb.AppendLine($"Alt (Ground): {groundAltText}");
            sb.AppendLine($"Accel: {accel.Length() / 9.81:F2} g");
            sb.AppendLine($"Mass: {mass.PhysicalMass / 1000:0.0} t");
            sb.AppendLine($"Empty Mass: {mass.BaseMass / 1000:0.0} t");
            
            sb.AppendLine($"H2: {H2CapacityPercent:0}%");
            sb.AppendLine($"H2 Time: {h2Time}"); 

            sb.AppendLine($"Battery:  {batStored / batCap * 100:0} %");
            sb.AppendLine($"Bat Time: {batTime}");
            
            string text = sb.ToString();
            foreach (var lcd in lcds)
                lcd.WriteText(text);
        }

        string FormatTime(double seconds)
        {
            if (double.IsInfinity(seconds) || seconds < 0)
                return "--";

            int s = (int)seconds;
            int h = s / 3600;
            int m = (s % 3600) / 60;
            int sec = s % 60;

            if (h > 0)
                return $"{h}h {m}m";
            if (m > 0)
                return $"{m}m {sec}s";
            return $"{sec}s";
        }

        double ParseDouble(string storage, string key, double defaultValue)
        {
            if (string.IsNullOrEmpty(storage)) return defaultValue;

            var parts = storage.Split(';');
            foreach (var part in parts)
            {
                var kv = part.Split(':');
                if (kv.Length == 2 && kv[0] == key)
                {
                    double val;
                    if (double.TryParse(kv[1], out val))
                        return val;
                }
            }
            return defaultValue;
        }
    }
}
