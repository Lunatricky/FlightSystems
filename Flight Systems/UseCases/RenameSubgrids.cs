using Sandbox.ModAPI.Ingame;
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
                if (subGrid == null || subGrid == mainGrid)
                    continue;
                subGrid.CustomName = baseName + " - Sub " + counter;
                counter++;
            }
        }

        static void CollectConnectedGrids(IMyCubeGrid current, HashSet<IMyCubeGrid> visited, List<IMyMechanicalConnectionBlock> allBlocks)
        {
            if (current == null || visited.Contains(current))
                return;

            visited.Add(current);

            for (int i = 0; i < allBlocks.Count; i++)
            {
                IMyMechanicalConnectionBlock block = allBlocks[i];
                if (block == null || block.Closed)
                    continue;
                if (block.CubeGrid != current)
                    continue;
                if (block.TopGrid == null)
                    continue;
                if (blockIds.Contains(block.EntityId))
                    continue;

                blockIds.Add(block.EntityId);
                CollectConnectedGrids(block.TopGrid, visited, allBlocks);
            }
        }
    }
}
