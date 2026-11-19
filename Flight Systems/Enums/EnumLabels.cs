namespace IngameScript.Enums
{
    static class EnumLabels
    {
        public static string Ship(ShipType t)
        {
            switch (t)
            {
                case ShipType.Atmo: return "Atmo";
                case ShipType.Space: return "Space";
                default: return "Interplanetary";
            }
        }

        public static string State(MainState t)
        {
            switch (t)
            {
                case MainState.Abort: return "Abort";
                case MainState.Reload: return "Reload";
                case MainState.Idle: return "Idle";
                case MainState.Cruise: return "Cruise";
                case MainState.Orbit: return "Orbit";
                case MainState.Glide: return "Glide";
                case MainState.CNav: return "CNav";
                case MainState.Land: return "Land";
                case MainState.SBurn: return "SBurn";
                case MainState.Gps: return "Gps";
                default: return "Idle";
            }
        }

        public static string Land(AutoLandState t)
        {
            switch (t)
            {
                case AutoLandState.Abort: return "Abort";
                case AutoLandState.Align: return "Align";
                case AutoLandState.Drop: return "Drop";
                case AutoLandState.Cushion: return "Cushion";
                case AutoLandState.LockGear: return "LockGear";
                default: return "Idle";
            }
        }

        public static string StepName(Step t)
        {
            switch (t)
            {
                case Step.On: return "On";
                case Step.Off: return "Off";
                case Step.AimToGPS: return "AimToGPS";
                case Step.Cruise: return "Cruise";
                case Step.Preclimb: return "Preclimb";
                case Step.Lift: return "Lift";
                case Step.Climb: return "Climb";
                case Step.Orbit: return "Orbit";
                default: return "Toggle";
            }
        }

        public static string TaskName(Task t)
        {
            switch (t)
            {
                case Task.ResetControllers: return "ResetControllers";
                case Task.IsDocked: return "IsDocked";
                case Task.CheckIni: return "CheckIni";
                case Task.PhysicsUpdate: return "PhysicsUpdate";
                case Task.FlightSystems: return "FlightSystems";
                default: return "LCDs";
            }
        }

    }
}
