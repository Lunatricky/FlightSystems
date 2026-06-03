using IngameScript.Domain;
using IngameScript.Physics;
using Sandbox.ModAPI.Ingame;
using System;
using VRageMath;

namespace IngameScript
{
    class VectorHelper
    {
        public static Vector3D GetLowestPoint(GridContext sc)
        {
            BoundingBoxD bb = sc.Me.CubeGrid.WorldAABB;

            Vector3D shipDown = Base6Directions.GetVector(
                Base6Directions.GetOppositeDirection(sc.Controller.Orientation.Up)
            );

            // This gives the true lowest point of the grid in the ship's "down" direction
            Vector3D lowestPoint = bb.Center - shipDown * bb.HalfExtents.Dot(shipDown);

            return lowestPoint;
        }

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

        public static bool AlignToGravity(GridContext gc, PhysicsContext pc)
        {
            return AlignToGravity(gc, pc, false);
        }

        public static bool AlignToGravity(GridContext gc, PhysicsContext pc, bool checkSpeed)
        {
            Vector3D shipUp = gc.Controller.WorldMatrix.Up;

            return AlignToVector(gc, pc, shipUp, checkSpeed, Vector3D.Normalize(pc.NaturalGravity));
        }

        public static bool AlignToVector(GridContext gc, PhysicsContext pc, Vector3D shipUp, bool checkSpeed, Vector3D desiredUpVector)
        {
            if (pc.Gravity < 0.01)
                return false;

            Vector3D axis = shipUp.Cross(desiredUpVector);
            double angle = axis.Length();

            if (angle < 0.005 && (!checkSpeed || pc.IsStopped))
            {
                foreach (var g in gc.Gyros)
                    g.GyroOverride = false;

                return true;
            }

            axis /= angle;

            Vector3D angVel = gc.Controller.GetShipVelocities().AngularVelocity;

            //-----------------------------------
            // ⭐ ANGULAR RATE LIMIT
            //-----------------------------------

            const double MAX_ROT_RATE = 0.6; // radians/sec
            const double RESPONSE = 1.0;     // lower = smoother

            Vector3D desiredRate = axis * Math.Min(angle * RESPONSE, MAX_ROT_RATE);

            //-----------------------------------
            // PD ShipContext.Controller on angular velocity
            //-----------------------------------

            Vector3D correction = desiredRate - angVel;

            //-----------------------------------

            foreach (var g in gc.Gyros)
            {
                MatrixD inv = MatrixD.Transpose(g.WorldMatrix);
                Vector3D local = Vector3D.TransformNormal(correction, inv);

                g.GyroOverride = true;

                g.Pitch = (float)MathHelper.Clamp(local.X / 2, -3, 3);
                g.Yaw = (float)MathHelper.Clamp(local.Y / 2, -3, 3);
                g.Roll = (float)MathHelper.Clamp(local.Z / 2, -3, 3);
            }

            return false;
        }

        public static bool AimYawOnlyAt(GridContext gc, PhysicsContext pc, Vector3D targetGps)
        {
            if (gc.Controller == null || gc.Gyros == null || gc.Gyros.Count == 0) return false;
            if (pc.NaturalGravity.LengthSquared() < 0.01) return false;

            // Yaw axis: away-from-gravity (up)
            Vector3D up = Vector3D.Normalize(pc.NaturalGravity);

            // Ship position and forward (use ShipContext.Controller forward in world)
            Vector3D shipPos = gc.Controller.GetPosition();
            Vector3D shipForward = gc.Controller.WorldMatrix.Forward;

            // Vector to target
            Vector3D toTarget = targetGps - shipPos;
            if (toTarget.LengthSquared() < 1e-6) return true; // target at ship

            // Project both onto plane perpendicular to up (horizon plane)
            Vector3D targetProj = toTarget - up * Vector3D.Dot(toTarget, up);
            if (targetProj.LengthSquared() < 1e-9) return true; // target exactly above/below — no yaw defined
            targetProj = Vector3D.Normalize(targetProj);

            Vector3D forwardProj = shipForward - up * Vector3D.Dot(shipForward, up);
            if (forwardProj.LengthSquared() < 1e-9)
            {
                // degenerate forward: pick any perp on plane
                forwardProj = Vector3D.Cross(up, Math.Abs(up.X) < 0.9 ? Vector3D.UnitX : Vector3D.UnitY);
            }
            forwardProj = Vector3D.Normalize(forwardProj);

            // Signed yaw angle from forwardProj -> targetProj around up
            double cosA = Vector3D.Dot(forwardProj, targetProj);
            cosA = Math.Max(-1.0, Math.Min(1.0, cosA));
            double angleMag = Math.Acos(cosA);
            double sign = Math.Sign(Vector3D.Dot(forwardProj.Cross(targetProj), up));
            double yawAngle = sign * angleMag; // radians; + = rotate around 'up' by right-hand rule

            // Finished if small
            const double ANGLE_EPS = 0.01; // ~0.57 deg
            if (Math.Abs(yawAngle) < ANGLE_EPS)
            {
                foreach (var g in gc.Gyros) g.GyroOverride = false;
                return true;
            }

            // Desired angular rate around up only
            const double MAX_ROT_RATE = 3.0;
            const double RESPONSE = 1.0;
            double desiredRateScalar = Math.Min(Math.Abs(yawAngle) * RESPONSE, MAX_ROT_RATE);
            Vector3D desiredRate = up * (Math.Sign(yawAngle) * desiredRateScalar);

            // PD correction (use full angular velocity but we'll only command yaw to sc.Gyros)
            Vector3D angVel = gc.Controller.GetShipVelocities().AngularVelocity;
            Vector3D correction = desiredRate - angVel;

            // Apply to sc.Gyros but zero pitch & roll commands so only yaw moves
            foreach (var g in gc.Gyros)
            {
                MatrixD inv = MatrixD.Transpose(g.WorldMatrix);
                Vector3D local = Vector3D.TransformNormal(correction, inv);

                g.GyroOverride = true;
                g.Pitch = 0f;
                // Some sc.Gyros have inverted yaw axis; if direction is reversed, invert local.Y here
                g.Yaw = (float)MathHelper.Clamp(-local.Y / 2, -3, 3);
                g.Roll = 0f;
            }

            return false;
        }
    }
}
