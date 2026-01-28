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
		// Forward Speed Limiter + Cruise Control

		const double MAX_SPEED = 99.0;      // m/s
		const double SPEED_TOLERANCE = 0.5;  // m/s deadzone
		const double OVERRIDE_STEP = 0.02;   // cruise adjustment rate

		List<IMyThrust> brakingThrusters = new List<IMyThrust>();

		List<IMyThrust> forwardThrusters = new List<IMyThrust>();
		IMyShipController controller;

		bool cruiseMode = false;
		double currentOverride = 0.0;

		public Program()
		{
			Runtime.UpdateFrequency = UpdateFrequency.Update10;
			CacheBlocks();
		}

		public void Main(string argument, UpdateType updateSource)
		{
			if (!string.IsNullOrWhiteSpace(argument))
			{
				switch (argument.ToLower().Trim())
				{
					case "cruise":
						cruiseMode = !cruiseMode;
						break;
					case "cruiseon":
						cruiseMode = true;
						break;
					case "cruiseoff":
						cruiseMode = false;
						break;

				}
				ResetThrusters();
			}

			if (controller == null) return;

			Vector3D velocity = controller.GetShipVelocities().LinearVelocity;
			Vector3D forward = controller.WorldMatrix.Forward;
			double forwardSpeed = Vector3D.Dot(velocity, forward);

			if (cruiseMode)
				CruiseControl(forwardSpeed);
			else
				ManualLimiter(forwardSpeed);
		}

		void CacheBlocks()
		{
			forwardThrusters.Clear();
			brakingThrusters.Clear();

			var allThrusters = new List<IMyThrust>();
			GridTerminalSystem.GetBlocksOfType(allThrusters, t =>
				t.CubeGrid == Me.CubeGrid && t.IsFunctional);

			foreach (var t in allThrusters)
			{
				// Thrusters pushing ship forward
				if (t.Orientation.Forward == Base6Directions.Direction.Backward)
					forwardThrusters.Add(t);

				// Thrusters that brake forward motion
				else if (t.Orientation.Forward == Base6Directions.Direction.Forward)
					brakingThrusters.Add(t);
			}


			var controllers = new List<IMyShipController>();
			GridTerminalSystem.GetBlocksOfType(controllers, c =>
				c.CubeGrid == Me.CubeGrid && c.IsMainCockpit);

			if (controllers.Count == 0)
				GridTerminalSystem.GetBlocksOfType(controllers, c => c.CubeGrid == Me.CubeGrid);

			controller = controllers.Count > 0 ? controllers[0] : null;
		}

		void ManualLimiter(double speed)
		{
			bool allowThrust = speed < MAX_SPEED;

			// Restore braking thrusters
			foreach (var t in brakingThrusters)
			{
				t.Enabled = true;
				t.ThrustOverridePercentage = 0f;
			}

			// Normal forward thrust behavior
			foreach (var t in forwardThrusters)
			{
				t.ThrustOverridePercentage = 0f;
				t.Enabled = allowThrust;
			}

		}

		void CruiseControl(double speed)
		{
			double error = MAX_SPEED - speed;

			if (Math.Abs(error) < SPEED_TOLERANCE)
				return;

			if (error > 0)
				currentOverride += OVERRIDE_STEP;
			else
				currentOverride -= OVERRIDE_STEP;

			currentOverride = MathHelper.Clamp(currentOverride, 0f, 1f);

			// Disable braking thrusters so they don't fight cruise
			foreach (var t in brakingThrusters)
				t.Enabled = false;

			// Control forward thrust smoothly
			foreach (var t in forwardThrusters)
			{
				t.Enabled = true;
				t.ThrustOverridePercentage = (float)currentOverride;
			}

		}

		void ResetThrusters()
		{
			currentOverride = 0;

			foreach (var t in forwardThrusters)
			{
				t.ThrustOverridePercentage = 0f;
				t.Enabled = true;
			}

			foreach (var t in brakingThrusters)
			{
				t.ThrustOverridePercentage = 0f;
				t.Enabled = true;
			}

		}

	}
}
