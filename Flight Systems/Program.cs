using IngameScript.Physics;
using IngameScript.Utils;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game.ModAPI.Ingame;
using VRageMath;
using IngameScript.UseCases;
using IngameScript.Domain;
using IngameScript.LCDInfo;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        GridManager gm;
        IniContext ic;
        PhysicsContext pc;
        CruiseControl cc;
        AutoLand al;
        DockMode dm;
        Text text;
        SpeedTimeTracker stt;

        IMyGridTerminalSystem gridTerminalSystem;
        IMyProgrammableBlock me;

        Command command;
        int tc;
        double timeSinceLastRun;
        StringBuilder scriptInfo;
        string argument;

        //Dock Mode
        bool lastDockState;

        Booleans b;
                
        public Program()
        {
            command = Command.Empty;

            scriptInfo = new StringBuilder();
            gridTerminalSystem = GridTerminalSystem;
            me = Me;

            b = new Booleans();
            stt = new SpeedTimeTracker();

            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            InicializeContexts();
        }

        private void InicializeContexts()
        {
            gm = new GridManager(gridTerminalSystem, me);
            ic = new IniContext(gm);
            pc = new PhysicsContext(gm, stt, command, timeSinceLastRun);
            cc = new CruiseControl(gm, ic, pc, b, command, timeSinceLastRun, tc);
            al = new AutoLand(gm, pc, command, b, tc);
            dm = new DockMode(gm, command);
            text = new Text(gm, ic, pc, b, command);

            ic.ParseIni();
            gm.IgnoreTag = ic.IgnoreTag;
        }

        int tick = 0;

        public void Main(string argument)
        {
            timeSinceLastRun = Runtime.TimeSinceLastRun.TotalSeconds;
            Echo("argument: " + argument);
            if (gm.ErrorMessage.Length > 0)
            {
                Echo("ErrorMessage: \n" + gm.ErrorMessage.ToString());
                return;
            }

            if (!string.IsNullOrEmpty(argument)) this.argument = argument;
            Echo("maxSpeed: " + ic.MaxSpeed);
            Echo("argument: " + this.argument);
            Echo("command: " + command.State);

            Echo(scriptInfo.ToString());
            gm.Me.GetSurface(0).WriteText(scriptInfo.ToString());

            tc++;
            if (tc % 1000 == 1)
            {
                ic.ParseIni();

                if (!string.IsNullOrWhiteSpace(gm.GridName) && !gm.GridName.Contains(" Grid "))
                {
                    gm.Me.CubeGrid.CustomName = gm.GridName;
                }
            }

            if (ic.IniAnyChanged || gm.Controller == null || gm.Controller.Closed)
            {
                ReloadGridManager(gm, ic);
                tick = 0;
                return;
            }

            switch (tick % 3)
            {
                case 0:
                    pc.NewRun(timeSinceLastRun);
                    scriptInfo = text.ScriptInfo();
                    break;
                case 1:
                    FlightSystems(gm, ic, pc);
                    break;
                case 2:
                    if (ic.UseSprites)
                    {
                        if (gm.Lcds1.Count > 0) LCD1Sprite();
                        if (gm.Lcds2.Count > 0) LCD2Sprite();
                    } else
                    {
                        if (gm.Lcds1.Count > 0) text.WriteInfo();
                        if (gm.Lcds2.Count > 0) text.WriteInfo2();
                    }
                    pc.CacheValues();
                    break;
            }

            tick++;

            Echo(GetRuntimeInfo());
        }

        private void FlightSystems(GridManager gm, IniContext ic, PhysicsContext pc)
        {
            if (!string.IsNullOrEmpty(argument))
            {
                command = new Command(argument);
                argument = "";
            }
                        
            if (ic.AllowDockMode)
            {
                bool anyConnected = gm.IsAnyConnectorConnected();
                bool isGearlocked = gm.Gears.Exists(g => g.IsLocked);
                dm.IsDockMode = anyConnected || isGearlocked;

                if (dm.IsDockMode != lastDockState)
                {
                    dm.DockToggle();
                    lastDockState = dm.IsDockMode;
                    return;
                }
            }

            if (dm.IsDockMode) return;

            if (ic.ControlAntennas)
            {
                gm.Antennas.ForEach(antenna => { if (antenna != null) antenna.Enabled = false; });
                if (gm.Antennas.Count > 0)
                {
                    var firstValid = gm.Antennas.FirstOrDefault(antenna => antenna != null && !antenna.Closed);
                    if (firstValid != null) firstValid.Enabled = true;
                }
            }

            MainState[] arr = {MainState.CNav, MainState.Cruise, MainState.Land, MainState.SBurn, MainState.Gps};
            List<MainState> MainStateList = new List<MainState>(arr);
            if (!ic.AllowFlightSystems && MainStateList.Contains(command.State))
            {
                return;
            }

            if (ic.AllowLowFuelLand && pc.Gravity > 0)
            {
                if (pc.H2Cache.Percent < ic.MinimumAcceptedFuel && gm.Controller.GetNaturalGravity().Length() / 9.81 > 0.75)
                {
                    command.State = MainState.Land;
                }
                else if (pc.H2Cache.Percent < ic.MinimumAcceptedFuel && gm.Controller.GetNaturalGravity().Length() / 9.81 < 0.75)
                {
                    command.State = MainState.Cruise;
                    command.Param.Text = "orbit";
                }
            }

            switch (command.State)
            {
                case MainState.Reload:
                    ReloadGridManager(gm, ic);
                    break;
                case MainState.Abort:
                    gm.AbortShipContext(command, b, ref tc);
                    break;
                case MainState.Dock:
                    dm.DockStateSwitch();
                    return;
                case MainState.Cruise:
                    gm.Controller.DampenersOverride = true;
                    cc.CruiseControlStateSwitch();
                    break;
                case MainState.CNav: // Circumnavigation
                    gm.Controller.DampenersOverride = true;
                    cc.CircumNavigateStateSwitch();
                    break;
                case MainState.Land: // Auto Land
                    if (pc.Gravity == 0)
                    {
                        gm.AbortShipContext(command, b, ref tc);
                        return;
                    }
                    if (command.Param.AutoLandState == AutoLandState.Idle) command.Param.AutoLandState = AutoLandState.Align;
                    al.AutoLandStateSwitch();
                    break;
                case MainState.SBurn: // Suicide Burn
                    if (command.Param.AutoLandState == AutoLandState.Idle) command.Param.AutoLandState = AutoLandState.Align;
                    al.SuicideBurnStateSwitch();
                    break;
                case MainState.Gps:
                    b.autoPilotToggle = true;
                    cc.CircumNavigateStateSwitch();
                    break;
            }

            // Stop cruise control when leaves gravity well
            if (b.stopCruiseWhenOutOfGrav && b.lastCheckIsOnNatGrav && pc.Gravity == 0.0)
            {
                b.stopCruiseWhenOutOfGrav = b.lastCheckIsOnNatGrav = b.cruiseToggle = false;
                gm.AbortShipContext(command, b, ref tc);
            }
            else
            {
                b.lastCheckIsOnNatGrav = pc.Gravity > 0.0;
            }
        }

        private void ReloadGridManager(GridManager gm, IniContext ic)
        {
            InicializeContexts();

            gm.ReloadLCDs(ic.Lcd1Tag, ic.Lcd2Tag)
                    .ReloadH2Tanks()
                    .ReloadBatteries(ic.BackupBatteryTag);

            // Flight cached blocks
            if (ic.AllowFlightSystems)
            {
                gm.ReloadControllers(ic.ControllerTag);

                if (gm.ErrorMessage.Length > 0)
                    return;

                gm.ReloadGridHeight()
                    .ReloadThrusters()
                    .ReloadGyros()
                    .ReloadGears();

                b.lastCheckIsOnNatGrav = gm.Controller.GetNaturalGravity().LengthSquared() > 0;

                gm.AbortShipContext(command, b, ref tc);
            }

            // Dock cached blocks
            if (ic.AllowDockMode)
                gm.ReloadConnectors()
                .ReloadGears()
                .ReloadTanks()
                .ReloadControlledBlocks(ic.DockGroupTag, ic.OverrideBlockTag);

            if (ic.ControlAntennas)
                gm.ReloadAntennas(ic.ControlAntennas);

            if (ic.RenameSubgrids)
            {
                // Get main grid (where this PB is)
                IMyCubeGrid mainGrid = gm.Me.CubeGrid;
                if (mainGrid != null)
                {
                    RenameSubgrids.GetSubgridsAndRename(gm.GridTS, mainGrid);
                }
            }

            if (ic.PaintSurfaces)
            {
                gm.ReloadSurfaces();

                foreach(IMyTextSurface surface in gm.Surfaces)
                {
                    Color backgroundColor = ColorMap.GetColorFromString(ic.BackgroundColor);
                    Color fontColor = ColorMap.GetColorFromString(ic.FontColor);

                    GridManager.PaintSurface(surface, backgroundColor, fontColor);
                }
            }
        }

        private void LCD1Sprite()
        {
            Sprites spt = new Sprites(ic);
            spt.Add(gm.GridName);

            StringBuilder state = new StringBuilder();
            state.Append("State: " + command.State);

            if (!string.IsNullOrEmpty(command.Param.Text))
                state.Append(" - " + command.Param.Text);
            if (command.Param.Number != 0)
                state.Append(" - " + command.Param.Number);
            if (command.Param.AutoLandState != AutoLandState.Idle)
                state.Append(" - " + command.Param.AutoLandState);

            spt.Add(state.ToString());

            spt.Add($"Mass: {pc.Mass.PhysicalMass / 1000:0.0} t");
            spt.Add($"Empty Mass: {pc.Mass.BaseMass / 1000:0.0} t");

            Color color = pc.PrevH2Fill < pc.H2Cache.Filled ? Color.LightBlue 
                : pc.H2Cache.Percent < ic.MinimumAcceptedFuel / 2 ? Color.DarkRed 
                : pc.H2Cache.Percent < ic.MinimumAcceptedFuel ? Color.DarkOrange 
                : new Color();

            if (!color.Equals(new Color()))
                    spt.Add($"H2: {pc.H2Cache.Percent:0}% - {pc.H2Cache.Time}", color, Color.Black);
            else spt.Add($"H2: {pc.H2Cache.Percent:0}% - {pc.H2Cache.Time}");

            color = pc.PrevBatFill < pc.BatCache.Filled ? Color.LightBlue
                : pc.BatCache.Percent < ic.MinimumAcceptedFuel / 2 ? Color.DarkRed 
                : pc.BatCache.Percent < ic.MinimumAcceptedFuel ? Color.DarkOrange 
                : new Color();

            if (!color.Equals(new Color()))
                spt.Add($"Bat:  {pc.BatCache.Percent:0}% - {pc.BatCache.Time}", color, Color.Black );
            else spt.Add($"Bat:  {pc.BatCache.Percent:0}% - {pc.BatCache.Time}");

            foreach (IMyTextSurface lcd in gm.Lcds1)
            {
                lcd.AddImageToSelection("Online");
                lcd.RemoveImageFromSelection("Online");
                spt.DrawInfoPanel(lcd, 1);
            }
        }

        void LCD2Sprite()
        {
            Sprites spt = new Sprites(ic);

            if (pc.Gravity > 0)
            {
                Color color = new Color();
                if (pc.ClimbRate < 0)
                {
                    color = pc.GroundLevel < 2 * pc.StopYDist
                        ? Color.DarkRed : pc.GroundLevel < 4 * pc.StopYDist
                        ? Color.DarkOrange : new Color();
                }

                if (!color.Equals(new Color()))
                {
                    spt.Add($"Ground level: {pc.GroundLevel:F1} m", color, Color.Black);
                    ClimbRateAndAccel(spt);
                    spt.Add($"Stop Y: {pc.StopYDist:F1} m | {pc.TimeToStopY:F1} s", color, Color.Black);
                }
                else
                {
                    spt.Add($"Ground level: {pc.GroundLevel:F1} m");
                    ClimbRateAndAccel(spt);
                    spt.Add($"Stop Y: {pc.StopYDist:F1} m | {pc.TimeToStopY:F1} s");
                }
            }


            spt.Add($"Stop Z: {pc.StopZDist:F1} m | {pc.TimeToStopZ:F1} s");

            if (b.autoPilotToggle)
            {
                spt.Add($"\nETA: {UtilsHelpder.FormatTime(pc.TimeToDistanceSmoothed)}");
            }
            else if (command.State == MainState.Land || command.State == MainState.SBurn)
            {
                spt.Add($"TTI: {pc.TimeToImpact:F1} s");
            }
            else
            {
                spt.Add($"Longitudinal v: {pc.ForwardVelocity:F1} m/s");
                spt.Add($"Lateral v: {pc.RightVelocity:F1} m/s");
                spt.Add($"Vertical v: {pc.UpVelocity:F1} m/s");
            }

            foreach (IMyTextSurface lcd in gm.Lcds2)
            {
                lcd.AddImageToSelection("Online");
                lcd.RemoveImageFromSelection("Online");
                spt.DrawInfoPanel(lcd, 1);
            }
        }

        private void ClimbRateAndAccel(Sprites spt)
        {
            spt.Add($"Rate of climb: {pc.ClimbRate:F1} m/s");
            spt.Add($"Accel: {pc.Accel.Length() / 9.81:F1} g");
        }

        private double tickCounter = 0;
        private double maxRuntimeMs = 0;

        private String GetRuntimeInfo()
        {
            tickCounter++;

            if (tickCounter % 20 == 1)
            {
                maxRuntimeMs = 0;
            }

            StringBuilder m_echoBuilder = new StringBuilder(512);
            m_echoBuilder.Append($"Runtime: {Math.Round(Runtime.LastRunTimeMs, 5)} Ms\n");

            double newRuntimeMs = Math.Round(Runtime.LastRunTimeMs, 5);
            maxRuntimeMs = Math.Max(newRuntimeMs, maxRuntimeMs);

            m_echoBuilder.Append($"Max Runtime: {maxRuntimeMs} Ms\n");
            m_echoBuilder.Append($"Instruction Count: {Runtime.CurrentInstructionCount}\n");
            m_echoBuilder.Append($"Complexity: {Math.Round((double)Runtime.CurrentInstructionCount / Runtime.MaxInstructionCount, 5)}%\n");
            return m_echoBuilder.ToString();
        }
    }
}

