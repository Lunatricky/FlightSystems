using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript.Domain
{
    public interface ICockpitSpriteSlots
    {
        string Lcd1 { get; }
        string Lcd2 { get; }
        string LcdSettings { get; }
    }

    public interface IBlockIniSync
    {
        bool Sync(IMyTerminalBlock block);
    }

    /// <summary>
    /// Copy-paste helper: tagged-cockpit Custom Data for sprite slots.
    /// Writes LCD1 / LCD2 / LCDSettings (empty until the player sets a surface index).
    /// Sync() rewrites the template and returns true when a value changed.
    /// </summary>
    public class CockpitSpriteIni : ICockpitSpriteSlots, IBlockIniSync
    {
        public const string Section = "Flight Systems";
        public const string LCD1 = "LCD1";
        public const string LCD2 = "LCD2";
        public const string LCDSETTINGS = "LCDSettings";

        readonly MyIni ini = new MyIni();
        readonly IniSnapshot snapshot = new IniSnapshot();

        public string Lcd1 { get; private set; }
        public string Lcd2 { get; private set; }
        public string LcdSettings { get; private set; }

        public bool Sync(IMyTerminalBlock block)
        {
            ini.Clear();
            if (block == null || !ini.TryParse(block.CustomData ?? ""))
                ini.Clear();

            Lcd1 = ini.Get(Section, LCD1).ToString("");
            Lcd2 = ini.Get(Section, LCD2).ToString("");
            LcdSettings = ini.Get(Section, LCDSETTINGS).ToString("");

            bool changed = false;
            changed |= snapshot.ReadAndDetectChange(ini, Section, LCD1, Lcd1 ?? "");
            changed |= snapshot.ReadAndDetectChange(ini, Section, LCD2, Lcd2 ?? "");
            changed |= snapshot.ReadAndDetectChange(ini, Section, LCDSETTINGS, LcdSettings ?? "");

            block.CustomData = ini.ToString();
            return changed;
        }

        public static bool TryParseSlot(string value, int surfaceCount, out int index)
        {
            index = -1;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            int parsed;
            if (!int.TryParse(value.Trim(), out parsed))
                return false;

            if (parsed < 0 || parsed >= surfaceCount)
                return false;

            index = parsed;
            return true;
        }
    }
}
