using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript.Domain
{
    public class GridContext
    {
        IMyGridTerminalSystem gridTS;
        IMyProgrammableBlock me;
        string gridName;
        string ignoreTag;
        readonly StringBuilder errorMessage;

        double centerGridHeight;
        double bottomGridHeight;
        double gridHeight;

        public bool IsLG;

        IMyRemoteControl controller;
        IMyBatteryBlock backupBattery;

        List<IMyFunctionalBlock> controlledBlocks = new List<IMyFunctionalBlock>();
        List<IMyFunctionalBlock> controlledToolBlocks = new List<IMyFunctionalBlock>();
        List<IMyShipConnector> connectors = new List<IMyShipConnector>();
        List<IMyGasTank> tanks = new List<IMyGasTank>();
        List<IMyGasTank> h2Tanks = new List<IMyGasTank>();
        List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
        List<IMyRadioAntenna> antennas = new List<IMyRadioAntenna>();
        List<IMyShipController> controllers = new List<IMyShipController>();

        List<IMyThrust> thrusters = new List<IMyThrust>();
        List<IMyThrust> breakingThrusters = new List<IMyThrust>();
        List<IMyThrust> forwardThrusters = new List<IMyThrust>();
        List<IMyThrust> upwardThrusters = new List<IMyThrust>();

        List<IMyGyro> gyros = new List<IMyGyro>();
        List<IMyLandingGear> gears = new List<IMyLandingGear>();
        List<IMyTextSurface> lcds1 = new List<IMyTextSurface>();
        List<IMyTextSurface> lcds2 = new List<IMyTextSurface>();

        List<IMyTextSurface> surfaces = new List<IMyTextSurface>();

        public GridContext(IMyGridTerminalSystem grid, IMyProgrammableBlock me)
        {
            errorMessage = new StringBuilder();
            GridTS = grid;
            Me = me;
            IsLG = Me.CubeGrid.GridSizeEnum.Equals(MyCubeSize.Large);
            string tempGridName = Me.CubeGrid.CustomName;
            if (!string.IsNullOrWhiteSpace(tempGridName) && !tempGridName.Contains(" Grid "))
                GridName = tempGridName;

            SetupSurface(me.GetSurface(0), 1.1f);
        }

        public GridContext ReloadControllers(string controllerTag)
        {
            Controllers.Clear();
            Controller = null;

            List<IMyRemoteControl> remotes = new List<IMyRemoteControl>();
            List<IMyCockpit> cockpits = new List<IMyCockpit>();

            GridManager.GetOwnGridBlocks(remotes, this, IgnoreTag);
            GridManager.GetOwnGridBlocks(cockpits, this, IgnoreTag);

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

        public GridContext ReloadGridHeight()
        {
            Vector3D gravityDir = Vector3D.Normalize(Controller.GetNaturalGravity());

            Vector3D center = Me.CubeGrid.WorldVolume.Center;
            Vector3D shipBottom = VectorHelper.GetLowestPoint(this);

            // project onto gravity vector
            centerGridHeight = center.Dot(gravityDir);
            bottomGridHeight = shipBottom.Dot(gravityDir);

            // height difference along gravity
            GridHeight = Math.Abs(centerGridHeight - bottomGridHeight);
            return this;
        }

        public GridContext ReloadThrusters()
        {
            Thrusters.Clear();
            ForwardThrusters.Clear();
            BreakingThrusters.Clear();
            UpwardThrusters.Clear();

            GridManager.GetOwnGridBlocks(Thrusters, this, IgnoreTag);

            foreach (var thruster in Thrusters)
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

        public GridContext ReloadGyros()
        {
            GridManager.GetOwnGridBlocks(Gyros, this, IgnoreTag);
            return this;
        }

        public GridContext ReloadGears()
        {
            GridManager.GetOwnGridBlocks(Gears, this, IgnoreTag);
            return this;
        }

        public GridContext ReloadAntennas(bool controlAntennas)
        {
            GridManager.GetOwnGridBlocks(Antennas, this, IgnoreTag);
            if (controlAntennas)
            {
                foreach (IMyRadioAntenna antenna in Antennas)
                {
                    if (string.IsNullOrEmpty(antenna.HudText)) antenna.HudText = GridName;
                }
            }
            return this;
        }

        public GridContext ReloadLCDs(string lcd1Tag, string lcd2Tag)
        {
            Lcds1.AddList(AddLCDsToList(lcd1Tag, false, true));
            Lcds2.AddList(AddLCDsToList(lcd2Tag, false, true));

            return this;
        }

        public GridContext ReloadSurfaces()
        {
            surfaces.AddList(AddLCDsToList(ignoreTag, true));

            return this;
        }

        List<IMyTextSurface> AddLCDsToList(string LCD_TAG = "", bool isIgnoreTag = false, bool setupSurface = false)
        {
            List<IMyTextSurface> lcds = new List<IMyTextSurface>();
            
            var blocks = new List<IMyTerminalBlock>();
            if (isIgnoreTag)
            {
                GridTS.GetBlocksOfType<IMyTextSurfaceProvider>(blocks, block =>
                    block.IsSameConstructAs(Me) &&
                    !block.CustomName.Contains(LCD_TAG) &&
                    !block.CustomData.Contains(LCD_TAG)
                );
            }
            else
            {
                GridTS.GetBlocksOfType<IMyTextSurfaceProvider>(blocks, block =>
                    block.IsSameConstructAs(Me) &&
                    block.CustomName.Contains(LCD_TAG)
                );
            }

            foreach (IMyTextSurfaceProvider surfaceProvider in blocks)
            {
                // Only take the first surface (index 0)
                if (surfaceProvider.SurfaceCount > 0)
                {
                    for (int i = 0; i < surfaceProvider.SurfaceCount; i++)
                    {
                        IMyTextSurface surface = surfaceProvider.GetSurface(i);
                        if (setupSurface) SetupSurface(surface);
                        lcds.Add(surface);
                    }
                }
            }

            foreach (IMyTextSurface lcd in lcds)
            {
                lcd.AddImageToSelection("Online");
                lcd.RemoveImageFromSelection("Online");
                lcd.ContentType = ContentType.SCRIPT;
                lcd.Script = "";
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

        public static void PaintSurfaces(IniContext ic, List<IMyTextSurface> surfaces)
        {
            foreach (IMyTextSurface surface in surfaces)
            {
                Color backgroundColor = ColorMap.GetColorFromString(ic.BackgroundColor);
                Color fontColor = ColorMap.GetColorFromString(ic.FontColor);

                PaintSurface(surface, backgroundColor, fontColor);
            }
        }

        static void PaintSurface(IMyTextSurface surface, Color BackgroundColor, Color FontColor)
        {
            surface.BackgroundColor = BackgroundColor;
            surface.FontColor = FontColor;
            surface.ScriptBackgroundColor = BackgroundColor;
            surface.ScriptForegroundColor = FontColor;
        }

        public GridContext ReloadConnectors()
        {
            // Connectors, Tanks & Batteries (own construct only)
            GridManager.GetOwnGridBlocks(Connectors, this, IgnoreTag);
            SetConnectors();

            return this;
        }

        public GridContext ReloadTanks()
        {
            GridManager.GetOwnGridBlocks(Tanks, this, IgnoreTag);

            return this;
        }

        public GridContext ReloadH2Tanks()
        {
            GridManager.GetOwnGridBlocks(Tanks, this, IgnoreTag);

            foreach (IMyGasTank tank in Tanks)
            {
                if (IsHydrogenTank(tank))
                {
                    H2Tanks.Add(tank);
                }
            }
            return this;
        }

        public GridContext ReloadBatteries(string backupTag)
        {
            GridManager.GetOwnGridBlocks(Batteries, this, IgnoreTag);

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

        public GridContext ReloadControlledBlocks(string dockGroupTag, string overrideBlockTag = "")
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
                ControlledBlocks.AddList(ReloadOverrideGroup(overrideBlockTag));
                ControlledBlocks.Remove(Me);
            }
            return this;
        }

        List<IMyFunctionalBlock> ReloadOverrideGroup(string overrideBlockTag)
        {
            List<IMyFunctionalBlock> OverrideBlocks = new List<IMyFunctionalBlock>();
            GridTS.GetBlocksOfType(OverrideBlocks, block =>
                block.IsSameConstructAs(Me) && (!block.CustomData.Contains("Flight Systems")) &&
                (block.CustomName.Contains(overrideBlockTag) || block.CustomData.Contains(overrideBlockTag))
                //TODO improve this!
            );
            return OverrideBlocks;
        }

        void SetConnectors()
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
                !tempBlock.CustomName.Contains(IgnoreTag) &&
                !tempBlock.CustomData.Contains(IgnoreTag)
            );

            foreach (var block in tempList)
                blocks.Add(block);
        }

        bool IsHydrogenTank(IMyGasTank tank)
        {
            return tank.BlockDefinition.SubtypeName
                .IndexOf("Hydrogen", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public string IgnoreTag
        {
            get
            {
                return ignoreTag;
            }

            set
            {
                ignoreTag = value;
            }
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

        public List<IMyFunctionalBlock> ControlledBlocks => controlledBlocks;
        public List<IMyFunctionalBlock> ControlledToolBlocks => controlledToolBlocks;
        public List<IMyShipConnector> Connectors => connectors;
        public List<IMyGasTank> Tanks => tanks;
        public List<IMyGasTank> H2Tanks => h2Tanks;
        public List<IMyBatteryBlock> Batteries => batteries;
        public List<IMyRadioAntenna> Antennas => antennas;
        List<IMyShipController> Controllers => controllers;
        public List<IMyThrust> Thrusters => thrusters;
        public List<IMyThrust> BreakingThrusters => breakingThrusters;
        public List<IMyThrust> ForwardThrusters => forwardThrusters;
        public List<IMyThrust> UpwardThrusters => upwardThrusters;
        public List<IMyGyro> Gyros => gyros;
        public List<IMyLandingGear> Gears => gears;
        public List<IMyTextSurface> Lcds1 => lcds1;
        public List<IMyTextSurface> Lcds2 => lcds2;

        public List<IMyTextSurface> Surfaces => surfaces;
        public StringBuilder ErrorMessage => errorMessage;
    }
}
