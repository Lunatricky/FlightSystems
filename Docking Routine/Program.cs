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
        // Connector-based Function Block Shutdown Script

        const string IGNORE_TAG = "ignore";

        List<IMyFunctionalBlock> cachedBlocks = new List<IMyFunctionalBlock>();
        List<IMyShipConnector> connectors = new List<IMyShipConnector>();
        List<IMyGasTank> tanks = new List<IMyGasTank>();
        List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
        IMyBatteryBlock backupBattery;

        bool running = true;
        bool lastConnectedState = false;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
            ReloadBlocks();
        }

        public void Save() { }

        public void Main(string argument, UpdateType updateSource)
        {
            argument = (argument ?? "").ToLower();

            if (!string.IsNullOrEmpty(argument))
                HandleArgument(argument);

            if (!running)
                return;

            bool anyConnected = IsAnyConnectorConnected();

            if (anyConnected != lastConnectedState)
            {
                SetBlocks(!anyConnected);
                StockpileTanks(anyConnected);
                ChargeBatteries(anyConnected);
                lastConnectedState = anyConnected;
            }
        }

        void HandleArgument(string arg)
        {
            switch (arg)
            {
                case "reload":
                    ReloadBlocks();
                    break;

                case "toggle":
                    ToggleBlocks();
                    break;

                case "on":
                    SetBlocks(true);
                    break;

                case "off":
                    SetBlocks(false);
                    break;

                case "stop":
                    running = false;
                    break;

                case "start":
                    running = true;
                    break;
            }
        }

        void ReloadBlocks()
        {
            cachedBlocks.Clear();
            connectors.Clear();
            tanks.Clear();
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

            // Connectors (own construct only)
            GridTerminalSystem.GetBlocksOfType(connectors,
                c => c.IsSameConstructAs(Me));

            // Gas tanks (own construct only)
            GridTerminalSystem.GetBlocksOfType(tanks,
                t => t.IsSameConstructAs(Me));

            // Batteries (own construct only)
            GridTerminalSystem.GetBlocksOfType(batteries,
                b => b.IsSameConstructAs(Me));

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

            lastConnectedState = IsAnyConnectorConnected();
            SetBlocks(!lastConnectedState);
            StockpileTanks(lastConnectedState);
            ChargeBatteries(lastConnectedState);
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
            foreach (IMyFunctionalBlock cachedBlock in cachedBlocks)
            {
                if (cachedBlock != null && cachedBlock.IsFunctional)
                    cachedBlock.Enabled = enabled;
            }
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

        void ToggleBlocks()
        {
            foreach (IMyFunctionalBlock b in cachedBlocks)
            {
                if (b != null && b.IsFunctional)
                    b.Enabled = !b.Enabled;
            }
        }

        bool ContainsIgnore(string s)
        {
            if (string.IsNullOrEmpty(s))
                return false;

            return s.ToLower().Contains(IGNORE_TAG);
        }

    }
}
