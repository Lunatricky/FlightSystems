using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;

namespace IngameScript.Domain
{
    class GridManager
    {
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


        public static bool IsAnyConnectorConnected(GridContext gc)
        {
            foreach (IMyShipConnector connector in gc.Connectors)
            {
                if (connector.Status == MyShipConnectorStatus.Connected)
                    return true;
            }
            return false;
        }

        public static void SetBlocks(GridContext gc, bool enabled, out bool isDockMode)
        {
            //Always turn tools OFF when dock/undock
            gc.ControlledToolBlocks.ForEach(b => b.Enabled = false);

            //Toggle other blocks when dock/undock
            foreach (IMyFunctionalBlock cachedBlock in gc.ControlledBlocks)
            {
                if (cachedBlock != null && cachedBlock.IsFunctional)
                    cachedBlock.Enabled = enabled;
            }

            isDockMode = !enabled;
        }
        public static void ResetGyros(GridContext gc)
        {
            foreach (var g in gc.Gyros)
            {
                g.GyroOverride = false;
                g.Enabled = true;
            }
        }

        public static void ResetThrusters(GridContext gc)
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

        public static void StockpileTanks(GridContext gc, bool stockpile)
        {
            foreach (IMyGasTank tank in gc.Tanks)
            {
                if (tank != null && tank.IsFunctional)
                    tank.Stockpile = stockpile;
            }
        }

        public static void ChargeBatteries(GridContext gc)
        {
            if (gc.BackupBattery != null)
            {
                gc.BackupBattery.ChargeMode = ChargeMode.Auto;
                foreach (IMyBatteryBlock battery in gc.Batteries) battery.ChargeMode = ChargeMode.Recharge;
            }
            else if (IsAnyConnectorConnected(gc))
            {
                foreach (IMyBatteryBlock battery in gc.Batteries) battery.ChargeMode = ChargeMode.Recharge;
            }
        }

        public static void AutoBatteries(GridContext gc)
        {
            if (gc.BackupBattery != null)
                gc.BackupBattery.ChargeMode = ChargeMode.Recharge;

            foreach (IMyBatteryBlock battery in gc.Batteries)
            {
                battery.ChargeMode = ChargeMode.Auto;
            }
        }

        public static void TurnOFfBreakingThrust(GridContext gc)
        {
            foreach (IMyThrust thruster in gc.BreakingThrusters)
            {
                thruster.Enabled = false;
            }
        }
    }
}
