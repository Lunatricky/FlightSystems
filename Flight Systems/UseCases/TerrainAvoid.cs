using IngameScript.Domain;
using IngameScript.Enums;
using IngameScript.Physics;
using System;
using VRageMath;

namespace IngameScript.UseCases
{
    class TerrainAvoid
    {
        public static bool Tick(
            GridContext gc,
            IniContext ic,
            PhysicsContext pc,
            Command command,
            Func<bool> level,
            Func<Vector3D, bool> aimHorizon)
        {
            if (pc == null || gc == null || gc.Controller == null || command == null || command.Param == null)
                return false;

            if (pc.Gravity <= 0)
            {
                command.Param.AvoidPhase = TerrainAvoidPhase.Off;
                return false;
            }

            bool modeOk = command.State == MainState.CNav
                || command.State == MainState.Gps
                || command.State == MainState.Orbit;
            if (!modeOk)
            {
                command.Param.AvoidPhase = TerrainAvoidPhase.Off;
                return false;
            }

            if (command.Param.Step == Step.Preclimb)
                return false;

            TerrainAvoidPhase phase = command.Param.AvoidPhase;
            if (phase == TerrainAvoidPhase.Off && !CanEnter(command, pc, ic))
                return false;

            double floor = Math.Max(ic.SafeAltitude * 0.5, 4 * pc.StopYDist + 2 * gc.GridHeight);
            bool closing = pc.ClimbRate < 0 && pc.GroundLevel < ic.SafeAltitude;
            bool danger = pc.GroundLevel < floor || closing;
            bool clear = pc.GroundLevel > ic.SafeAltitude && pc.ClimbRate >= 0;

            if (phase == TerrainAvoidPhase.Off)
            {
                if (!danger)
                    return false;
                EnterBrake(gc, pc, command);
                phase = TerrainAvoidPhase.BrakeClimb;
            }

            if (phase == TerrainAvoidPhase.BrakeClimb)
            {
                if (clear)
                {
                    command.Param.AvoidPhase = TerrainAvoidPhase.Hold;
                    phase = TerrainAvoidPhase.Hold;
                }
                else
                {
                    BrakeClimb(gc, pc, ic, level);
                    return true;
                }
            }

            if (phase == TerrainAvoidPhase.Hold)
            {
                if (!ResumeHeading(gc, command, aimHorizon))
                    return true;
                command.Param.Step = command.Param.AvoidResumeStep;
                command.Param.AvoidPhase = TerrainAvoidPhase.Off;
                return false;
            }

            return false;
        }

        static bool CanEnter(Command command, PhysicsContext pc, IniContext ic)
        {
            Step step = command.Param.Step;
            if (step == Step.Cruise || step == Step.Orbit || step == Step.Climb)
                return true;
            if (step == Step.On && command.State == MainState.CNav && pc.GroundLevel >= ic.SafeAltitude)
                return true;
            return false;
        }

        static void EnterBrake(GridContext gc, PhysicsContext pc, Command command)
        {
            command.Param.AvoidPhase = TerrainAvoidPhase.BrakeClimb;
            command.Param.AvoidResumeStep = command.Param.Step;
            Vector3D gDown = Vector3D.Normalize(pc.NaturalGravity);
            Vector3D heading = VectorHelper.Reject(gc.Controller.WorldMatrix.Forward, gDown);
            if (heading.LengthSquared() > 1e-12)
                heading.Normalize();
            else
                heading = Vector3D.Zero;
            command.Param.AvoidHeading = heading;
        }

        static void BrakeClimb(GridContext gc, PhysicsContext pc, IniContext ic, Func<bool> level)
        {
            gc.ResetThrusters(gc.ForwardThrusters);
            level();
            VectorHelper.MatchVerticalSpeed(gc, pc, ic.MaxSpeed);
        }

        static bool ResumeHeading(GridContext gc, Command command, Func<Vector3D, bool> aimHorizon)
        {
            if (command.State == MainState.Gps)
                return aimHorizon(command.Param.TargetCoordinates);

            Vector3D heading = command.Param.AvoidHeading;
            if (heading.LengthSquared() < 1e-12)
                return true;
            Vector3D target = gc.Controller.GetPosition() + heading * 10000;
            return aimHorizon(target);
        }
    }
}
