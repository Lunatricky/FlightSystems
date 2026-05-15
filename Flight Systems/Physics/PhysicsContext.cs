using IngameScript.Domain;

namespace IngameScript.Physics
{
    class PhysicsContext
    {
        PhysicsContextLastTick physicsContextPersistent;
        PhysicsContextTransient physicsContextTransient;

        public PhysicsContext(GridContext gc, IniContext ic, SpeedTimeTracker speedTimeTracker, double timeSinceLastRun)
        {
            physicsContextTransient = new PhysicsContextTransient(gc, ic, speedTimeTracker, timeSinceLastRun);
            physicsContextPersistent = new PhysicsContextLastTick(physicsContextTransient, speedTimeTracker);
        }

        internal PhysicsContextLastTick Persistent => physicsContextPersistent;

        internal PhysicsContextTransient Transient => physicsContextTransient;

        public void ResetTransientPhysicsContext(GridContext gc, IniContext ic, SpeedTimeTracker speedTimeTracker, double timeSinceLastRun)
        {
            physicsContextTransient = new PhysicsContextTransient(gc, ic, speedTimeTracker, timeSinceLastRun);
            physicsContextPersistent = new PhysicsContextLastTick(physicsContextTransient, speedTimeTracker);
        }
    }
}
