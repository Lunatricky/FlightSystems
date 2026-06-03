using IngameScript.UseCases;
using IngameScript.Utils;
using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;

namespace IngameScript.Domain
{
    class GridManager
    {
        readonly GridContext gc;
        private Booleans b;

        public GridManager(GridContext gc, Booleans b)
        {
            this.gc = gc;
            this.b = b;
        }

        public static void GetOwnGridBlocks<T>(List<T> list, GridContext gc, string __ignoreTag = "") where T : class, IMyTerminalBlock
        {
            list.Clear();
            bool hasIgnore = !string.IsNullOrEmpty(__ignoreTag);
            gc.GridTS.GetBlocksOfType(list, block =>
                block.IsSameConstructAs(gc.Me)
                && (!hasIgnore || !block.CustomName.Contains(__ignoreTag))
                && (!hasIgnore || !block.CustomData.Contains(__ignoreTag))
            );
        }


        public bool IsAnyConnectorConnected()
        {
            foreach (IMyShipConnector connector in gc.Connectors)
            {
                if (connector.Status == MyShipConnectorStatus.Connected)
                    return true;
            }
            return false;
        }

        public bool SetBlocks(bool isDockMode)
        {
            //Always turn tools OFF when dock/undock
            gc.ControlledToolBlocks.ForEach(b => b.Enabled = false);

            //Toggle other blocks when dock/undock
            foreach (IMyFunctionalBlock cachedBlock in gc.ControlledBlocks)
            {
                if (cachedBlock != null && cachedBlock.IsFunctional)
                    cachedBlock.Enabled = isDockMode;
            }

            return !isDockMode;
        }
        public void ResetGyros()
        {
            foreach (var g in gc.Gyros)
            {
                g.GyroOverride = false;
                g.Enabled = true;
            }
        }

        public void ResetThrusters()
        {
            foreach (var forwardThruster in gc.ForwardThrusters)
            {
                forwardThruster.ThrustOverridePercentage = 0f;
                forwardThruster.Enabled = true;
            }

            foreach (var brakingThruster in gc.BreakingThrusters)
            {
                brakingThruster.ThrustOverridePercentage = 0f;
                brakingThruster.Enabled = true;
            }

            foreach (var upThruster in gc.UpwardThrusters)
            {
                upThruster.ThrustOverridePercentage = 0f;
                upThruster.Enabled = true;
            }

        }

        public void StockpileTanks(bool stockpile)
        {
            foreach (IMyGasTank tank in gc.Tanks)
            {
                if (tank != null && tank.IsFunctional)
                    tank.Stockpile = stockpile;
            }
        }

        public void ChargeBatteries()
        {
            if (gc.BackupBattery != null)
            {
                gc.BackupBattery.ChargeMode = ChargeMode.Auto;
                foreach (IMyBatteryBlock battery in gc.Batteries) battery.ChargeMode = ChargeMode.Recharge;
            }
            else if (IsAnyConnectorConnected())
            {
                foreach (IMyBatteryBlock battery in gc.Batteries) battery.ChargeMode = ChargeMode.Recharge;
            }
        }

        public void AutoBatteries()
        {
            if (gc.BackupBattery != null)
                gc.BackupBattery.ChargeMode = ChargeMode.Recharge;

            foreach (IMyBatteryBlock battery in gc.Batteries)
            {
                battery.ChargeMode = ChargeMode.Auto;
            }
        }

        public void TurnOFfBreakingThrust()
        {
            foreach (IMyThrust thruster in gc.BreakingThrusters)
            {
                thruster.Enabled = false;
            }
        }

        public void AbortShipContext(ref Command command, ref int tickCount)
        {
            tickCount = 0;

            b = new Booleans();

            command = Command.Empty;

            gc.Controller.DampenersOverride = true;
            b.autoPilotToggle = false;

            ResetGyros();
            ResetThrusters();
        }

        public void SoftAbort()
        {
            gc.Controller.DampenersOverride = true;
            b.stopCruiseWhenOutOfGrav = false;

            ResetGyros();
            ResetThrusters();
        }
    }
}
