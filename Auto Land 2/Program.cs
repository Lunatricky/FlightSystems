using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        int tickCount;

        // Descent()
        double alt;
        double effectiveAlt;
        double stopDist;
        double mass;
        double vSpeed;
        double vEffectiveSpeed;
        double targetSpeed;
        double maxDecel;
        double gravity;
        double oldGravity;
        double gravityRatio = 1;
        Vector3D naturalGrav;
        double timeToImpact;
        double timeToStop;


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
            KillHorizontal,
            Descent,
            Cushion,
            Lock
        }

        State state = State.Idle;

        List<IMyGyro> gyros = new List<IMyGyro>();
        List<IMyThrust> upThrusters = new List<IMyThrust>();
        List<IMyLandingGear> gears = new List<IMyLandingGear>();

        IMyShipController ctrl;

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

            Echo("state: " + state);
            Echo("controller: " + ctrl.CustomName);
            Echo($"alt: {alt:F2}");
            Echo($"effectiveAlt: {effectiveAlt:F2}");
            Echo($"stopDist: {stopDist:F2}");
            Echo($"maxDecel: {maxDecel:F2}");
            tickCount++;
            if (tickCount % 10 == 0)
            {
                gravityRatio = gravity / oldGravity;
                oldGravity = gravity;
            }

            Echo($"vSpeed: {vSpeed:F2}");
            Echo($"targetSpeed: {targetSpeed:F2}");
            Echo($"timeToImpact: {timeToImpact:F2}");
            Echo($"timeToStop: {timeToStop:F2}");
            Echo($"gridHight: {gridHight:F2}");

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
            maxDecel = GetMaxDecel();

            mass = ctrl.CalculateShipMass().PhysicalMass;

            ctrl.TryGetPlanetElevation(MyPlanetElevation.Surface, out alt);

            vSpeed = GetVerticalSpeed();
            vEffectiveSpeed = vSpeed + maxDecel * Runtime.TimeSinceLastRun.TotalSeconds;

            timeToImpact = alt / Math.Abs(vEffectiveSpeed);
            timeToStop = SAFETY * Math.Abs(vEffectiveSpeed) / maxDecel;

            effectiveAlt = alt - vEffectiveSpeed * Runtime.TimeSinceLastRun.TotalSeconds - gridHight;
            effectiveAlt = effectiveAlt / gravityRatio;
            stopDist = Math.Abs((vEffectiveSpeed * vEffectiveSpeed) / (2 * maxDecel));


            targetSpeed = -MathHelper.Clamp(alt * 0.05, 5, 110);
        }

        void Reload()
        {
            tickCount = 0;
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

            if (angle < 0.01)
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
            if (maxDecel <= 0)
            {
                Echo("INSUFFICIENT LIFT!");
                MatchVerticalSpeed(0); // full burn
                return;
            }

            AlignToGravity();
            ctrl.DampenersOverride = false;

            MatchVerticalSpeed(-110);

            if (effectiveAlt < Math.Abs(stopDist + gridHight + LOCK_ALT))
            {
                ctrl.DampenersOverride = true;
                state = State.Lock;
            }
            else if (effectiveAlt < 2 * Math.Abs(stopDist + CUSHION_ALT))
            {
                MatchVerticalSpeed(targetSpeed);
            }
        }
        



        void TryLock()
        {
            AlignToGravity();
            MatchVerticalSpeed(-2);

            foreach (var g in gears)
                g.Lock();

            if (gears.Exists(g => g.IsLocked))
            {
                Abort(); // finished
                tickCount = 0;
            }
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
