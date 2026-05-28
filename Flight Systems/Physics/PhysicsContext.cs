using IngameScript.Domain;
using IngameScript.UseCases;
using IngameScript.Utils;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using VRageMath;

namespace IngameScript.Physics
{
    class PhysicsContext
    {
        GridContext gc;
        IniContext ic;
        SpeedTimeTracker stt;
        Command command;

        double accumulatedTime = 0.0;
        double timeSinceLastRun;

        Cached<MatrixD> worldMatrix;

        // backing fields
        Cached<Vector3D> naturalGravity;
        Cached<Vector3D> velocity;
        Cached<Vector3D> accel;
        Cached<Vector3D> desiredUpVector;
        Cached<MyShipMass> mass;

        Cached<H2Totals> h2Cache;
        Cached<BatTotals> batCache;

        Cached<double> avgSpeed;
        Cached<double> groundLevel;
        Cached<double> gravity;
        Cached<double> climbRate;
        Cached<double> vEffectiveYSpeed;
        Cached<double> vEffectiveZSpeed;
        Cached<double> timeToImpact;
        Cached<double> timeToStopY;
        Cached<double> timeToStopZ;
        Cached<double> timeToDistanceSmoothed;
        Cached<double> forwardVelocity;
        Cached<double> rightVelocity;
        Cached<double> upVelocity;
        Cached<double> netDecel;
        Cached<double> distanceToLine;

        Vector3D prevVelocity;
        double prevGravity;
        double prevSmoothedSpeed;
        double prevH2Fill;

        public struct H2Totals { public double Capacity; public double Filled; public double Percent; public double Rate; public string Time; }
        public struct BatTotals { public double Capacity; public double Filled; public string Time; }

        // constants
        const double ALPHA = 0.2;

        public PhysicsContext(GridContext gc, IniContext ic, SpeedTimeTracker stt, Command command, double timeSinceLastRun)
        {
            this.gc = gc;
            this.ic = ic;
            this.stt = stt;
            this.command = command;
            this.timeSinceLastRun = timeSinceLastRun;

            //previousVelocity = Velocity;
        }

        // Call at start of each Program.Run with Runtime.TimeSinceLastRun.TotalSeconds
        public void NewRun(double dt)
        {
            if (dt <= 0) dt = 1e-6;
            accumulatedTime += dt;

            // advance velocity snapshots once per Run
            prevVelocity = Velocity;
            prevGravity = Gravity;
            prevH2Fill = H2Cache.Filled;
            prevSmoothedSpeed = (ALPHA * AvgSpeed) + ((1.0 - ALPHA) * PrevSmoothedSpeed);

            // Note: do not call Get on caches here; they will compute lazily on demand using the same 'now' timestamp
        }

        double Now => accumulatedTime;

        public MatrixD WorldMatrix => worldMatrix.Get(Now, () => gc.Controller.WorldMatrix);
        public MyShipMass Mass => mass.Get(Now, () => gc.Controller.CalculateShipMass());
        public Vector3D NaturalGravity => naturalGravity.Get(Now, () => gc.Controller.GetNaturalGravity());
        public Vector3D Velocity => velocity.Get(Now, () => gc.Controller.GetShipVelocities().LinearVelocity);
        public Vector3D Accel => accel.Get(Now, () => ((Velocity - PrevVelocity) / timeSinceLastRun));
        public Vector3D DesiredUpVector => desiredUpVector.Get(Now, () => VectorHelper.RotateUpTowardForwardForNoseUp(gc, -0.9 * GetMaxPitchAngle(gc)));
        public double Gravity => gravity.Get(Now, () => NaturalGravity.Length());
        public double GroundLevel => groundLevel.Get(Now, () => GetPlanetElevation());
        public double EffectiveAlt => (GroundLevel - gc.GridHeight - VEffectiveYSpeed * timeSinceLastRun) / Gravity / PrevGravity;
        public double AvgSpeed => avgSpeed.Get(Now, () => (ALPHA * AvgSpeed) + ((1.0 - ALPHA) * PrevSmoothedSpeed));
        public double StopYDist => Math.Abs(VEffectiveYSpeed * VEffectiveYSpeed / (2 * MaxYDecel));
        public double StopZDist => Math.Abs((VEffectiveZSpeed * VEffectiveZSpeed) / (2 * MaxZDecel));
        public double CruiseSpeed => ic.MaxSpeed;
        public double ClimbRate => climbRate.Get(Now, ()=> VectorHelper.GetGravityAlignedVerticalVelocity(gc, this));
        public double VEffectiveYSpeed => vEffectiveYSpeed.Get(Now, ()=> ClimbRate + MaxYDecel * timeSinceLastRun);
        public double VEffectiveZSpeed => vEffectiveZSpeed.Get(Now, ()=> ForwardVelocity + MaxZDecel * timeSinceLastRun);
        public double MaxYDecel => GetMaxDecel(gc.UpwardThrusters);
        public double MaxZDecel => GetMaxDecel(gc.BreakingThrusters);
        public double TimeToImpact => timeToImpact.Get(Now, () => GroundLevel / Math.Abs(VEffectiveYSpeed));
        public double TimeToStopY => timeToStopY.Get(Now, () => Math.Abs(ClimbRate / MaxYDecel));
        public double TimeToStopZ => timeToStopZ.Get(Now, () => Math.Abs(ForwardVelocity / MaxZDecel));
        public double TimeToDistanceSmoothed => timeToDistanceSmoothed.Get(Now, () => GetTimeToDistanceSmoothed(DistanceToLine, timeSinceLastRun));
        public double ForwardVelocity => forwardVelocity.Get(Now, () => Vector3D.Dot(Velocity, WorldMatrix.Forward));
        public double RightVelocity => rightVelocity.Get(Now, () => Vector3D.Dot(Velocity, WorldMatrix.Right));
        public double UpVelocity => upVelocity.Get(Now, () => Vector3D.Dot(Velocity, WorldMatrix.Up));
        public double NetDecel => netDecel.Get(Now, () => ComputeNetDecel(gc));
        public double DistanceToLine => distanceToLine.Get(Now, () => DistanceToGps(gc.Controller, command.Param.TargetCoordinates));
        public H2Totals H2Cache => h2Cache.Get(Now, () => ComputeH2Totals());
        public BatTotals BatCache => batCache.Get(Now, () => ComputeBatTotals());


        public Vector3D PrevVelocity => prevVelocity;
        public double PrevGravity => prevGravity;
        public double PrevSmoothedSpeed => prevSmoothedSpeed;
        public double PrevH2Fill => prevH2Fill;

        H2Totals ComputeH2Totals()
        {
            double cap = 0.0, filled = 0.0, percent, rate;
            string time = "";
            foreach (var tank in gc.H2Tanks)
            {
                cap += tank.Capacity;
                filled += tank.Capacity * tank.FilledRatio;
            }

            percent = 100 * H2Cache.Filled / H2Cache.Capacity;
            rate = (H2Cache.Filled - PrevH2Fill) / timeSinceLastRun;

            if (Math.Abs(H2Cache.Rate) > 1e-6)
            {
                if (H2Cache.Rate > 0)
                    time = UtilsHelpder.FormatTime((H2Cache.Capacity - H2Cache.Filled) / H2Cache.Rate) + " /\\";
                else if (H2Cache.Rate < 0)
                    time = UtilsHelpder.FormatTime(H2Cache.Filled / -H2Cache.Rate) + " \\/";
            }

            return new H2Totals { Capacity = cap, Filled = filled, Percent = percent, Rate = rate, Time = time };
        }

        // TODO Add these local vars to PhysicsContext properties
        // TODO Put each of these calculations in the properties get =>
        // TODO Make a method to call in Program for each cicle where instead of pre loading all PhysicsCcontext properties delete all except the "old/last" properties.
        BatTotals ComputeBatTotals()
        {
            // Batteries
            double batCap = 0, batStored = 0;
            double batIn = 0, batOut = 0;

            foreach (var battery in gc.Batteries)
            {
                batCap += battery.MaxStoredPower;
                batStored += battery.CurrentStoredPower;
                batIn += battery.CurrentInput;
                batOut += battery.CurrentOutput;
            }

            double netPower = batIn - batOut;
            string batTime = "--";

            if (Math.Abs(netPower) > 0.01)
            {
                if (netPower > 0)
                    batTime = UtilsHelpder.FormatTime(3600 * (batCap - batStored) / netPower) + " /\\";
                else if (netPower < 0)
                    batTime = UtilsHelpder.FormatTime(3600 * batStored / -netPower) + " \\/";
            }
            return new BatTotals { Capacity = batCap, Filled = batStored, Time = batTime };
        }

        double GetPlanetElevation()
        {
            double alt;
            gc.Controller.TryGetPlanetElevation(MyPlanetElevation.Surface, out alt);
            return alt;
        }

        void UpdateSmoothedSpeed(double avgSpeed)
        {
            prevSmoothedSpeed = (ALPHA * avgSpeed) + ((1.0 - ALPHA) * prevSmoothedSpeed);
        }

        double GetTimeToDistanceSmoothed(double distance, double dt)
        {
            stt.AddValue(ForwardVelocity, dt);

            if (dt <= 0) return double.PositiveInfinity;
            double avgSpeed = stt.GetAverageSpeed();

            if (avgSpeed <= 1e-6) avgSpeed = 0.0;

            if (PrevSmoothedSpeed <= 1e-6) return double.PositiveInfinity;
            return distance / PrevSmoothedSpeed;
        }

        double GetMaxDecel(List<IMyThrust> thrusters)
        {
            double thrust = 0;

            Vector3D up = -Vector3D.Normalize(NaturalGravity);

            foreach (var t in thrusters)
            {
                double dot = t.WorldMatrix.Backward.Dot(up);

                if (dot > 0.7)
                    thrust += t.MaxEffectiveThrust * dot;
            }

            return (thrust / Mass.PhysicalMass) - Gravity;
        }

        double DistanceToGps(IMyShipController controller, Vector3D gps)
        {
            Vector3D shipPos = controller.GetPosition();
            double vertical;

            Vector3D up = -Vector3D.Normalize(NaturalGravity); // up direction
            Vector3D toTarget = gps - shipPos;

            // vertical distance along up (signed): positive = target is "above" ship in up direction
            vertical = Vector3D.Dot(toTarget, up);

            // horizontal vector: component of toTarget on plane perpendicular to up
            Vector3D horizVec = toTarget - up * vertical;

            return horizVec.Length();
        }

        double ComputeNetDecel(GridContext gc)
        {
            double maxThrustUp = 0;
            foreach (var t in gc.UpwardThrusters) maxThrustUp += t.MaxEffectiveThrust;

            double thrustAccel = maxThrustUp / Mass.TotalMass;

            return thrustAccel - Gravity;  // positive = can decelerate
        }

        double GetMaxPitchAngle(GridContext gc)
        {
            double fwdThrust = 0, upThrust = 0;
            foreach (var t in gc.ForwardThrusters)
                if (t.IsFunctional) fwdThrust += t.MaxEffectiveThrust;
            foreach (var t in gc.UpwardThrusters)
                if (t.IsFunctional) upThrust += t.MaxEffectiveThrust;

            return MathHelper.ToDegrees(Math.Atan2(fwdThrust, upThrust));
        }
    }
}
