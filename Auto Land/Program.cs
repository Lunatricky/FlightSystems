using System;
using System.Collections.Generic;
using System.Text;
using System.Linq; // Limited LINQ ok in PB
using VRageMath;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        /*
         * R e a d m e
         * -----------
         * 
         * In this file you can include any instructions or other comments you want to have injected onto the 
         * top of your final script. You can safely delete this file if you do not want any such comments.
         */

        // Descent()
        int tickCount;
        double alt;
        double effectiveAlt;
        double stopDist;
        double mass;
        double vSpeed;
        double vEffectiveSpeed;
        double maxDecel;
        double gravity;
        double oldGravity;
        double gravityRatio = 1;
        Vector3D naturalGrav;
        double timeToImpact;
        double timeToStop;

        double thrust = 0;

        double ctrlGridHight;
        double gearGridHight;
        double gridHight;

        // safety buffer (VERY important)
        const double SAFETY = 1.2;


        //======================================
        //   PLANET AUTO LAND — SAFE VERSION
        //   C#6 Programmable Block
        //======================================

        enum State
        {
            Test,
            Idle,
            Align,
            SuicideBurn,
            Cushion,
            LockGear
        }

        State state = State.Idle;

        List<IMyGyro> gyros = new List<IMyGyro>();
        List<IMyThrust> upThrusters = new List<IMyThrust>();
        List<IMyLandingGear> gears = new List<IMyLandingGear>();

        IMyShipController ctrl;

        public Program()
        {
            Reload();
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Main(string arg)
        {
            UpdatePhysics();


            Echo("state: " + state);
            Echo("controller: " + ctrl.CustomName);
            Echo($"alt: {alt:F2}");
            Echo($"stopDist: {stopDist:F2}");
            Echo($"gravity: {gravity:F2}");
            Echo($"maxDecel: {maxDecel:F2}");
            Echo($"mass: {mass:F2}");
            Echo($"thrust: {thrust:F2}");
            Echo($"vSpeed: {vSpeed:F2}");
            Echo($"timeToImpact: {timeToImpact:F2}");
            Echo($"timeToStop: {timeToStop:F2}");
            Echo($"gridHight: {gridHight:F2}");

            tickCount++;
            if (tickCount % 10 == 0)
            {
                gravityRatio = gravity / oldGravity;
                oldGravity = gravity;
            }

            Echo("\ngyros: " + gyros.Count);
            Echo("upThrusters: " + upThrusters.Count);
            Echo("gears: " + gears.Count);

            if (state == State.SuicideBurn && maxDecel < 1) Abort();
            if (arg == "reload") Reload();
            if (arg == "sburn") StartSuicideBurn();
            if (arg == "abort") Abort();
            if (arg == "test") state = State.Test;

            if (ctrl == null) return;

            switch (state)
            {
                case State.Idle:
                    return;

                case State.Align:
                    if (AlignToGravity()) state = State.SuicideBurn;
                    break;

                case State.SuicideBurn:
                    SuicideBurn();
                    break;

                case State.Cushion:
                    Cushion();
                    break;

                case State.LockGear:
                    TryLock();
                    break;

                case State.Test:
                    MatchVerticalSpeed(-0.5);
                    return;
            }
        }

        ////////////////////////////////////////////////////////
        /// SETUP
        ////////////////////////////////////////////////////////

        void UpdatePhysics()
        {
            naturalGrav = ctrl.GetNaturalGravity();
            mass = ctrl.CalculateShipMass().PhysicalMass;
            gravity = naturalGrav.Length();
            maxDecel = GetMaxDecel();


            ctrl.TryGetPlanetElevation(MyPlanetElevation.Surface, out alt);

            vSpeed = GetVerticalSpeed();
            vEffectiveSpeed = vSpeed + maxDecel * Runtime.TimeSinceLastRun.TotalSeconds;

            stopDist = Math.Abs((vEffectiveSpeed * vEffectiveSpeed) / (2 * maxDecel));

            effectiveAlt = alt - vEffectiveSpeed * Runtime.TimeSinceLastRun.TotalSeconds - gridHight;
            effectiveAlt = effectiveAlt / gravityRatio;

            timeToImpact = alt / Math.Abs(vEffectiveSpeed);
            timeToStop = Math.Abs(vSpeed) / maxDecel;
        }

        void Reload()
        {
            gyros.Clear();
            upThrusters.Clear();
            gears.Clear();

            var group = GridTerminalSystem.GetBlockGroupWithName("AUTO LAND");

            if (group == null)
                throw new Exception("Missing group AUTO LAND");

            group.GetBlocksOfType(gyros);
            group.GetBlocksOfType(gears);

            var thrusters = new List<IMyThrust>();
            group.GetBlocksOfType(thrusters);

            foreach (var t in thrusters) upThrusters.Add(t);

            var ctrls = new List<IMyShipController>();
            group.GetBlocksOfType(ctrls);

            ctrl = ctrls.Find(c => c.IsMainCockpit) ?? ctrls[0];

            Vector3D gravityDir = Vector3D.Normalize(ctrl.GetNaturalGravity());

            // world positions
            Vector3D ctrlPos = ctrl.GetPosition();
            Vector3D gearPos = gears[0].GetPosition();

            // project onto gravity vector
            ctrlGridHight = ctrlPos.Dot(gravityDir);
            gearGridHight = gearPos.Dot(gravityDir);

            // height difference along gravity
            gridHight = Math.Abs(ctrlGridHight - gearGridHight);

            KillGyroOverride();
            KillThrustOverride();
        }

        void StartSuicideBurn()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            state = State.Align;
        }

        void Abort()
        {
            tickCount = 0;
            KillGyroOverride();
            KillThrustOverride();

            ctrl.DampenersOverride = true;

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            state = State.Idle;
        }

        private void KillGyroOverride()
        {
            foreach (var g in gyros)
                g.GyroOverride = false;
        }

        private void KillThrustOverride()
        {

            foreach (var t in upThrusters)
                t.ThrustOverridePercentage = 0;
        }

        ////////////////////////////////////////////////////////
        /// FLIGHT
        ////////////////////////////////////////////////////////

        bool AlignToGravity()
        {
            if (naturalGrav.LengthSquared() < 0.01)
                return false;

            Vector3D desiredUp = Vector3D.Normalize(naturalGrav);
            Vector3D shipUp = ctrl.WorldMatrix.Up;

            Vector3D axis = shipUp.Cross(desiredUp);
            double angle = axis.Length();

            if (angle < 0.01 && !HasSpeed())
            {
                foreach (var g in gyros)
                    g.GyroOverride = false;

                return true;
            }

            axis /= angle;

            Vector3D angVel = ctrl.GetShipVelocities().AngularVelocity;

            //-----------------------------------
            // ⭐ ANGULAR RATE LIMIT
            //-----------------------------------

            const double MAX_ROT_RATE = 0.6; // radians/sec
            const double RESPONSE = 3.0;     // lower = smoother

            Vector3D desiredRate = axis * Math.Min(angle * RESPONSE, MAX_ROT_RATE);

            //-----------------------------------
            // PD controller on angular velocity
            //-----------------------------------

            Vector3D correction = desiredRate - angVel;

            //-----------------------------------

            foreach (var g in gyros)
            {
                MatrixD inv = MatrixD.Transpose(g.WorldMatrix);
                Vector3D local = Vector3D.TransformNormal(correction, inv);

                g.GyroOverride = true;

                g.Pitch = (float)MathHelper.Clamp(local.X / 2, -3, 3);
                g.Yaw = (float)MathHelper.Clamp(local.Y / 2, -3, 3);
                g.Roll = (float)MathHelper.Clamp(local.Z / 2, -3, 3);
            }

            return false;
        }

        /// <summary>
        /// Aligns ship's Up mostly to anti-gravity, with optional forward/down pitch tilt for glide.
        /// Returns true when alignment is good enough.
        /// </summary>
        /// <param name="tiltAngleDeg">Desired pitch-down angle in degrees (0 = perfect vertical, 5–12 typical for glide)</param>
        /// <param name="toleranceDeg">When angle error is below this → consider aligned (default 1.5°)</param>
        bool AlignWithGlideTilt(double tiltAngleDeg = 0, double toleranceDeg = 1.5)
        {
            if (naturalGrav.LengthSquared() < 0.01)
                return false;

            Vector3D gravNorm = Vector3D.Normalize(naturalGrav);          // points down
            Vector3D desiredUp = -gravNorm;                                // ship up should oppose gravity

            // Add forward/down tilt for glide (positive tiltAngleDeg = nose down)
            if (Math.Abs(tiltAngleDeg) > 0.01)
            {
                Vector3D shipForward = ctrl.WorldMatrix.Forward;
                // Rotate desired up vector around local right axis (pitch down)
                MatrixD pitchRot = MatrixD.CreateFromAxisAngle(ctrl.WorldMatrix.Right, MathHelper.ToRadians(tiltAngleDeg));
                desiredUp = Vector3D.TransformNormal(desiredUp, pitchRot);
                desiredUp = Vector3D.Normalize(desiredUp);
            }

            Vector3D shipUp = ctrl.WorldMatrix.Up;
            Vector3D axis = Vector3D.Cross(shipUp, desiredUp);
            double angleRad = axis.Length();

            // Early exit if already well aligned and not moving much
            if (angleRad < MathHelper.ToRadians(toleranceDeg) && !HasSpeed())
            {
                foreach (var g in gyros) g.GyroOverride = false;
                return true;
            }

            if (angleRad > 1e-6) axis /= angleRad;   // normalize rotation axis

            Vector3D angVel = ctrl.GetShipVelocities().AngularVelocity;

            // ────────────────────────────────────────────────
            // PD + rate limit (your existing logic – very good)
            // ────────────────────────────────────────────────
            const double MAX_ROT_RATE = 0.6;     // rad/s
            const double RESPONSE = 3.0;     // lower = smoother

            Vector3D desiredRate = axis * Math.Min(angleRad * RESPONSE, MAX_ROT_RATE);
            Vector3D correction = desiredRate - angVel;

            foreach (var g in gyros)
            {
                if (!g.IsFunctional) continue;

                // Local correction in gyro frame
                MatrixD gyroToLocal = MatrixD.Transpose(g.WorldMatrix);
                Vector3D localCorr = Vector3D.TransformNormal(correction, gyroToLocal);

                g.GyroOverride = true;
                g.Pitch = (float)MathHelper.Clamp(localCorr.X / 2, -3, 3);
                g.Yaw = (float)MathHelper.Clamp(localCorr.Y / 2, -3, 3);
                g.Roll = (float)MathHelper.Clamp(localCorr.Z / 2, -3, 3);
            }

            return false;
        }

        bool HasSpeed(double threshold = 1)
        {
            return ctrl.GetShipVelocities().LinearVelocity.LengthSquared() >= threshold;
        }

        ////////////////////////////////////////////////////////
        /// SAFE DESCENT
        ////////////////////////////////////////////////////////

        void SuicideBurn()
        {
            if (!AlignToGravity()) return;
            ctrl.DampenersOverride = false;
            if (effectiveAlt < SAFETY * (Math.Abs(stopDist) + 2 * gridHight))
            {
                state = State.Cushion;
            }
        }

        void Cushion()
        {
            MatchVerticalSpeed(-10);

            if (effectiveAlt < 0)
            {
                state = State.LockGear;
            }
        }

        void TryLock()
        {
            AlignToGravity();
            MatchVerticalSpeed(-3);
            ctrl.DampenersOverride = true;

            foreach (var g in gears)
                g.Lock();

            if (gears.Exists(g => g.IsLocked))
                Abort(); // finished
        }

        ////////////////////////////////////////////////////////
        /// PHYSICS HELPERS
        ////////////////////////////////////////////////////////

        double GetVerticalSpeed()
        {
            Vector3D gNorm = Vector3D.Normalize(naturalGrav);

            return -ctrl.GetShipVelocities()
                .LinearVelocity.Dot(gNorm);
        }

        double GetMaxDecel()
        {
            thrust = 0;

            Vector3D up = -Vector3D.Normalize(naturalGrav);

            foreach (var t in upThrusters)
            {
                double dot = t.WorldMatrix.Backward.Dot(up);

                if (dot > 0.7)
                    thrust += t.MaxEffectiveThrust * dot;
            }

            return (thrust / mass) - gravity;
        }

        void MatchVerticalSpeed(double target)
        {
            double hover = (mass * gravity) / SumThrust();

            double current = GetVerticalSpeed();
            double error = target - current;

            double output = MathHelper.Clamp(hover + error * 0.5, 0, 1);

            foreach (var t in upThrusters)
                t.ThrustOverridePercentage = (float)output;
        }

        double SumThrust()
        {
            double total = 0;

            foreach (var t in upThrusters)
                total += t.MaxEffectiveThrust;

            return total;
        }
    }
}