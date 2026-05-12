using IngameScript.Domain;

namespace IngameScript.Physics
{
    class PhysicsContext
    {
        PhysicsContextLastTick physicsContextPersistent;
        PhysicsContextTransient physicsContextTransient;

        public PhysicsContext(GridContext gc, IniContext ic, double timeSinceLastRun)
        {
            physicsContextTransient = new PhysicsContextTransient(gc, ic, timeSinceLastRun);
            physicsContextPersistent = new PhysicsContextLastTick(physicsContextTransient);
        }

        internal PhysicsContextLastTick Persistent => physicsContextPersistent;

        internal PhysicsContextTransient Transient => physicsContextTransient;

        public void ResetTransientPhysicsContext(GridContext gc, IniContext ic, double timeSinceLastRun)
        {
            physicsContextTransient = new PhysicsContextTransient(gc, ic, timeSinceLastRun);
            physicsContextPersistent = new PhysicsContextLastTick(physicsContextTransient);
        }
    }
}
