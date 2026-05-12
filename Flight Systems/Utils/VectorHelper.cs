using IngameScript.Domain;
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

        /// <summary>
        /// Rotates the ship's Up vector toward the ship's Forward vector (nose-UP pitch).
        /// Positive angleDeg = nose UP.
        /// </summary>
        public static Vector3D RotateUpTowardForwardForNoseUp(GridContext sc, double angleDeg)
        {
            if (sc.Controller == null)
                return Vector3D.Up;

            Vector3D currentUp = sc.Controller.WorldMatrix.Up;
            Vector3D rightAxis = sc.Controller.WorldMatrix.Right;  // pitch axis

            double angleRad = MathHelper.ToRadians(angleDeg);
            MatrixD rotation = MatrixD.CreateFromAxisAngle(rightAxis, -angleRad);  // NEGATIVE = nose UP!

            Vector3D rotatedUp = Vector3D.TransformNormal(currentUp, rotation);
            return Vector3D.Normalize(rotatedUp);
        }
    }
}
