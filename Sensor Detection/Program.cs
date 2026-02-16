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
        IMyTextSurface LCD;
        IMySensorBlock Sensor;
        StringBuilder output;

        public Program()
        {
            LCD = GridTerminalSystem.GetBlockWithName("Gay") as IMyTextSurface;
            Sensor = GridTerminalSystem.GetBlockWithName("Sensor") as IMySensorBlock;
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            output = new StringBuilder();
            List<MyDetectedEntityInfo> entities = new List<MyDetectedEntityInfo>();
            Sensor.DetectedEntities(entities);

            output.AppendLine(new string('-', 10) + "Plantir" + new string('-', 10));

            foreach (var entity in entities)
            {
                output.AppendLine(entity.Name + " | " + entity.EntityId);
                output.AppendLine($"Speed: {entity.Velocity.Length():F2}");
                output.AppendLine($"TimeStamp: {entity.TimeStamp:F2}");
                output.AppendLine($"Relationship: {entity.Relationship}");
                output.AppendLine($"Type: {entity.Type}");
                output.AppendLine($"Position: {entity.Position.X:F2}, {entity.Position.Y:F2}, {entity.Position.Z:F2}");
                output.AppendLine($"BoundingBox Min: {entity.BoundingBox.Min.X:F2}, {entity.BoundingBox.Min.Y:F2}, {entity.BoundingBox.Min.Z:F2}");
                output.AppendLine($"BoundingBox Max: {entity.BoundingBox.Max.X:F2}, {entity.BoundingBox.Max.Y:F2}, {entity.BoundingBox.Max.Z:F2}");
                output.AppendLine(new string('-', 27));
            }

            LCD.WriteText(output);
        }
    }
}
