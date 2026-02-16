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
        double alt;
        double vSpeed;
        double maxDecel;
        double stopDist;
        double mass;
        double gravity;
        Vector3D naturalGrav;


        //======================================
        //   PLANET AUTO LAND — SAFE VERSION
        //   C#6 Programmable Block
        //======================================

        enum State
        {
            Test,
            Idle,
            Align,
            Descent,
            Cushion,
            Lock
        }

        State state = State.Idle;

        List<IMyGyro> gyros = new List<IMyGyro>();
        List<IMyThrust> upThrusters = new List<IMyThrust>();
        List<IMyLandingGear> gears = new List<IMyLandingGear>();

        IMyShipController ctrl;

        const double HORIZONTAL_LIMIT = 1.2;
        const double CUSHION_ALT = 20;
        const double LOCK_ALT = 5;

        public Program()
        {
            Reload();
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Main(string arg)
        {
            UpdatePhysics();

            double thrust = 0;

            Vector3D up = Vector3D.Normalize(naturalGrav);

            foreach (var t in upThrusters)
            {
                double dot = t.WorldMatrix.Backward.Dot(up);

                if (dot > 0.7)
                    thrust += t.MaxEffectiveThrust * dot;
            }

            maxDecel = (thrust / mass) - gravity;

            stopDist = Math.Abs((vSpeed * vSpeed) / (2 * maxDecel));

            Echo("state: " + state);
            Echo("controller: " + ctrl.CustomName);
            Echo($"alt: {alt:F2}");
            Echo($"stopDist: {stopDist:F2}");
            Echo($"vSpeed: {vSpeed:F2}");
            Echo($"gravity: {gravity:F2}");
            Echo($"maxDecel: {maxDecel:F2}");

            Echo("\ngyros: " + gyros.Count);
            Echo("upThrusters: " + upThrusters.Count);
            Echo("gears: " + gears.Count);

            if (arg == "reload") Reload();
            if (arg == "land") StartLanding();
            if (arg == "abort") Abort();
            if (arg == "test") state = State.Test;

            if (ctrl == null) return;

            switch (state)
            {
                case State.Idle:
                    return;

                case State.Align:
                    if (AlignToGravity()) state = State.Descent;
                    break;

                case State.Descent:
                    Descent();
                    break;

                case State.Lock:
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
            gravity = naturalGrav.Length();
            mass = ctrl.CalculateShipMass().PhysicalMass;
            ctrl.TryGetPlanetElevation(MyPlanetElevation.Surface, out alt);
            vSpeed = -ctrl.GetShipVelocities().LinearVelocity.Dot(Vector3D.Normalize(naturalGrav));
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

            KillGyroOverride();
            KillThrustOverride();
        }

        void StartLanding()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            state = State.Align;
        }

        void Abort()
        {
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

            if (angle < 0.005)
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

        ////////////////////////////////////////////////////////
        /// SAFE DESCENT
        ////////////////////////////////////////////////////////

        void Descent()
        {
            ctrl.DampenersOverride = false;
            AlignToGravity();

            if (alt > Math.Abs(stopDist + CUSHION_ALT))
            {
                MatchVerticalSpeed(-105);
            }
            else if (alt > Math.Abs(stopDist + LOCK_ALT))
            {
                MatchVerticalSpeed(-10);
            }
            else
            {
                ctrl.DampenersOverride = true;
                state = State.Lock;
            }
        }

        void TryLock()
        {
            AlignToGravity();
            MatchVerticalSpeed(-1);

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
            double thrust = 0;

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