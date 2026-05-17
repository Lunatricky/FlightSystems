using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    class ShipContext
    {

        IMyGridTerminalSystem gridTS;
        IMyProgrammableBlock me;
        string gridName;
        string ignoreTag;
        readonly StringBuilder errorMessage;

        double centerGridHight;
        double bottomGridHight;
        double gridHeight;
        double h2CapacityPercent;

        IMyRemoteControl controller;
        IMyBatteryBlock backupBattery;

        List<IMyFunctionalBlock> controlledBlocks = new List<IMyFunctionalBlock>();
        List<IMyFunctionalBlock> controlledToolBlocks = new List<IMyFunctionalBlock>();
        List<IMyFunctionalBlock> overrideBlocks = new List<IMyFunctionalBlock>();
        List<IMyShipConnector> connectors = new List<IMyShipConnector>();
        List<IMyGasTank> tanks = new List<IMyGasTank>();
        List<IMyGasTank> h2Tanks = new List<IMyGasTank>();
        List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
        List<IMyRadioAntenna> antennas = new List<IMyRadioAntenna>();
        List<IMyShipController> controllers = new List<IMyShipController>();

        List<IMyThrust> breakingThrusters = new List<IMyThrust>();
        List<IMyThrust> forwardThrusters = new List<IMyThrust>();
        List<IMyThrust> upwardThrusters = new List<IMyThrust>();

        List<IMyGyro> gyros = new List<IMyGyro>();
        List<IMyLandingGear> gears = new List<IMyLandingGear>();
        List<IMyTextSurface> lcds1 = new List<IMyTextSurface>();
        List<IMyTextSurface> lcds2 = new List<IMyTextSurface>();

        List<IMyTextSurface> surfaces = new List<IMyTextSurface>();

        public ShipContext(IMyGridTerminalSystem grid, IMyProgrammableBlock me, string ignoreTag)
        {
            errorMessage = new StringBuilder();
            GridTS = grid;
            Me = me;

            string tempGridName = Me.CubeGrid.CustomName;
            if (!string.IsNullOrWhiteSpace(tempGridName) && !tempGridName.Contains(" Grid "))
                GridName = tempGridName;

            this.ignoreTag = ignoreTag;

            SetupSurface(me.GetSurface(0), 1.1f);
        }

        public ShipContext ReloadControllers(string controllerTag)
        {
            Controllers.Clear();
            Controller = null;

            List<IMyRemoteControl> remotes = new List<IMyRemoteControl>();
            List<IMyCockpit> cockpits = new List<IMyCockpit>();

            GridHelper.GetOwnGridBlocks(remotes, this, ignoreTag);
            GridHelper.GetOwnGridBlocks(cockpits, this, ignoreTag);

            foreach (IMyRemoteControl remote in remotes)
            {
                if (Controller == null && remote.CustomName.Contains(controllerTag.ToLower()))
                    Controller = remote;
                Controllers.Add(remote);
            }

            if (Controller == null && remotes.Count > 0)
                Controller = remotes.First();
            else
            {
                ErrorMessage.AppendLine("===============================");
                ErrorMessage.AppendLine("No Remote Control block found!");
                ErrorMessage.AppendLine("Place a RC on the grid facing forward.");
                ErrorMessage.AppendLine("Or name a RC with Reference in it's name, facing forward, in case you need RCs in different directions.");
                ErrorMessage.AppendLine("===============================");
            }

            foreach (IMyCockpit cockpit in cockpits)
            {
                Controllers.Add(cockpit);
            }
            return this;
        }

        public ShipContext ReloadGridHeight()
        {
            Vector3D gravityDir = Vector3D.Normalize(Controller.GetNaturalGravity());

            Vector3D center = Me.CubeGrid.WorldVolume.Center;
            Vector3D shipBottom = VectorHelper.GetLowestPoint(this);

            // project onto gravity vector
            CenterGridHeight = center.Dot(gravityDir);
            BottomGridHeight = shipBottom.Dot(gravityDir);

            // height difference along gravity
            GridHeight = Math.Abs(CenterGridHeight - BottomGridHeight);
            return this;
        }

        public ShipContext ReloadThrusters()
        {
            ForwardThrusters.Clear();
            BreakingThrusters.Clear();
            UpwardThrusters.Clear();

            List<IMyThrust> allThrusters = new List<IMyThrust>();
            GridHelper.GetOwnGridBlocks(allThrusters, this, ignoreTag);

            foreach (var thruster in allThrusters)
            {
                // Thrusters that push the ship forward
                if (thruster.Orientation.Forward == Base6Directions.GetOppositeDirection(Controller.Orientation.Forward))
                    ForwardThrusters.Add(thruster);

                // Thrusters that push the ship backward
                else if (thruster.Orientation.Forward == Controller.Orientation.Forward)
                    BreakingThrusters.Add(thruster);

                // Thrusters that push the ship upwards
                else if (thruster.Orientation.Forward == Base6Directions.GetOppositeDirection(Controller.Orientation.Up))
                    UpwardThrusters.Add(thruster);
            }
            return this;
        }

        public ShipContext ReloadGyros()
        {
            GridHelper.GetOwnGridBlocks(Gyros, this, ignoreTag);
            return this;
        }

        public ShipContext ReloadGears()
        {
            GridHelper.GetOwnGridBlocks(Gears, this, ignoreTag);
            return this;
        }

        public ShipContext ReloadAntennas(bool controlAntennas)
        {
            GridHelper.GetOwnGridBlocks(Antennas, this, ignoreTag);
            if (controlAntennas)
            {
                foreach (IMyRadioAntenna antenna in Antennas)
                {
                    if (string.IsNullOrEmpty(antenna.HudText)) antenna.HudText = GridName;
                }
            }
            return this;
        }

        public ShipContext ReloadLCDs(string lcd1Tag, string lcd2Tag)
        {
            Lcds1 = AddLCDsToList(lcd1Tag, false, true);
            Lcds2 = AddLCDsToList(lcd2Tag, false, true);

            return this;
        }

        public ShipContext ReloadSurfaces()
        {
            Surfaces = AddLCDsToList(ignoreTag, true);

            return this;
        }

        private List<IMyTextSurface> AddLCDsToList(string LCD_TAG = "", bool isIgnoreTag = false, bool setupSurface = false)
        {
            List<IMyTextSurface> lcds = new List<IMyTextSurface>();
            // LCDs
            var blocks = new List<IMyTerminalBlock>();
            if (isIgnoreTag)
            {
                GridTS.GetBlocksOfType<IMyTextSurfaceProvider>(blocks, block =>
                    block.IsSameConstructAs(Me) &&
                    !block.CustomName.Contains(LCD_TAG) &&
                    !block.CustomData.Contains(LCD_TAG)
                );
            } else
            {
                GridTS.GetBlocksOfType<IMyTextSurfaceProvider>(blocks, block =>
                    block.IsSameConstructAs(Me) &&
                    (block.CustomName.Contains(LCD_TAG)||
                    block.CustomData.Contains(LCD_TAG))
                );
            }

            foreach (IMyTextSurfaceProvider surfaceProvider in blocks)
            {
                // Only take the first surface (index 0)
                if (surfaceProvider.SurfaceCount > 0)
                {
                    for(int i = 0; i < surfaceProvider.SurfaceCount; i++)
                    {
                        IMyTextSurface surface = surfaceProvider.GetSurface(i);
                        if (setupSurface) SetupSurface(surface);
                        lcds.Add(surface);
                    }
                }
            }
            return lcds;
        }

        public static IMyTextSurface SetupSurface(IMyTextSurface surface, float fontSize = 1.7f)
        {
            surface.ContentType = ContentType.TEXT_AND_IMAGE;
            surface.Font = "DEBUG";
            surface.FontSize = fontSize;
            surface.Alignment = TextAlignment.LEFT;
            return surface;
        }

        public static void PaintSurface(IMyTextSurface surface, Color BackgroundColor, Color FontColor)
        {
            surface.BackgroundColor = BackgroundColor;
            surface.FontColor = FontColor;
            surface.ScriptBackgroundColor = BackgroundColor;
            surface.ScriptForegroundColor = FontColor;
        }

        public ShipContext ReloadConnectors()
        {
            // Connectors, Tanks & Batteries (own construct only)
            GridHelper.GetOwnGridBlocks(Connectors, this, ignoreTag);
            SetConnectors();

            return this;
        }

        public ShipContext ReloadTanks()
        {
            GridHelper.GetOwnGridBlocks(Tanks, this, ignoreTag);

            return this;
        }

        public ShipContext ReloadH2Tanks()
        {
            GridHelper.GetOwnGridBlocks(Tanks, this, ignoreTag);

            foreach (IMyGasTank tank in Tanks)
            {
                if (IsHydrogenTank(tank))
                {
                    H2Tanks.Add(tank);
                }
            }
            return this;
        }

        public ShipContext ReloadBatteries(string backupTag)
        {
            GridHelper.GetOwnGridBlocks(Batteries, this, ignoreTag);

            // Backup Battery
            if (BackupBattery == null || BackupBattery.Closed)
            {
                foreach (var battery in Batteries)
                {
                    if (!battery.Closed && battery.CustomName.ToLower().Contains(backupTag.ToLower()))
                    {
                        BackupBattery = battery;
                        break;
                    }
                }
                Batteries.Remove(BackupBattery);
            }

            if ((BackupBattery == null || BackupBattery.Closed) && Batteries.Count > 1)
            {
                BackupBattery = Batteries.First();
                BackupBattery.CustomName = BackupBattery.CustomName + " " + backupTag;
            }

            return this;
        }

        public ShipContext ReloadControlledBlocks(string dockGroupTag)
        {
            ControlledBlocks.Clear();

            IMyBlockGroup group = GridTS.GetBlockGroupWithName(dockGroupTag);

            if (ControlledBlocks.Count == 0 && group != null)
            {
                List<IMyFunctionalBlock> blocksGroup = new List<IMyFunctionalBlock>();
                group.GetBlocksOfType(blocksGroup);
                ControlledBlocks.AddList(blocksGroup);
            }
            else
            {
                ReloadControlledBlocks();
                ControlledBlocks.AddList(OverrideBlocks);
                ControlledBlocks.Remove(Me);
            }
            return this;
        }

        public ShipContext ReloadOverrideGroup(string __overrideBlockTag)
        {
            OverrideBlocks.Clear();

            var blocks = new List<IMyFunctionalBlock>();
            GridTS.GetBlocksOfType(blocks, b =>
                b.IsSameConstructAs(Me) &&
                b.CustomName.Contains(__overrideBlockTag)
            );

            foreach (IMyFunctionalBlock block in blocks)
            {
                if (block.IsSameConstructAs(Me))
                    OverrideBlocks.Add(block);
            }
            return this;
        }

        private void SetConnectors()
        {
            foreach (IMyShipConnector connector in Connectors)
            {
                connector.IsParkingEnabled = false;
                connector.PullStrength = 0.00005f;
            }
        }

        void ReloadControlledBlocks()
        {
            ControlledBlocks.Clear();
            ControlledToolBlocks.Clear();

            AddBlocks<IMyShipToolBase>(ControlledToolBlocks);
            AddBlocks<IMyThrust>(ControlledBlocks);
            AddBlocks<IMyMechanicalConnectionBlock>(ControlledBlocks);
            AddBlocks<IMyReflectorLight>(ControlledBlocks);
            AddBlocks<IMySearchlight>(ControlledBlocks);
            AddBlocks<IMySensorBlock>(ControlledBlocks);
            AddBlocks<IMyLaserAntenna>(ControlledBlocks);
            AddBlocks<IMyRadioAntenna>(ControlledBlocks);
            AddBlocks<IMyBeacon>(ControlledBlocks);
            AddBlocks<IMyOreDetector>(ControlledBlocks);
            AddBlocks<IMyTextPanel>(ControlledBlocks);
            AddBlocks<IMyProgrammableBlock>(ControlledBlocks);
        }

        void AddBlocks<T>(List<IMyFunctionalBlock> blocks) where T : class, IMyFunctionalBlock
        {
            var tempList = new List<T>();

            GridTS.GetBlocksOfType(tempList, tempBlock =>
                tempBlock.IsSameConstructAs(Me) &&
                !ContainsIgnore(tempBlock.CustomName)
            );

            foreach (var block in tempList)
                blocks.Add(block);
        }

        bool ContainsIgnore(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            return text.IndexOf("ignore", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        bool IsHydrogenTank(IMyGasTank tank)
        {
            return tank.BlockDefinition.SubtypeName
                .IndexOf("Hydrogen", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public IMyGridTerminalSystem GridTS
        {
            get
            {
                return gridTS;
            }

            set
            {
                gridTS = value;
            }
        }

        public IMyProgrammableBlock Me
        {
            get
            {
                return me;
            }

            set
            {
                me = value;
            }
        }

        public string GridName
        {
            get
            {
                return gridName;
            }

            set
            {
                gridName = value;
            }
        }

        public double CenterGridHeight
        {
            get
            {
                return centerGridHight;
            }

            set
            {
                centerGridHight = value;
            }
        }

        public double BottomGridHeight
        {
            get
            {
                return bottomGridHight;
            }

            set
            {
                bottomGridHight = value;
            }
        }

        public double GridHeight
        {
            get
            {
                return gridHeight;
            }

            set
            {
                gridHeight = value;
            }
        }

        public double H2CapacityPercent
        {
            get
            {
                return h2CapacityPercent;
            }

            set
            {
                h2CapacityPercent = value;
            }
        }

        public IMyRemoteControl Controller
        {
            get
            {
                return controller;
            }

            set
            {
                controller = value;
            }
        }

        public IMyBatteryBlock BackupBattery
        {
            get
            {
                return backupBattery;
            }

            set
            {
                backupBattery = value;
            }
        }

        public List<IMyFunctionalBlock> ControlledBlocks
        {
            get
            {
                return controlledBlocks;
            }

            set
            {
                controlledBlocks = value;
            }
        }

        public List<IMyFunctionalBlock> ControlledToolBlocks
        {
            get
            {
                return controlledToolBlocks;
            }

            set
            {
                controlledToolBlocks = value;
            }
        }

        public List<IMyFunctionalBlock> OverrideBlocks
        {
            get
            {
                return overrideBlocks;
            }

            set
            {
                overrideBlocks = value;
            }
        }

        public List<IMyShipConnector> Connectors
        {
            get
            {
                return connectors;
            }

            set
            {
                connectors = value;
            }
        }

        public List<IMyGasTank> Tanks
        {
            get
            {
                return tanks;
            }

            set
            {
                tanks = value;
            }
        }

        public List<IMyGasTank> H2Tanks
        {
            get
            {
                return h2Tanks;
            }

            set
            {
                h2Tanks = value;
            }
        }

        public List<IMyBatteryBlock> Batteries
        {
            get
            {
                return batteries;
            }

            set
            {
                batteries = value;
            }
        }

        public List<IMyRadioAntenna> Antennas
        {
            get
            {
                return antennas;
            }

            set
            {
                antennas = value;
            }
        }

        public List<IMyShipController> Controllers
        {
            get
            {
                return controllers;
            }

            set
            {
                controllers = value;
            }
        }

        public List<IMyThrust> BreakingThrusters
        {
            get
            {
                return breakingThrusters;
            }

            set
            {
                breakingThrusters = value;
            }
        }

        public List<IMyThrust> ForwardThrusters
        {
            get
            {
                return forwardThrusters;
            }

            set
            {
                forwardThrusters = value;
            }
        }

        public List<IMyThrust> UpwardThrusters
        {
            get
            {
                return upwardThrusters;
            }

            set
            {
                upwardThrusters = value;
            }
        }

        public List<IMyGyro> Gyros
        {
            get
            {
                return gyros;
            }

            set
            {
                gyros = value;
            }
        }

        public List<IMyLandingGear> Gears
        {
            get
            {
                return gears;
            }

            set
            {
                gears = value;
            }
        }

        public List<IMyTextSurface> Lcds1
        {
            get
            {
                return lcds1;
            }

            set
            {
                lcds1 = value;
            }
        }

        public List<IMyTextSurface> Lcds2
        {
            get
            {
                return lcds2;
            }

            set
            {
                lcds2 = value;
            }
        }

        public StringBuilder ErrorMessage => errorMessage;

        public List<IMyTextSurface> Surfaces
        {
            get
            {
                return surfaces;
            }

            set
            {
                surfaces = value;
            }
        }
    }
}
