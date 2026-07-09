using IngameScript.Domain;
using IngameScript.Utils;
using Sandbox.ModAPI.Ingame;
using System;
using VRageMath;

namespace IngameScript.Physics
{
    class PhysicsContext
    {
        GridContext gc;
        SpeedTimeTracker stt;

        double accumulatedTime = 0;
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
        double effectiveAlt;
        double gravity;
        double climbRate;
        double vEffectiveYSpeed;
        double vEffectiveZSpeed;
        double stopYDist;
        double stopZDist;
        double maxYDecel;
        double maxZDecel;
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
        double distanceToGPS;
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
        public void NewRun(double timeSinceLastRun, Vector3D targetCoordinates)
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
            groundLevel = GetPlanetElevation(gc.Controller, MyPlanetElevation.Surface);
            seaLevel = GetPlanetElevation(gc.Controller, MyPlanetElevation.Sealevel);
            climbRate = VectorHelper.GetGravityAlignedVerticalVelocity(gc, this);

            forwardVelocity = Vector3D.Dot(Velocity, WorldMatrix.Forward);
            rightVelocity = Vector3D.Dot(Velocity, WorldMatrix.Right);
            upVelocity = Vector3D.Dot(Velocity, WorldMatrix.Up);

            maxYDecel = GetMaxDecel(gc.Controller.WorldMatrix.Down);
            maxZDecel = GetMaxDecel(gc.Controller.WorldMatrix.Backward);

            vEffectiveYSpeed = UpVelocity == 0 ? 0 : ClimbRate + MaxYDecel * timeSinceLastRun;
            vEffectiveZSpeed = ForwardVelocity == 0 ? 0 : ForwardVelocity + MaxZDecel * timeSinceLastRun;

            effectiveAlt = (GroundLevel - gc.GridHeight - vEffectiveYSpeed * timeSinceLastRun);

            stopYDistTemp = Math.Abs(VEffectiveYSpeed * VEffectiveYSpeed / (2 * MaxYDecel));
            stopZDistTemp = Math.Abs(VEffectiveZSpeed * VEffectiveZSpeed / (2 * MaxZDecel));

            stopYDist = StopYDistTemp < 0.4 ? 0 : StopYDistTemp;
            stopZDist = StopZDistTemp < 0.4 ? 0 : StopZDistTemp;

            timeToImpact = Math.Abs(VEffectiveYSpeed) < 0.1 ? 0 : GroundLevel / Math.Abs(VEffectiveYSpeed);
            timeToStopY = Math.Abs(ClimbRate / MaxYDecel);
            timeToStopZ = Math.Abs(ForwardVelocity / MaxZDecel);
            netDecel = ComputeNetDecel(gc);

            planetCenter = GetPlanetCenter(gc.Controller);
            isGpsOnPlanet = GetIsGpsOnPlanet(gc.Controller, targetCoordinates);
            distanceToGPS = IsGpsOnPlanet ? GetDistanceToPlanetGps(gc.Controller, targetCoordinates) : Vector3D.Distance(targetCoordinates, gc.Controller.GetPosition());
            timeToDistanceSmoothed = GetTimeToDistanceSmoothed(DistanceToGPS, timeSinceLastRun);
            isStopped = threshold > UpVelocity && threshold >= Math.Abs(ForwardVelocity) && threshold >= Math.Abs(RightVelocity);
            h2Cache = ComputeH2Totals();
            batCache = ComputeBatTotals();
        }
        

        public void CacheValues()
        {
            prevVelocity = Velocity;
            prevH2Fill = H2Cache.Filled;
        }

        public double Now => accumulatedTime;

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
        public double EffectiveAlt => effectiveAlt;
        double VEffectiveYSpeed => vEffectiveYSpeed; 
        double VEffectiveZSpeed => vEffectiveZSpeed;
        double StopYDistTemp => stopYDistTemp;
        double StopZDistTemp => stopZDistTemp;
        public double StopYDist => stopYDist;
        public double StopZDist => stopZDist;
        public double ClimbRate => climbRate;
        double MaxYDecel => maxYDecel;
        public double MaxZDecel => maxZDecel;
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
            {
                if (rate > 0)
                    time = UtilsHelpder.FormatTime((cap - filled) / rate) + " /\\";
                else if (rate < 0)
                    time = UtilsHelpder.FormatTime(filled / -rate) + " \\/";
            }

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
            {
                if (rate > 0)
                    batTime = UtilsHelpder.FormatTime(3600 * (cap - filled) / rate) + " /\\";
                else if (rate < 0)
                    batTime = UtilsHelpder.FormatTime(3600 * filled / -rate) + " \\/";
            }

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

        double GetMaxDecel(Vector3D direction)
        {
            double thrust = 0;

            foreach (var t in gc.Thrusters)
            {
                double dot = Vector3D.Dot(t.WorldMatrix.Backward, Vector3D.Normalize(direction));

                thrust += Math.Max(0, dot) * t.MaxEffectiveThrust;
            }

            double gravityComponent = gravity > 0 ? - Vector3D.Dot(Vector3D.Normalize(NaturalGravity), direction) * NaturalGravity.Length() : 0;

            return (thrust / Mass.PhysicalMass) - gravityComponent;
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
