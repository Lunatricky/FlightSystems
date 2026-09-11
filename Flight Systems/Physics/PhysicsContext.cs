using IngameScript.Domain;
using IngameScript.Enums;
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

        double timeSinceLastRun = 0.00001;
        double threshold = 0.1;

        MatrixD worldMatrix = new MatrixD();
        MyShipMass mass = new MyShipMass();

        Vector3D naturalGravity = new Vector3D();
        Vector3D velocity = new Vector3D();
        Vector3D accel = new Vector3D();
        Vector3D desiredUpVector = new Vector3D();
        Vector3D planetCenter = new Vector3D();

        H2Totals h2Cache = new H2Totals();
        BatTotals batCache = new BatTotals();

        double groundLevel;
        double seaLevel;
        double gravity;
        double climbRate;
        double stopYDist;
        double stopZDist;
        double maxYDecel;
        double maxZDecel;
        double timeToImpact;
        double timeToStopY;
        double timeToStopZ;
        double timeToDistanceSmoothed;
        double forwardVelocity;
        double rightVelocity;
        double upVelocity;
        double netDecel;
        double distanceToGPS;

        double peakGravity = double.NaN;

        bool isGpsOnPlanet;
        bool isStopped;

        Vector3D prevVelocity = new Vector3D();
        double smoothedSpeed = 0;
        double prevH2Fill = 0;

        public struct H2Totals { public double Capacity; public double Filled; public double Percent; public string Time; public double Rate; }
        public struct BatTotals { public double Capacity; public double Filled; public double Percent; public string Time; public double Rate; }

        const double ALPHA = 0.2;

        public PhysicsContext(GridContext gc, SpeedTimeTracker stt, double timeSinceLastRun)
        {
            this.gc = gc;
            this.stt = stt;

            if (timeSinceLastRun > 0) this.timeSinceLastRun = timeSinceLastRun;
        }

        // Call at start of each Program.Run with Runtime.TimeSinceLastRun.TotalSeconds
        public void NewRun(double timeSinceLastRun, Vector3D targetCoordinates, Command command)
        {
            if (timeSinceLastRun > 0) this.timeSinceLastRun = timeSinceLastRun;

            naturalGravity = gc.Controller.GetNaturalGravity();
            gravity = NaturalGravity.Length();
            mass = gc.Controller.CalculateShipMass();
            worldMatrix = gc.Controller.WorldMatrix;
            velocity = gc.Controller.GetShipVelocities().LinearVelocity;
            accel = ((Velocity - prevVelocity) / timeSinceLastRun);

            forwardVelocity = Vector3D.Dot(Velocity, WorldMatrix.Forward);
            rightVelocity = Vector3D.Dot(Velocity, WorldMatrix.Right);
            upVelocity = Vector3D.Dot(Velocity, WorldMatrix.Up);

            maxZDecel = GetMaxDecel(gc.BreakingThrusters, WorldMatrix.Backward);
            stopZDist = MaxZDecel > 1e-6
                ? Math.Abs(forwardVelocity * forwardVelocity / (2 * MaxZDecel))
                : double.PositiveInfinity;
            //stopZDist = StopZDistTemp < 0.4 ? 0 : StopZDistTemp;
            timeToStopZ = MaxZDecel > 1e-6 ? Math.Abs(ForwardVelocity / MaxZDecel) : double.PositiveInfinity;

            if (Gravity > 0)
            {
                if (double.IsNaN(peakGravity) || Gravity > peakGravity)
                    peakGravity = Gravity;

                desiredUpVector = VectorHelper.PitchUp(gc, NaturalGravity, GetMaxPitchAngle(gc));
                groundLevel = GetPlanetElevation(gc.Controller, MyPlanetElevation.Surface);
                seaLevel = GetPlanetElevation(gc.Controller, MyPlanetElevation.Sealevel);
                climbRate = VectorHelper.GetGravityAlignedVerticalVelocity(gc, this);

                maxYDecel = GetMaxDecel(gc.UpwardThrusters, WorldMatrix.Up);
                stopYDist = MaxYDecel > 1e-6
                    ? Math.Abs(upVelocity * upVelocity / (2 * MaxYDecel))
                    : double.PositiveInfinity;
                //stopYDist = StopYDistTemp < 0.4 ? 0 : StopYDistTemp;

                if (command.State == MainState.Land || command.State == MainState.SBurn)
                    timeToImpact = Math.Abs(UpVelocity) < 0.1 ? 0 : GroundLevel / Math.Abs(UpVelocity);

                timeToStopY = MaxYDecel > 1e-6 ? Math.Abs(ClimbRate / MaxYDecel) : double.PositiveInfinity;

                if (command.State == MainState.Gps)
                {
                    isGpsOnPlanet = GetIsGpsOnPlanet(gc.Controller, targetCoordinates);
                    planetCenter = GetPlanetCenter(gc.Controller);
                }
            }
            else
            {
                peakGravity = double.NaN;
            }

            netDecel = ComputeNetDecel(gc);

            if (command.State == MainState.Gps)
            {
                distanceToGPS = IsGpsOnPlanet
                    ? GetDistanceToPlanetGps(gc.Controller, targetCoordinates)
                    : Vector3D.Distance(targetCoordinates, gc.Controller.GetPosition());
                timeToDistanceSmoothed = GetTimeToDistanceSmoothed(DistanceToGPS, timeSinceLastRun);
            }

            isStopped = threshold > UpVelocity
                && threshold >= Math.Abs(ForwardVelocity)
                && threshold >= Math.Abs(RightVelocity);
            h2Cache = ComputeH2Totals();
            batCache = ComputeBatTotals();
        }


        public void CacheValues()
        {
            prevVelocity = Velocity;
            prevH2Fill = H2Cache.Filled;
        }

        public Vector3D PrevVelocity => prevVelocity;
        MatrixD WorldMatrix => worldMatrix;
        public MyShipMass Mass => mass;
        public Vector3D NaturalGravity => naturalGravity;
        Vector3D Velocity => velocity;
        public Vector3D Accel => accel;
        public Vector3D DesiredUpVector => desiredUpVector;
        public Vector3D PlanetCenter => planetCenter;
        public double Gravity => gravity;
        public double GroundLevel => groundLevel;
        public double SeaLevel => seaLevel;
        public string GroundLevelStr => groundLevel > 1000 ? $"{groundLevel / 1000:F1} km" : $"{groundLevel:F1} m";
        public string SeaLevelStr => seaLevel > 1000 ? $"{seaLevel / 1000:F1} km" : $"{seaLevel:F1} m";
        public double StopYDist => stopYDist;
        public double StopZDist => stopZDist;
        public double ClimbRate => climbRate;
        public double MaxYDecel => maxYDecel;
        double MaxZDecel => maxZDecel;
        public double TimeToImpact => timeToImpact;
        public double TimeToStopY => timeToStopY;
        public double TimeToStopZ => timeToStopZ;
        public double TimeToDistanceSmoothed => timeToDistanceSmoothed;
        public double ForwardVelocity => forwardVelocity;
        public double RightVelocity => rightVelocity;
        public double UpVelocity => upVelocity;
        public double NetDecel => netDecel;
        public double DistanceToGPS => distanceToGPS;
        public bool IsGpsOnPlanet => isGpsOnPlanet;
        public bool IsStopped => isStopped;
        public H2Totals H2Cache => h2Cache;
        public BatTotals BatCache => batCache;

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
                time = UtilsHelpder.FormatFillEta(filled, cap, rate);

            return new H2Totals { Capacity = cap, Filled = filled, Percent = percent, Time = time, Rate = rate };
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

            double rate = batIn - batOut;
            string batTime = "--";

            if (Math.Abs(rate) > 0.01)
                batTime = UtilsHelpder.FormatFillEta(filled, cap, rate / 3600);

            return new BatTotals { Capacity = cap, Filled = filled, Percent = percent, Time = batTime, Rate = rate };
        }

        double GetPlanetElevation(IMyShipController controller, MyPlanetElevation elevation)
        {
            double alt;
            controller.TryGetPlanetElevation(elevation, out alt);
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

        double GetMaxDecel(List<IMyThrust> thrusters, Vector3D decelDir)
        {
            if (Mass.PhysicalMass <= 1e-6)
                return 0;

            double thrust = GridContext.SumEffectiveThrust(thrusters);

            double thrustAccel = thrust / Mass.PhysicalMass;

            if (Gravity <= 0 || decelDir.LengthSquared() < 1e-12)
                return thrustAccel;

            Vector3D dir = Vector3D.Normalize(decelDir);
            Vector3D gDir = Vector3D.Normalize(NaturalGravity);
            // +dot: gravity helps stop along this axis; -dot: gravity fights (up axis)
            double gravityAlongDecel = Vector3D.Dot(gDir, dir) * Gravity;

            return thrustAccel + gravityAlongDecel;
        }


        Vector3D GetPlanetCenter(IMyShipController controller)
        {
            Vector3D planetCenter;
            controller.TryGetPlanetPosition(out planetCenter);

            return planetCenter;
        }


        bool GetIsGpsOnPlanet(IMyShipController controller, Vector3D gps)
        {
            double sealevel;
            controller.TryGetPlanetElevation(MyPlanetElevation.Sealevel, out sealevel);

            return (planetCenter - gps).Length() > 2 * sealevel;
        }

        double GetDistanceToPlanetGps(IMyShipController controller, Vector3D gps)
        {
            Vector3D shipPos = controller.GetPosition();

            // Assume planet center is at origin
            Vector3D planetCenter;
            controller.TryGetPlanetPosition(out planetCenter);

            double planetRadius = (planetCenter - shipPos).Length();  // distance from ship to planet center

            // Normalize positions to unit sphere
            Vector3D shipDir = Vector3D.Normalize(shipPos - planetCenter);
            Vector3D gpsDir = Vector3D.Normalize(gps - planetCenter);

            // Angular distance between ship and GPS on the sphere (in radians)
            double cosAngle = Vector3D.Dot(shipDir, gpsDir);
            cosAngle = Math.Max(-1.0, Math.Min(1.0, cosAngle));
            double angle = Math.Acos(cosAngle);

            // Arc distance along planet surface
            double arcDistance = planetRadius * angle;

            return arcDistance;
        }

        double ComputeNetDecel(GridContext gc)
        {
            double maxThrustUp = GridContext.SumEffectiveThrust(gc.UpwardThrusters);

            double thrustAccel = maxThrustUp / Mass.TotalMass;

            return thrustAccel - Gravity;  // positive = can decelerate
        }

        double GetMaxPitchAngle(GridContext gc)
        {
            double upThrust = GridContext.SumEffectiveThrust(gc.UpwardThrusters);
            double fwdThrust = GridContext.SumEffectiveThrust(gc.ForwardThrusters);

            double massKg = gc.Controller.CalculateShipMass().PhysicalMass;
            double g = double.IsNaN(peakGravity) ? Gravity : peakGravity;
            double weight = massKg * g;

            if (upThrust <= 1e-6 || weight <= 0)
                return 0;

            // spare lift for dampeners + effective-thrust lag (atmo)
            const double UpReserve = 0.90;   // use at most 70% of up T for hover
            const double FwdReserve = 0.80;  // keep half of forward for speed

            double maxFromLift = 0;
            double liftBudget = upThrust * UpReserve;
            if (liftBudget > weight)
                maxFromLift = MathHelper.ToDegrees(Math.Acos(MathHelper.Clamp(weight / liftBudget, 0, 1)));

            double maxFromAft = 0;
            double aftBudget = fwdThrust * FwdReserve;
            if (aftBudget > 0)
                maxFromAft = MathHelper.ToDegrees(Math.Atan(aftBudget / weight));

            return Math.Min(maxFromLift, maxFromAft);
        }

        public void UnlockClimbPitch()
        {
            peakGravity = double.NaN;
        }
    }
}
