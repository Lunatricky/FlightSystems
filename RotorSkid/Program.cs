using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // Rotor skid-steer — MDK2 PB API only
        // W/S = throttle ramp. A/D = yaw. Space = throttle 0 + brake.
        // Custom Data INI on this PB. Optional [RotorDrive] on each rotor.

        const string SEC = "RotorDrive";

        MyIni _ini = new MyIni();
        MyIni _rotorIni = new MyIni();

        string _groupName;
        string _referenceName;
        float _maxSpeed;
        float _maxYawRate;
        float _maxRpm;
        float _wheelRadius;
        float _driveTorque;
        float _brakeTorque;
        float _deadzone;
        float _turnSlowdown;
        float _minYBrake;
        float _throttleRate;
        bool _idleCoast;
        bool _lockOnBrake;
        bool _invertSteer;
        bool _requirePilot;
        bool _autoInvert;

        float _throttle;

        List<IMyShipController> _controllers = new List<IMyShipController>();
        List<IMyMotorStator> _rotorBuf = new List<IMyMotorStator>();
        List<Wheel> _wheels = new List<Wheel>();
        IMyTerminalBlock _reference;
        string _error;  
        int _reinit;

        struct Wheel
        {
            public IMyMotorStator Rotor;
            public float Radius;
            public int UserSign;
        }

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            LoadIni();
            Discover();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (argument.Equals("reload", StringComparison.OrdinalIgnoreCase))
            {
                LoadIni();
                Discover();
                return;
            }

            _reinit++;
            if (_reinit >= 360)
            {
                _reinit = 0;
                Discover();
            }

            if (_wheels.Count == 0)
            {
                Echo(Status());
                return;
            }

            IMyShipController seat = GetSeat();
            if (seat == null)
            {
                if (_requirePilot)
                {
                    _throttle = 0f;
                    ApplyBrake();
                }
                Echo(Status());
                Echo("No pilot");
                return;
            }

            float dt = (float)Runtime.TimeSinceLastRun.TotalSeconds;
            if (dt < 0f)
                dt = 0f;
            if (dt > 0.1f)
                dt = 0.1f;

            Vector3 move = seat.MoveIndicator;
            // SE: W is negative Z. Positive input = add throttle.
            float throttleInput = Dead(Clamp1(-move.Z));
            float steer = Dead(Clamp1(move.X));
            if (_invertSteer)
                steer = -steer;

            bool brake = seat.HandBrake || move.Y >= _minYBrake;
            if (brake)
            {
                _throttle = 0f;
                ApplyBrake();
                Echo(Status());
                Echo("BRAKE");
                return;
            }

            _throttle += throttleInput * _throttleRate * dt;
            if (_throttle > 1f)
                _throttle = 1f;
            if (_throttle < -1f)
                _throttle = -1f;

            if (!_idleCoast && _throttle == 0f && steer == 0f)
            {
                ApplyBrake();
                Echo(Status());
                Echo("thr 0.00  str 0.00");
                return;
            }

            Drive(seat, _throttle, steer);
            Echo(Status());
            Echo("thr " + _throttle.ToString("0.00") + "  str " + steer.ToString("0.00"));
        }

        void Drive(IMyShipController seat, float throttle, float steer)
        {
            MatrixD wm = seat.WorldMatrix;
            Vector3D origin = seat.CenterOfMass;
            if (_reference != null)
                origin = _reference.GetPosition();

            double maxAbsX = 0.01;
            double[] xs = new double[_wheels.Count];
            int[] signs = new int[_wheels.Count];

            for (int i = 0; i < _wheels.Count; i++)
            {
                IMyMotorStator r = _wheels[i].Rotor;
                Vector3D local = Vector3D.TransformNormal(r.GetPosition() - origin, MatrixD.Transpose(wm));
                xs[i] = local.X;
                if (Math.Abs(local.X) > maxAbsX)
                    maxAbsX = Math.Abs(local.X);

                int axis = 1;
                if (_autoInvert)
                {
                    double align = r.WorldMatrix.Up.Dot(wm.Right);
                    if (align < -0.1)
                        axis = -1;
                }
                signs[i] = axis * _wheels[i].UserSign;
            }

            double v = throttle * _maxSpeed;
            double yawCap = _maxYawRate;
            if (yawCap <= 0)
                yawCap = _maxSpeed / maxAbsX;
            yawCap /= (1.0 + _turnSlowdown * Math.Abs(throttle));
            double omega = steer * yawCap;

            float[] rpm = new float[_wheels.Count];
            float peak = 0f;
            float cap = _maxRpm > 1f ? _maxRpm : 30f;

            for (int i = 0; i < _wheels.Count; i++)
            {
                double rad = _wheels[i].Radius;
                if (rad < 0.05)
                    rad = 0.05;
                float cmd = (float)((v - omega * xs[i]) / (2.0 * Math.PI * rad) * 60.0);
                rpm[i] = cmd;
                float ratio = Math.Abs(cmd) / cap;
                if (ratio > peak)
                    peak = ratio;
            }

            if (peak > 1f)
            {
                for (int i = 0; i < rpm.Length; i++)
                    rpm[i] /= peak;
            }

            for (int i = 0; i < _wheels.Count; i++)
            {
                IMyMotorStator r = _wheels[i].Rotor;
                if (!r.Enabled)
                    r.Enabled = true;
                if (r.RotorLock)
                    r.RotorLock = false;
                r.Torque = _driveTorque;
                r.TargetVelocityRPM = rpm[i] * signs[i];
            }
        }

        void ApplyBrake()
        {
            for (int i = 0; i < _wheels.Count; i++)
            {
                IMyMotorStator r = _wheels[i].Rotor;
                r.TargetVelocityRPM = 0f;
                r.Torque = _driveTorque;
                r.BrakingTorque = _brakeTorque;
                if (_lockOnBrake)
                    r.RotorLock = true;
                else if (r.RotorLock)
                    r.RotorLock = false;
            }
        }

        IMyShipController GetSeat()
        {
            IMyShipController fallback = null;
            for (int i = 0; i < _controllers.Count; i++)
            {
                IMyShipController c = _controllers[i];
                if (c == null || c.Closed || !c.CanControlShip)
                    continue;
                if (fallback == null)
                    fallback = c;
                if (c.IsUnderControl)
                    return c;
            }
            return _requirePilot ? null : fallback;
        }

        void Discover()
        {
            _error = null;
            _wheels.Clear();
            _controllers.Clear();
            _reference = null;

            GridTerminalSystem.GetBlocksOfType(_controllers, c => c.CanControlShip);

            if (!string.IsNullOrWhiteSpace(_referenceName))
            {
                _reference = GridTerminalSystem.GetBlockWithName(_referenceName);
                if (_reference == null)
                    _error = "ReferenceBlock not found: " + _referenceName;
            }

            IMyBlockGroup group = GridTerminalSystem.GetBlockGroupWithName(_groupName);
            if (group == null)
            {
                _error = "No group '" + _groupName + "'";
                return;
            }

            _rotorBuf.Clear();
            group.GetBlocksOfType(_rotorBuf);
            for (int i = 0; i < _rotorBuf.Count; i++)
            {
                IMyMotorStator r = _rotorBuf[i];
                if (r == null || !r.IsFunctional || !r.IsAttached)
                    continue;

                bool invert = false;
                float radius = _wheelRadius;
                if (!string.IsNullOrEmpty(r.CustomData) && _rotorIni.TryParse(r.CustomData))
                {
                    invert = _rotorIni.Get(SEC, "Invert").ToBoolean(false);
                    float rr = (float)_rotorIni.Get(SEC, "Radius").ToDouble(0);
                    if (rr > 0)
                        radius = rr;
                }
                if (radius <= 0)
                    radius = r.CubeGrid.GridSize * 1.5f;

                _wheels.Add(new Wheel
                {
                    Rotor = r,
                    Radius = radius,
                    UserSign = invert ? -1 : 1
                });
                r.BrakingTorque = _brakeTorque;
                r.Torque = _driveTorque;
            }

            if (_wheels.Count == 0 && _error == null)
                _error = "Group has no attached rotors";
        }

        void LoadIni()
        {
            _ini.Clear();
            _ini.TryParse(Me.CustomData);

            _groupName = _ini.Get(SEC, "GroupName").ToString("Rotor Wheels");
            _referenceName = _ini.Get(SEC, "ReferenceBlock").ToString("");
            _maxSpeed = (float)_ini.Get(SEC, "MaxSpeed").ToDouble(8);
            _maxYawRate = (float)_ini.Get(SEC, "MaxYawRate").ToDouble(0);
            _maxRpm = (float)_ini.Get(SEC, "MaxRpm").ToDouble(30);
            _wheelRadius = (float)_ini.Get(SEC, "WheelRadius").ToDouble(0);
            _driveTorque = (float)_ini.Get(SEC, "DriveTorque").ToDouble(20000000);
            _brakeTorque = (float)_ini.Get(SEC, "BrakeTorque").ToDouble(30000000);
            _idleCoast = _ini.Get(SEC, "IdleCoast").ToBoolean(false);
            _lockOnBrake = _ini.Get(SEC, "RotorLockOnBrake").ToBoolean(false);
            _invertSteer = _ini.Get(SEC, "InvertSteer").ToBoolean(false);
            _autoInvert = _ini.Get(SEC, "AutoInvert").ToBoolean(true);
            _deadzone = (float)_ini.Get(SEC, "Deadzone").ToDouble(0.05);
            _turnSlowdown = (float)_ini.Get(SEC, "TurnSlowdown").ToDouble(1);
            _minYBrake = (float)_ini.Get(SEC, "MinInputYForBrake").ToDouble(0.5);
            _requirePilot = _ini.Get(SEC, "RequirePilot").ToBoolean(true);
            _throttleRate = (float)_ini.Get(SEC, "ThrottleRate").ToDouble(1);

            _ini.Set(SEC, "GroupName", _groupName);
            _ini.Set(SEC, "ReferenceBlock", _referenceName);
            _ini.Set(SEC, "MaxSpeed", _maxSpeed);
            _ini.Set(SEC, "MaxYawRate", _maxYawRate);
            _ini.Set(SEC, "MaxRpm", _maxRpm);
            _ini.Set(SEC, "WheelRadius", _wheelRadius);
            _ini.Set(SEC, "DriveTorque", _driveTorque);
            _ini.Set(SEC, "BrakeTorque", _brakeTorque);
            _ini.Set(SEC, "IdleCoast", _idleCoast);
            _ini.Set(SEC, "RotorLockOnBrake", _lockOnBrake);
            _ini.Set(SEC, "InvertSteer", _invertSteer);
            _ini.Set(SEC, "AutoInvert", _autoInvert);
            _ini.Set(SEC, "Deadzone", _deadzone);
            _ini.Set(SEC, "TurnSlowdown", _turnSlowdown);
            _ini.Set(SEC, "MinInputYForBrake", _minYBrake);
            _ini.Set(SEC, "RequirePilot", _requirePilot);
            _ini.Set(SEC, "ThrottleRate", _throttleRate);

            string text = _ini.ToString();
            if (Me.CustomData != text)
                Me.CustomData = text;
        }

        string Status()
        {
            string s = "Rotor skid-steer\nWheels: " + _wheels.Count + "\n";
            if (_error != null)
                s += _error + "\n";
            s += "Run reload to rediscover";
            return s;
        }

        float Dead(float v)
        {
            return Math.Abs(v) < _deadzone ? 0f : v;
        }

        float Clamp1(float v)
        {
            if (v > 1f) return 1f;
            if (v < -1f) return -1f;
            return v;
        }
    }
}
