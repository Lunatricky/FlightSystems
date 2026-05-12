using IngameScript.Domain;
using IngameScript.UseCases;
using IngameScript.Utils;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using VRageMath;

namespace IngameScript.Physics
{
    class PhysicsContextTransient
    {
        GridContext gc;
        IniContext ic;
        PhysicsContextLastTick pcp;

        // private backing fields
        double timeSinceLastRun;
        private double alt;
        private double effectiveAlt;
        private double cruiseSpeed;
        private double climbRate;
        private double vEffectiveYSpeed;
        private double vEffectiveZSpeed;
        private double maxYDecel;
        private double maxZDecel;
        private double timeToImpact;
        private double timeToStopY;
        private double timeToStopZ;
        private double thrust = 0;
        private double forwardVelocity;
        private double rightVelocity;
        private double upVelocity;
        private double netDecel;
        private double maxThrustUp;
        private double distanceToLine;
        private Vector3D desiredUpVector;
        private Vector3D velocity;
        private Vector3D accel;
        private double h2Cap = 0;
        private double h2Fill = 0;

        // constants
        private const double ALPHA = 0.2;

        public PhysicsContextTransient(GridContext gc, IniContext ic, double timeSinceLastRun)
        {
            pcp = new PhysicsContextLastTick(this);
            this.gc = gc;
            this.ic = ic;
            this.timeSinceLastRun = timeSinceLastRun;
        }


        public double Alt
        {
            get
            {
                gc.Controller.TryGetPlanetElevation(MyPlanetElevation.Surface, out alt);
                return alt;
            }
        }
        public Vector3D NaturalGravity => gc.Controller.GetNaturalGravity();
        public double Gravity => NaturalGravity.Length();
        public double EffectiveAlt => (Alt - gc.GridHeight - VEffectiveYSpeed * timeSinceLastRun) / Gravity / pcp.OldGravity;
        public double StopYDist => Math.Abs((vEffectiveYSpeed * vEffectiveYSpeed) / (2 * maxYDecel));
        public double StopZDist => Math.Abs((vEffectiveZSpeed * vEffectiveZSpeed) / (2 * maxZDecel));
        public Vector3D Velocity => gc.Controller.GetShipVelocities().LinearVelocity;
        public Vector3D Accel => (Velocity - pcp.LastVelocity) / timeSinceLastRun;
        public MyShipMass Mass => gc.Controller.CalculateShipMass();
        public double CruiseSpeed => cruiseSpeed;
        public double ClimbRate => climbRate;
        public double VEffectiveYSpeed => vEffectiveYSpeed;
        public double VEffectiveZSpeed => vEffectiveZSpeed;
        public double MaxYDecel => GetMaxDecel(gc.UpwardThrusters);
        public double MaxZDecel => GetMaxDecel(gc.BreakingThrusters);
        public double OldGravity => oldGravity;
        public double TimeToImpact => timeToImpact;
        public double TimeToStopY => timeToStopY;
        public double TimeToStopZ => timeToStopZ;
        public double Thrust => thrust;
        public double ForwardVelocity => forwardVelocity;
        public double RightVelocity => rightVelocity;
        public double UpVelocity => upVelocity;
        public double NetDecel => netDecel;
        public double MaxThrustUp => maxThrustUp;
        public double DistanceToLine => distanceToLine;
        public Vector3D DesiredUpVector => desiredUpVector;
        public double H2Cap
        {
            get
            {
                CalculateH2CapacityAndPercent();
                return h2Cap;
            }
        }
        public double H2Fill
        {
            get
            {
                CalculateH2CapacityAndPercent();
                return h2Fill;
            }
        }
        public double H2Rate => (h2Fill - pcp.LastH2Fill) / timeSinceLastRun;

        public static double Alpha => ALPHA;

        private void CalculateH2CapacityAndPercent()
        {
            if (h2Cap == 0 || h2Fill == 0)
            {
                foreach (var tank in gc.H2Tanks)
                {
                    h2Cap += tank.Capacity;
                    h2Fill += tank.Capacity * tank.FilledRatio;
                }
            }
        }

        // TODO Add these local vars to PhysicsContext properties
        // TODO Put each of these calculations in the properties get =>
        // TODO Make a method to call in Program for each cicle where instead of pre loading all PhysicsCcontext properties delete all except the "old/last" properties.
        public void MoreUpdatePhysics(GridContext gc, IniContext ic, Command command, int tickCount)
        {
            Mass.

            pcp.LastH2Fill = h2Fill;

            string h2Time = "--";
            if (Math.Abs(h2Rate) > 1e-6)
            {
                if (h2Rate > 0)
                    h2Time = UtilsHelpder.FormatTime((h2Cap - h2Fill) / h2Rate) + " /\\";
                else if (h2Rate < 0)
                    h2Time = UtilsHelpder.FormatTime(h2Fill / -h2Rate) + " \\/";
            }

            gc.H2CapacityPercent = h2Fill / h2Cap * 100;

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
        }

        public void UpdatePhysics(GridContext gc, IniContext ic, Command command, int tickCount)
        {
            GetShipAxisVelocities(gc);

            tickCount++;
            if (tickCount % 10 == 0)
            {
                gravityRatio = gravity / oldGravity;
            }

            var paramSpeed = command.Param.Number;
            cruiseSpeed = ic.MaxSpeed;

            climbRate = GetGravityAlignedVerticalVelocity(gc);
            vEffectiveYSpeed = climbRate + maxYDecel * timeSinceLastRun;
            vEffectiveZSpeed = forwardVelocity + maxZDecel * timeSinceLastRun;

            timeToImpact = Alt / Math.Abs(vEffectiveYSpeed);
            timeToStopY = Math.Abs(climbRate / maxYDecel);
            timeToStopZ = Math.Abs(forwardVelocity / maxZDecel);

            netDecel = ComputeNetDecel(gc);

            //if (b.autoPilotToggle)
            distanceToLine = DistanceToGps(gc.Controller, command.Param.TargetCoordinates);
            

            desiredUpVector = VectorHelper.RotateUpTowardForwardForNoseUp(gc, -0.9 * GetMaxPitchAngle(gc));

            // Velocity & acceleration
            lastVelocity = velocity;
        }

        public void UpdateSmoothedSpeed(double avgSpeed)
        {
            prevSmoothedSpeed = (Alpha * avgSpeed) + ((1.0 - Alpha) * prevSmoothedSpeed);
        }

        // Other helpers you might need
        public void SetLastH2Fill(double fill) => lastH2Fill = fill;
        public double GetLastH2Fill() => lastH2Fill;

        // Reset or initialize as needed
        public void ResetPerTick()
        {
            thrust = 0;
            // keep prevSmoothedSpeed, lastVelocity etc. as they persist between ticks
        }

        public double TimeToDistanceSmoothed(double distance, double dt, SpeedTimeTracker speedTimeTracker)
        {
            speedTimeTracker.AddValue(ForwardVelocity, dt);

            if (dt <= 0) return double.PositiveInfinity;
            double avgSpeed = speedTimeTracker.GetAverageSpeed();

            if (avgSpeed <= 1e-6) avgSpeed = 0.0;

            // EMA smoothing
            prevSmoothedSpeed = (Alpha * avgSpeed) + ((1.0 - Alpha) * prevSmoothedSpeed);

            if (prevSmoothedSpeed <= 1e-6) return double.PositiveInfinity;
            return distance / prevSmoothedSpeed;
        }

        double GetMaxDecel(List<IMyThrust> thrusters)
        {
            thrust = 0;

            Vector3D up = -Vector3D.Normalize(NaturalGravity);

            foreach (var t in thrusters)
            {
                double dot = t.WorldMatrix.Backward.Dot(up);

                if (dot > 0.7)
                    thrust += t.MaxEffectiveThrust * dot;
            }

            return (Thrust / Mass.PhysicalMass) - Gravity;
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
        double GetGravityAlignedVerticalVelocity(GridContext gc)
        {
            Vector3D gNorm = Vector3D.Normalize(NaturalGravity);

            return -gc.Controller.GetShipVelocities()
                .LinearVelocity.Dot(gNorm);
        }

        void GetShipAxisVelocities(GridContext gc)
        {
            Vector3D velocity = gc.Controller.GetShipVelocities().LinearVelocity;
            MatrixD wm = gc.Controller.WorldMatrix;

            forwardVelocity = Vector3D.Dot(velocity, wm.Forward);
            rightVelocity = Vector3D.Dot(velocity, wm.Right);
            upVelocity = Vector3D.Dot(velocity, wm.Up);
        }

        public void MatchVerticalSpeed(GridContext gc, double target)
        {
            double hover = (Mass.PhysicalMass * Gravity) / SumThrust(gc);

            double current = GetGravityAlignedVerticalVelocity(gc);
            double error = target - current;

            double minThrustOverride = (climbRate < 10 ? 0.001 : 0);
            double output = MathHelper.Clamp(hover + error * 0.5, 0.01, 1);

            foreach (var t in gc.UpwardThrusters)
                t.ThrustOverridePercentage = (float)output;
        }

        double SumThrust(GridContext gc)
        {
            double total = 0;

            foreach (var t in gc.UpwardThrusters)
                total += t.MaxEffectiveThrust;

            return total;
        }
        double ComputeNetDecel(GridContext gc)
        {
            maxThrustUp = 0;
            foreach (var t in gc.UpwardThrusters) maxThrustUp += t.MaxEffectiveThrust;

            double thrustAccel = maxThrustUp / mass;

            return thrustAccel - gravity;  // positive = can decelerate
        }

        private double GetMaxPitchAngle(GridContext gc)
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
