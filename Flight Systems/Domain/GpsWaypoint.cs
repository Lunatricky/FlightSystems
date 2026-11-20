using VRageMath;

namespace IngameScript.Domain
{
    class GpsWaypoint
    {
        public string Name;
        public Vector3D Position;

        public GpsWaypoint()
        {
            Name = "";
        }

        public GpsWaypoint Copy()
        {
            return new GpsWaypoint
            {
                Name = Name,
                Position = Position
            };
        }
    }
}
