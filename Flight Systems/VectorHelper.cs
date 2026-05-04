using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRageMath;

namespace IngameScript
{
    class VectorHelper
    {
        public static Vector3D GetLowestPoint(SC sc)
        {
            BoundingBoxD bb = sc.Me.CubeGrid.WorldAABB;

            Vector3D shipDown = Base6Directions.GetVector(
                Base6Directions.GetOppositeDirection(sc.Controller.Orientation.Up)
            );

            // This gives the true lowest point of the grid in the ship's "down" direction
            Vector3D lowestPoint = bb.Center - shipDown * bb.HalfExtents.Dot(shipDown);

            return lowestPoint;
        }
    }
}
