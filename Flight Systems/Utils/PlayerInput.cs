using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;

namespace IngameScript.Utils
{
    class PlayerInput
    {
        /// <summary>
        /// Only allow player input when MainState = Idle
        /// so gyro lock won't interfere with gyro override from flight systems
        /// </summary>
        /// 

        float DeadZone;
        IMyShipController controller;

        public PlayerInput(List<IMyShipController> controllers, float deadZone = 0.1f)
        {
            DeadZone = deadZone;
        }

        public void OcupiedController(List<IMyShipController> controllers)
        {
            foreach (IMyShipController controller in controllers)
            {
                if (controller.IsUnderControl)
                {
                    this.controller = controller;
                    //PrepareController();
                    return;
                }
            }
        }

        public void PrepareController()
        {
            controller.ControlThrusters = false;
            controller.ControlWheels = false;
        }

        public void ResetController()
        {
            if (controller == null) return;
            controller.ControlThrusters = true;
            controller.ControlWheels = true;
        }

        public void LockGyros(List<IMyGyro> gyros)
        {
            foreach (IMyGyro g in gyros)
            {
                g.Roll = 0;
                g.Pitch = 0;
                g.Yaw = 0;
                g.GyroOverride = true;
            }
        }

        public bool W() => controller.MoveIndicator.Z < - DeadZone;
        public bool S() => controller.MoveIndicator.Z > DeadZone;
        public bool A() => controller.MoveIndicator.X > DeadZone;
        public bool D() => controller.MoveIndicator.X < - DeadZone;
        public bool Space() => controller.MoveIndicator.Y > DeadZone;
        public bool C() => controller.MoveIndicator.Y < - DeadZone;
        public bool MouseL() => controller.RotationIndicator.X > DeadZone;
        public bool MouseR() => controller.RotationIndicator.X < - DeadZone;
        public bool MouseUp() => controller.RotationIndicator.Y > DeadZone;
        public bool MouseDown() => controller.RotationIndicator.Y < - DeadZone;
        public bool E() => controller.RollIndicator > DeadZone;
        public bool Q() => controller.RollIndicator < - DeadZone;
    }
}
