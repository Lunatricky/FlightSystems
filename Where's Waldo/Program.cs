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
        List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
            GridTerminalSystem.GetBlocksOfType(blocks);
        }

        public void Main(string argument, UpdateType updateSource)
        {
            List<IMyTerminalBlock> blocks = GetAllCustomNamedBlocks();
            foreach (IMyTerminalBlock block in blocks)
            {
                Echo(block.CustomName + " : " + block.DefinitionDisplayNameText);
            }
        }

        /// <summary>
        /// Returns all blocks on the grid whose CustomName does NOT contain their default display/subtype name.
        /// </summary>
        List<IMyTerminalBlock> GetAllCustomNamedBlocks()
        {
            var result = new List<IMyTerminalBlock>();

            GridTerminalSystem.GetBlocksOfType(result, block =>
            {
                if (!block.IsSameConstructAs(Me)) return false;

                string name = block.CustomName?.Trim();
                if (string.IsNullOrWhiteSpace(name)) return false;

                // Try to get a sensible default name
                string defaultPart = block.DefinitionDisplayNameText
                                  ?? block.BlockDefinition.SubtypeName
                                  ?? block.BlockDefinition.TypeId.ToString();

                return !name.Contains(defaultPart);
            });

            return result;
        }
    }
}
