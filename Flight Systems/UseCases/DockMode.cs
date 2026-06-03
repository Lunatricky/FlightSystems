using IngameScript.Domain;

namespace IngameScript.UseCases
{
    class DockMode
    {
        readonly GridManager gm;
        Command command;

        bool isDockMode;

        private bool isDockMode1;

        public DockMode(GridManager gm, Command command)
        {
            this.gm = gm;
            this.command = command;
        }

        public bool IsDockMode
        {
            get
            {
                return isDockMode1;
            }

            set
            {
                isDockMode1 = value;
            }
        }

        public void DockStateSwitch()
        {
            switch (command.Param.Text.ToLowerInvariant())
            {
                case "toggle":
                case "":
                    isDockMode = !isDockMode;
                    if (isDockMode) command.Param.Text = "on";
                    else command.Param.Text = "off";
                    break;
                case "on":
                    isDockMode = true;
                    command = Command.Empty;
                    DockToggle();
                    break;

                case "off":
                    isDockMode = false;
                    command = Command.Empty;
                    DockToggle();
                    break;
            }
        }

        public void DockToggle()
        {
            isDockMode = gm.SetBlocks(isDockMode);
            gm.StockpileTanks(isDockMode);
            if (isDockMode)
            {
                gm.ChargeBatteries();
            }
            else
            {
                gm.AutoBatteries();
            }
        }
    }
}
