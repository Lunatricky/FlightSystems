// ╔════════════════════════════════════════════════════════════════════════════╗
// ║                  CustomData INI Configuration Template                     ║
// ║         (C#6 - Space Engineers Programmable Block - 2025/2026)            ║
// ╚════════════════════════════════════════════════════════════════════════════╝
//
// How to use:
// 1. Paste this into your PB script
// 2. Add your own variables in the Config class
// 3. Call LoadConfig() in Program() or Reload()
// 4. Use config.MyVariable anywhere in the script
// 5. Optional: SaveConfig() when you want to persist changes

using System;
using System.Collections.Generic;
using System.Text;
using VRageMath;
using Sandbox.ModAPI.Ingame;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // ────────────────────────────────────────────────
        // Your configuration class – add your variables here
        // ────────────────────────────────────────────────
        class Config
        {
            // General
            public string ScriptName = "My Script";
            public bool DebugEnabled = true;
            public double UpdateIntervalSec = 1.0;

            // Cruise control example
            public double CruiseSpeed = 95.0;
            public bool CruiseAutoStart = false;
            public bool StopOnNoGravity = true;

            // Landing example
            public double SafeLandingSpeed = 5.0;
            public double MaxPitchAngleDeg = 35.0;

            // Add more variables here as needed
            // public int    MaxAttempts       = 3;
            // public string TargetLCDName     = "Status LCD";
        }

        // Global config instance
        Config config = new Config();

        // ────────────────────────────────────────────────
        // Load config from CustomData (INI style)
        // ────────────────────────────────────────────────
        void LoadConfig()
        {
            if (string.IsNullOrWhiteSpace(Me.CustomData))
            {
                SaveDefaultConfig(); // create template if empty
                return;
            }

            var lines = Me.CustomData.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith(";") || trimmed.StartsWith("#"))
                    continue;

                int eqIndex = trimmed.IndexOf('=');
                if (eqIndex < 0) continue;

                string key = trimmed.Substring(0, eqIndex).Trim().ToLowerInvariant();
                string value = trimmed.Substring(eqIndex + 1).Trim();

                SetConfigValue(key, value);
            }

            Echo("Config loaded from CustomData.");
        }

        // ────────────────────────────────────────────────
        // Save current config back to CustomData
        // ────────────────────────────────────────────────
        void SaveConfig()
        {
            var sb = new StringBuilder();
            sb.AppendLine("; " + config.ScriptName + " Configuration");
            sb.AppendLine("; Edit values below. Lines starting with ; are comments.");
            sb.AppendLine("");

            sb.AppendLine("ScriptName=" + config.ScriptName);
            sb.AppendLine("DebugEnabled=" + config.DebugEnabled);
            sb.AppendLine("UpdateIntervalSec=" + config.UpdateIntervalSec);
            sb.AppendLine("");
            sb.AppendLine("; Cruise Settings");
            sb.AppendLine("CruiseSpeed=" + config.CruiseSpeed);
            sb.AppendLine("CruiseAutoStart=" + config.CruiseAutoStart);
            sb.AppendLine("StopOnNoGravity=" + config.StopOnNoGravity);
            sb.AppendLine("");
            sb.AppendLine("; Landing Settings");
            sb.AppendLine("SafeLandingSpeed=" + config.SafeLandingSpeed);
            sb.AppendLine("MaxPitchAngleDeg=" + config.MaxPitchAngleDeg);
            // Add more lines for new variables

            Me.CustomData = sb.ToString();
            Echo("Config saved to CustomData.");
        }

        // ────────────────────────────────────────────────
        // Create default config if CustomData is empty
        // ────────────────────────────────────────────────
        void SaveDefaultConfig()
        {
            SaveConfig(); // uses current config values (defaults)
            Echo("Default config created in CustomData.");
        }

        // ────────────────────────────────────────────────
        // Set a config value by key (case-insensitive)
        // ────────────────────────────────────────────────
        void SetConfigValue(string key, string value)
        {
            key = key.ToLowerInvariant();

            bool bVal;
            double dVal;
            int iVal;

            switch (key)
            {
                case "scriptname": config.ScriptName = value; break;
                case "debugenabled": config.DebugEnabled = bool.TryParse(value, out bVal) ? bVal : config.DebugEnabled; break;
                case "updateintervalsec": config.UpdateIntervalSec = double.TryParse(value, out dVal) ? dVal : config.UpdateIntervalSec; break;

                case "cruisespeed": config.CruiseSpeed = double.TryParse(value, out dVal) ? dVal : config.CruiseSpeed; break;
                case "cruiseautostart": config.CruiseAutoStart = bool.TryParse(value, out bVal) ? bVal : config.CruiseAutoStart; break;
                case "stoponno gravity": config.StopOnNoGravity = bool.TryParse(value, out bVal) ? bVal : config.StopOnNoGravity; break;

                case "safel landingspeed": config.SafeLandingSpeed = double.TryParse(value, out dVal) ? dVal : config.SafeLandingSpeed; break;
                case "maxpitchangledeg": config.MaxPitchAngleDeg = double.TryParse(value, out dVal) ? dVal : config.MaxPitchAngleDeg; break;

                // Add new cases here when adding variables
                default:
                    Echo($"Unknown config key: {key}");
                    break;
            }
        }

        // ────────────────────────────────────────────────
        // Example usage in Program() and Main()
        // ────────────────────────────────────────────────
        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;

            LoadConfig();           // Load settings from CustomData
            // SaveDefaultConfig(); // Uncomment to reset to defaults

            Echo($"Loaded config: {config.ScriptName} | Debug: {config.DebugEnabled}");
        }

        public void Main(string argument, UpdateType updateSource)
        {
            // Example: react to command to save config
            if (argument?.ToLower() == "saveconfig")
            {
                SaveConfig();
                return;
            }

            // Example usage of config values
            if (config.DebugEnabled)
            {
                Echo($"Cruise speed from config: {config.CruiseSpeed:F1} m/s");
            }

            // Your normal logic here...
        }
    }
}