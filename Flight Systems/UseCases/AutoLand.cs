using IngameScript.Domain;
using IngameScript.Physics;
using IngameScript.Utils;
using System;
using VRageMath;

namespace IngameScript.UseCases
{
    class AutoLand
    {
        readonly GridContext gc;
        readonly GridManager gm;
        readonly PhysicsContext pc;
        Command command;
        int tc;

        public AutoLand(GridContext gc, GridManager gm, PhysicsContext pc, Command command, int tickCount)
        {
            this.gc = gc;
            this.gm = gm;
            this.pc = pc;
            this.command = command;
            tc = tickCount;
        }

        public void AutoLandStateSwitch()
        {
            switch (command.Param.AutoLandState)
            {
                case AutoLandState.Idle:
                    break;

                case AutoLandState.Align:
                    gm.SoftAbort();
                    if (VectorHelper.AlignToGravity(gc, pc, true)) command.Param.AutoLandState = AutoLandState.Drop;
                    break;

                case AutoLandState.Drop:
                    if (AutoLandCalc(gc)) command.Param.AutoLandState = AutoLandState.LockGear;
                    break;

                case AutoLandState.LockGear:
                    if (TryLock(gc)) gm.AbortShipContext(ref command, ref tc);
                    break;
            }
        }

        public void SuicideBurnStateSwitch()
        {
            switch (command.Param.AutoLandState)
            {
                case AutoLandState.Idle:
                    break;

                case AutoLandState.Align:
                    gm.SoftAbort();
                    if (VectorHelper.AlignToGravity(gc, pc, true)) command.Param.AutoLandState = AutoLandState.Drop;
                    break;

                case AutoLandState.Drop:
                    if (SuicideBurn(gc)) command.Param.AutoLandState = AutoLandState.LockGear;
                    break;

                case AutoLandState.LockGear:
                    if (TryLock(gc)) gm.AbortShipContext(ref command, ref tc);
                    break;
            }
        }

        bool SuicideBurn(GridContext gc)
        {
            if (pc.NetDecel - 1 < 0)
            {
                gm.AbortShipContext(ref command, ref tc);
                command.State = MainState.Cruise;
                command.Param.Text = "orbit";
            }

            gc.Controller.DampenersOverride = false;
            VectorHelper.AlignToGravity(gc, pc);
            VectorHelper.MatchVerticalSpeed(gc, pc, -104);
            return pc.EffectiveAlt < 1.1 * pc.StopYDist + gc.GridHeight;
        }

        bool AutoLandCalc(GridContext gc)
        {
            if (pc.NetDecel - 0.5 < 0)
            {
                gm.AbortShipContext(ref command, ref tc);
                command.State = MainState.Cruise;
                command.Param.Text = "orbit";
            }

            gc.Controller.DampenersOverride = false;
            VectorHelper.AlignToGravity(gc, pc);

            double speedFromAlt = (100 + pc.GroundLevel) * 0.08;
            double speedFromAccel = 20 * pc.NetDecel;
            double speedMin = -Math.Min(speedFromAlt, speedFromAccel);

            if (speedMin > -104) VectorHelper.MatchVerticalSpeed(gc, pc, speedMin);
            return pc.EffectiveAlt < 10 + 2 * gc.GridHeight;
        }

        bool TryLock(GridContext gc)
        {
            VectorHelper.AlignToGravity(gc, pc);
            VectorHelper.MatchVerticalSpeed(gc, pc, -2);
            gc.Controller.DampenersOverride = true;

            foreach (var g in gc.Gears)
                g.Lock();

            return gc.Gears.Exists(g => g.IsLocked);
        }
    }
}
