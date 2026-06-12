using IngameScript.Domain;
using IngameScript.Physics;
using Sandbox.ModAPI.Ingame;
using VRageMath;

namespace IngameScript
{
    class VectorHelper
    {
        public static Vector3D GetLowestPoint(GridContext sc)
        {
            BoundingBoxD bb = sc.Me.CubeGrid.WorldAABB;

            Vector3D shipDown = Base6Directions.GetVector(Base6Directions.GetOppositeDirection(sc.Controller.Orientation.Up));

            // This gives the true lowest point of the grid in the ship's "down" direction
            Vector3D lowestPoint = bb.Center - shipDown * bb.HalfExtents.Dot(shipDown);

            return lowestPoint;
        }

        /// <summary>
        /// Rotates the ship's Up vector toward the ship's Forward vector (nose-UP pitch).
        /// Positive angleDeg = nose UP.
        /// </summary>
        public static Vector3D PitchUp(GridContext sc, double angleDeg)
        {
            if (sc.Controller == null)
                return Vector3D.Up;

            Vector3D currentForward = sc.Controller.WorldMatrix.Forward;
            Vector3D rightAxis = sc.Controller.WorldMatrix.Right;  // pitch axis

            double angleRad = MathHelper.ToRadians(angleDeg);
            MatrixD rotation = MatrixD.CreateFromAxisAngle(rightAxis, -angleRad);  // NEGATIVE = nose UP!

            Vector3D rotatedForward = Vector3D.TransformNormal(currentForward, rotation);
            return Vector3D.Normalize(rotatedForward);
        }

        public static void MatchVerticalSpeed(GridContext gc, PhysicsContext pc, double target)
        {
            double hover = (pc.Mass.PhysicalMass * pc.Gravity) / SumThrust(gc);

            double current = GetGravityAlignedVerticalVelocity(gc, pc);
            double error = target - current;

            double minThrustOverride = (pc.ClimbRate < 10 ? 0.001 : 0);
            double output = MathHelper.Clamp(hover + error * 0.5, 0.01, 1);

            foreach (var t in gc.UpwardThrusters)
                t.ThrustOverridePercentage = (float)output;
        }

        public static double GetGravityAlignedVerticalVelocity(GridContext gc, PhysicsContext pc)
        {
            Vector3D gNorm = Vector3D.Normalize(pc.NaturalGravity);

            return -gc.Controller.GetShipVelocities()
                .LinearVelocity.Dot(gNorm);
        }

        static double SumThrust(GridContext gc)
        {
            double total = 0;

            foreach (var t in gc.UpwardThrusters)
                total += t.MaxEffectiveThrust;

            return total;
        }

        Vector3D TryGetPlanetPosition(IMyShipController controller)
        {
            Vector3D planetCenter;

            // Get planet center
            controller.TryGetPlanetPosition(out planetCenter);

            return planetCenter;
        }
    }
}
