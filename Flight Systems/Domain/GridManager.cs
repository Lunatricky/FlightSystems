using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;

namespace IngameScript.Domain
{
    class GridManager
    {
        public static void GetOwnGridBlocks<T>(List<T> list, GridContext sc, string __ignoreTag = "") where T : class, IMyTerminalBlock
        {
            list.Clear();
            bool hasIgnore = !string.IsNullOrEmpty(__ignoreTag);
            sc.GridTS.GetBlocksOfType(list, block =>
                block.IsSameConstructAs(sc.Me)
                && (!hasIgnore || !block.CustomName.Contains(__ignoreTag))
                && (!hasIgnore || !block.CustomData.Contains(__ignoreTag))
            );
        }


        public static bool IsAnyConnectorConnected(GridContext sc)
        {
            foreach (IMyShipConnector connector in sc.Connectors)
            {
                if (connector.Status == MyShipConnectorStatus.Connected)
                    return true;
            }
            return false;
        }

        public static void SetBlocks(GridContext sc, bool enabled, out bool isDockMode)
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
        public static void ResetGyros(GridContext sc)
        {
            foreach (var g in sc.Gyros)
            {
                g.GyroOverride = false;
                g.Enabled = true;
            }
        }

        public static void ResetThrusters(GridContext sc)
        {
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

        public static void StockpileTanks(GridContext sc, bool stockpile)
        {
            foreach (IMyGasTank tank in sc.Tanks)
            {
                if (tank != null && tank.IsFunctional)
                    tank.Stockpile = stockpile;
            }
        }

        public static void ChargeBatteries(GridContext sc)
        {
            if (sc.BackupBattery != null)
            {
                sc.BackupBattery.ChargeMode = ChargeMode.Auto;
                foreach (IMyBatteryBlock battery in sc.Batteries) battery.ChargeMode = ChargeMode.Recharge;
            }
            else if (IsAnyConnectorConnected(sc))
            {
                foreach (IMyBatteryBlock battery in sc.Batteries) battery.ChargeMode = ChargeMode.Recharge;
            }
        }

        public static void AutoBatteries(GridContext sc)
        {
            if (sc.BackupBattery != null)
                sc.BackupBattery.ChargeMode = ChargeMode.Recharge;

            foreach (IMyBatteryBlock battery in sc.Batteries)
            {
                battery.ChargeMode = ChargeMode.Auto;
            }
        }

        private void TurnOFfBreakingThrust(GridContext sc)
        {
            foreach (IMyThrust thruster in sc.BreakingThrusters)
            {
                thruster.Enabled = false;
            }
        }
    }
}
