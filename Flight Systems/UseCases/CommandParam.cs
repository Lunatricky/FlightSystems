using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRageMath;

namespace IngameScript.UseCases
{
    class CommandParam
    {
        public ParamType Type;
        public AutoLandStateEnum AutoLandState = AutoLandStateEnum.Idle;

        public double Number;
        public string Text = "";
        public Vector3D TargetCoordinates = new Vector3D();

        // ────────────────────────────────────────────────
        // Constructors — one per type
        // ────────────────────────────────────────────────

        public CommandParam(double n)
        {
            Type = ParamType.Number;
            Number = n;
        }

        public CommandParam(string t)
        {
            Type = ParamType.Text;
            Text = t ?? "";
        }
        public CommandParam(Vector3D targetCoordinates)
        {
            Type = ParamType.Vector3D;
            TargetCoordinates = targetCoordinates;
        }

        // Empty
        public static CommandParam Empty => new CommandParam(null);
    }
}
