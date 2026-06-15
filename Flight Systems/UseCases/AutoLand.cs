using IngameScript.Domain;
using IngameScript.Physics;
using IngameScript.Utils;
using System;

namespace IngameScript.UseCases
{
    public class AutoLand
    {
        GridManager gm;
        PhysicsContext pc;
        Command command;
        Booleans b;
        int tc;

        public AutoLand(GridManager gm, PhysicsContext pc, Command command, Booleans b, int tickCount)
        {
            this.gm = gm;
            this.pc = pc;
            this.command = command;
            this.b = b;
            tc = tickCount;
        }

        public void AutoLandStateSwitch()
        {
            switch (command.Param.AutoLandState)
            {
                case AutoLandState.Idle:
                    break;

                case AutoLandState.Align:
                    gm.SoftAbort(b);
                    if (VectorHelper.AlignToGravity(gm, pc, true)) command.Param.AutoLandState = AutoLandState.Drop;
                    break;

                case AutoLandState.Drop:
                    if (AutoLandCalc(gm)) command.Param.AutoLandState = AutoLandState.LockGear;
                    break;

                case AutoLandState.LockGear:
                    if (TryLock(gm)) gm.AbortShipContext(command, b, ref tc);
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
                    gm.SoftAbort(b);
                    if (VectorHelper.AlignToGravity(gm, pc, true)) command.Param.AutoLandState = AutoLandState.Drop;
                    break;

                case AutoLandState.Drop:
                    if (SuicideBurn(gm)) command.Param.AutoLandState = AutoLandState.LockGear;
                    break;

                case AutoLandState.LockGear:
                    if (TryLock(gm)) gm.AbortShipContext( command, b,ref tc);
                    break;
            }
        }

        bool SuicideBurn(GridManager gm)
        {
            if (pc.NetDecel - 1 < 0)
            {
                this.gm.AbortShipContext(command, b, ref tc);
                command.State = MainState.Cruise;
                command.Param.Text = "orbit";
            }

            gm.Controller.DampenersOverride = false;
            VectorHelper.AlignToGravity(gm, pc);
            VectorHelper.MatchVerticalSpeed(gm, pc, -104);
            return pc.EffectiveAlt < 1.1 * pc.StopYDist + gm.GridHeight;
        }

        bool AutoLandCalc(GridManager gm)
        {
            if (pc.NetDecel - 0.5 < 0)
            {
                this.gm.AbortShipContext(command, b, ref tc);
                command.State = MainState.Cruise;
                command.Param.Text = "orbit";
            }

            gm.Controller.DampenersOverride = false;
            VectorHelper.AlignToGravity(gm, pc);

            double speedFromAlt = (100 + pc.GroundLevel) * 0.08;
            double speedFromAccel = 20 * pc.NetDecel;
            double speedMin = -Math.Min(speedFromAlt, speedFromAccel);

            if (speedMin > -104) VectorHelper.MatchVerticalSpeed(gm, pc, speedMin);
            return pc.EffectiveAlt < 10 + 2 * gm.GridHeight;
        }

        bool TryLock(GridManager gm)
        {
            VectorHelper.AlignToGravity(gm, pc);
            VectorHelper.MatchVerticalSpeed(gm, pc, -2);
            gm.Controller.DampenersOverride = true;

            foreach (var g in gm.Gears)
                g.Lock();

            return gm.Gears.Exists(g => g.IsLocked);
        }
    }
}
