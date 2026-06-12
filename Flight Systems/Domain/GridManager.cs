using IngameScript.UseCases;
using IngameScript.Utils;
using Sandbox.ModAPI.Ingame;

namespace IngameScript.Domain
{
    class GridManager : GridContext
    {
        public GridManager(IMyGridTerminalSystem grid, IMyProgrammableBlock me) : base(grid, me)
        {
        }

        public bool IsAnyConnectorConnected()
        {
            foreach (IMyShipConnector connector in Connectors)
            {
                if (connector.Status == MyShipConnectorStatus.Connected)
                    return true;
            }
            return false;
        }

        public bool SetBlocks(bool isDockMode)
        {
            //Always turn tools OFF when dock/undock
            ControlledToolBlocks.ForEach(b => b.Enabled = false);

            //Toggle other blocks when dock/undock
            foreach (IMyFunctionalBlock cachedBlock in ControlledBlocks)
            {
                if (cachedBlock != null && cachedBlock.IsFunctional)
                    cachedBlock.Enabled = isDockMode;
            }

            return !isDockMode;
        }
        public void ResetGyros()
        {
            foreach (var g in Gyros)
            {
                g.GyroOverride = false;
                g.Enabled = true;
            }
        }

        public void ResetThrusters()
        {
            foreach (var forwardThruster in ForwardThrusters)
            {
                forwardThruster.ThrustOverridePercentage = 0f;
                forwardThruster.Enabled = true;
            }

            foreach (var brakingThruster in BreakingThrusters)
            {
                brakingThruster.ThrustOverridePercentage = 0f;
                brakingThruster.Enabled = true;
            }

            foreach (var upThruster in UpwardThrusters)
            {
                upThruster.ThrustOverridePercentage = 0f;
                upThruster.Enabled = true;
            }

        }

        public void StockpileTanks(bool stockpile)
        {
            foreach (IMyGasTank tank in Tanks)
            {
                if (tank != null && tank.IsFunctional)
                    tank.Stockpile = stockpile;
            }
        }

        public void ChargeBatteries()
        {
            if (BackupBattery != null)
            {
                BackupBattery.ChargeMode = ChargeMode.Auto;
                foreach (IMyBatteryBlock battery in Batteries) battery.ChargeMode = ChargeMode.Recharge;
            }
            else if (IsAnyConnectorConnected())
            {
                foreach (IMyBatteryBlock battery in Batteries) battery.ChargeMode = ChargeMode.Recharge;
            }
        }

        public void AutoBatteries()
        {
            if (BackupBattery != null)
                BackupBattery.ChargeMode = ChargeMode.Recharge;

            foreach (IMyBatteryBlock battery in Batteries)
            {
                battery.ChargeMode = ChargeMode.Auto;
            }
        }

        public void TurnOFfBreakingThrust()
        {
            foreach (IMyThrust thruster in BreakingThrusters)
            {
                thruster.Enabled = false;
            }
        }

        public void AbortShipContext(Command command, Booleans b, ref int tickCount)
        {
            tickCount = 0;

            b = new Booleans();

            command = Command.Empty;

            Controller.DampenersOverride = true;
            b.autoPilotToggle = false;

            ResetGyros();
            ResetThrusters();
        }

        public void SoftAbort(Booleans b)
        {
            Controller.DampenersOverride = true;
            b.stopCruiseWhenOutOfGrav = false;

            ResetGyros();
            ResetThrusters();
        }
    }
}
