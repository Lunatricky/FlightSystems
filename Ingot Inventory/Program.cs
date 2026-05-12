
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // What is the name of the container(s) used to store ingots 
        string tag = "[Ingots]";
        List<IMyTerminalBlock> ingotStorageContainers = new List<IMyTerminalBlock>();
        List<IMyTerminalBlock> lcds = new List<IMyTerminalBlock>();
        List<IMyTextSurface> surfaces = new List<IMyTextSurface>();

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            Echo("[Color=#FF00FF00]OK[/color] [Color=#FFFF0000]ERR[/color]");
            if (ingotStorageContainers.Count == 0)
            {
                ingotStorageContainers.Clear();
                GridTerminalSystem.GetBlocksOfType<IMyInventoryOwner>(ingotStorageContainers, block =>
                block.IsSameConstructAs(Me) &&
                block.CustomName.Contains(tag)
            );
            }
            if (surfaces.Count == 0)
            {
                lcds.Clear();
                surfaces.Clear();
                GridTerminalSystem.GetBlocksOfType<IMyTextSurfaceProvider>(lcds, block =>
                block.IsSameConstructAs(Me) &&
                block.CustomName.Contains(tag)
            );
                foreach (IMyTextSurfaceProvider surfaceProvider in lcds)
                {
                    // Only take the first surface (index 0)
                    if (surfaceProvider.SurfaceCount > 0)
                    {
                        IMyTextSurface surface = surfaceProvider.GetSurface(0);
                        surfaces.Add(surface);
                    }
                }
            }

            Echo("Lcds: " + lcds.Count);
            Echo("Containers: " + ingotStorageContainers.Count);

            CheckIngotStatus();
        }

        // Function to check ingot status 
        // and update a beacon accordingly 
        void CheckIngotStatus()
        {
            // Name of ingot types to workaround being unable to enumerate dictionary keys 
            string[] ingotNames = { "Stone", "Iron", "Nickel", "Silicon", "Cobalt", "Silver", "Gold", "Platinum", "Uranium", "Magnesium" };

            // Some magic numbers we use to try and align the output 
            // because the game uses a variable width font so 'ili' is much narrower 
            // than 'num' 
            Dictionary<String, int> stringDisplayLengths = new Dictionary<String, int>() {
              { "Nickel", 6 },
              { "Cobalt", 6 },
              { "Stone", 5 },
              { "Magnesium", 8 },
              { "Silver", 5 },
              { "Gold", 3 },
              { "Silicon", 6 },
              { "Uranium", 5},
              { "Platinum", 6 },
              { "Iron", 5 }
            };

            // Ingot names and the total amount we have will be stored in here 
            Dictionary<String, int> currentIngots = new Dictionary<String, int>() { };

            // Current inventory items will be temp stored here (One type of ingot might have multiple entries) 
            List<MyInventoryItem> allIngots = new List<MyInventoryItem>();

            // Loop through the containers 
            for (int i = 0; i < ingotStorageContainers.Count; i++)
            {
                var inventoryOwner = (IMyInventoryOwner)ingotStorageContainers[i];
                var sourceInventory = inventoryOwner.GetInventory(0);
                sourceInventory.GetItems(allIngots);
            }

            // Loop through the full list of inventory items (for all containers) 
            for (int i = 0; i < allIngots.Count; i++)
            {

                // If we've seen this ingot type already... 
                if (currentIngots.ContainsKey(allIngots[i].Type.SubtypeId))
                {

                    // ... increase the amount we're storing 
                    currentIngots[allIngots[i].Type.SubtypeId] = currentIngots[allIngots[i].Type.SubtypeId] + (int)allIngots[i].Amount;

                    // ... otherwise this is the first of this ingot type we've seen, so store the amount 
                }
                else
                {
                    currentIngots.Add(allIngots[i].Type.SubtypeId, (int)allIngots[i].Amount);
                }
            }

            StringBuilder stringBuilder = new StringBuilder();


            stringBuilder.AppendLine("Ingot Status");
            // Now we have a Dictionary of Ingot Types and the amount we're storing, lets 
            // put together the output which will have a new line per type in the form: 
            // Nickel     - Full (10000kg) 
            for (int i = 0; i < ingotNames.Length; ++i)
            {
                string name = ingotNames[i];
                double amount = currentIngots.ContainsKey(name) ? currentIngots[name] : 0;

                // Put the output together, padding the string  
                stringBuilder.AppendLine($"{name.PadRight(name.Length + 20 - (stringDisplayLengths[name] * 2), ' ')} - {amount/1000:F2} ton");
            }

            foreach (IMyTextSurface surface in surfaces)
            {
                surface.ContentType = ContentType.TEXT_AND_IMAGE;
                surface.Font = "DEBUG";
                surface.FontSize = 1.5f;
                surface.Alignment = TextAlignment.CENTER;
                surface.WriteText(stringBuilder.ToString(), false);
            }

        }
    }
}
