using IngameScript.Enums;
using VRageMath;

namespace IngameScript.UseCases
{
    class CommandParam
    {
        public ParamType Type;
        public AutoLandState AutoLandState = AutoLandState.Idle;

        public double Number;
        public Step Step = Step.Toggle;
        public Vector3D TargetCoordinates = new Vector3D();

        // ────────────────────────────────────────────────
        // Constructors — one per type
        // ────────────────────────────────────────────────

        public CommandParam()
        {
            AutoLandState = AutoLandState.Idle;
            Number = 0;
            Step = Step.Toggle;
            TargetCoordinates = new Vector3D();
        }

        public CommandParam(double n)
        {
            Type = ParamType.Number;
            Number = n;
        }

        public CommandParam(Step t)
        {
            Type = ParamType.Step;
            Step = t;
        }
        public CommandParam(Vector3D targetCoordinates)
        {
            Type = ParamType.Vector3D;
            TargetCoordinates = targetCoordinates;
        }

        // Empty
        public void Empty()
        {
            AutoLandState = AutoLandState.Idle;
            Number = 0;
            Step = Step.Toggle;
            TargetCoordinates = new Vector3D();
    }
    }
}
