using IngameScript.Domain;
using VRageMath;

namespace IngameScript.Physics
{
    class PhysicsContextLastTick
    {
        // persistent between ticks
        private readonly double oldGravity;
        private readonly double prevSmoothedSpeed = 1;
        private Vector3D lastVelocity;
        private readonly double lastH2Fill;

        public PhysicsContextLastTick(PhysicsContextTransient physicsContextTransient)
        {
            oldGravity = physicsContextTransient.Gravity;
            prevSmoothedSpeed = (physicsContextTransient.Alpha * physicsContextTransient.AvgSpeed) + ((1.0 - physicsContextTransient.Alpha) * prevSmoothedSpeed);
            lastVelocity = physicsContextTransient.Velocity;
            lastH2Fill = physicsContextTransient.GetLastH2Fill();
        }

        public double OldGravity => oldGravity;
        public double PrevSmoothedSpeed => prevSmoothedSpeed;
        public Vector3D LastVelocity => lastVelocity;
        public double LastH2Fill => lastH2Fill;
    }
}
