using IngameScript.Domain;
using IngameScript.UseCases;
using IngameScript.Utils;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using VRageMath;

namespace IngameScript.Physics
{
    public class PhysicsContext
    {
        readonly GridContext gc;
        readonly SpeedTimeTracker stt;
        readonly Command command;

        double accumulatedTime = 0;
        double timeSinceLastRun = 0.00001;
        readonly double threshold = 0.1;

        MatrixD worldMatrix = new MatrixD();
        MyShipMass mass = new MyShipMass();

        Vector3D naturalGravity = new Vector3D();
        Vector3D velocity = new Vector3D();
        Vector3D accel = new Vector3D();
        Vector3D desiredUpVector = new Vector3D();

        H2Totals h2Cache = new H2Totals();
        BatTotals batCache = new BatTotals();

        double groundLevel;
        double effectiveAlt;
        double gravity;
        double climbRate;
        double vEffectiveYSpeed;
        double vEffectiveZSpeed;
        double stopYDist;
        double stopZDist;
        double stopYDistTemp;
        double stopZDistTemp;
        double timeToImpact;
        double timeToStopY;
        double timeToStopZ;
        double timeToDistanceSmoothed;
        double forwardVelocity;
        double rightVelocity;
        double upVelocity;
        double netDecel;
        double distanceToLine;

        bool isStopped;

        Vector3D prevVelocity = new Vector3D();
        double smoothedSpeed = 0;
        double prevH2Fill = 0;
        double prevBatFill = 0;

        public struct H2Totals { public double Capacity; public double Filled; public double Percent; public string Time; }
        public struct BatTotals { public double Capacity; public double Filled; public double Percent; public string Time; }

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

            worldMatrix = gc.Controller.WorldMatrix;
            mass = gc.Controller.CalculateShipMass();
            naturalGravity = gc.Controller.GetNaturalGravity();
            velocity = gc.Controller.GetShipVelocities().LinearVelocity;
            accel = ((Velocity - prevVelocity) / timeSinceLastRun);
            desiredUpVector = VectorHelper.PitchUp(gc, 0.9 * GetMaxPitchAngle(gc));
            gravity = NaturalGravity.Length();
            groundLevel = GetPlanetElevation();
            effectiveAlt = (GroundLevel - gc.GridHeight - vEffectiveYSpeed * timeSinceLastRun);
            vEffectiveYSpeed = UpVelocity == 0 ? 0 : ClimbRate + MaxYDecel * timeSinceLastRun;
            vEffectiveZSpeed = ForwardVelocity == 0 ? 0 : ForwardVelocity + MaxZDecel * timeSinceLastRun;
            stopYDist = StopYDistTemp < 0.4 ? 0 : StopYDistTemp;
            stopZDist = StopZDistTemp < 0.4 ? 0 : StopZDistTemp;
            stopYDistTemp = Math.Abs(VEffectiveYSpeed * VEffectiveYSpeed / (2 * MaxYDecel));
            stopZDistTemp = Math.Abs(VEffectiveZSpeed * VEffectiveZSpeed / (2 * MaxZDecel));
            climbRate = VectorHelper.GetGravityAlignedVerticalVelocity(gc, this);
            timeToImpact = GroundLevel / Math.Abs(VEffectiveYSpeed);
            timeToStopY = Math.Abs(ClimbRate / MaxYDecel);
            timeToStopZ = Math.Abs(ForwardVelocity / MaxZDecel);
            timeToDistanceSmoothed = GetTimeToDistanceSmoothed(DistanceToLine, timeSinceLastRun);
            forwardVelocity = Vector3D.Dot(Velocity, WorldMatrix.Forward);
            rightVelocity = Vector3D.Dot(Velocity, WorldMatrix.Right);
            upVelocity = Vector3D.Dot(Velocity, WorldMatrix.Up);
            netDecel = ComputeNetDecel(gc);
            distanceToLine = DistanceToGps(gc.Controller, command.Param.TargetCoordinates);
            isStopped = threshold > UpVelocity && threshold >= Math.Abs(ForwardVelocity) && threshold >= Math.Abs(RightVelocity);
            h2Cache = ComputeH2Totals();
            batCache = ComputeBatTotals();
        }
        

        public void CacheValues()
        {
            prevVelocity = Velocity;
            prevH2Fill = H2Cache.Filled;
            prevBatFill = BatCache.Filled;
        }

        public double Now => accumulatedTime;

        MatrixD WorldMatrix => worldMatrix;
        public MyShipMass Mass => mass;
        public Vector3D NaturalGravity => naturalGravity;
        Vector3D Velocity => velocity;
        public Vector3D Accel => accel;
        public Vector3D DesiredUpVector => desiredUpVector;
        public double Gravity => gravity;
        public double GroundLevel => groundLevel;
        public double EffectiveAlt => effectiveAlt;
        double VEffectiveYSpeed => vEffectiveYSpeed; 
        double VEffectiveZSpeed => vEffectiveZSpeed;
        double StopYDistTemp => stopYDistTemp;
        double StopZDistTemp => stopZDistTemp;
        public double StopYDist => stopYDist;
        public double StopZDist => stopZDist;
        public double ClimbRate => climbRate;
        double MaxYDecel => GetMaxDecel(gc.UpwardThrusters);
        double MaxZDecel => GetMaxDecel(gc.BreakingThrusters);
        public double TimeToImpact => timeToImpact;
        public double TimeToStopY => timeToStopY;
        public double TimeToStopZ => timeToStopZ;
        public double TimeToDistanceSmoothed => timeToDistanceSmoothed;
        public double ForwardVelocity => forwardVelocity;
        public double RightVelocity => rightVelocity;
        public double UpVelocity => upVelocity;
        public double NetDecel => netDecel;
        public double DistanceToLine => distanceToLine;
        public bool IsStopped => isStopped;
        public H2Totals H2Cache => h2Cache;
        public BatTotals BatCache => batCache;
        public double PrevH2Fill => prevH2Fill;
        public double PrevBatFill => prevBatFill;

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
            double cap = 0, filled = 0, percent;
            double batIn = 0, batOut = 0;

            foreach (var battery in gc.Batteries)
            {
                cap += battery.MaxStoredPower;
                filled += battery.CurrentStoredPower;
                batIn += battery.CurrentInput;
                batOut += battery.CurrentOutput;
            }

            percent = 100 * filled / cap;

            double netPower = batIn - batOut;
            string batTime = "--";

            if (Math.Abs(netPower) > 0.01)
            {
                if (netPower > 0)
                    batTime = UtilsHelpder.FormatTime(3600 * (cap - filled) / netPower) + " /\\";
                else if (netPower < 0)
                    batTime = UtilsHelpder.FormatTime(3600 * filled / -netPower) + " \\/";
            }

            return new BatTotals { Capacity = cap, Filled = filled, Percent = percent, Time = batTime };
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
