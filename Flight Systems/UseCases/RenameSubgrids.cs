using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.ModAPI.Ingame;

namespace IngameScript
{
    class RenameSubgrids
    {
        public static void GetSubgridsAndRename(IMyGridTerminalSystem gridTerminalSystem, IMyCubeGrid mainGrid)
        {
            string baseName = mainGrid.CustomName;
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = "Subgrid";

            // Collect all connected grids (recursive)
            HashSet<IMyCubeGrid> connectedGrids = new HashSet<IMyCubeGrid>();
            CollectConnectedGrids(gridTerminalSystem, mainGrid, connectedGrids);

            // Remove the main grid itself
            connectedGrids.Remove(mainGrid);

            if (connectedGrids.Count == 0)
            {
                return;
            }

            // Rename each subgrid
            int counter = 1;
            foreach (IMyCubeGrid subGrid in connectedGrids)
            {
                string newName = baseName + " - Sub " + counter;
                subGrid.CustomName = newName;
                counter++;
            }
        }


        // Recursive helper to find all connected grids via mechanical connections
        private static void CollectConnectedGrids(IMyGridTerminalSystem gridTerminalSystem, IMyCubeGrid current, HashSet<IMyCubeGrid> visited)
        {
            if (visited.Contains(current))
                return;

            visited.Add(current);

            // Get all terminal blocks on this grid
            List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
            gridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks);


            // Filter to current grid only                                                                                                                                                                                                                                                              
            List<IMyTerminalBlock> currentBlocks = new List<IMyTerminalBlock>();
            foreach (IMyTerminalBlock block in blocks)
            {
                if (block.CubeGrid == current)
                    currentBlocks.Add(block);
            }

            foreach (IMyTerminalBlock currentBlock in currentBlocks)
            {
                // Rotor
                if (currentBlock is IMyMotorStator)
                {
                    IMyMotorStator rotor = (IMyMotorStator)currentBlock;
                    if (rotor.TopGrid != null)
                        CollectConnectedGrids(gridTerminalSystem, rotor.TopGrid, visited);
                }
                // Piston
                else if (currentBlock is IMyPistonBase)
                {
                    IMyPistonBase piston = (IMyPistonBase)currentBlock;
                    if (piston.TopGrid != null)
                        CollectConnectedGrids(gridTerminalSystem, piston.TopGrid, visited);
                }
                // Hinge / Mechanical Connection
                else if (currentBlock is IMyMechanicalConnectionBlock)
                {
                    IMyMechanicalConnectionBlock mech = (IMyMechanicalConnectionBlock)currentBlock;
                    if (mech.TopGrid != null)
                        CollectConnectedGrids(gridTerminalSystem, mech.TopGrid, visited);
                }
            }
        }
    }
}
