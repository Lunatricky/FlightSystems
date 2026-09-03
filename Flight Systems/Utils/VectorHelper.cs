using IngameScript.Domain;
using IngameScript.Physics;
using System;
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

        public static Vector3D PitchUp(GridContext sc, Vector3D naturalGravity, double angleDeg)
        {
            if (sc.Controller == null || naturalGravity.LengthSquared() < 1e-12)
                return Vector3D.Up;

            Vector3D gDown = Vector3D.Normalize(naturalGravity);
            Vector3D uSky = -gDown;
            Vector3D forward = sc.Controller.WorldMatrix.Forward;
            Vector3D fHoriz = forward - uSky * Vector3D.Dot(forward, uSky);

            if (fHoriz.LengthSquared() < 1e-12)
                fHoriz = Vector3D.Cross(sc.Controller.WorldMatrix.Right, uSky);

            if (fHoriz.LengthSquared() < 1e-12)
                return gDown;

            fHoriz.Normalize();
            double theta = MathHelper.ToRadians(MathHelper.Clamp(angleDeg, 0, 35));

            // Passed into VectorAlignedOverride, which settles on the antipode:
            // θ=0 → +g (same as GravityAlignedOverride) → ship Up = -g
            // θ>0 → +g tilted toward horizon → ship Up = -g tilted aft (nose up)
            return Vector3D.Normalize(Math.Cos(theta) * gDown + Math.Sin(theta) * fHoriz);
        }

        public static void MatchVerticalSpeed(GridContext gc, PhysicsContext pc, double target)
        {
            double hover = (pc.Mass.PhysicalMass * pc.Gravity) / SumThrust(gc);

            double current = GetGravityAlignedVerticalVelocity(gc, pc);
            double error = target - current;

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

        public static bool IsWithinAngle(Vector3D planetCenter, Vector3D shipPosition, Vector3D gpsPosition, double angleDegrees)
        {
            Vector3D shipVector = Vector3D.Normalize(shipPosition - planetCenter);
            Vector3D gpsVector = Vector3D.Normalize(gpsPosition - planetCenter);
            double limit = Math.Cos(MathHelper.ToRadians(angleDegrees));

            return Vector3D.Dot(shipVector, gpsVector) > limit;
        }
    }
}
