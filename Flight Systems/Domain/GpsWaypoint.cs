using VRageMath;

namespace IngameScript.Domain
{
    class GpsWaypoint
    {
        public string Name;
        public Vector3D Position;
        public bool Dock;

        public GpsWaypoint()
        {
            Name = "";
        }

        public GpsWaypoint Copy()
        {
            return new GpsWaypoint
            {
                Name = Name,
                Position = Position,
                Dock = Dock
            };
        }
    }
}
