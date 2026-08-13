using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript.Domain
{
    public class GridContext : GridManager
    {
        IMyGridTerminalSystem gridTS;
        IMyProgrammableBlock me;
        string gridName;
        string ignoreTag;
        readonly StringBuilder errorMessage;

        double centerGridHeight;
        double bottomGridHeight;
        double gridHeight;

        IMyRemoteControl controller;
        IMyBatteryBlock backupBattery;

        List<IMyTerminalBlock> unfilteredBlocks = new List<IMyTerminalBlock>();
        List<IMyFunctionalBlock> controlledBlocks = new List<IMyFunctionalBlock>();
        List<IMyFunctionalBlock> controlledToolBlocks = new List<IMyFunctionalBlock>();
        List<IMyShipConnector> connectors = new List<IMyShipConnector>();
        List<IMyLandingGear> gears = new List<IMyLandingGear>();
        List<IMyGasTank> tanks = new List<IMyGasTank>();
        List<IMyGasTank> h2Tanks = new List<IMyGasTank>();
        List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
        List<IMyRadioAntenna> antennas = new List<IMyRadioAntenna>();
        List<IMyShipController> controllers = new List<IMyShipController>();
        List<IMyShipController> cockpits = new List<IMyShipController>();

        List<IMyThrust> thrusters = new List<IMyThrust>();
        List<IMyThrust> breakingThrusters = new List<IMyThrust>();
        List<IMyThrust> forwardThrusters = new List<IMyThrust>();
        List<IMyThrust> upwardThrusters = new List<IMyThrust>();

        List<IMyGyro> gyros = new List<IMyGyro>();

        List<IMyTextSurface> lcds1 = new List<IMyTextSurface>();
        List<IMyTextSurface> lcds2 = new List<IMyTextSurface>();
        List<IMyTextSurface> lcdsSettings = new List<IMyTextSurface>();

        List<IMyTextSurface> surfaces = new List<IMyTextSurface>();

        public GridContext(IMyGridTerminalSystem grid, IMyProgrammableBlock me)
        {
            errorMessage = new StringBuilder();
            GridTS = grid;
            Me = me;
            IsLG = Me.CubeGrid.GridSizeEnum == MyCubeSize.Large;
            string tempGridName = Me.CubeGrid.CustomName;
            if (!string.IsNullOrWhiteSpace(tempGridName) && !tempGridName.Contains(" Grid "))
                GridName = tempGridName;
        }

        public void Setup(IniContext ic)
        {
            List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();

            Controllers.Clear();
            Cockpits.Clear();
            Controller = null;

            Thrusters.Clear();
            Gyros.Clear();
            Gears.Clear();

            Antennas.Clear();

            GetSameConstructBlocks(blocks, IgnoreTag);

            foreach (IMyTerminalBlock block in blocks)
            {
                if (IsBlockType<IMyRemoteControl>(block) != null)
                {
                    IMyRemoteControl remote = (IMyRemoteControl)block;
                    remote.ControlThrusters = true;
                    remote.IsMainCockpit = false;
                    if (Controller == null && remote.CustomName.Contains(ic.ControllerTag.ToLower()))
                        Controller = remote;
                    Controllers.Add(remote);
                }
                else if (IsBlockType<IMyCockpit>(block) != null)
                {
                    IMyCockpit cockpit = (IMyCockpit)block;
                    if (cockpit.CanControlShip)
                    {
                        cockpit.ControlThrusters = true;
                        cockpit.IsMainCockpit = false;
                        Cockpits.Add(cockpit);
                        Controllers.Add(cockpit);
                    }
                }
                else if (IsBlockType<IMyThrust>(block) != null)
                {
                    Add(Thrusters, block);
                }
                else if (IsBlockType<IMyGyro>(block) != null)
                {
                    Add(Gyros, block);
                }
                else if (IsBlockType<IMyLandingGear>(block) != null)
                {
                    Add(Gears, block);
                }
                else if (ic.ControlAntennas && IsBlockType<IMyRadioAntenna>(block) != null)
                {
                    IMyRadioAntenna antenna = (IMyRadioAntenna)block;
                    Antennas.Add(antenna);
                    if (string.IsNullOrEmpty(antenna.HudText)) antenna.HudText = GridName;
                }
                else
                {
                    unfilteredBlocks.Add(block);
                }
            }

            if (Controller == null)
            {
                foreach (IMyTerminalBlock c in Controllers)
                {
                    if (IsBlockType<IMyRemoteControl>(c) != null)
                    {
                        Controller = (IMyRemoteControl)c;
                        break;
                    }
                }
            }

            if (Controller == null)
            {
                ErrorMessage.AppendLine("===============================");
                ErrorMessage.AppendLine("No Remote Control block found!");
                ErrorMessage.AppendLine("Place a RC on the grid facing forward.");
                ErrorMessage.AppendLine("Or name a RC with Reference in it's name, facing forward, in case you need RCs in different directions.");
                ErrorMessage.AppendLine("===============================");
                return;
            }

            ReloadGridHeight();
            if (Thrusters.Count > 0) ReloadThrusters();
        }

        private T IsBlockType<T>(IMyTerminalBlock block) where T : class
        {
            return block is T ? (T) block: null;
        }

        private void Add<T>(List<T>blocks, IMyTerminalBlock block)
        {
            blocks.Add((T)block);
        }

        void ReloadGridHeight()
        {
            Vector3D gravityDir = Vector3D.Normalize(Controller.GetNaturalGravity());

            Vector3D center = Me.CubeGrid.WorldVolume.Center;
            Vector3D shipBottom = VectorHelper.GetLowestPoint(this);

            // project onto gravity vector
            centerGridHeight = center.Dot(gravityDir);
            bottomGridHeight = shipBottom.Dot(gravityDir);

            // height difference along gravity
            GridHeight = Math.Abs(centerGridHeight - bottomGridHeight);

            GridHeight = IsLG ? GridHeight * 2.5 : GridHeight * 0.5;
        }

        void ReloadThrusters()
        {
            ForwardThrusters.Clear();
            BreakingThrusters.Clear();
            UpwardThrusters.Clear();

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
        }

        public GridContext ReloadLCDs(string lcd1Tag, string lcd2Tag, string lcdSettingsTag)
        {
            Lcds1.Clear();
            Lcds2.Clear();
            lcdsSettings.Clear();

            Lcds1.AddList(AddLCDsToList(lcd1Tag, false, true));
            Lcds2.AddList(AddLCDsToList(lcd2Tag, false, true));
            lcdsSettings.AddList(AddLCDsToList(lcdSettingsTag, false, true));
            lcdsSettings.Add(Me.GetSurface(0));

            CleanSurfaces(Lcds1);
            CleanSurfaces(Lcds2);
            CleanSurfaces(lcdsSettings);

            return this;
        }

        public GridContext ReloadSurfaces()
        {
            surfaces.Clear();
            surfaces.AddList(AddLCDsToList(ignoreTag, true));
            return this;
        }

        List<IMyTextSurface> AddLCDsToList(string tag, bool isIgnoreTag, bool setupSurface = false)
        {
            List<IMyTextSurface> lcds = new List<IMyTextSurface>();
            
            var blocks = new List<IMyTerminalBlock>();
            if (isIgnoreTag)
            {
                GridTS.GetBlocksOfType<IMyTextSurfaceProvider>(blocks, block =>
                    block.IsSameConstructAs(Me) &&
                    !block.CustomName.Contains(tag) &&
                    !block.CustomData.Contains(tag)
                );
            }
            else
            {
                GridTS.GetBlocksOfType<IMyTextSurfaceProvider>(blocks, block =>
                    block.IsSameConstructAs(Me) &&
                    block.CustomName.Contains(tag)
                );
            }

            foreach (IMyTextSurfaceProvider surfaceProvider in blocks)
            {
                for (int i = 0; i < surfaceProvider.SurfaceCount; i++)
                {
                    IMyTextSurface surface = surfaceProvider.GetSurface(i);
                    if (setupSurface) SetupSurface(surface);
                    lcds.Add(surface);
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

        public static void PaintSurfaces(IniContext ic, List<IMyTextSurface> surfaces)
        {
            foreach (IMyTextSurface surface in surfaces)
            {
                Color backgroundColor;
                if (ic.TransparentLCD && surface.Name.ToLower().Contains("transparent")) backgroundColor = Color.Black;
                else backgroundColor = ColorMap.GetColorFromString(ic.LcdBackgroundColor);
                Color fontColor = ColorMap.GetColorFromString(ic.LcdFontColor);

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
            GetSameConstructBlocks(Connectors, IgnoreTag);
            SetConnectors();

            return this;
        }

        public GridContext ReloadTanks()
        {
            GetSameConstructBlocks(Tanks, IgnoreTag);

            return this;
        }

        public GridContext ReloadH2Tanks()
        {
            GetSameConstructBlocks(Tanks, IgnoreTag);

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
            GetSameConstructBlocks(Batteries, IgnoreTag);

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
                foreach (var battery in Batteries)
                {
                    if (Me.CubeGrid == battery.CubeGrid)
                    {
                        BackupBattery = battery;
                        BackupBattery.CustomName = BackupBattery.CustomName + " " + backupTag;
                        return this;
                    }
                }
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
            AddBlocks<IMyEmotionControllerBlock>(ControlledBlocks);
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

        public bool IsLG { get; }

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
        public List<IMyShipController> Controllers => controllers;
        public List<IMyShipController> Cockpits => cockpits;
        public List<IMyThrust> Thrusters => thrusters;
        public List<IMyThrust> BreakingThrusters => breakingThrusters;
        public List<IMyThrust> ForwardThrusters => forwardThrusters;
        public List<IMyThrust> UpwardThrusters => upwardThrusters;
        public List<IMyGyro> Gyros => gyros;
        public List<IMyLandingGear> Gears => gears;
        public List<IMyTextSurface> Lcds1 => lcds1;
        public List<IMyTextSurface> Lcds2 => lcds2;
        public List<IMyTextSurface> LcdsSettings => lcdsSettings;

        public List<IMyTextSurface> Surfaces => surfaces;
        public StringBuilder ErrorMessage => errorMessage;

        public void GetSameConstructBlocks<T>(List<T> list, string __ignoreTag = "") where T : class, IMyTerminalBlock
        {
            list.Clear();
            bool hasIgnore = !string.IsNullOrEmpty(__ignoreTag);
            GridTS.GetBlocksOfType(list, block =>
                block.IsSameConstructAs(Me)
                && (!hasIgnore || !block.CustomName.Contains(__ignoreTag))
                && (!hasIgnore || !block.CustomData.Contains(__ignoreTag))
            );
        }

        public void GetOwnGridBlocks<T>(List<T> list, string __ignoreTag = "") where T : class, IMyTerminalBlock
        {
            list.Clear();
            bool hasIgnore = !string.IsNullOrEmpty(__ignoreTag);
            GridTS.GetBlocksOfType(list, block =>
                block.CubeGrid == Me.CubeGrid
                && (!hasIgnore || !block.CustomName.Contains(__ignoreTag))
                && (!hasIgnore || !block.CustomData.Contains(__ignoreTag))
            );
        }


        public bool IsAnyConnectorConnected()
        {
            foreach (IMyShipConnector connector in Connectors)
            {
                if (connector.Status == MyShipConnectorStatus.Connected)
                    return true;
            }
            return false;
        }

        public void SetBlocks(bool enabled, out bool isDockMode)
        {
            //Always turn tools OFF when dock/undock
            ControlledToolBlocks.ForEach(b => b.Enabled = false);

            //Toggle other blocks when dock/undock
            ControlledBlocks.ForEach(b => b.Enabled = enabled);

            isDockMode = !enabled;
        }

        public void KillThrusters() => KillThrusters(thrusters);
        public void KillThrusters(List<IMyThrust> thrusters) => thrusters.ForEach(b => b.Enabled = false);

        public void ResetThrusters() => ResetThrusters(thrusters);
        public void ResetThrusters(List<IMyThrust> thrusters)
        {
            foreach (var t in thrusters)
            {
                t.ThrustOverridePercentage = 0f;
                t.Enabled = true;
            }

        }

        public void ResetGyros()
        {
            foreach (var g in gyros)
            {
                g.Pitch = 0f;
                g.Yaw = 0f;
                g.Roll = 0f;
                g.GyroOverride = false;
                g.Enabled = true;
            }
        }

        public void StockpileTanks(bool stockpile)
        {
            foreach (IMyGasTank tank in Tanks)
            {
                if (tank != null && tank.IsFunctional)
                    tank.Stockpile = stockpile;
            }
        }

        public void ChargeBatteries()
        {
            if (BackupBattery != null)
            {
                BackupBattery.ChargeMode = ChargeMode.Auto;
                foreach (IMyBatteryBlock battery in Batteries) battery.ChargeMode = ChargeMode.Recharge;
            }
            else if (IsAnyConnectorConnected())
            {
                foreach (IMyBatteryBlock battery in Batteries) battery.ChargeMode = ChargeMode.Recharge;
            }
        }

        public void AutoBatteries()
        {
            if (BackupBattery != null)
                BackupBattery.ChargeMode = ChargeMode.Recharge;

            foreach (IMyBatteryBlock battery in Batteries)
            {
                battery.ChargeMode = ChargeMode.Auto;
            }
        }

        public void CleanSurfaces() => CleanSurfaces(surfaces);
        public void CleanSurfaces(List<IMyTextSurface> lcds)
        {
            foreach (IMyTextSurface lcd in lcds)
            {
                lcd.AddImageToSelection("Online");
                lcd.RemoveImageFromSelection("Online");
                lcd.ContentType = ContentType.SCRIPT;
                lcd.Script = "";
            }
        }
    }
}
