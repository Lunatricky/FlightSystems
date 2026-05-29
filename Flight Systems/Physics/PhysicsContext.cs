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
        SpeedTimeTracker stt;
        Command command;

        double accumulatedTime = 0.0;
        double timeSinceLastRun = 0.00001;
        double threshold = 0.1;

        Cached<MatrixD> worldMatrix = new Cached<MatrixD>();
        Cached<MyShipMass> mass = new Cached<MyShipMass>();

        Cached<Vector3D> naturalGravity = new Cached<Vector3D>();
        Cached<Vector3D> velocity = new Cached<Vector3D>();
        Cached<Vector3D> accel = new Cached<Vector3D>();
        Cached<Vector3D> desiredUpVector = new Cached<Vector3D>();

        Cached<H2Totals> h2Cache = new Cached<H2Totals>();
        Cached<BatTotals> batCache = new Cached<BatTotals>();

        Cached<double> groundLevel = new Cached<double>();
        Cached<double> gravity = new Cached<double>();
        Cached<double> climbRate = new Cached<double>();
        Cached<double> vEffectiveYSpeed = new Cached<double>();
        Cached<double> vEffectiveZSpeed = new Cached<double>();
        Cached<double> timeToImpact = new Cached<double>();
        Cached<double> timeToStopY = new Cached<double>();
        Cached<double> timeToStopZ = new Cached<double>();
        Cached<double> timeToDistanceSmoothed = new Cached<double>();
        Cached<double> forwardVelocity = new Cached<double>();
        Cached<double> rightVelocity = new Cached<double>();
        Cached<double> upVelocity = new Cached<double>();
        Cached<double> netDecel = new Cached<double>();
        Cached<double> distanceToLine = new Cached<double>();

        Cached<bool> isStopped = new Cached<bool>();

        Vector3D prevVelocity = new Vector3D();
        double smoothedSpeed = 0;
        double prevH2Fill = 0;

        public struct H2Totals { public double Capacity; public double Filled; public double Percent; public string Time; }
        public struct BatTotals { public double Capacity; public double Filled; public string Time; }

        const double ALPHA = 0.2;

        public PhysicsContext(GridContext gc, SpeedTimeTracker stt, Command command, double timeSinceLastRun)
        {
            this.gc = gc;
            this.stt = stt;
            this.command = command;

            if (timeSinceLastRun > 0) this.timeSinceLastRun = timeSinceLastRun;
        }

        // Call at start of each Program.Run with Runtime.TimeSinceLastRun.TotalSeconds
        public void NewRun(double timeSinceLastRun)
        {
            if (timeSinceLastRun > 0) this.timeSinceLastRun = timeSinceLastRun;
            accumulatedTime += timeSinceLastRun;
        }

        public void CacheValues()
        {
            prevVelocity = Velocity;
            prevH2Fill = H2Cache.Filled;
        }

        public double Now => accumulatedTime;

        MatrixD WorldMatrix => worldMatrix.Get(Now, () => gc.Controller.WorldMatrix);
        public MyShipMass Mass => mass.Get(Now, () => gc.Controller.CalculateShipMass());
        public Vector3D NaturalGravity => naturalGravity.Get(Now, () => gc.Controller.GetNaturalGravity());
        Vector3D Velocity => velocity.Get(Now, () => gc.Controller.GetShipVelocities().LinearVelocity);
        public Vector3D Accel => accel.Get(Now, () => ((Velocity - prevVelocity) / timeSinceLastRun));
        public Vector3D DesiredUpVector => desiredUpVector.Get(Now, () => VectorHelper.PitchUp(gc, 0.9 * GetMaxPitchAngle(gc)));
        public double Gravity => gravity.Get(Now, () => NaturalGravity.Length());
        public double GroundLevel => groundLevel.Get(Now, () => GetPlanetElevation());
        public double EffectiveAlt => (GroundLevel - gc.GridHeight - VEffectiveYSpeed * timeSinceLastRun);
        double VEffectiveYSpeed => (UpVelocity == 0 ? 0 : vEffectiveYSpeed.Get(Now, () => ClimbRate + MaxYDecel * timeSinceLastRun));
         double VEffectiveZSpeed => (ForwardVelocity == 0 ? 0 : vEffectiveZSpeed.Get(Now, () => ForwardVelocity + MaxZDecel * timeSinceLastRun));
        double StopYDistTemp => Math.Abs(VEffectiveYSpeed * VEffectiveYSpeed / (2 * MaxYDecel));
        double StopZDistTemp => Math.Abs(VEffectiveZSpeed * VEffectiveZSpeed / (2 * MaxZDecel));
        public double StopYDist => (StopYDistTemp < 0.4 ? 0 : StopYDistTemp);
        public double StopZDist => (StopZDistTemp < 0.4 ? 0 : StopZDistTemp);
        public double ClimbRate => climbRate.Get(Now, () => VectorHelper.GetGravityAlignedVerticalVelocity(gc, this));
        double MaxYDecel => GetMaxDecel(gc.UpwardThrusters);
        double MaxZDecel => GetMaxDecel(gc.BreakingThrusters);
        public double TimeToImpact => timeToImpact.Get(Now, () => GroundLevel / Math.Abs(VEffectiveYSpeed));
        public double TimeToStopY => timeToStopY.Get(Now, () => Math.Abs(ClimbRate / MaxYDecel));
        public double TimeToStopZ => timeToStopZ.Get(Now, () => Math.Abs(ForwardVelocity / MaxZDecel));
        public double TimeToDistanceSmoothed => timeToDistanceSmoothed.Get(Now, () => GetTimeToDistanceSmoothed(DistanceToLine, timeSinceLastRun));
        public double ForwardVelocity => forwardVelocity.Get(Now, () => Vector3D.Dot(Velocity, WorldMatrix.Forward));
        public double RightVelocity => rightVelocity.Get(Now, () => Vector3D.Dot(Velocity, WorldMatrix.Right));
        public double UpVelocity => upVelocity.Get(Now, () => Vector3D.Dot(Velocity, WorldMatrix.Up));
        public double NetDecel => netDecel.Get(Now, () => ComputeNetDecel(gc));
        public double DistanceToLine => distanceToLine.Get(Now, () => DistanceToGps(gc.Controller, command.Param.TargetCoordinates));
        public bool IsStopped => isStopped.Get(Now, () => threshold > UpVelocity && threshold >= Math.Abs(ForwardVelocity) && threshold >= Math.Abs(RightVelocity));
        public H2Totals H2Cache => h2Cache.Get(Now, () => ComputeH2Totals());
        public BatTotals BatCache => batCache.Get(Now, () => ComputeBatTotals());

        H2Totals ComputeH2Totals()
        {
            double cap = 0.0, filled = 0.0, percent, rate;
            string time = "";
            foreach (var tank in gc.H2Tanks)
            {
                cap += tank.Capacity;
                filled += tank.Capacity * tank.FilledRatio;
            }

            percent = 100 * filled / cap;
            rate = (filled - prevH2Fill) / timeSinceLastRun;

            if (Math.Abs(rate) > 1e-6)
            {
                if (rate > 0)
                    time = UtilsHelpder.FormatTime((cap - filled) / rate) + " /\\";
                else if (rate < 0)
                    time = UtilsHelpder.FormatTime(filled / -rate) + " \\/";
            }

            return new H2Totals { Capacity = cap, Filled = filled, Percent = percent, Time = time };
        }

        BatTotals ComputeBatTotals()
        {
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

        double GetTimeToDistanceSmoothed(double distance, double dt)
        {
            stt.AddValue(ForwardVelocity, dt);

            if (dt <= 0) return double.PositiveInfinity;
            double avgSpeed = stt.GetAverageSpeed();

            if (avgSpeed <= 1e-6) avgSpeed = 0.0;

            // EMA smoothing
            smoothedSpeed = (ALPHA * avgSpeed) + ((1.0 - ALPHA) * smoothedSpeed);

            if (smoothedSpeed <= 1e-6) return double.PositiveInfinity;
            return distance / smoothedSpeed;
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
