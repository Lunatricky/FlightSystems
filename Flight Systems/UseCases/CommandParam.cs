using VRageMath;

namespace IngameScript.UseCases
{
    class CommandParam
    {
        public ParamType Type;
        public AutoLandState AutoLandState = AutoLandState.Idle;

        public double Number;
        public string Text = "";
        public Vector3D TargetCoordinates = new Vector3D();

        // ────────────────────────────────────────────────
        // Constructors — one per type
        // ────────────────────────────────────────────────

        public CommandParam(){}

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
