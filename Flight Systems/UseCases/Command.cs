using IngameScript.Enums;
using IngameScript.Utils;
using System;
using VRageMath;

namespace IngameScript.UseCases
{
    class Command
    {
        public MainState State { get; set; }
        public CommandParam Param { get; set; }

        public void Empty(MainState ms = MainState.Idle)
        {
            State = ms;
            Param.Empty();
            Param.Step = Step.Toggle;
        }

        public Command()
        {
            State = MainState.Idle;
            Param = new CommandParam();
        }

        public Command(string argument)
        {
            var parts = argument.Trim().Split(
                new[] { ' ', '\t' },
                StringSplitOptions.RemoveEmptyEntries
            );

            if (parts.Length == 0)
                return;

            // First word = command (lowercase)
            State = TryParseArgument(parts[0].ToLowerInvariant());
            Param = new CommandParam();

            // No second part → no parameter
            if (parts.Length == 1) return;

            // Second part: try number, then string
            string second = parts[1].Trim();
            string end = argument.Substring(parts[0].Length + 1);

            double num;

            Vector3D v = new Vector3D();
            if (UtilsHelpder.TryParseGPS(end, out v))
            {
                Param.TargetCoordinates = v;
                return;
            }

            if (double.TryParse(second, out num))
                Param = new CommandParam(num);
            else
                Param = new CommandParam(TryParseStep(second));
        }

        static MainState TryParseArgument(string input)
        {
            MainState State;
            try
            {
                State = (MainState)Enum.Parse(typeof(MainState), input, true);
            }
            catch
            {
                State = MainState.Abort;
            }
            return State;
        }

        static Step TryParseStep(string input)
        {
            Step mainStateEnum;
            try
            {
                mainStateEnum = (Step)Enum.Parse(typeof(MainState), input, true);
            }
            catch
            {
                mainStateEnum = Step.Off;
            }
            return mainStateEnum;
        }
    }
}
