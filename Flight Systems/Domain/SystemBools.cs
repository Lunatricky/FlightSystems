using IngameScript.Enums;

namespace IngameScript.Domain
{
    public struct SystemBools
    {
        public bool CruiseToggle;
        public bool OrbitToggle;
        public bool GlideToggle;
        public bool CNavToggle;
        public bool LandToggle;
        public bool SBurnToggle;
        public bool GpsToggle;
        public bool GpsMenuToggle;
        public bool LastCheckIsOnNatGrav;
        public bool StopCruiseWhenOutOfGrav;

        public void SetActiveMode(MainState modeName)
        {
            // Get current state of the target mode
            bool currentState = GetModeState(modeName);

            // Clear all modes
            CruiseToggle = false;
            OrbitToggle = false;
            GlideToggle = false;
            CNavToggle = false;
            LandToggle = false;
            SBurnToggle = false;
            GpsToggle = false;

            // Toggle the target mode (if it was true, now false; if false, now true)
            SetModeState(modeName, !currentState);
        }

        public bool GetModeState(MainState modeName)
        {
            switch (modeName)
            {
                case MainState.Cruise: return CruiseToggle;
                case MainState.Orbit: return OrbitToggle;
                case MainState.Glide: return GlideToggle;
                case MainState.CNav: return CNavToggle;
                case MainState.Land: return LandToggle;
                case MainState.SBurn: return SBurnToggle;
                case MainState.Gps: return GpsToggle;
                default: return false;
            }
        }

        private void SetModeState(MainState modeName, bool value)
        {
            switch (modeName)
            {
                case MainState.Cruise: CruiseToggle = value; break;
                case MainState.Orbit: OrbitToggle = value; break;
                case MainState.Glide: GlideToggle = value; break;
                case MainState.CNav: CNavToggle = value; break;
                case MainState.Land: LandToggle = value; break;
                case MainState.SBurn: SBurnToggle = value; break;
                case MainState.Gps: GpsToggle = value; break;
            }
        }
    }
}
