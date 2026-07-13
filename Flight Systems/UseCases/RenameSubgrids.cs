using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using VRage.Game.ModAPI.Ingame;

namespace IngameScript
{
    class RenameSubgrids
    {
        static List<long> blockIds = new List<long>();

        static public void GetSubgridsAndRename(IMyGridTerminalSystem gridTerminalSystem, IMyCubeGrid mainGrid)
        {
            string baseName = mainGrid.CustomName;
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = "Subgrid";

            // Collect all connected grids (recursive)
            HashSet<IMyCubeGrid> connectedGrids = new HashSet<IMyCubeGrid>();

            var allBlocks = new List<IMyMechanicalConnectionBlock>();

            gridTerminalSystem.GetBlocksOfType(allBlocks);

            CollectConnectedGrids(mainGrid, connectedGrids, allBlocks);

            if (connectedGrids == null || connectedGrids.Count == 0)
            {
                return;
            }

            // Remove the main grid itself
            connectedGrids.Remove(mainGrid);

            // Rename each subgrid
            int counter = 1;
            foreach (IMyCubeGrid subGrid in connectedGrids)
            {                
                string newName = baseName + " - Sub " + counter;
                subGrid.CustomName = newName;
                counter++;
            }
        }

        static void CollectConnectedGrids(IMyCubeGrid current, HashSet<IMyCubeGrid> visited, List<IMyMechanicalConnectionBlock> allBlocks)
        {
            if (visited.Contains(current))
                return;

            visited.Add(current);
            // Filter to current grid only
            foreach (IMyMechanicalConnectionBlock block in allBlocks)
            {
                if (block.CubeGrid != current)
                    continue;

                if (blockIds.Contains(block.EntityId))
                    continue;

                blockIds.Add(block.EntityId);

                CollectConnectedGrids(block.TopGrid, visited, allBlocks);
            }
        }
    }
}
