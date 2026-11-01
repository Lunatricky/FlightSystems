using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using VRage.Game.ModAPI.Ingame;

namespace IngameScript
{
    // Stamps FS_STATIC=1 on connectors that share a rotor, hinge, or piston
    // with a static grid. Wheels, connectors, and landing gear are not links.
    class StaticConnectorStamp
    {
        public const string Line = "FS_STATIC=1";
        public const string DockMe = "FS_DOCK=Me";
        public const string DockOther = "FS_DOCK=Other";

        static readonly List<IMyMotorStator> motors = new List<IMyMotorStator>();
        static readonly List<IMyPistonBase> pistons = new List<IMyPistonBase>();
        static readonly List<IMyShipConnector> connectors = new List<IMyShipConnector>();
        static readonly List<IMyCubeGrid> grids = new List<IMyCubeGrid>();
        static readonly HashSet<long> seen = new HashSet<long>();

        public static void Apply(IMyGridTerminalSystem gts, IMyCubeGrid start)
        {
            if (gts == null || start == null || start.Closed)
                return;

            motors.Clear();
            pistons.Clear();
            connectors.Clear();
            grids.Clear();
            seen.Clear();

            gts.GetBlocksOfType(motors);
            gts.GetBlocksOfType(pistons);
            gts.GetBlocksOfType(connectors);

            grids.Add(start);
            seen.Add(start.EntityId);

            for (int q = 0; q < grids.Count; q++)
            {
                IMyCubeGrid current = grids[q];
                if (current == null || current.Closed)
                    continue;

                for (int i = 0; i < motors.Count; i++)
                {
                    IMyMotorStator motor = motors[i];
                    if (motor is IMyMotorSuspension)
                        continue;
                    EnqueueLinked(motor, current);
                }

                for (int i = 0; i < pistons.Count; i++)
                    EnqueueLinked(pistons[i], current);
            }

            bool anyStatic = false;
            for (int i = 0; i < grids.Count; i++)
            {
                IMyCubeGrid grid = grids[i];
                if (grid != null && !grid.Closed && grid.IsStatic)
                {
                    anyStatic = true;
                    break;
                }
            }

            for (int i = 0; i < connectors.Count; i++)
            {
                IMyShipConnector connector = connectors[i];
                if (connector == null || connector.Closed || connector.CubeGrid == null)
                    continue;
                if (!seen.Contains(connector.CubeGrid.EntityId))
                    continue;
                SetLine(connector, Line, anyStatic);
            }
        }

        // One managed connector keeps FS_DOCK=Other. Other lines on that block stay.
        public static void ApplyDockPort(List<IMyShipConnector> connectors, string portName)
        {
            if (connectors == null || string.IsNullOrEmpty(portName))
                return;

            bool matched = false;
            for (int i = 0; i < connectors.Count; i++)
            {
                IMyShipConnector connector = connectors[i];
                if (connector == null || connector.Closed)
                    continue;
                if (connector.CustomName == portName)
                {
                    matched = true;
                    break;
                }
            }
            if (!matched)
                return;

            for (int i = 0; i < connectors.Count; i++)
            {
                IMyShipConnector connector = connectors[i];
                if (connector == null || connector.Closed)
                    continue;
                SetLine(connector, DockOther, connector.CustomName == portName);
            }
        }

        static void EnqueueLinked(IMyMechanicalConnectionBlock block, IMyCubeGrid current)
        {
            if (block == null || block.Closed || current == null)
                return;

            IMyCubeGrid next = null;
            if (SameGrid(block.CubeGrid, current))
            {
                IMyCubeGrid top = block.TopGrid;
                if (top == null || top.Closed)
                    return;
                next = top;
            }
            else if (SameGrid(block.TopGrid, current))
            {
                IMyCubeGrid host = block.CubeGrid;
                if (host == null || host.Closed)
                    return;
                next = host;
            }

            if (next == null || next.Closed)
                return;
            if (!seen.Add(next.EntityId))
                return;
            grids.Add(next);
        }

        static bool SameGrid(IMyCubeGrid a, IMyCubeGrid b)
        {
            return a != null && b != null && !a.Closed && !b.Closed && a.EntityId == b.EntityId;
        }

        static void SetLine(IMyTerminalBlock block, string line, bool shouldHave)
        {
            string data = block.CustomData ?? "";
            bool has = HasExactLine(data, line);
            if (shouldHave == has)
                return;

            if (shouldHave)
            {
                block.CustomData = AppendExactLine(data, line);
                return;
            }

            string stripped = data;
            while (HasExactLine(stripped, line))
                stripped = RemoveExactLine(stripped, line);
            if (stripped != data)
                block.CustomData = stripped;
        }

        public static bool HasExactLine(string data, string line)
        {
            if (string.IsNullOrEmpty(data) || string.IsNullOrEmpty(line))
                return false;

            int i = 0;
            while (i <= data.Length)
            {
                int nl = data.IndexOf('\n', i);
                int end = nl < 0 ? data.Length : nl;
                int contentEnd = end;
                if (contentEnd > i && data[contentEnd - 1] == '\r')
                    contentEnd--;
                if (contentEnd - i == line.Length && data.Substring(i, line.Length) == line)
                    return true;
                if (nl < 0)
                    break;
                i = nl + 1;
            }
            return false;
        }

        public static string AppendExactLine(string data, string line)
        {
            if (string.IsNullOrEmpty(data))
                return line;
            if (data[data.Length - 1] == '\n')
                return data + line;
            return data + "\n" + line;
        }

        public static string RemoveExactLine(string data, string line)
        {
            if (string.IsNullOrEmpty(data) || !HasExactLine(data, line))
                return data;

            int i = 0;
            while (i <= data.Length)
            {
                int nl = data.IndexOf('\n', i);
                int end = nl < 0 ? data.Length : nl;
                int contentEnd = end;
                if (contentEnd > i && data[contentEnd - 1] == '\r')
                    contentEnd--;
                if (contentEnd - i == line.Length && data.Substring(i, line.Length) == line)
                {
                    if (nl < 0)
                    {
                        int cut = i;
                        if (cut > 0 && data[cut - 1] == '\n')
                        {
                            cut--;
                            if (cut > 0 && data[cut - 1] == '\r')
                                cut--;
                        }
                        return data.Substring(0, cut);
                    }
                    return data.Substring(0, i) + data.Substring(nl + 1);
                }
                if (nl < 0)
                    break;
                i = nl + 1;
            }
            return data;
        }
    }
}
