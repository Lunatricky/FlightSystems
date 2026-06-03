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
        GridContext gc;
        GridManager gm;
        IniContext ic;
        PhysicsContext pc;
        CruiseControl cc;
        AutoLand al;
        DockMode dm;
        Text text;
        readonly SpeedTimeTracker stt;

        readonly IMyGridTerminalSystem gridTerminalSystem;
        readonly IMyProgrammableBlock me;

        Command command = Command.Empty;
        int tickCount;
        double timeSinceLastRun;
        StringBuilder scriptInfo;
        string argument;

        //Dock Mode
        bool lastDockState;

        Booleans b;
                
        public Program()
        {
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
            gc = new GridContext(gridTerminalSystem, me);
            gm = new GridManager(gc, b);
            ic = new IniContext(gc);
            pc = new PhysicsContext(gc, stt, command, timeSinceLastRun);
            cc = new CruiseControl(gc, gm, ic, pc, b, command, timeSinceLastRun, tickCount);
            al = new AutoLand(gc, gm, pc, command, tickCount);
            dm = new DockMode(gm, command);
            text = new Text(gc, ic, pc, b, command);

            ic.ParseIni();
            gc.IgnoreTag = ic.IgnoreTag;
        }

        int tick = 0;

        public void Main(string argument)
        {
            Echo("argument: " + argument);
            if (gc.ErrorMessage.Length > 0)
            {
                Echo("ErrorMessage: \n" + gc.ErrorMessage.ToString());
                return;
            }

            if (!string.IsNullOrEmpty(argument)) this.argument = argument;
            Echo("argument: " + this.argument);

            Echo(scriptInfo.ToString());
            gc.Me.GetSurface(0).WriteText(scriptInfo.ToString());

            tickCount++;
            if (tickCount % 1000 == 1)
            {
                ic.ParseIni();

                if (!string.IsNullOrWhiteSpace(gc.GridName) && !gc.GridName.Contains(" Grid "))
                {
                    gc.Me.CubeGrid.CustomName = gc.GridName;
                }
            }

            if (ic.IniAnyChanged || gc.Controller == null || gc.Controller.Closed)
            {
                ReloadGridContext(ref gc, ref ic);
                tick = 0;
                return;
            }

            switch (tick % 3)
            {
                case 0:
                    timeSinceLastRun = Runtime.TimeSinceLastRun.TotalSeconds;
                    pc.NewRun(timeSinceLastRun);
                    scriptInfo = text.ScriptInfo();
                    break;
                case 1:
                    FlightSystems(gc, ic, pc);
                    break;
                case 2:
                    if (ic.UseSprites)
                    {
                        if (gc.Lcds1.Count > 0) LCD1Sprite();
                        if (gc.Lcds2.Count > 0) LCD2Sprite();
                    } else
                    {
                        if (gc.Lcds1.Count > 0) text.WriteInfo();
                        if (gc.Lcds2.Count > 0) text.WriteInfo2();
                    }
                    pc.CacheValues();
                    break;
            }

            tick++;

            Echo(GetRuntimeInfo());
        }

        private void FlightSystems(GridContext gc, IniContext ic, PhysicsContext pc)
        {
            if (!string.IsNullOrEmpty(argument))
            {
                command = new Command(argument);
                argument = "";
            }
                        
            if (ic.AllowDockMode)
            {
                bool anyConnected = gm.IsAnyConnectorConnected();
                bool isGearlocked = gc.Gears.Exists(g => g.IsLocked);
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
                gc.Antennas.ForEach(b => { if (b != null) b.Enabled = false; });
                if (gc.Antennas.Count > 0)
                {
                    var firstValid = gc.Antennas.FirstOrDefault(b => b != null && !b.Closed);
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
                if (pc.H2Cache.Percent < ic.MinimumAcceptedFuel && gc.Controller.GetNaturalGravity().Length() / 9.81 > 0.75)
                {
                    command.State = MainState.Land;
                }
                else if (pc.H2Cache.Percent < ic.MinimumAcceptedFuel && gc.Controller.GetNaturalGravity().Length() / 9.81 < 0.75)
                {
                    command.State = MainState.Cruise;
                    command.Param.Text = "orbit";
                }
            }

            switch (command.State)
            {
                case MainState.Reload:
                    ReloadGridContext(ref gc, ref ic);
                    break;
                case MainState.Abort:
                    gm.AbortShipContext(ref command, ref tickCount);
                    break;
                case MainState.Dock:
                    dm.DockStateSwitch();
                    return;
                case MainState.Cruise:
                    gc.Controller.DampenersOverride = true;
                    cc.CruiseControlStateSwitch();
                    break;
                case MainState.CNav: // Circumnavigation
                    gc.Controller.DampenersOverride = true;
                    cc.CircumNavigateStateSwitch();
                    break;
                case MainState.Land: // Auto Land
                    if (pc.Gravity == 0)
                    {
                        gm.AbortShipContext(ref command, ref tickCount);
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
                gm.AbortShipContext(ref command, ref tickCount);
            }
            else
            {
                b.lastCheckIsOnNatGrav = pc.Gravity > 0.0;
            }
        }

        private void ReloadGridContext(ref GridContext gc, ref IniContext ic)
        {
            InicializeContexts();

            gc.ReloadLCDs(ic.Lcd1Tag, ic.Lcd2Tag)
                    .ReloadH2Tanks()
                    .ReloadBatteries(ic.BackupBatteryTag);

            // Flight cached blocks
            if (ic.AllowFlightSystems)
            {
                gc.ReloadControllers(ic.ControllerTag);

                if (gc.ErrorMessage.Length > 0)
                    return;

                gc.ReloadGridHeight()
                    .ReloadThrusters()
                    .ReloadGyros()
                    .ReloadGears();

                b.lastCheckIsOnNatGrav = gc.Controller.GetNaturalGravity().LengthSquared() > 0;
                gm.AbortShipContext(ref command, ref tickCount);
            }

            // Dock cached blocks
            if (ic.AllowDockMode)
                gc.ReloadConnectors()
                .ReloadGears()
                .ReloadTanks()
                .ReloadControlledBlocks(ic.DockGroupTag, ic.OverrideBlockTag);

            if (ic.ControlAntennas)
                gc.ReloadAntennas(ic.ControlAntennas);

            if (ic.RenameSubgrids)
            {
                // Get main grid (where this PB is)
                IMyCubeGrid mainGrid = gc.Me.CubeGrid;
                if (mainGrid != null)
                {
                    RenameSubgrids.GetSubgridsAndRename(gc.GridTS, mainGrid);
                }
            }

            if (ic.PaintSurfaces)
            {
                gc.ReloadSurfaces();

                foreach(IMyTextSurface surface in gc.Surfaces)
                {
                    Color backgroundColor = ColorMap.GetColorFromString(ic.BackgroundColor);
                    Color fontColor = ColorMap.GetColorFromString(ic.FontColor);

                    GridContext.PaintSurface(surface, backgroundColor, fontColor);
                }
            }
        }

        private void LCD1Sprite()
        {
            Sprites spt = new Sprites(ic);
            spt.Add(gc.GridName);

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

            foreach (IMyTextSurface lcd in gc.Lcds1)
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

            foreach (IMyTextSurface lcd in gc.Lcds2)
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
