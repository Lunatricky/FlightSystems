using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        /*Skid Steering Suspensions by sunoko*/

        //=============BASIC SETTINGS=============//
        //use hydroneumatic suspension
        bool hydroneumatic = true;
        //Activate hydroneumatic key
        HydroneumaticInputKey keyAssign = HydroneumaticInputKey.C;
        //Start setup command
        const string commandSetUp = "Setup";
        //Reset command
        const string commandReset = "Reset";
        //Suspension ignore tag
        const string ignoreName = "Ignore";
        //Start autocruise command
        const string commandAutoCruise = "AutoCruise";
        //Change speed limit command
        //example : 
        //ChangeSpeed=60  Set speed to 60
        //ChangeSpeed=+10 Add 10 to speed
        //ChangeSpeed=-10 Subtract 10 to speed
        const string commandChangeSpeed = "ChangeSpeed=";
        //--Strength adjust settings--//
        //Enable strength adjust
        bool enableStrengthAdjust = false;
        //Offset for strength adjust
        float offset = 0.05f;
        //Proportional value for strength adjust
        float strengthProportional = 50;
        //Lower strength limit for strength adjust
        float strengthLowerLimit = 5;
        //Sychronize all suspension strength
        bool synchronizeStrength = false;
        //--Skid Steer settings--//
        //Turning Mode
        VehicleMode vehicleMode = VehicleMode.Tank;
        //Center wheel friction during turning
        float skidFrictionCenter = 100;
        //Other wheel friction during turning
        float skidFrictionSlide = 1f;
        //Power during turning
        float skidPower = 100;
        //assist by gyroscope
        bool gyroAssist = true;
        //yaw turning rpm limit
        float yawRPM = 1f;
        //anti flip rpm limit
        float antiFlipRPM = 10f;
        //--Friction controll settings--//
        //Friction restore delay
        float frictionRestoreTime = 0;
        //Extra friction restore delay when wheel is outside
        float frictionRestoreOutsideDelay = 2;
        //--Hydroneumatic suspension settings--//
        //Body tilt speed
        float tiltSpeed = 0.1f;
        //========================================//

        //--------DO NOT EDIT BELLOW CODE---------//
        //-------------other settings-------------//
        //script name
        const string scriptName = "Skid Steering Suspensions";
        //---------------block list---------------//
        List<IMyTerminalBlock> cockpitList = new List<IMyTerminalBlock>();
        List<IMyTerminalBlock> suspensionList = new List<IMyTerminalBlock>();
        List<IMyTerminalBlock> gyroList = new List<IMyTerminalBlock>();

        //int blockCount = 0;
        //----------------variable----------------//
        MyIni ini = new MyIni();
        IMyShipController controller;
        List<SkidSuspension> suspensionControlList = new List<SkidSuspension>();
        public ControlInput ThisShipControl;
        float tiltFront = 0;
        float tiltSide = 0;
        bool initialized = false;
        bool neutral = true;
        bool subgridMode = false;
        bool autocruise = false;
        bool runSetup = false;
        bool runReset = false;

        public enum SuspensionPosition
        {
            None,
            Front,
            Rear,
            Center,
        }

        public enum SuspensionOrientation
        {
            None,
            Left,
            Right,
        }

        public enum HydroneumaticInputKey
        {
            C,
            Q,
            E,
            Mouse,
            MouseQE,
            QE,
        }

        public enum VehicleMode
        {
            WheeledVehicle,
            Tank,
        }

        //--gyro timer--//
        const double runPerSecGyro = 4;
        const double cycleGyro = 1 / runPerSecGyro;
        double currentTimeGyro = 0;
        //--run timer--//
        const double runPerSec = 6;
        const double cycle = 1 / runPerSec;
        double currentTime = 0;
        //--update timer--//
        int updateLimit = 10;
        double updateTimer = 0;
        string error = "";

        public Program()//Program() is run once at loading PB
        {
            updateTimer = updateLimit + 1;
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            try
            {
                ConfigHandler(true);
            }
            catch (Exception e)
            {
                Echo("Enum parse failed.\nProbably CD format is wrong.\nScript will try reset CD");
                DeleteCustomData();
                return;
            }
        }

        public void ArgumentHandler(string argument)
        {
            if (argument.ToLower() == commandAutoCruise.ToLower())
            {
                autocruise = !autocruise;
                return;
            }
            if (argument.ToLower().Contains(commandChangeSpeed.ToLower()))
            {
                ChangeSpeed(argument);
                return;
            }
            if (argument.ToLower() == commandSetUp.ToLower())
            {
                runSetup = true;
                return;
            }
            if (argument.ToLower() == commandReset.ToLower())
            {
                runReset = true;
                return;
            }
        }

        public void Main(string argument, UpdateType type)
        {
            if (type != UpdateType.Update1 && type != UpdateType.Update10 && type != UpdateType.Update100 && type != UpdateType.Once)
            {
                ArgumentHandler(argument);
                return;
            }
            currentTime += Runtime.TimeSinceLastRun.TotalSeconds;
            currentTimeGyro += Runtime.TimeSinceLastRun.TotalSeconds;
            RunEveryTick();
            if (currentTimeGyro > cycleGyro)
            {
                GyroAssist();
                currentTimeGyro = 0;
            }
            if (currentTime < cycle)
            {
                return;
            }
            Run();
            currentTime = 0;
        }

        private void RunEveryTick()
        {
            if (!hydroneumatic || !suspensionControlList.Any() || ThisShipControl == null)
            {
                return;
            }
            if (!IsMouseMode() || ThisShipControl.Controller.HandBrake)
            {
                return;
            }
            bool activeHydro = CheckHydroneumaticInput();
            if (!activeHydro)
            {
                return;
            }

            float addition = tiltSpeed * -Math.Sign(ThisShipControl.Pitch) * (0.016f / (float)cycle);
            float result = tiltFront - addition;
            if (Math.Abs(result) < 0.64f)
            {
                tiltFront = result;
            }

            if (keyAssign == HydroneumaticInputKey.MouseQE)
            {
                addition = tiltSpeed * Math.Sign(ThisShipControl.Roll) * (0.016f / (float)cycle);
                result = tiltSide - addition;
                if (Math.Abs(result) < 0.64f)
                {
                    tiltSide = result;
                }
            }

            foreach (SkidSuspension Skid in suspensionControlList)
            {
                Skid.Hydroneumatic(ThisShipControl, tiltFront, tiltSide);
            }
        }

        private void GyroAssist()
        {
            if (!gyroAssist)
            {
                return;
            }
            if (!gyroList.Any())
            {
                return;
            }
            if (ThisShipControl == null)
            {
                return;
            }
            if (ThisShipControl.Controller.HandBrake)
            {
                GyroControl(ThisShipControl.Controller, 0, 0, 0, true);
                return;
            }
            if (hydroneumatic && CheckHydroneumaticInput())
            {
                if (!IsMouseMode())
                {
                    GyroControl(ThisShipControl.Controller, 0, 0, 0, true);
                    return;
                }

                foreach (IMyGyro gyro in gyroList)
                {
                    if (!gyro.IsWorking)
                    {
                        gyro.ApplyAction("OnOff_On");
                    }
                }
                return;
            }

            foreach (IMyGyro gyro in gyroList)
            {
                if (!gyro.IsWorking)
                {
                    gyro.ApplyAction("OnOff_On");
                }
            }

            if (vehicleMode == VehicleMode.Tank)
            {
                // Not turning -> completely disable gyro override
                if (ThisShipControl.Left == 0)
                {
                    GyroControl(ThisShipControl.Controller, 0, 0, 0, false);
                    return;
                }

                // Turning -> enable gyro override
                float input = ThisShipControl.Left > 0 ? yawRPM : -yawRPM;

                if (ThisShipControl.Forward > 0)
                    input = -input;

                GyroControl(ThisShipControl.Controller, input, 0, 0, true);
                return;
            }
            if (vehicleMode == VehicleMode.WheeledVehicle)
            {
                if (ThisShipControl.Forward == 0 || ThisShipControl.Left == 0)
                {
                    GyroControl(ThisShipControl.Controller, 0, 0, 0, true);
                    return;
                }
                if (ThisShipControl.Left > 0)
                {
                    GyroControl(ThisShipControl.Controller, 0, 0, antiFlipRPM, true);
                    return;
                }
                if (ThisShipControl.Left < 0)
                {
                    GyroControl(ThisShipControl.Controller, 0, 0, -antiFlipRPM, true);
                    return;
                }
            }
        }

        private void Run()
        {
            updateTimer += currentTime;
            //This is... evil way, but can be fix to crash from suspension CD format change
            try
            {
                //system failure check
                if (GetBlocks())
                {
                    Echo(error);
                    return;
                }
            }
            catch (Exception e)
            {
                Echo("Enum parse failed.\nProbably CD format is wrong.\nScript will try reset CD");
                DeleteCustomData();
                return;
            }
            if (runReset)
            {
                DeleteCustomData();
                initialized = false;
                runReset = false;
                return;
            }
            //try get controlled cockpit
            if (!TryGetControlledCockpit(out controller))
            {
                if (ThisShipControl == null)
                {
                    return;
                }
                foreach (SkidSuspension Skid in suspensionControlList)
                {
                    Skid.SubgridPropolution(ThisShipControl, ref autocruise);
                }
                return;
            }
            if (ThisShipControl == null)
            {
                ThisShipControl = new ControlInput(controller);
            }
            else
            {
                ThisShipControl.Controller = controller;
            }
            //Find suspension orientation and position;
            if (runSetup)
            {
                SetSuspensionPotision(controller, suspensionList, controller.CenterOfMass);
                runSetup = false;
                updateTimer = updateLimit + 1;
                return;
            }
            //Initialize check
            if (!initialized)
            {
                int count;
                if (CheckSetUp(suspensionList, out count))
                {
                    initialized = true;
                }
                else
                {
                    Echo($"{count} unassigned suspension detected.\nPlease align the cars straight,\nenter \"{commandSetUp}\" in argument,\nand run once\nOr write \"{ignoreName}\" into suspension\ncustom data if don't controlling it with this sctipt");
                    return;
                }
            }

            SyncParkingBrake(ThisShipControl.Controller, ref cockpitList);
            //determine subgrid control mode
            subgridMode = suspensionControlList.Any(b => b.SuspensionBlock.CubeGrid != controller.CubeGrid);
            //suspension control
            neutral = true;
            bool braked = ThisShipControl.Controller.HandBrake;
            bool activeHydro = CheckHydroneumaticInput();
            if (braked)
            {
                Echo("Config mode active.");
                tiltFront = 0;
                tiltSide = 0;
            }
            else if (hydroneumatic && activeHydro)
            {
                //Hydroneumatic suspension input
                float v = 0;
                if (keyAssign == HydroneumaticInputKey.QE)
                {
                    v = -Math.Sign(ThisShipControl.Roll);
                }
                else if (!IsMouseMode())
                {
                    v = Math.Sign(ThisShipControl.Forward);
                }

                float addition = tiltSpeed * v;
                float result = tiltFront - addition;
                if (Math.Abs(result) < 0.64f)
                {
                    tiltFront = result;
                }

                if (keyAssign == HydroneumaticInputKey.QE)
                {
                    v = 0;
                }
                else if (!IsMouseMode())
                {
                    v = Math.Sign(ThisShipControl.Left);
                }

                addition = tiltSpeed * v;
                result = tiltSide - addition;
                if (Math.Abs(result) < 0.64f)
                {
                    tiltSide = result;
                }
            }

            float resultValue = 0;
            int c = 0;
            foreach (SkidSuspension Skid in suspensionControlList)
            {
                c++;
                if (braked)
                {
                    Skid.ChangeStatus();
                }
                else
                {
                    float str = Skid.CalculateStrengthAdjust();
                    if (enableStrengthAdjust)
                    {
                        if (synchronizeStrength)
                        {
                            resultValue += str;
                        }
                        else
                        {
                            Skid.SuspensionBlock.Strength = str;

                        }
                        Skid._sequencedStrengthAdjust = true;
                    }
                    if (IsStopSuspensionControl() && hydroneumatic && activeHydro)
                    {
                        ThisShipControl.Controller.ControlWheels = false;
                        Skid.Hydroneumatic(ThisShipControl, tiltFront, tiltSide);
                    }
                    else
                    {
                        if (hydroneumatic && activeHydro && !IsMouseMode())
                        {
                            Skid.Hydroneumatic(ThisShipControl, tiltFront, tiltSide);
                        }
                        ThisShipControl.Controller.ControlWheels = true;
                        Skid.Steering(ThisShipControl, (float)currentTime, vehicleMode);
                        if (subgridMode)
                        {
                            Skid.SubgridPropolution(ThisShipControl, ref autocruise);
                        }
                        else
                        {
                            Skid.AutoCruisePropolution(ThisShipControl, ref autocruise);
                        }
                    }
                }
                if (!neutral)
                {
                    continue;
                }
                neutral = Skid.NeutralCheck();
            }
            if (!enableStrengthAdjust || !synchronizeStrength || braked || c == 0)
            {
                return;
            }
            resultValue = resultValue / c;
            foreach (SkidSuspension Skid in suspensionControlList)
            {
                Skid.SuspensionBlock.Strength = resultValue;
            }
        }

        private bool IsMouseMode()
        {
            return (keyAssign == HydroneumaticInputKey.Mouse || keyAssign == HydroneumaticInputKey.MouseQE);
        }

        private void DeleteCustomData()
        {
            var blockList = GetOwnGridBlock<IMyMotorSuspension>();
            foreach (var b in blockList)
            {
                b.CustomData = b.CustomData.Replace(FindMyConfig(b.CustomData), "");
            }
            Me.CustomData = "";
            updateTimer = updateLimit + 1;
        }

        private bool CheckHydroneumaticInput()
        {
            if (keyAssign == HydroneumaticInputKey.C && ThisShipControl.Up < 0)
            {
                return true;
            }
            if (keyAssign == HydroneumaticInputKey.Q && ThisShipControl.Roll < 0)
            {
                return true;
            }
            if (keyAssign == HydroneumaticInputKey.E && ThisShipControl.Roll > 0)
            {
                return true;
            }
            if (keyAssign == HydroneumaticInputKey.Mouse && ThisShipControl.Pitch != 0)
            {
                return true;
            }
            if (keyAssign == HydroneumaticInputKey.MouseQE && (ThisShipControl.Pitch != 0 || ThisShipControl.Roll != 0))
            {
                return true;
            }
            if (keyAssign == HydroneumaticInputKey.QE && ThisShipControl.Roll != 0)
            {
                return true;
            }
            return false;
        }

        private bool IsStopSuspensionControl()
        {
            if (keyAssign == HydroneumaticInputKey.C)
            {
                return true;
            }
            if (keyAssign == HydroneumaticInputKey.Q)
            {
                return true;
            }
            if (keyAssign == HydroneumaticInputKey.E)
            {
                return true;
            }
            return false;
        }

        private bool CheckSetUp(List<IMyTerminalBlock> list, out int missingCount)
        {
            missingCount = 0;
            foreach (var b in list)
            {
                if (!b.CustomData.Contains(scriptName))
                {
                    missingCount++;
                }
            }
            return missingCount == 0;
        }

        public void SyncParkingBrake(IMyShipController controller, ref List<IMyTerminalBlock> cockpitList)
        {
            cockpitList.ForEach(b => (b as IMyShipController).HandBrake = controller.HandBrake);
        }

        private void ChangeSpeed(string command)
        {
            if (ThisShipControl == null)
            {
                return;
            }
            if (ThisShipControl.Left != 0)
            {
                return;
            }
            string str = command.Replace(commandChangeSpeed, "");
            float amount;
            if (str.Contains("+"))
            {
                Single.TryParse(str.Replace("+", ""), out amount);
                float current;
                foreach (var s in suspensionControlList)
                {
                    current = s.SuspensionBlock.GetValue<float>("Speed Limit");
                    s.SuspensionBlock.SetValue("Speed Limit", MathHelper.Clamp(current + amount, 0, 50));
                }
            }
            else if (str.Contains("-"))
            {
                Single.TryParse(str.Replace("-", ""), out amount);
                float current;
                foreach (var s in suspensionControlList)
                {
                    current = s.SuspensionBlock.GetValue<float>("Speed Limit");
                    s.SuspensionBlock.SetValue("Speed Limit", MathHelper.Clamp(current - amount, 0, 50));
                }
            }
            else
            {
                Single.TryParse(str, out amount);
                foreach (var s in suspensionControlList)
                {
                    s.SuspensionBlock.SetValue("Speed Limit", MathHelper.Clamp(amount, 0, 50));
                }
            }
        }

        private void SetSuspensionPotision(IMyTerminalBlock Referecne, List<IMyTerminalBlock> susList, Vector3D CenterOfMass)
        {
            List<IMyTerminalBlock> suspensions = new List<IMyTerminalBlock>();
            if (!susList.Any())
            {
                foreach (var s in suspensionControlList)
                {
                    suspensions.Add(s.SuspensionBlock);
                }
            }
            else
            {
                suspensions = susList;
            }
            List<IMyTerminalBlock> Left = suspensions.FindAll(b => controller.WorldMatrix.Left.Dot(b.WorldMatrix.Up) > 0);
            WriteSuspensionPositionStatus(Referecne, Left, CenterOfMass, SuspensionOrientation.Left);

            List<IMyTerminalBlock> Right = suspensions.FindAll(b => controller.WorldMatrix.Left.Dot(b.WorldMatrix.Up) < 0);
            WriteSuspensionPositionStatus(Referecne, Right, CenterOfMass, SuspensionOrientation.Right);
        }

        private void WriteSuspensionPositionStatus(IMyTerminalBlock Referecne, List<IMyTerminalBlock> suspensionList, Vector3D CenterOfMass, SuspensionOrientation side)
        {
            double maxFront = suspensionList.Max(b => (b.GetPosition() - CenterOfMass).Dot(controller.WorldMatrix.Forward));
            IMyTerminalBlock Front = suspensionList.First(b => (b.GetPosition() - CenterOfMass).Dot(controller.WorldMatrix.Forward) == maxFront);
            double maxBackward = suspensionList.Max(b => (b.GetPosition() - CenterOfMass).Dot(controller.WorldMatrix.Backward));
            IMyTerminalBlock Back = suspensionList.First(b => (b.GetPosition() - CenterOfMass).Dot(controller.WorldMatrix.Backward) == maxBackward);
            double min = suspensionList.Min(b => Math.Abs((b.GetPosition() - CenterOfMass).Dot(controller.WorldMatrix.Forward)));
            IMyTerminalBlock Center = suspensionList.First(b => Math.Abs((b.GetPosition() - CenterOfMass).Dot(controller.WorldMatrix.Forward)) == min);
            foreach (var sus in suspensionList)
            {
                string str = "";
                string[] del = { "---" };
                string[] split = sus.CustomData.Split(del, StringSplitOptions.RemoveEmptyEntries);
                int i;
                for (i = 0; i < split.Count(); i++)
                {
                    if (split[i].Contains("[") && split[i].Contains("]") && !split[i].Contains(scriptName))
                    {
                        str += split[i] + "---";
                        continue;
                    }
                }
                ini.Clear();

                ini.Set(scriptName, "Orientation", side.ToString());
                if (sus == Front)
                {
                    ini.Set(scriptName, "Position", SuspensionPosition.Front.ToString());
                }
                else if (sus == Back)
                {
                    ini.Set(scriptName, "Position", SuspensionPosition.Rear.ToString());
                }
                else if (sus == Center)
                {
                    ini.Set(scriptName, "Position", SuspensionPosition.Center.ToString());
                }
                else
                {
                    ini.Set(scriptName, "Position", SuspensionPosition.None.ToString());
                }
                double offset = (sus.GetPosition() - CenterOfMass).Dot(controller.WorldMatrix.Forward);
                offset = offset / (offset > 0 ? maxFront : maxBackward);
                ini.Set(scriptName, "DistanceOffset", (float)offset);

                string result = ini.ToString();
                if (result.Length == 0)
                {
                    return;
                }
                sus.CustomData = (str == "" ? str : str + "\n") + result + "---";
            }
        }

        public class SkidSuspension
        {
            public IMyMotorSuspension SuspensionBlock { get; }

            public SuspensionPosition Position { get; }
            public SuspensionOrientation Orientation { get; }

            public float Friction { get; set; }
            public float Power { get; set; }
            public float Strength { get; set; }
            public float Height { get; set; }
            public float SpeedLimit { get; set; }

            public float Offset { get; }
            public float StrengthProportional { get; }
            public float StrengthLowerLimit { get; }
            public float FrictionRestoreTime { get; }
            public float FrictionRestoreOutsideDelay { get; }
            public float SkidFrictionCenter { get; }
            public float SkidFrictionOther { get; }
            public float SkidPower { get; }

            public float DistanceOffset { get; }

            private float _frictionStep = 0;
            private bool _isSkidOutside = false;
            private bool _sequencedTurn = false;
            private bool _sequencedTurnWithWheel = false;
            private bool _sequencedHydro = false;
            public bool _sequencedStrengthAdjust { get; set; } = false;

            public SkidSuspension(IMyMotorSuspension SuspensionBlock, SuspensionPosition Position, SuspensionOrientation Orientation, float FrictionConf, float PowerConf, float StrengthConf, float HeightConf, float SpeedLimitConf, float Offset, float StrengthProportional, float StrengthLowerLimit, float FrictionRestoreTime, float FrictionRestoreOutsideDelay, float DistanceOffset, float SkidFrictionCenter, float SkidFrictionOther, float SkidPower)
            {
                this.SuspensionBlock = SuspensionBlock;
                this.Position = Position;
                this.Orientation = Orientation;
                this.Offset = Offset;
                this.StrengthProportional = StrengthProportional;
                this.StrengthLowerLimit = StrengthLowerLimit;
                this.FrictionRestoreTime = FrictionRestoreTime;
                this.FrictionRestoreOutsideDelay = FrictionRestoreOutsideDelay;
                this.DistanceOffset = DistanceOffset;
                this.SkidFrictionCenter = SkidFrictionCenter;
                this.SkidFrictionOther = SkidFrictionOther;
                this.SkidPower = SkidPower;

                Friction = (FrictionConf == 0 ? SuspensionBlock.Friction : FrictionConf);
                Power = (PowerConf == 0 ? SuspensionBlock.Power : PowerConf);
                Strength = (StrengthConf == 0 ? SuspensionBlock.Strength : StrengthConf);
                Height = (HeightConf == 0 ? SuspensionBlock.Height : HeightConf);
                SpeedLimit = (SpeedLimitConf == 0 ? SuspensionBlock.GetValue<float>("Speed Limit") : SpeedLimitConf);
            }

            public void ChangeStatus()
            {
                if (_sequencedHydro)
                {
                    SuspensionBlock.Strength = Strength;
                    SuspensionBlock.Height = Height;
                    _sequencedHydro = false;
                }
                else if (_sequencedStrengthAdjust)
                {
                    SuspensionBlock.Strength = Strength;
                    _sequencedStrengthAdjust = false;
                }
                else
                {
                    Strength = SuspensionBlock.Strength;
                    Height = SuspensionBlock.Height;
                }
                if (_sequencedTurn)
                {
                    SuspensionBlock.Friction = Friction;
                    SuspensionBlock.Power = Power;
                    _sequencedTurn = false;
                }
                else
                {
                    Friction = SuspensionBlock.Friction;
                    Power = SuspensionBlock.Power;
                    SpeedLimit = SuspensionBlock.GetValue<float>("Speed Limit");
                }
            }

            public void Steering(ControlInput ctrl, float currentTimeF, VehicleMode mode)
            {
                if (mode == VehicleMode.Tank)
                {
                    if (SuspensionBlock.Steering)
                    {
                        SuspensionBlock.Steering = false;
                    }
                    //turn right
                    if (ctrl.Left > 0)
                    {
                        if (Orientation == SuspensionOrientation.Left && SuspensionBlock.InvertPropulsion)
                        {
                            SuspensionBlock.ApplyAction("InvertPropulsion");
                        }
                        else if (Orientation == SuspensionOrientation.Right && !SuspensionBlock.InvertPropulsion)
                        {
                            SuspensionBlock.ApplyAction("InvertPropulsion");
                        }
                        if (Position == SuspensionPosition.Center)
                        {
                            SuspensionBlock.Friction = SkidFrictionCenter;
                            SuspensionBlock.Power = SkidPower;
                        }
                        else
                        {
                            SuspensionBlock.Friction = SkidFrictionOther;
                        }
                        SuspensionBlock.SetValue<float>("Speed Limit", 50);
                        _isSkidOutside = Orientation == SuspensionOrientation.Left;
                        _sequencedTurn = true;
                    }
                    //turn left
                    else if (ctrl.Left < 0)
                    {
                        if (Orientation == SuspensionOrientation.Left && !SuspensionBlock.InvertPropulsion)
                        {
                            SuspensionBlock.ApplyAction("InvertPropulsion");
                        }
                        else if (Orientation == SuspensionOrientation.Right && SuspensionBlock.InvertPropulsion)
                        {
                            SuspensionBlock.ApplyAction("InvertPropulsion");
                        }
                        if (Position == SuspensionPosition.Center)
                        {
                            SuspensionBlock.Friction = SkidFrictionCenter;
                            SuspensionBlock.Power = SkidPower;
                        }
                        else
                        {
                            SuspensionBlock.Friction = SkidFrictionOther;
                        }
                        SuspensionBlock.SetValue<float>("Speed Limit", 50);
                        _isSkidOutside = Orientation == SuspensionOrientation.Right;
                        _sequencedTurn = true;
                    }
                    else
                    {
                        _sequencedTurn = DelayedFrictionRestoreSequence(_isSkidOutside, currentTimeF);
                        SuspensionBlock.Power = Power;
                        if (SuspensionBlock.InvertPropulsion)
                        {
                            SuspensionBlock.ApplyAction("InvertPropulsion");
                        }
                        if (SuspensionBlock.GetValue<float>("Speed Limit") != SpeedLimit)
                        {
                            SuspensionBlock.SetValue("Speed Limit", SpeedLimit);
                        }
                    }
                }
                else if (mode == VehicleMode.WheeledVehicle)
                {
                    if (!SuspensionBlock.Steering)
                    {
                        SuspensionBlock.SetValue<bool>("Steering", true);
                    }
                    SteeringWithWheel(ctrl, Math.Sign(ctrl.Left), currentTimeF);
                }
            }

            private void SteeringWithWheel(ControlInput ctrl, float angle, float currentTimeF)
            {
                if (ctrl.Controller.CubeGrid == SuspensionBlock.CubeGrid)
                {
                    return;
                }
                if (ctrl.Left != 0)
                {
                    SuspensionBlock.SetValue("Steer override", Math.Sign(ctrl.Left) * DistanceOffset);
                    _sequencedTurn = true;
                    _sequencedTurnWithWheel = true;
                    if (ctrl.Left < 0)
                    {
                        _isSkidOutside = Orientation == SuspensionOrientation.Right;
                    }
                    else if (ctrl.Left > 0)
                    {
                        _isSkidOutside = Orientation == SuspensionOrientation.Left;
                    }
                }
                else
                {
                    SuspensionBlock.SetValue<float>("Steer override", 0);
                    if (_sequencedTurnWithWheel)
                    {
                        if (_isSkidOutside)
                        {
                            SuspensionBlock.Friction = SkidFrictionOther;
                        }
                        _sequencedTurnWithWheel = false;
                    }
                    _sequencedTurn = DelayedFrictionRestoreSequence(_isSkidOutside, currentTimeF);
                }
            }

            public bool DelayedFrictionRestoreSequence(bool isOutside, float currentTimeF)
            {
                if (SuspensionBlock.Friction == Friction)
                {
                    return false;
                }

                if (_frictionStep == 0)
                {
                    float delay = FrictionRestoreTime + (isOutside ? FrictionRestoreOutsideDelay : 0);
                    if (delay == 0)
                    {
                        SuspensionBlock.Friction = Friction;
                        return false;
                    }
                    _frictionStep = -(SuspensionBlock.Friction - Friction) * (currentTimeF / delay);
                }

                SuspensionBlock.Friction += _frictionStep;
                if (Math.Abs(SuspensionBlock.Friction - Friction) < _frictionStep)
                {
                    SuspensionBlock.Friction = Friction;
                    _frictionStep = 0;
                    return false;
                }
                if (_frictionStep > 0 && Friction - SuspensionBlock.Friction < 0)
                {
                    SuspensionBlock.Friction = Friction;
                    _frictionStep = 0;
                    return false;
                }
                if (_frictionStep < 0 && Friction - SuspensionBlock.Friction > 0)
                {
                    SuspensionBlock.Friction = Friction;
                    _frictionStep = 0;
                    return false;
                }
                return true;
            }

            public float CalculateStrengthAdjust()
            {
                if ((SuspensionBlock).Top == null) return SuspensionBlock.Strength;
                float height = (float)SuspensionBlock.WorldMatrix.Forward.Dot((SuspensionBlock).Top.GetPosition() - SuspensionBlock.GetPosition());
                float difference = (float)(SuspensionBlock.Height + Offset - height);
                float manipulatedVariable = MathHelper.Clamp(SuspensionBlock.Strength - difference * StrengthProportional, StrengthLowerLimit, 100f);
                return manipulatedVariable;
            }

            public void Hydroneumatic(ControlInput ctrl, float tiltFront, float tiltSide)
            {
                float sideAdditional = tiltSide * (Orientation == SuspensionOrientation.Left ? 1 : -1);
                SuspensionBlock.Height = Height + tiltFront * DistanceOffset + sideAdditional;
                _sequencedHydro = !NeutralCheck();
            }

            public bool NeutralCheck()
            {
                if (_sequencedHydro)
                {
                    return false;
                }
                if (_sequencedTurn)
                {
                    return false;
                }
                if (_sequencedStrengthAdjust)
                {
                    return false;
                }
                if (SuspensionBlock.Friction != Friction)
                {
                    return false;
                }
                if (SuspensionBlock.Power != Power)
                {
                    return false;
                }
                if (SuspensionBlock.Strength != Strength)
                {
                    return false;
                }
                if (SuspensionBlock.Height != Height)
                {
                    return false;
                }
                if (SuspensionBlock.GetValue<float>("Speed Limit") != SpeedLimit)
                {
                    return false;
                }
                return true;
            }

            public void SubgridPropolution(ControlInput ThisShipControl, ref bool autocruise)//Port from Enhanced Suspension Control script
            {
                //There is no brake on this vehicle! Script use backward for brakes instead
                if (ThisShipControl.Up > 0)
                {
                    autocruise = false;
                    Vector3 velocity = ThisShipControl.Controller.GetShipVelocities().LinearVelocity;
                    double dot = velocity.Dot(SuspensionBlock.WorldMatrix.Left);
                    dot = dot * (Orientation == SuspensionOrientation.Right ? 1 : -1);
                    if (dot != 0)
                    {
                        Propolution(Math.Sign(dot), (float)MathHelper.Clamp(Math.Abs(dot * dot), 0, 1));
                    }
                    else
                    {
                        Propolution(0, 0);
                    }
                    //Propolution
                }
                else if (autocruise || ThisShipControl.Forward < 0)
                {
                    Propolution(1, 0);
                }
                else if (ThisShipControl.Forward > 0)
                {
                    autocruise = false;
                    Propolution(-1, 0);
                }
                else
                {
                    Propolution(0, 0);
                }
            }

            public void AutoCruisePropolution(ControlInput ThisShipControl, ref bool autocruise)
            {
                if (autocruise && ThisShipControl.Up == 0)
                {// && !this.SuspensionBlock.Brake
                    Propolution(1, 0);
                }
                else
                {
                    SuspensionBlock.SetValue<float>("Propulsion override", 0);
                    autocruise = false;
                }
            }

            private void Propolution(int direction, float powerOverride)
            {
                if (Orientation == SuspensionOrientation.Left)
                {
                    SuspensionBlock.SetValue("Propulsion override", direction * (powerOverride == 0 ? (SuspensionBlock.Power / 100) : powerOverride));
                }
                else if (Orientation == SuspensionOrientation.Right)
                {
                    SuspensionBlock.SetValue("Propulsion override", -direction * (powerOverride == 0 ? (SuspensionBlock.Power / 100) : powerOverride));
                }
            }
        }

        public class ControlInput
        {
            public IMyShipController Controller { get; set; }

            public float Forward { get { return GetValue(0); } private set { Forward = GetValue(0); } }
            public float Up { get { return GetValue(1); } private set { Up = GetValue(1); } }
            public float Left { get { return GetValue(2); } private set { Left = GetValue(2); } }
            public float Yaw { get { return GetValue(3); } private set { Yaw = GetValue(3); } }
            public float SimRelatedYaw { get { return GetValue(4); } private set { SimRelatedYaw = GetValue(4); } }
            public float Pitch { get { return GetValue(5); } private set { Pitch = GetValue(5); } }
            public float SimRelatedPitch { get { return GetValue(6); } private set { SimRelatedPitch = GetValue(6); } }
            public float Roll { get { return GetValue(7); } private set { Roll = GetValue(7); } }
            public float SimRelatedRoll { get { return GetValue(8); } private set { SimRelatedRoll = GetValue(8); } }

            public float SimSpeed { get; private set; } = 1;

            private DateTime _prevDT;

            public ControlInput(IMyShipController Controller)
            {
                this.Controller = Controller;
            }

            private float GetValue(int propertyIndex)
            {
                if (Controller == null) return 0;
                switch (propertyIndex)
                {
                    case 0: return Controller.MoveIndicator.Z;
                    case 1: return Controller.MoveIndicator.Y;
                    case 2: return Controller.MoveIndicator.X;
                    case 3: return Controller.RotationIndicator.Y;
                    case 4: return Controller.RotationIndicator.Y * SimSpeed;
                    case 5: return Controller.RotationIndicator.X;
                    case 6: return Controller.RotationIndicator.X * SimSpeed;
                    case 7: return Controller.RollIndicator;
                    case 8: return Controller.RollIndicator * SimSpeed;
                    default: return 0;
                }
            }

            public void UpdateSimSpeed(double currentTime)
            {
                DateTime nowDt = DateTime.Now;
                TimeSpan ts = nowDt.Subtract(_prevDT);
                SimSpeed = ts.TotalSeconds != 0 ? (float)((currentTime / 0.96) / ts.TotalSeconds) : 1;
                _prevDT = nowDt;
            }
        }

        public void GyroControl(IMyTerminalBlock Referecne, float yaw, float pitch, float roll, bool _override)
        {
            //create rotation vector
            Vector3 rotationVector = new Vector3(-pitch, yaw, roll);
            //convert to reference direction
            Vector3 refRotationVector = Vector3.TransformNormal(rotationVector, Referecne.WorldMatrix);
            foreach (IMyGyro gyro in gyroList)
            {
                //translate to gyro direction
                Vector3 localRotationVector = Vector3.TransformNormal(refRotationVector, Matrix.Transpose(gyro.WorldMatrix));

                gyro.GyroOverride = _override;

                gyro.Pitch = localRotationVector.X;
                gyro.Yaw = localRotationVector.Y;
                gyro.Roll = localRotationVector.Z;
            }
        }

        public bool TryGetControlledCockpit(out IMyShipController controller)
        {
            foreach (IMyShipController block in cockpitList)
            {
                if (block.IsUnderControl)
                {
                    controller = block;
                    return true;
                }
            }
            controller = cockpitList[0] as IMyShipController;
            return false;
        }

        public void UpdateSettings()
        {
            MyIniParseResult result;
            foreach (SkidSuspension sus in suspensionControlList)
            {
                ini.Clear();
                if (!sus.SuspensionBlock.CustomData.Contains(scriptName) || !ini.TryParse(FindMyConfig(sus.SuspensionBlock.CustomData), out result))
                {
                    return;
                }

                string str = "";
                string[] del = { "---" };
                string[] split = sus.SuspensionBlock.CustomData.Split(del, StringSplitOptions.RemoveEmptyEntries);
                int i;
                for (i = 0; i < split.Count(); i++)
                {
                    if (split[i].Contains("[") && split[i].Contains("]") && !split[i].Contains(scriptName))
                    {
                        str += split[i] + "---";
                        continue;
                    }
                }
                ini.Set(scriptName, "Friction", sus.Friction);
                ini.Set(scriptName, "Power", sus.Power);
                ini.Set(scriptName, "Strength", sus.Strength);
                ini.Set(scriptName, "Height", sus.Height);
                ini.Set(scriptName, "SpeedLimit", sus.SpeedLimit);
                sus.SuspensionBlock.CustomData = (str == "" ? str : str + "\n") + ini.ToString() + "---";
            }
        }

        private string FindMyConfig(string config)
        {
            string[] del = { "---" };
            string[] split = config.Split(del, StringSplitOptions.RemoveEmptyEntries);
            foreach (string str in split)
            {
                if (str.Contains(scriptName))
                {
                    return str;
                }
            }
            return config;
        }

        public bool GetBlocks()
        {
            Echo(scriptName);
            if (updateTimer < updateLimit)
            {
                Echo($"Next Update : {updateLimit - updateTimer:#}");
                if (error.Length > 0)
                {
                    return true;
                }
                return false;
            }
            if (!neutral)
            {
                Echo("Blocks Update Skipped");
                updateTimer = 0;
                return false;
            }
            Echo("Blocks Update...");
            updateTimer = 0;
            var blockList = GetOwnGridBlock<IMyTerminalBlock>();
            if (!blockList.Any())
            {
                error = "**System Failure**\nBlocks Not Found";
                return true;
            }
            //    if(blockList.Count == blockCount){
            //        return false;
            //    }
            UpdateSettings();
            //if add or lost blocks
            cockpitList.Clear();
            gyroList.Clear();
            suspensionControlList.Clear();
            suspensionList.Clear();
            //sort blocks
            MyIniParseResult result;
            foreach (var b in blockList)
            {
                if (b is IMyMotorSuspension && !b.CustomData.Contains(ignoreName))
                {
                    if (b.CustomData.Contains(scriptName) && b.CustomData.Contains("Position") && b.CustomData.Contains("Orientation"))
                    {
                        ini.Clear();
                        if (!ini.TryParse(FindMyConfig(b.CustomData), out result))
                        {
                            suspensionList.Add(b);
                            continue;
                        }
                        var position = (SuspensionPosition)Enum.Parse(typeof(SuspensionPosition), ini.Get(scriptName, "Position").ToString());
                        var orientation = (SuspensionOrientation)Enum.Parse(typeof(SuspensionOrientation), ini.Get(scriptName, "Orientation").ToString());
                        var friction = ini.Get(scriptName, "Friction").ToSingle();
                        var power = ini.Get(scriptName, "Power").ToSingle();
                        var strength = ini.Get(scriptName, "Strength").ToSingle();
                        var height = ini.Get(scriptName, "Height").ToSingle();
                        var speedLimit = ini.Get(scriptName, "SpeedLimit").ToSingle();
                        var distanceOffset = ini.Get(scriptName, "DistanceOffset").ToSingle();
                        suspensionControlList.Add(new SkidSuspension(b as IMyMotorSuspension, position, orientation, friction, power, strength, height, speedLimit, offset, strengthProportional, strengthLowerLimit, frictionRestoreTime, frictionRestoreOutsideDelay, distanceOffset, skidFrictionCenter, skidFrictionSlide, skidPower));
                    }
                    else
                    {
                        suspensionList.Add(b);
                    }
                }
                else if (b is IMyShipController)
                {
                    cockpitList.Add(b);
                }
                else if (b is IMyGyro)
                {
                    gyroList.Add(b);
                }
            }
            //block check
            if (!suspensionList.Any() && !suspensionControlList.Any())
            {//suspension are missing
                error = "**System Failure**\nSuspension Not Found";
                return true;
            }
            if (!cockpitList.Any())
            {//cockpit are missing
                error = "**System Failure**\nCockpit Not Found";
                return true;
            }
            if (!gyroList.Any() && gyroAssist)
            {//gyro are missing
                error = "**System Failure**\nGyroscope Not Found";
                return true;
            }
            //    blockCount = blockList.Count;
            error = "";

            ConfigHandler(false);
            return false;
        }

        private List<IMyTerminalBlock> GetOwnGridBlock<T>(Func<IMyTerminalBlock, bool> collect = null) where T : class, IMyTerminalBlock
        {
            var blockList = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyMechanicalConnectionBlock>(blockList);
            HashSet<IMyCubeGrid> CubeGridSet = new HashSet<IMyCubeGrid>();
            CubeGridSet.Add(Me.CubeGrid);
            bool continueLoop;
            IMyMechanicalConnectionBlock block;
            //get all CubeGrid connected on ship
            do
            {
                continueLoop = false;
                for (int i = 0; i < blockList.Count; i++)
                {
                    block = blockList[i] as IMyMechanicalConnectionBlock;
                    if (CubeGridSet.Contains(block.CubeGrid) || CubeGridSet.Contains(block.TopGrid))
                    {
                        CubeGridSet.Add(block.CubeGrid);
                        CubeGridSet.Add(block.TopGrid);
                        blockList.Remove(blockList[i]);
                        continueLoop = true;
                    }
                }
            }
            while (continueLoop);

            //get filtered block
            blockList.Clear();
            GridTerminalSystem.GetBlocksOfType<T>(blockList, b => CubeGridSet.Contains(b.CubeGrid) && (collect == null || collect(b)));
            return blockList;
        }

        private void ConfigHandler(bool initial)
        {
            ini.Clear();
            MyIniParseResult result;
            //---initialize---//
            if (!Me.CustomData.Contains(scriptName) || !ini.TryParse(Me.CustomData, out result))
            {
                ini.Set(scriptName, "hydroneumatic", hydroneumatic);
                ini.Set(scriptName, "keyAssign", keyAssign.ToString());
                ini.Set(scriptName, "tiltSpeed", tiltSpeed);
                ini.Set(scriptName, "vehicleMode", vehicleMode.ToString());
                ini.Set(scriptName, "skidFrictionCenter", skidFrictionCenter);
                ini.Set(scriptName, "skidFrictionSlide", skidFrictionSlide);
                ini.Set(scriptName, "skidPower", skidPower);
                ini.Set(scriptName, "gyroAssist", gyroAssist);
                ini.Set(scriptName, "yawRPM", yawRPM);
                ini.Set(scriptName, "antiFlipRPM", antiFlipRPM);
                ini.Set(scriptName, "enableStrengthAdjust", enableStrengthAdjust);
                ini.Set(scriptName, "offset", offset);
                ini.Set(scriptName, "strengthProportional", strengthProportional);
                ini.Set(scriptName, "strengthLowerLimit", strengthLowerLimit);
                ini.Set(scriptName, "synchronizeStrength", synchronizeStrength);
                ini.Set(scriptName, "frictionRestoreTime", frictionRestoreTime);
                ini.Set(scriptName, "frictionRestoreOutsideDelay", frictionRestoreOutsideDelay);
                Me.CustomData = ini.ToString();
                return;
            }
            //---config section---//
            hydroneumatic = ini.Get(scriptName, "hydroneumatic").ToBoolean();
            keyAssign = (HydroneumaticInputKey)Enum.Parse(typeof(HydroneumaticInputKey), ini.Get(scriptName, "keyAssign").ToString());
            tiltSpeed = ini.Get(scriptName, "tiltSpeed").ToSingle();
            vehicleMode = (VehicleMode)Enum.Parse(typeof(VehicleMode), ini.Get(scriptName, "vehicleMode").ToString());
            skidFrictionCenter = ini.Get(scriptName, "skidFrictionCenter").ToSingle();
            skidFrictionSlide = ini.Get(scriptName, "skidFrictionSlide").ToSingle();
            skidPower = ini.Get(scriptName, "skidPower").ToSingle();
            gyroAssist = ini.Get(scriptName, "gyroAssist").ToBoolean();
            yawRPM = ini.Get(scriptName, "yawRPM").ToSingle();
            antiFlipRPM = ini.Get(scriptName, "antiFlipRPM").ToSingle();
            enableStrengthAdjust = ini.Get(scriptName, "enableStrengthAdjust").ToBoolean();
            offset = ini.Get(scriptName, "offset").ToSingle();
            strengthProportional = ini.Get(scriptName, "strengthProportional").ToSingle();
            strengthLowerLimit = ini.Get(scriptName, "strengthLowerLimit").ToSingle();
            synchronizeStrength = ini.Get(scriptName, "synchronizeStrength").ToBoolean();
            frictionRestoreTime = ini.Get(scriptName, "frictionRestoreTime").ToSingle();
            frictionRestoreOutsideDelay = ini.Get(scriptName, "frictionRestoreOutsideDelay").ToSingle();
            //---variable section---//
            /*  if(initial){
                    //load variable data
                    example = ini.Get(scriptName,"example").ToSingle();
                }else{
                    //save variable data
                    ini.Set(scriptName,"example",example);
                }
            */
            Me.CustomData = ini.ToString();
        }
    }
}
