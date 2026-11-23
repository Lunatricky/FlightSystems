namespace IngameScript.Enums
{
    static class EnumLabels
    {
        public static string State(MainState t)
        {
            switch (t)
            {
                case MainState.Abort: return "Abort";
                case MainState.Reload: return "Reload";
                case MainState.Idle: return "Idle";
                case MainState.Cruise: return "Cruise";
                case MainState.Orbit: return "Orbit";
                case MainState.CNav: return "CNav";
                case MainState.Land: return "Land";
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

        public static bool TryState(string text, out MainState state)
        {
            if (text == State(MainState.Abort)) { state = MainState.Abort; return true; }
            if (text == State(MainState.Reload)) { state = MainState.Reload; return true; }
            if (text == State(MainState.Idle)) { state = MainState.Idle; return true; }
            if (text == State(MainState.Cruise)) { state = MainState.Cruise; return true; }
            if (text == State(MainState.Orbit)) { state = MainState.Orbit; return true; }
            if (text == State(MainState.CNav)) { state = MainState.CNav; return true; }
            if (text == State(MainState.Land)) { state = MainState.Land; return true; }
            if (text == State(MainState.Gps)) { state = MainState.Gps; return true; }
            state = MainState.Idle;
            return false;
        }

        public static bool TryLand(string text, out AutoLandState state)
        {
            if (text == Land(AutoLandState.Abort)) { state = AutoLandState.Abort; return true; }
            if (text == Land(AutoLandState.Align)) { state = AutoLandState.Align; return true; }
            if (text == Land(AutoLandState.Drop)) { state = AutoLandState.Drop; return true; }
            if (text == Land(AutoLandState.Cushion)) { state = AutoLandState.Cushion; return true; }
            if (text == Land(AutoLandState.LockGear)) { state = AutoLandState.LockGear; return true; }
            if (text == Land(AutoLandState.Idle)) { state = AutoLandState.Idle; return true; }
            state = AutoLandState.Idle;
            return false;
        }

        public static bool TryStep(string text, out Step step)
        {
            if (text == StepName(Step.On)) { step = Step.On; return true; }
            if (text == StepName(Step.Off)) { step = Step.Off; return true; }
            if (text == StepName(Step.AimToGPS)) { step = Step.AimToGPS; return true; }
            if (text == StepName(Step.Cruise)) { step = Step.Cruise; return true; }
            if (text == StepName(Step.Preclimb)) { step = Step.Preclimb; return true; }
            if (text == StepName(Step.Lift)) { step = Step.Lift; return true; }
            if (text == StepName(Step.Climb)) { step = Step.Climb; return true; }
            if (text == StepName(Step.Orbit)) { step = Step.Orbit; return true; }
            if (text == StepName(Step.Toggle)) { step = Step.Toggle; return true; }
            step = Step.Toggle;
            return false;
        }

    }
}
