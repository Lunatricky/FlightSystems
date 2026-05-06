using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IngameScript
{
    class GridHelper
    {

        public static void GetOwnGridBlocks<T>(List<T> list, ShipContext sc, string __ignoreTag = "") where T : class, IMyTerminalBlock
        {
            list.Clear();
            sc.GridTS.GetBlocksOfType(list, block =>
            (block.IsSameConstructAs(sc.Me) && !block.CustomName.Contains(__ignoreTag))
            );
        }
    }
}
