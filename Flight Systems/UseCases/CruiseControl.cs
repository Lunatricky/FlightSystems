using IngameScript.Domain;
using IngameScript.Physics;
using IngameScript.Utils;
using System;
using VRageMath;

namespace IngameScript.UseCases
{
    class CruiseControl
    {
        readonly GridContext gc;
        readonly GridManager gm;
        readonly IniContext ic;
        readonly PhysicsContext pc;
        Booleans b;
        Command command;
        readonly double tslr;
        int tc;

        Vector3D desiredUp;

        public CruiseControl(GridContext gc, GridManager gm, IniContext ic, PhysicsContext pc, Booleans b, Command command, double timeSinceLastRun, int tickCount)
        {
            this.gc = gc;
            this.gm = gm;
            this.ic = ic;
            this.pc = pc;
            this.b = b;
            this.command = command;
            tslr = timeSinceLastRun;
            tc = tickCount;
        }

        public void CruiseControlStateSwitch()
        {
            double CruiseSpeed = (command.Param.Number > 0 ? command.Param.Number : ic.MaxSpeed);

            switch (command.Param.Text.ToLowerInvariant())
            {
                case "toggle":
                case "":
                    b.cruiseToggle = !b.cruiseToggle;
                    if (b.cruiseToggle) command.Param.Text = "on";
                    else command.Param.Text = "off";
                    break;
                case "on":
                    CruiseControlCalc(CruiseSpeed, tslr);
                    break;
                case "off":
                    gm.AbortShipContext(ref command, ref tc);
                    break;
                case "orbit":
                    b.cruiseToggle = !b.cruiseToggle;
                    if (b.cruiseToggle)
                    {
                        command.Param.Text = "align";
                        b.stopCruiseWhenOutOfGrav = true;
                        CruiseControlCalc(CruiseSpeed, tslr);
                    }
                    else
                    {
                        if (b.autoPilotToggle)
                        {
                            command.State = MainState.Gps;
                            command.Param.Text = "on";
                        }
                        else gm.AbortShipContext(ref command, ref tc);
                    }
                    break;
                case "align":
                    if (VectorHelper.AlignToGravity(gc, pc))
                    {
                        command.Param.Text = "climb";
                        desiredUp = pc.DesiredUpVector;
                    }
                    break;
                case "climb":
                    if (b.circumnavCheckAltitude && pc.EffectiveAlt > ic.CnavAltitude)
                    {
                        if (b.autoPilotToggle)
                        {
                            command.State = MainState.Gps;
                            command.Param.Text = "on";
                            break;
                        }
                        else
                        {
                            gm.AbortShipContext(ref command, ref tc);
                            command.State = MainState.CNav;
                        }
                    }
                    Vector3D shipUp = gc.Controller.WorldMatrix.Up;
                    VectorHelper.AlignToVector(gc, pc, shipUp, false, desiredUp);
                    CruiseControlCalc(CruiseSpeed, tslr);
                    break;
                case "glide":
                    CruiseControlCalc(CruiseSpeed, tslr);
                    if (pc.EffectiveAlt < 500 + pc.StopYDist)
                    {
                        gm.AbortShipContext(ref command, ref tc);
                        command.State = MainState.Land;
                    }
                    break;
            }
        }

        public void CircumNavigateStateSwitch()
        {
            double CruiseSpeed = (command.Param.Number > 0 ? command.Param.Number : ic.MaxSpeed);
            switch (command.Param.Text.ToLowerInvariant())
            {
                case "toggle":
                case "":
                    b.circumnavToggle = !b.circumnavToggle;
                    if (b.circumnavToggle) command.Param.Text = "on";
                    else command.Param.Text = "off";
                    break;
                case "on":
                    if (pc.EffectiveAlt < ic.CnavAltitude)
                    {
                        gm.SoftAbort();
                        b.circumnavCheckAltitude = true;
                        command.State = MainState.Cruise;
                        command.Param.Text = "orbit";
                    }

                    CruiseControlCalc(CruiseSpeed, tslr);
                    if (!b.autoPilotToggle)
                    {
                        VectorHelper.AlignToGravity(gc, pc);
                    }
                    else if (pc.DistanceToLine < ic.DistanceToGPS + pc.StopZDist)
                    {
                        command.State = MainState.Land;
                        b.autoPilotToggle = false;
                    }
                    else if (VectorHelper.AlignToGravity(gc, pc) && b.autoPilotToggle && VectorHelper.AimYawOnlyAt(gc, pc, command.Param.TargetCoordinates)) ;
                    break;
                case "off":
                    gm.AbortShipContext(ref command, ref tc);
                    break;
            }
        }

        double currentOverride = 0.0;   // 0..1 forward thrust command
        double currentBrake = 0.0;      // 0..1 braking command
        double integral = 0.0;
        double lastError = 0.0;

        // tuning
        const double Kp = 0.4;
        const double Ki = 0.03;
        const double Kd = 0.5;

        const double SPEED_TOLERANCE = 0.25;   // deadzone while cruising
        const double OVERRIDE_STEP = 0.02;     // max absolute change per tick (smoothness)
        const double MAX_INTEGRAL = 1.0;       // anti-windup clamp

        void CruiseControlCalc(double cruiseSpeed, double dt)
        {
            if (pc.ForwardVelocity > ic.MaxSpeed)
            {
                gm.ResetThrusters();
                gm.TurnOFfBreakingThrust();
                return;
            }

            // error: positive => need more forward thrust
            double error = cruiseSpeed - pc.ForwardVelocity;

            // small deadzone: don't integrate or react strongly inside it
            if (Math.Abs(error) < SPEED_TOLERANCE)
            {
                // gently decay integral to avoid wind-up and reduce chatter
                integral *= 0.9;
                lastError = error;
                return;
            }

            // PID terms
            integral += error * dt;
            integral = Math.Max(-MAX_INTEGRAL, Math.Min(MAX_INTEGRAL, integral));
            double derivative = (error - lastError) / dt;

            double pid = Kp * error + Ki * integral + Kd * derivative;

            // map pid to desired forward/brake targets (complementary)
            double desiredForward = Math.Max(0.0, Math.Min(1.0, pid));   // if pid>0 -> forward
            double desiredBrake = Math.Max(0.0, Math.Min(1.0, -pid));  // if pid<0 -> brake

            // step limiter per tick to keep smooth visuals (OVERRIDE_STEP controls smoothness)
            // The step is applied independently to forward and brake, but we keep them complementary.
            double step = OVERRIDE_STEP; // fixed per tick step (tune for desired smoothness)

            // move currentOverride toward desiredForward by at most step
            double diffF = desiredForward - currentOverride;
            if (diffF > step) diffF = step;
            else if (diffF < -step) diffF = -step;
            currentOverride += diffF;

            // move currentBrake toward desiredBrake by at most step
            double diffB = desiredBrake - currentBrake;
            if (diffB > step) diffB = step;
            else if (diffB < -step) diffB = -step;
            currentBrake += diffB;

            // Prevent both fighting: if both non-zero, reduce them proportionally so they don't sum >1
            if (currentOverride > 0 && currentBrake > 0)
            {
                double sum = currentOverride + currentBrake;
                if (sum > 1.0)
                {
                    currentOverride /= sum;
                    currentBrake /= sum;
                }
            }

            // Apply thrusters: enable brake thrusters only when brake significant
            bool useBrakes = currentBrake > 1e-4;
            foreach (var bt in gc.BreakingThrusters)
            {
                bt.Enabled = useBrakes;
                bt.ThrustOverridePercentage = (float)currentBrake;
            }

            // Apply forward thrusters
            bool useForward = currentOverride > 1e-4;
            foreach (var ft in gc.ForwardThrusters)
            {
                ft.Enabled = useForward;
                ft.ThrustOverridePercentage = (float)currentOverride;
            }

            lastError = error;
        }
    }
}
