using System.Collections.Generic;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript.Domain
{
    /// <summary>
    /// Copy-paste helper: last-known INI values plus ReadAndDetectChange.
    /// One instance per Custom Data block (PB, cockpit, …).
    /// </summary>
    public class IniSnapshot
    {
        readonly Dictionary<string, string> values = new Dictionary<string, string>();

        public bool ReadAndDetectChange(MyIni ini, string section, string key, object newVal)
        {
            ini.Set(section, key, newVal.ToString());

            string old;
            string newValString = newVal.ToString();
            values.TryGetValue(key, out old);
            if (old != newValString)
            {
                values[key] = newValString;
                return true;
            }
            return false;
        }
    }
}
