// =======================================================
// Manual Target Info Display
// Shows detailed info of the target you manually locked from cockpit
// =======================================================

using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        IMyTextSurface targetLCD;
        IMyShipController controller;
        List<IMyShipController> controllers;

        public Program()
        {
            // Change "Target LCD" to the exact name of your LCD
            targetLCD = GridTerminalSystem.GetBlockWithName("Target LCD") as IMyTextSurface;

            // Get the main cockpit / remote control (you can also use a specific one)
            GridTerminalSystem.GetBlocksOfType(controllers);

            controller = controllers[0];

            if (targetLCD == null)
                Echo("ERROR: LCD 'Target LCD' not found!");

            if (controller == null)
                Echo("ERROR: No ship controller found!");

            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }

        public void Main(string arg, UpdateType updateSource)
        {
            if (targetLCD == null || controller == null) return;

            var target = controller.GetTargetedEntity();

            var sb = new StringBuilder();

            if (target.EntityId == 0)
            {
                sb.AppendLine("=== NO TARGET LOCKED ===");
                sb.AppendLine("Lock a target from cockpit");
            }
            else
            {
                double distance = (target.Position - controller.GetPosition()).Length();

                Vector3D myVel = controller.GetShipVelocities().LinearVelocity;
                double relSpeed = (target.Velocity - myVel).Length();

                string relation = GetRelation(target.Relationship);

                sb.AppendLine("=== TARGET LOCKED ===");
                sb.AppendLine("Name: " + (string.IsNullOrEmpty(target.Name) ? "Unknown" : target.Name));
                sb.AppendLine("Type: " + target.Type.ToString());
                sb.AppendLine("Distance: " + distance.ToString("N0") + " m");
                sb.AppendLine("Rel Speed: " + relSpeed.ToString("N1") + " m/s");
                sb.AppendLine("Relation: " + relation);

                if (target.Type == MyDetectedEntityType.LargeGrid ||
                    target.Type == MyDetectedEntityType.SmallGrid)
                {
                    double approxMass = target.BoundingBox.Size.Length() * 800; // rough estimate
                    sb.AppendLine("Est. Mass: " + approxMass.ToString("N0") + " kg");
                }
            }

            targetLCD.ContentType = ContentType.TEXT_AND_IMAGE;
            targetLCD.WriteText(sb.ToString());
        }

        string GetRelation(MyRelationsBetweenPlayerAndBlock rel)
        {
            switch (rel)
            {
                case MyRelationsBetweenPlayerAndBlock.Owner: return "OWNER";
                case MyRelationsBetweenPlayerAndBlock.Friends: return "FRIENDLY";
                case MyRelationsBetweenPlayerAndBlock.FactionShare: return "FACTION";
                case MyRelationsBetweenPlayerAndBlock.Neutral: return "NEUTRAL";
                case MyRelationsBetweenPlayerAndBlock.Enemies: return "ENEMY";
                default: return "UNKNOWN";
            }
        }
    }
}