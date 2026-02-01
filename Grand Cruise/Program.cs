using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        /*************
         Grand Cruise 2.0 - script for safe flight of ships on planets and in space
 
         Author: Survival Ready - steamcommunity.com/profiles/76561199069720721/myworkshopfiles/

         Programm Block (PB) argiments (case insenitive) NN - decimal number:

         reset        - reset script when ship configuration was changed 

         stop         - stop cruise
 
         NN           - planets and space cruise on NN speed
                         0  : flat fly (align to gravity on planets) 
                        +NN : increase cruise speed 
                        -NN : decrease cruise speed
 
         escape [NN]  - change ship altitude in gravity by NN meters or controller name
                         NN : move to altitude NN
                        +NN : increase altitude at NN meters
                        -NN : decrease altitude at NN meters
                        Low orbit takeoff: without NN - use current controller
                                           Block Name - use the alternative controller 
                
         landing [NN] - landing ship in the planet's gravity
                        without NN - use current controller
                        Block Name - use the alternative controller 

         lidar [NN]   - distance to a object or point on the planet's surface
                        without NN : measure distance to target (repeat for fly to target)
                        NN = 0: disable raycast obstacle detection 
                        NN < 0: set raycast obstacle avoidance accuracy in meters
                        NN < max velocity : set default velocity, measure distance to target and fly
                        NN > max velocity : set default range and measure distance to target
                
         view MODE    - set default MODE for LCD panel from View MODE dictionary (see below)
                        view without MODE : detailed information about the grid
                        MODE without view : out according to the dictionary
        */

        string prefix = "cruise";    // script prefix for blocks custom name
        string referent = "";        // exact controller custom name (optional)
        string pbeasy = "";          // EasyPlay PB block name (optional)

        // --- Timers or EasyPlay @scenario name (optional)
        Dictionary<string, string> timers = new Dictionary<string, string>() {
            {"before landing", ""},
            {"after landing", ""},
            {"before escape", ""},
        };

        // --- Groups name (optional)
        Dictionary<string, string> groups = new Dictionary<string, string>() {
            {"thrusts", ""},                // thrusters group for control  
            {"landing", ""},                // landing gear or magnetic plates group for landing
        };

        // --- LCD out
        Dictionary<string, string> lcdout = new Dictionary<string, string>() {
            {"header", "-= Grand Cruise 2.0 =-\n\n"},
            {"view", "ship"},
            {"text color", "255:215:0"},
            {"text background", "0:0:0"},
            {"script color", "255:215:0"},
            {"script background", "0:0:0"},
        };

        const int xrate = 1;            // world constant: cargo blocks multiplicator
        const double mvel = 100;        // world constant: maximum velocity

        double raycast = 100;           // cruise obstacle detect in meters (0 == off)
        double coward = 1;              // obstacle approach in ship length (min = 0.1)
        float minalt = 500;             // minimal altitude correction in meters (0 = off)
        float minaltLanding = 20;       // minimal altitude correction in meters (0 = off)
        int landsmin = 2;               // landing velocity close to the ground (min = 1)

        // --- Lidar sets
        float rayspeed = 90;            // default velocity for fly to target by lidar m/s
        double rayrange = 10000;        // default lidar raycast camera range in meters

        // --- Landing sets
        bool lockship = true;           // try parking ship after landing (gears/connects)
        bool lockwait = false;          // wait for ship to align with terrain after landing
        bool thrustoff = false;         // turn off all thrusters after landing
        bool freefall = false;          // descent by gravity alone

        /* --- View MODE dictionary ---

          mode  = script "parameter" in lowercase
          new Progs 
          {
            RUN = [inner]  - inner programm 
                  [script] - vanilla script inner name
                  PB name  - exact name of programm block to run
            OUT = name inner mode or parameter for external PB
          }
         */

        Dictionary<string, Progs> viewDict = new Dictionary<string, Progs>()
        {
            // system scripts - don't change this settings
            { "ship",    new Progs { RUN="[inner]",  OUT="ship" } },
            { "sets",    new Progs { RUN="[inner]",  OUT="sets" } },
            { "clear",   new Progs { RUN="[script]", OUT="clear" } },
            { "clock",   new Progs { RUN="[script]", OUT="TSS_ClockAnalog" } },
            { "horizon", new Progs { RUN="[script]", OUT="TSS_ArtificialHorizon" } },
            { "energy",  new Progs { RUN="[script]", OUT="TSS_EnergyHydrogen" } },
            { "weather", new Progs { RUN="[script]", OUT="TSS_Weather" } },
            { "help",    new Progs { RUN="PB EasyPlay", OUT="help" } },
        };

        ////////////// DO NOT CHANGE ANYTHING BEYOND THIS POINT ///////////////

        DateTime tstart = DateTime.Now;

        public IMyShipController controller = null;
        public IMyShipController navigator = null;
        public IMyTerminalBlock lcd = null;
        public IMyCameraBlock cam = null;

        Thrusters thrusters;

        public List<IMyGyro> gyros;
        public List<IMyLandingGear> gears;
        public List<IMyParachute> parachute;
        public List<IMyCameraBlock> cameras;
        public List<IMyShipConnector> connects;
        public List<IMyCargoContainer> containers;
        public List<IMyTextSurface> surfaces;
        public List<IMyFlightMovementBlock> flymove;
        public List<IMyRemoteControl> remote;

        string mode = "";

        float tspeed = 0;     // target forward speed
        float zspeed = 0;     // forward speed
        float yspeed = 0;     // down speed

        double alt = 0;       // current altitude
        double altt = 0;      // target altitude

        Vector3D gravity;     // current natural gravity
        Vector3D normal;      // normalized gravity vector
        double gms = 0;       // gravity m/s
        bool ing = false;     // in gravity

        Vector3D atarget;     // lidar distination target
        double raycorr = 0;      // var cruise raycast correction
        double altcorr = 0;      // var cruise altitude correction
        double totarget = 0;  // pitch override distance for lidar

        double egms = 0;      // start escape gravity
        double evel = 0;      // escape min velocity

        bool sland = true;    // for before landing timer
        bool usercont = false;// user reassign controller

        bool waitland = false;
        bool flatfly = false;
        bool down2up = false;

        Dictionary<string, float> dim = new Dictionary<string, float>();

        Dictionary<long, string> surs = new Dictionary<long, string>();

        public Program()
        {
            //debug(""); 
            Init(); Runtime.UpdateFrequency = UpdateFrequency.Once;
        }

        void stopCruise(bool onoff = true, bool land = false)
        {
            tspeed = 0; altt = 0; mode = "Stop"; lcdOut("stop");
            gyroFree(); thrusters.SetThrust(Dir.All, 0, onoff);
            if (land && lockship) lockShip();
            controller.DampenersOverride = true;
            if (usercont) { controller = null; Init(); }
            Runtime.UpdateFrequency = UpdateFrequency.None;
        }

        void Init(string newcont = "")
        {
            surfaces = new List<IMyTextSurface>();
            cameras = new List<IMyCameraBlock>();
            usercont = (newcont != "");

            var list = new List<IMyTerminalBlock>();
            GridTerminalSystem.SearchBlocksOfName(prefix, list);

            foreach (var b in list)
            {
                if (b is IMyShipController && controller == null)
                {
                    var c = (IMyShipController)b; if (c.CanControlShip && myGrid(b)) controller = c;

                }
                else if (b is IMyCameraBlock)
                {
                    var c = (IMyCameraBlock)b; cameras.Add(c); c.EnableRaycast = true;

                }
                else if (b is IMyTextPanel)
                {
                    surfaces.Add((IMyTextSurface)b);
                }

                if (b is IMyTextSurfaceProvider)
                {
                    var s = b as IMyTextSurfaceProvider;
                    if (s.SurfaceCount > 0)
                    {
                        surfaces.Add(s.GetSurface(lcdSur(b.CustomName, s.SurfaceCount, prefix)));
                    }
                }
            }

            if (controller == null || referent != "" || newcont != "")
            {
                if (usercont)
                {
                    controller = GridTerminalSystem.GetBlockWithName(newcont) as IMyShipController;
                }
                else if (referent != "")
                {
                    controller = GridTerminalSystem.GetBlockWithName(referent) as IMyShipController;
                }

                if (controller == null)
                {
                    var c = new List<IMyShipController>();
                    GridTerminalSystem.GetBlocksOfType(c, b => myGrid(b) && b.CanControlShip);
                    if (c.Count() != 0) controller = c[0] as IMyShipController;
                }
            }

            gyros = new List<IMyGyro>(); GridTerminalSystem.GetBlocksOfType(gyros, myGrid);

            parachute = new List<IMyParachute>(); GridTerminalSystem.GetBlocksOfType(parachute, myShip);

            connects = new List<IMyShipConnector>(); GridTerminalSystem.GetBlocksOfType(connects, myShip);

            var n = new List<IMyShipConnector>(connects); connects.Clear();
            foreach (var b in n) { if (b.IsParkingEnabled) connects.Add(b); }

            gears = new List<IMyLandingGear>();

            if (groups["landing"] != "")
            {
                var gr = groupBlock(groups["landing"]);
                foreach (var b in gr)
                {
                    if (b is IMyLandingGear && myShip(b)) gears.Add(b as IMyLandingGear);
                }
            }
            else
            {
                GridTerminalSystem.GetBlocksOfType(gears, myShip);
            }

            var g = new List<IMyLandingGear>(gears); gears.Clear();
            foreach (var b in g) { if (b.IsParkingEnabled) gears.Add(b); }

            containers = new List<IMyCargoContainer>(); GridTerminalSystem.GetBlocksOfType(containers, myShip);

            flymove = new List<IMyFlightMovementBlock>(); GridTerminalSystem.GetBlocksOfType(flymove, myGrid);

            remote = new List<IMyRemoteControl>(); GridTerminalSystem.GetBlocksOfType(remote, myGrid);

            if (surfaces.Count() == 0) surfaces.Add((IMyTextSurface)Me.GetSurface(0));

            if (cameras.Count == 0) raycast = 0;
            if (rayspeed > mvel) rayspeed = (float)mvel;
            if (Math.Round(coward, 1) <= 0.1) coward = 0.1;

            tspeed = 0; altt = 0; altcorr = 0; raycorr = 0; mode = lcdout["view"]; down2up = false; gridSize();
        }

        void sysError(int err = 0, string par = "")
        {
            string s = "Unknow error";

            switch (err)
            {
                case 0:
                    s = $"Invalid parameter: {par}"; break;
                case 1:
                    s = "Grid is static"; break;
                case 2:
                    s = "Ship controller or\ngyros not found"; break;
                case 3:
                    s = "No thrusters on axles\nUp, Forward or Backward"; break;
                case 4:
                    s = "Gravity required"; break;
                case 5:
                    s = "No active cameras"; break;
            }
            lcdOut(s);
        }

        bool getArgument(string argument)
        {
            tstart = DateTime.Now; raycorr = 0; altcorr = 0;

            string[] arg = argument.ToLower().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            switch (arg[0])
            {
                case "reset":
                    Init(); stopCruise(); return false;

                case "stop":
                    stopCruise(); return false;

                case "view":
                    if (arg.Count() > 1 && viewDict.ContainsKey(arg[1]))
                    {
                        lcdout["view"] = arg[1]; lcdOut(arg[1], true);
                    }
                    else
                    {
                        lcdOut("ship");
                    }
                    return false;

                case "escape":
                    if (!ing) { sysError(4); return false; }
                    mode = "Escape";
                    lockShip("unlock", 1); runTimer(timers["before escape"]);
                    egms = gms; evel = Math.Round(mvel / 2); altt = 100000;

                    if (arg.Count() > 1)
                    {
                        double a = (double)numParse(arg[1]);

                        if (arg[1][0] == '+' || arg[1][0] == '-')
                        {
                            altt = alt + a;
                        }
                        else if (a > 0)
                        {
                            altt = a;
                        }
                        else
                        {
                            Init(argument.Substring(6).Trim());
                            altt = 100000; mode = "Escape";
                        }
                    }

                    if (altt <= 0) altt = minalt; tspeed = 0; break;

                case "landing":
                    if (gravity.Length() == 0) { sysError(4); return false; }
                    if (arg.Count() > 1) Init(argument.Substring(8).Trim());
                    mode = "Landing"; sland = true; tspeed = 0; altt = minaltLanding; break;

                case "lidar":
                    double n = mvel + 1;

                    if (arg.Count() > 1)
                    {
                        n = (double)numParse(arg[1]);
                        if (n == 0) { raycast = 0; lcdOut("Lidar disabled"); return false; }
                        else if (n < 0) { raycast = Math.Abs(n); lcdOut($"Lidar accuracy: {raycast} m"); return false; }
                        else if (n > mvel) rayrange = n; else rayspeed = (float)n;
                    }
                    if (!shipLidar(n < mvel)) return false; break;

                default:
                    mode = "Cruise";
                    try
                    {
                        float s = float.Parse(arg[0]);

                        if (arg[0][0] == '+' || arg[0][0] == '-')
                        {
                            tspeed += s;
                            if (tspeed > mvel) tspeed = (float)mvel;
                            if (tspeed <= 0) tspeed = 0;
                        }
                        else
                        {
                            tspeed = s;
                        }

                        if (tspeed == 0)
                        {
                            thrusters.SetThrust(Dir.Backward, 0, true);
                            thrusters.SetThrust(Dir.Forward, 0, true);
                            tspeed = 0.0001f;
                        }
                        lockShip("unlock", 1); break;

                    }
                    catch
                    {

                        if (viewDict.ContainsKey(arg[0]))
                        {
                            lcdOut(arg[0]);
                        }
                        else
                        {
                            sysError(0, arg[0] + "\n");
                        }
                        return false;
                    }
            }

            return true;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (Me.CubeGrid.IsStatic) { sysError(1); return; }

            if (controller == null || gyros.Count() == 0) { sysError(2); return; }

            gravity = controller.GetNaturalGravity(); ing = (gravity.Length() > 0.05);
            controller.TryGetPlanetElevation(MyPlanetElevation.Surface, out alt);
            gms = Math.Sqrt(Math.Pow(gravity.X, 2) + Math.Pow(gravity.Y, 2) + Math.Pow(gravity.Z, 2));

            List<IMyThrust> allThrusters = new List<IMyThrust>();

            if (groups["thrusts"] != "")
            {
                var tr = groupBlock(groups["thrusts"]);
                foreach (var t in tr)
                {
                    if (t is IMyThrust && myShip(t) && t.IsFunctional) allThrusters.Add(t as IMyThrust);
                }
            }
            else
            {
                GridTerminalSystem.GetBlocksOfType<IMyThrust>(allThrusters, b => myShip(b) && b.IsFunctional);
            }

            if (argument.Length > 0) { if (!getArgument(argument)) return; }

            thrusters = new Thrusters(allThrusters, controller);

            if (thrusters.upThrust.Count == 0 ||
                thrusters.forwardThrust.Count == 0 ||
                thrusters.backwardThrust.Count == 0) { sysError(3); return; }

            if ((updateSource & UpdateType.Once) != 0) { lcdOut("init"); return; }

            // Flight (Move) && RC
            if (isAuto()) { stopCruise(); return; }

            // escape to orbit 
            if (!ing && mode == "Escape") { stopCruise(); return; }

            if (tspeed > 0 || argument.Length > 0)
            {
                Runtime.UpdateFrequency = UpdateFrequency.Update1;
            }

            if (!controller.DampenersOverride) controller.DampenersOverride = true;

            flatfly = (tspeed == 0.0001f); if (ing) shipGravity(); else shipSpace();

            if (mode != "Stop") lcdOut();
        }

        // SHIP *******************************************************************************************

        private void shipSpace()
        {
            zspeed = (float)getVelocity("z");
            yspeed = (float)getVelocity("y");

            float dev = Math.Abs(zspeed - tspeed);
            double speed = Math.Abs(controller.GetShipSpeed());

            // cruise if no raycast correction
            if (!flatfly && raycorr == 0)
            {
                if (zspeed < tspeed)
                {
                    thrusters.SetThrust(Dir.Backward, 0, false);
                    thrusters.SetThrust(Dir.Forward, Math.Min(1, Math.Abs(dev)), true);

                }
                else if (zspeed > tspeed && Math.Abs(dev) > 0.5)
                {
                    thrusters.SetThrust(Dir.Backward, Math.Min(1, Math.Abs(dev)), true);
                }
            }

            if (raycast > 0)
            {
                double safe = (altcorr == 0 ? speed : altcorr);
                double accure = (dim["l"] * coward) + ((mode == "Lidar") || flatfly ? 0 : raycast);
                double range = brakeDistance(thrusters.ntBackward, thrusters.ntForward, "f", safe) + accure;

                if (getRaycast(range) > 0)
                {

                    if (mode == "Lidar")
                    {
                        cam.CustomData = ""; raycorr = 0.1;
                        thrusters.SetThrust(Dir.Forward, 0, true);
                        thrusters.SetThrust(Dir.Backward, 100, true);

                    }
                    else if (flatfly)
                    {
                        thrusters.SetThrust(Dir.Forward, 0, zspeed > 1 ? false : true);
                        thrusters.SetThrust(Dir.Backward, zspeed > 1 ? 100 : 0, true);

                    }
                    else
                    {
                        // cruise obstacle
                        if (altcorr == 0)
                        {
                            altcorr = (int)tspeed; tspeed = 0.5f;
                            thrusters.SetThrust(Dir.Backward, 100, true);
                            thrusters.SetThrust(Dir.Forward, 0, true);
                        }
                        Obstacle(10);
                    }

                }
                else if (altcorr > 0 && Math.Abs(zspeed) <= 0.5f)
                {
                    tspeed = (float)altcorr; altcorr = 0; raycorr = 100;
                    thrusters.SetThrust(Dir.Backward, 0, true);
                    thrusters.SetThrust(Dir.Forward, 0, true);
                }
            }

            // obstacle cruise final rise
            if (Math.Round(raycorr) > 0) raycorr -= Obstacle(15);

            // obstacle stop fly by lidar
            if (speed < 0.5 && raycorr == 0.1) stopCruise();
        }

        private void shipGravity()
        {
            zspeed = (float)getVelocity("z");
            yspeed = (float)getVelocity("y");

            float dev = Math.Abs(zspeed - tspeed);

            // gyroscope stabilization
            normal = Vector3D.Normalize(gravity);
            navigator = controller; gyroAlign();

            if (tspeed == 0)
            {
                if (mode == "Landing")
                {
                    shipLanding(); return;

                }
                else if (mode == "Escape")
                {
                    shipEscape(); return;
                }
            }

            if (flatfly) { Correction(); return; }

            if (zspeed < tspeed)
            {
                thrusters.SetThrust(Dir.Backward, 0, false);
                thrusters.SetThrust(Dir.Forward, Math.Min(1, Math.Abs(dev)), true);

            }
            else if (zspeed > tspeed && Math.Abs(dev) > 0.5)
            {
                thrusters.SetThrust(Dir.Backward, Math.Min(1, Math.Abs(dev)), true);
            }

            Correction();
        }

        bool shipLidar(bool go = false)
        {
            if (rayrange == 0) rayrange = 10000; if (rayspeed == 0) return false;

            string s = $"Lidar: {rayrange} m"; double d = 0; down2up = false;

            cam = getCamera(); if (cam == null) { sysError(5); return false; }

            MyDetectedEntityInfo info = cam.Raycast(rayrange);

            if (info.IsEmpty() || !info.HitPosition.HasValue)
            {
                lcdOut(s + "\nTarget: N/A"); cam.CustomData = ""; atarget = Vector3D.Zero; return false;
            }

            string oldc = cam.CustomData; atarget = info.HitPosition.Value;
            double oldd = Math.Round(Vector3D.Distance(str2vec(oldc), atarget));

            d = Math.Round(Vector3D.Distance(cam.GetPosition(), atarget));

            cam.CustomData = "GPS:Hit:" + vec2str(atarget) + ":";

            s += $"\nDistance: {d} m" +
                  $"\nType: {info.Type}" +
                  $"\nSize: {Math.Round(Vector3D.Distance(info.BoundingBox.Min, info.BoundingBox.Max) / 2, 2)} m";

            d = Math.Round(vectorAngleBetween(str2vec(oldc), atarget) * 1000, 3);

            if (!go && (oldd > 10 || d > 1)) { lcdOut(s); return false; }

            mode = "Lidar"; tspeed = rayspeed; lockShip("unlock");

            // down2up = fly in gravity without pitch alignment
            down2up = (Vector3D.Normalize(gravity).Dot(controller.WorldMatrix.Forward) < 0);
            d = Math.Round((down2up ? Vector3D.Distance(cam.GetPosition(), atarget) : getProjection()));

            // decrease tspeed over short distances in gravity
            if (ing) while ((d / brakeDistance(thrusters.ntBackward, thrusters.ntForward, "f", tspeed)) < 2.5) --tspeed;

            return true;
        }

        private void shipLanding()
        {
            float land = (DateTime.Now - tstart).Seconds;
            double yms = Math.Round(getVelocity("y"), 1);

            if (alt > minaltLanding) { shipEscape("land"); return; }

            if (sland)
            {
                sland = false; runTimer(timers["before landing"]);
            }

            if (yms < 0.5 && land > 0 && !waitland)
            {
                tstart = DateTime.Now; land = 0; waitland = true;
            }

            if (waitland)
            {
                gyroFree(); thrusters.SetThrust(Dir.All, 0, !lockwait);

                if (land > 1)
                {
                    if (lockship && !lockwait) lockShip();
                    waitland = false; stopCruise(!thrustoff, lockship);
                }

            }
            else
            {
                thrusters.SetThrust(Dir.Up, 0, yms > landsmin);
            }
        }

        private bool shipEscape(string emode = "")
        {
            bool esc = true;
            var v = getVelocity("y"); var y = Math.Abs(v);
            double emax = Math.Round(mvel / 100) * 100;

            // start/stop velocity acording gravity level
            if (gms / egms < 0.90)
            {
                // free fall down
                if (v > 0 && (y > emax || freefall)) thrusters.SetThrust(Dir.Down, 0, true);

                if (controller.GetShipSpeed() > evel)
                {
                    thrusters.SetThrust(Dir.Up, 0, false); return true;
                }
                else
                {
                    evel = emax - 1; // 99
                    if (controller.GetShipSpeed() > (emax - 2)) evel = Math.Round(mvel / 2, 0);
                }
            }

            if (altt > alt)
            {
                esc = (alt + brakeDistance(thrusters.ntUp, thrusters.ntDown, "u")) < altt;
                thrusters.SetThrust(Dir.Up, 100, false); thrusters.SetThrust(Dir.Down, 0, false);
            }
            else
            {
                // Pertam!
                if (!double.IsInfinity(alt))
                {
                    var b = brakeDistance(thrusters.ntUp, thrusters.ntDown, "d", y);
                    esc = (altt < (alt - b)); float over = (esc && y < emax && !freefall ? 100 : 0);
                    thrusters.SetThrust(Dir.Down, over, true); thrusters.SetThrust(Dir.Up, 0, !esc);
                }
                else
                {
                    thrusters.SetThrust(Dir.Up, 100, true); thrusters.SetThrust(Dir.Down, 0, false);
                }
            }

            if (!esc)
            {
                if (emode == "")
                {
                    if (y > 2 && thrusters.upSubThrust.Count > 0)
                    {
                        foreach (var t in thrusters.upSubThrust)
                        {
                            if (t != null) t.ThrustOverridePercentage = 100;
                        }
                    }
                    else
                    {
                        stopCruise();
                    }
                }
                else
                    return true;
            }

            return false;
        }

        private void Correction()
        {
            // ticks timer
            int upcorr = (int)(getShipMass() / ((thrusters.ntUp) * (10 / gms) / 1000));

            double safe = (dim["l"] * coward) + (mode == "Lidar" ? 0 : raycast);
            double speed = Math.Abs(controller.GetShipSpeed());

            // altitude correction
            if (tspeed > 0 && minalt > 0)
            {
                if (alt < minalt)
                {
                    altcorr = upcorr; thrusters.SetThrust(Dir.Up, 100, true);

                }
                else if (altcorr > 0)
                {
                    if (--altcorr == 0)
                    {
                        thrusters.SetThrust(Dir.Up, 0, true);
                    }
                }
            }

            // raycast correction
            if (raycast > 0)
            {
                double range = brakeDistance(thrusters.ntBackward, thrusters.ntForward, "f", speed) + safe;

                if (mode == "Lidar")
                {
                    double td = (down2up ? Vector3D.Distance(cam.GetPosition(), atarget) : getProjection());
                    totarget = (td - range); if (totarget <= 0) { tspeed = 0.1f; cam.CustomData = ""; }

                }
                else
                {
                    if (getRaycast(range) > 0)
                    {
                        raycorr = upcorr + 1; thrusters.SetThrust(Dir.Up, 100, true);

                    }
                    else if (raycorr > 1)
                    {
                        if (--raycorr == 1) { thrusters.SetThrust(Dir.Up, 0, true); raycorr = 0; }
                    }
                }
            }

            // parachutes
            if (navigator != null)
            {
                var move = navigator.MoveIndicator;

                if (move.LengthSquared() > 0)
                {
                    var dir = Vector3D.Normalize(Vector3D.TransformNormal(move, navigator.WorldMatrix));
                    // press down key "C"
                    if (Math.Round(vectorAngleBetween(normal, dir)) == 0)
                    {
                        var y = Math.Abs(getVelocity("y"));
                        var b = brakeDistance(thrusters.ntUp, thrusters.ntDown, "d", y);
                        if (b > alt && parachute.Count() > 0)
                        {
                            foreach (var p in parachute)
                            {
                                if (p.OpenRatio == 0) p.OpenDoor();
                            }
                        }
                    }
                }
            }

            // lidar final coorection
            if (mode == "Lidar" && tspeed == 0.1f)
            {
                if (speed < 1)
                {
                    double d = Vector3D.Distance(cam.GetPosition(), atarget);
                    if (d > safe && !down2up)
                    {
                        mode = "Escape"; altt = alt - d + minalt; tspeed = 0; shipEscape();
                    }
                    else
                    {
                        stopCruise();
                    }
                }
            }
        }

        private int Obstacle(double speed)
        {
            int ret = Math.Abs(yspeed) < speed ? 0 : 1;

            if (ret == 0)
            {
                thrusters.SetThrust(Dir.Up, 100, true);
                thrusters.SetThrust(Dir.Down, 0, true);
            }
            else
            {
                thrusters.SetThrust(Dir.Up, 0, true);
                thrusters.SetThrust(Dir.Down, 0, true);
            }
            return ret;
        }

        // BLOCKS *****************************************************************************************

        private void runTimer(string val)
        {
            var tm = getTimer(val); if (tm != null) { tm.Trigger(); return; }
            var pb = getPBlock(pbeasy); if (pb != null) pb.TryRun(val);
        }

        private bool runPbExt(string pbl, string val)
        {
            var pb = getPBlock(pbl); if (pb == null) return false; pb.TryRun(val); return true;
        }

        private bool lockShip(string m = "lock", int lr = 0)
        {
            bool r = false; string s = "";

            if (gears.Count() > 0)
            {
                foreach (IMyLandingGear g in gears)
                {
                    if (g != null)
                    {
                        s = g.LockMode.ToString().ToLower();

                        switch (m)
                        {
                            case "lock":
                                if (s == "readytolock" || s == "locked")
                                {
                                    r = true; g.Lock();
                                }
                                break;
                            case "unlock":
                                r = true; g.Unlock(); break;
                            case "ready":
                                g.Unlock(); if (g.AutoLock) g.AutoLock = false;
                                r = r || (s == "readytolock"); break;
                            default:
                                break;
                        }
                    }
                }
            }

            if (connects.Count() > 0)
            {
                foreach (IMyShipConnector c in connects)
                {
                    if (c != null)
                    {
                        s = c.Status.ToString().ToLower();
                        switch (m)
                        {
                            case "lock":
                                if (s == "connectable" || s == "connected")
                                {
                                    r = true; c.Connect();
                                }
                                break;
                            case "unlock":
                                r = true; c.Disconnect(); break;
                            case "ready":
                                r = r || (s == "connectable"); break;
                            default:
                                break;
                        }
                    }
                }
            }

            // left & right thrusters on for frozen start
            if (m == "unlock" && lr > 0)
            {
                thrusters.SetThrust(Dir.Left, 0, true);
                thrusters.SetThrust(Dir.Right, 0, true);
            }

            return r;
        }

        string gearStatus()
        {
            string gs = "No"; int gr = 0;

            if (gears.Count > 0)
            {
                foreach (IMyLandingGear g in gears)
                {
                    if (g != null)
                    {
                        string s = g.LockMode.ToString().ToLower();
                        if (s == "locked") { gs = "Lock"; break; }
                        if (s == "readytolock") gr += 1;
                    }
                }
                if (gs == "No") gs = (gr > 0 ? "Ready" : "Unlock");
            }
            return gs;
        }

        private void gyroFree()
        {
            foreach (IMyGyro g in gyros)
            {
                if (g.IsWorking) { g.GyroOverride = true; g.Yaw = 0; g.Roll = 0; g.Pitch = 0; g.GyroOverride = false; }
            }
        }

        // credit halcight: gyros orientation on grid
        private void gyroAlign()
        {
            Matrix nav; navigator.Orientation.GetMatrix(out nav); Vector3D vdown = nav.Down;

            foreach (IMyGyro g in gyros)
            {
                Matrix gyr; g.Orientation.GetMatrix(out gyr);

                var vcur = Vector3D.Transform(vdown, Matrix.Transpose(gyr));
                var vtrg = Vector3D.Transform(normal, Matrix.Transpose(g.WorldMatrix.GetOrientation()));

                var rot = Vector3D.Cross(vcur, vtrg);
                double ang = rot.Length();

                ang = Math.Atan2(ang, Math.Sqrt(Math.Max(0.0, 1.0 - ang * ang)));
                if (Vector3D.Dot(vcur, vtrg) < 0) ang = Math.PI - ang;

                if (ang < 0.01) { g.GyroOverride = false; continue; }

                float yawMax = (float)(2 * Math.PI);
                double ctrl_vel = yawMax * (ang / Math.PI);

                ctrl_vel = Math.Min(yawMax, ctrl_vel);
                ctrl_vel = Math.Max(0.01, ctrl_vel);
                rot.Normalize(); rot *= ctrl_vel;

                float pitch = -(float)rot.X;
                if (Math.Abs(g.Pitch - pitch) > 0.01) g.Pitch = (down2up ? 0 : pitch);

                float yaw = -(float)rot.Y;
                if (Math.Abs(g.Yaw - yaw) > 0.01) g.Yaw = yaw;

                float roll = -(float)rot.Z;
                if (Math.Abs(g.Roll - roll) > 0.01) g.Roll = roll;

                g.GyroOverride = true;
            }
        }

        // credit ScriptedEngineer: gyros orientation on grid
        private void setGyros(float roll = 0, float yaw = 0, float pitch = 0)
        {
            foreach (IMyGyro g in gyros)
            {
                if (g.IsWorking)
                {
                    if (pitch == 0 && yaw == 0 && roll == 0)
                    {
                        g.Yaw = 0; g.Roll = 0; g.Pitch = 0; g.GyroOverride = false;
                    }
                    else
                    {
                        Vector3D axis = Vector3D.TransformNormal(new Vector3D((pitch >= 0 ? 1 : -1) * pitch, -yaw, -roll), controller.WorldMatrix);
                        Vector3D local = Vector3D.TransformNormal(axis, MatrixD.Transpose(g.WorldMatrix));
                        g.Pitch = (pitch >= 0 ? -1 : 1) * (float)local.X; g.Yaw = (float)-local.Y; g.Roll = (float)-local.Z; g.GyroOverride = true;
                    }
                }
            }
        }

        private double getRaycast(double range = 0)
        {
            cam = getCamera(); if (cam == null) return 0;
            MyDetectedEntityInfo info = cam.Raycast(range);
            if (info.IsEmpty() || !info.HitPosition.HasValue) return 0;
            return (Math.Round(Vector3D.Distance(cam.GetPosition(), info.HitPosition.Value), 2));
        }

        IMyCameraBlock getCamera()
        {
            if (cam != null && cam.IsWorking && cam.IsSameConstructAs(Me)) return cam;
            var cn = new List<IMyShipConnector>(); GridTerminalSystem.GetBlocksOfType(cn, myShip);

            foreach (var c in cameras)
            {
                if (c.IsWorking)
                {
                    if (c.IsSameConstructAs(Me)) return c;
                    // connected camera
                    foreach (var n in cn)
                    {
                        if (n.Status.ToString() == "Connected")
                        {
                            IMyShipConnector con = n.OtherConnector;
                            if (con.CubeGrid == c.CubeGrid) return c;
                        }
                    }
                }
            }
            return null;
        }

        private double getProjection()
        {
            Vector3D planet = Vector3D.Zero; controller.TryGetPlanetPosition(out planet);
            Vector3D target = (atarget + Vector3D.Normalize(atarget - planet) * alt);
            return Vector3D.Distance(cam.GetPosition(), target);
        }

        private double brakeDistance(double ptrust, double ntrust, string ttype = "d", double speed = 0)
        {
            // -1 = d || f (down & forward; 1 = u || b (up & backward);
            double dir = ((ttype == "d") || (ttype == "f") ? -1 : 1);
            double grv = ((ttype == "d") || (ttype == "u") ? gms : 0);

            if (speed == 0) speed = ((ttype == "d") || (ttype == "u") ? yspeed : Math.Abs(zspeed));

            double ship_mass = getShipMass();
            double brake_force = (dir < 0 ? ptrust : ntrust) + (ship_mass * grv * dir);
            double braking = brake_force / ship_mass; return (Math.Abs(speed * speed / (2 * braking)));
        }

        public bool isAuto()
        {
            foreach (var c in flymove) { if (c != null && c.IsAutoPilotEnabled) return true; }
            foreach (var c in remote) { if (c != null && c.IsAutoPilotEnabled) return true; }
            return false;
        }

        // * VIEW *****************************************************************************************

        private void lcdOut(string mes = "", bool set = false)
        {
            string text = "", m = mode; if (flatfly) m = "FlatFly";

            if (mes == "")
            {
                text = $"{m}: {(DateTime.Now - tstart).Minutes.ToString("#00")}:" +
                       $"{((DateTime.Now - tstart).Seconds.ToString("#00"))}";

                if (ing) text += $"\nAltitude: {Math.Round(alt, 0)} m";

                text += $"\nSpeed: {Math.Round(zspeed, 0)} / {0 - Math.Round(yspeed, 0)}";

                switch (m)
                {
                    case "Escape":
                        text += $"\nGravity: {Math.Round(gms / 10, 2)}\nFly to: ";
                        if (altt == 100000) text += "Orbit"; else text += $"{altt} m"; break;

                    case "Lidar":
                        double d = Math.Round(ing ? totarget : Vector3D.Distance(cam.GetPosition(), atarget), 0);
                        text += $"\nTarget: {d} " + (down2up ? "M" : "m");
                        if (raycorr > 0) text += $"\nRay: {new String('|', Math.Abs((int)raycorr / 10))}";
                        break;

                    case "Cruise":
                        if (tspeed != 0)
                        {
                            if (raycorr > 0)
                            {
                                text += $"\nRay: {new String('|', Math.Abs((int)raycorr / 10))}";
                            }
                            else if (altcorr != 0)
                            {
                                text += $"\nAlt: {new String('|', Math.Abs((int)altcorr / 10))}";
                            }
                            else
                            {
                                text += "\n" + (ing ? "Alt/" : "") + "Ray: ";
                                text += (ing ? $"{minalt}/" : "") + $"{raycast} m";
                            }
                        }
                        break;
                }

            }
            else if (mes == "init")
            {
                text = shipInit(); mes = "ship";

            }
            else if (mes == "stop")
            {
                text = "Stop Cruise"; mes = lcdout["view"];

            }
            else
            {
                text += $"{mes}";
                if (viewDict.ContainsKey(mes)) text = "View " + mes.ToUpper(); if (set) text += " set as default";
            }

            if (text != "") Echo($"= Grand Cruise 2.0 =\n\n{text}");

            if (mes != "" && viewDict.ContainsKey(mes))
            {
                text = lcdProg(mes); if (text == "") return;
            }

            if (surfaces.Count() > 0) lcdWrite(lcdout["header"] + text);
        }

        private string shipInit()
        {
            return $"Controller: {controller.CustomName}\n" +
                   $"Gyroscopes: {gyros.Count()}\n" +
                   $"Thruster Up: {thrusters.upThrust.Count}/{thrusters.upSubThrust.Count}\n" +
                   $"Thruster Down: {thrusters.downThrust.Count}/{thrusters.downSubThrust.Count}\n" +
                   $"Thruster Frwd: {thrusters.forwardThrust.Count}/{thrusters.forwardSubThrust.Count}\n" +
                   $"Thruster Back: {thrusters.backwardThrust.Count}/{thrusters.backwardSubThrust.Count}\n" +
                   $"Landing Gr/Cn: {gears.Count()}/{connects.Count()}\n" +
                   $"Parachutes: {shipInfo("para")}\n" +
                   $"Raycast: {(cameras.Count() > 0 && raycast > 0 ? "Enabled" : "Disabled")}\n";
        }

        private string shipInfo(string info = "")
        {
            double h2 = 0, pw = 0, cargo = 0, pch = 0, jd = 0, jr = 0, c, t, tu, td, tf, tb, tl, tr;

            List<IMyParachute> para = new List<IMyParachute>(); c = 0; t = 0;
            GridTerminalSystem.GetBlocksOfType<IMyParachute>(para, b => myShip(b) && b.IsFunctional);
            foreach (var v in para) pch += (v.GetInventory().CurrentVolume > 0 ? 1 : 0);
            if (info == "para") return $"{pch} ({parachute.Count()})";

            List<IMyGasTank> tank = new List<IMyGasTank>(); c = 0; t = 0;
            GridTerminalSystem.GetBlocksOfType<IMyGasTank>(tank, b => myShip(b) && b.IsFunctional);
            foreach (var v in tank) { c += v.Capacity * v.FilledRatio; t += v.Capacity; }
            if (t > 0) h2 = Math.Round(c / t * 100, 0); if (info == "tank") return h2.ToString();

            List<IMyBatteryBlock> pow = new List<IMyBatteryBlock>(); c = 0; t = 0;
            GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(pow, b => myShip(b) && b.IsFunctional);
            foreach (var v in pow) { c += (double)v.CurrentStoredPower; t += (double)v.MaxStoredPower; }
            if (t > 0) pw = Math.Round(c / t * 100, 0); if (info == "power") return pw.ToString();

            t = 0; c = 0;
            foreach (var v in containers) { c += (double)v.GetInventory().CurrentVolume; t += (double)v.GetInventory().MaxVolume; }
            if (t > 0) cargo = (c / t * 100); if (info == "cont") return cargo.ToString();

            foreach (var v in connects) { c += (double)v.GetInventory().CurrentVolume; t += (double)v.GetInventory().MaxVolume; }

            tu = Math.Round(!ing ? thrusters.ntUp / 1000 : (thrusters.ntUp / 10) * (10 / gms) / 1000, 0);
            td = Math.Round(!ing ? thrusters.ntDown / 1000 : (thrusters.ntDown / 10) * (10 / gms) / 1000, 0);
            tf = Math.Round(!ing ? thrusters.ntForward / 1000 : (thrusters.ntForward / 10) * (10 / gms) / 1000, 0);
            tb = Math.Round(!ing ? thrusters.ntBackward / 1000 : (thrusters.ntBackward / 10) * (10 / gms) / 1000, 0);
            tl = Math.Round(!ing ? thrusters.ntLeft / 1000 : (thrusters.ntLeft / 10) * (10 / gms) / 1000, 0);
            tr = Math.Round(!ing ? thrusters.ntRight / 1000 : (thrusters.ntRight / 10) * (10 / gms) / 1000, 0);

            t = Math.Round((getShipMass() - controller.CalculateShipMass().BaseMass) / 1000, 0);

            return $"Ship: {Me.CubeGrid.CustomName}\n" +
                   $"Size LWH: {dim["l"]} / {dim["w"]} / {dim["h"]} m\n" +
                   $"Thrust U/D:  {tu} / {td} {(ing ? "tn" : "kN")}\n" +
                   $"Thrust F/B:  {tf} / {tb} {(ing ? "tn" : "kN")}\n" +
                   $"Thrust L/R:  {tr} / {tl} {(ing ? "tn" : "kN")}\n" +
                   $"H2/Power: {h2} / {pw} %\n" +
                   $"Cargo: {Math.Round(cargo, 0)}% ({t} tn)\n" +
                   $"Gear status: {gearStatus()}";

        }

        private string setsInfo()
        {
            string rayprec = (raycast == 0 ? "None" : raycast.ToString() + " m");

            return $"Obstacle detect: {rayprec}\n" +
                   $"Minimum altitude: {minalt} m\n" +
                   $"Surface velocity: {landsmin} m/s\n\n" +
                   $"Flight by lidar: {rayspeed} m/s\n" +
                   $"Raycast range: {rayrange} m\n\n" +
                   $"Autoparking ship: {lockship}\n" +
                   $"Turn off thrusters: {thrustoff}\n";
        }


        private void lcdScript(string s = "")
        {
            if (s == "clear")
            {
                lcdWrite();
            }
            else
            {
                foreach (var surface in surfaces)
                {
                    surface.Script = s;
                    surface.ScriptForegroundColor = getColor(lcdout["script color"]);
                    surface.ScriptBackgroundColor = getColor(lcdout["script background"]); ;
                    surface.ContentType = VRage.Game.GUI.TextPanel.ContentType.SCRIPT;
                }
            }
        }

        void lcdWrite(string t = "")
        {
            //if(surs.Count > 0) 
            lcdStore();

            foreach (var surface in surfaces)
            {
                surface.WriteText(t);
                surface.FontColor = getColor(lcdout["text color"]);
                surface.BackgroundColor = getColor(lcdout["text background"]);
                surface.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
            }
        }

        string lcdProg(string prog)
        {
            var p = new Progs { };

            if (!viewDict.TryGetValue(prog, out p))
                return "Nothing to view";

            switch (p.RUN)
            {
                case "[inner]":
                    switch (p.OUT)
                    {
                        case "ship":
                            return shipInfo();
                        case "sets":
                            return setsInfo();
                    }
                    break;

                case "[script]":
                    lcdScript(p.OUT); break;

                default:
                    lcdStore(true);
                    if (!runPbExt(p.RUN, p.OUT))
                        return $"PB {prog} error"; break;
            }
            return "";
        }

        private void lcdStore(bool save = false)
        {
            if (surfaces.Count == 0 || (save && surs.Count() > 0)) return;

            var list = new List<IMyTerminalBlock>();
            GridTerminalSystem.SearchBlocksOfName(prefix, list);

            if (save)
            {
                surs = new Dictionary<long, string>();

                foreach (var b in list)
                {
                    if (b is IMyTextSurfaceProvider)
                    {
                        var s = b as IMyTextSurfaceProvider;

                        if (s.SurfaceCount > 0)
                        {
                            var ss = s.GetSurface(lcdSur(b.CustomName, s.SurfaceCount, prefix));
                            surs[b.EntityId] = $"{ss.Font}|{ss.FontSize}|{colorStr(ss.FontColor)}|" +
                                $"{colorStr(ss.BackgroundColor)}|{ss.TextPadding}|{ss.Alignment}";
                        }
                    }
                }
            }
            else
            {
                foreach (var b in list)
                {
                    if (surs.ContainsKey(b.EntityId))
                    {
                        var p = GridTerminalSystem.GetBlockWithId(b.EntityId) as IMyTextSurfaceProvider;
                        var s = p.GetSurface(lcdSur(b.CustomName, p.SurfaceCount, prefix));
                        string[] v = surs[b.EntityId].Split('|');

                        s.Font = v[0];
                        s.FontSize = numParse(v[1]);
                        s.FontColor = getColor(v[2]);
                        s.BackgroundColor = getColor(v[3]);
                        s.TextPadding = numParse(v[4]);
                        switch (v[5].ToLower())
                        {
                            case "center":
                                s.Alignment = VRage.Game.GUI.TextPanel.TextAlignment.CENTER; break;
                            case "right":
                                s.Alignment = VRage.Game.GUI.TextPanel.TextAlignment.RIGHT; break;
                            default:
                                s.Alignment = VRage.Game.GUI.TextPanel.TextAlignment.LEFT; break;
                        }
                    }
                }
            }
        }

        private int lcdSur(string n, int max, string p)
        {
            n = n.ToLower(); int i = n.IndexOf($"{p.ToLower()}_");
            if (i > 0) i = (int)numParse(n.Substring(i + p.Length + 1, 1)) - 1;
            return (i < 0 || (i > max - 1) ? 0 : i);
        }

        // COMMON **************************************************************************************************************

        IMyTimerBlock getTimer(string name)
        {
            List<IMyTimerBlock> tm = new List<IMyTimerBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyTimerBlock>(tm, b => b.CubeGrid == Me.CubeGrid && b.CustomName == name);
            if (tm.Count > 0) return tm[0]; else return null;
        }

        IMyProgrammableBlock getPBlock(string name)
        {
            List<IMyProgrammableBlock> pb = new List<IMyProgrammableBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyProgrammableBlock>(pb, b => b.CubeGrid == Me.CubeGrid && b.CustomName == name);
            if (pb.Count > 0) return pb[0]; else return null;
        }

        List<IMyTerminalBlock> groupBlock(string group)
        {
            List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock> { };
            IMyBlockGroup grp = GridTerminalSystem.GetBlockGroupWithName(group);
            if (grp != null) grp.GetBlocks(blocks); return blocks;
        }

        int getCargoVol()
        {
            IMyInventory inv; long cur = 0, max = 1;

            foreach (var b in containers)
            {
                inv = b.GetInventory();
                cur += inv.CurrentVolume.RawValue;
                max += inv.MaxVolume.RawValue;
            }

            return (int)((double)cur / max * 100f);
        }

        double getVelocity(string axe)
        {
            // +x is right, +y is up and +z is backwards.
            Vector3D speed = controller.GetShipVelocities().LinearVelocity;
            MatrixD matrix = controller.WorldMatrix.GetOrientation();
            Vector3D velocity = Vector3D.Transform(speed, MatrixD.Transpose(matrix));

            if (axe == "y") return -velocity.Y;
            if (axe == "z") return -velocity.Z;
            else return velocity.X;
        }

        double getShipMass()
        {
            double total = controller.CalculateShipMass().TotalMass;
            double net = controller.CalculateShipMass().BaseMass;
            double cargo = total - net; return (net + (cargo / xrate));
        }

        void gridSize()
        {
            float gs = (float)Me.CubeGrid.GridSize; Vector3D[] points = new Vector3D[4];

            OrientedBoundingBoxFaces _obbf = new OrientedBoundingBoxFaces(controller);
            _obbf.GetFaceCorners(OrientedBoundingBoxFaces.LookupFront, points);

            dim["w"] = (float)Math.Round((points[0] - points[1]).Length(), 1);
            dim["h"] = (float)Math.Round((points[0] - points[2]).Length(), 1); _obbf.GetFaceCorners(0, points);
            dim["l"] = (float)Math.Round((points[0] - points[2]).Length(), 1);
        }

        string vec2str(Vector3D v)
        {
            return v.GetDim(0) + ":" + v.GetDim(1) + ":" + v.GetDim(2);
        }

        Vector3D str2vec(string coord)
        {
            double x, y, z; string[] c = coord.Trim(':').Split(':'); int i = c.Count();

            if (c.Count() > 2)
            {
                while (i-- > 0)
                {
                    if (double.TryParse(c[i].Trim(), out z))
                    {
                        if (double.TryParse(c[i - 1].Trim(), out y))
                        {
                            if (double.TryParse(c[i - 2].Trim(), out x)) return new Vector3D(x, y, z);
                        }
                    }
                }
            }
            return new Vector3D(0, 0, 0);
        }

        string colorStr(Color cc) // {R:200 G:100 B:100 A:255}
        {
            string c = Convert.ToString(cc), v = ""; int a = 0, b = 0;

            while ((a = c.IndexOf(':', a)) != -1 && b < 3)
            {
                v += c.Substring(a, c.IndexOf(' ', a) - a); ++a; ++b;
            }
            return (v.Trim(':'));
        }

        Color getColor(string c)
        {
            string[] a = c.Split(':'); if (a.Count() != 3) a = new string[] { "0", "0", "0" };
            return (new Color(int.Parse(a[0]), int.Parse(a[1]), int.Parse(a[2])));
        }

        float numParse(string val)
        {
            int b = -1; string sep = $"0123456789-+.";

            for (int i = 0; i < val.Length; ++i)
            {
                bool num = (sep.IndexOf(val[i]) != -1);
                if (b < 0 && num) { b = i; }
                else if (!num && b >= 0) { val = val.Substring(b, i - b); break; }
                else if (val[i] == '.') sep = sep.Replace(".", "");
                else if (val[i] == '-') sep = sep.Replace("-", "");
                else if (val[i] == '+') sep = sep.Replace("+", "");
            }

            float f; float.TryParse(val, out f); return f;
        }

        bool myGrid(IMyTerminalBlock b) { return (Me.CubeGrid == b.CubeGrid); }

        bool myShip(IMyTerminalBlock b) { return b.IsSameConstructAs(Me); }

        // credit Whiplash141: base vectors Math
        double vectorAngleBetween(Vector3D a, Vector3D b)
        {
            return Math.Acos(a.Dot(b) / a.Length() / b.Length()) * 180 / Math.PI;
        }

        Vector3D vectorProjection(Vector3D a, Vector3D b)
        {
            Vector3D projection = a.Dot(b) / b.LengthSquared() * b; return projection;
        }

        int vectorCompareDirection(Vector3D a, Vector3D b)
        {
            double check = a.Dot(b); return check < 0 ? -1 : 1;
        }

        // inspired class by DeltaWing
        public class Thrusters
        {
            public List<IMyThrust> upThrust;
            public List<IMyThrust> downThrust;
            public List<IMyThrust> leftThrust;
            public List<IMyThrust> rightThrust;
            public List<IMyThrust> forwardThrust;
            public List<IMyThrust> backwardThrust;
            public List<IMyThrust> allThrust;

            public List<IMyThrust> upSubThrust;
            public List<IMyThrust> downSubThrust;
            public List<IMyThrust> forwardSubThrust;
            public List<IMyThrust> backwardSubThrust;

            public double ntUp;
            public double ntDown;
            public double ntLeft;
            public double ntRight;
            public double ntForward;
            public double ntBackward;

            public Thrusters(List<IMyThrust> allThrusters, IMyShipController controller)
            {
                upThrust = new List<IMyThrust>();
                downThrust = new List<IMyThrust>();
                leftThrust = new List<IMyThrust>();
                rightThrust = new List<IMyThrust>();
                forwardThrust = new List<IMyThrust>();
                backwardThrust = new List<IMyThrust>();
                allThrust = new List<IMyThrust>(allThrusters);

                upSubThrust = new List<IMyThrust>();
                downSubThrust = new List<IMyThrust>();
                forwardSubThrust = new List<IMyThrust>();
                backwardSubThrust = new List<IMyThrust>();

                ntUp = 0;
                ntDown = 0;
                ntLeft = 0;
                ntRight = 0;
                ntForward = 0;
                ntBackward = 0;

                foreach (var thruster in allThrusters)
                {
                    switch (GetDirection(controller, thruster))
                    {
                        case Base6Directions.Direction.Forward:
                            ntBackward += thruster.MaxEffectiveThrust;
                            if (thruster.CubeGrid != controller.CubeGrid) backwardSubThrust.Add(thruster);
                            backwardThrust.Add(thruster); break;

                        case Base6Directions.Direction.Backward:
                            ntForward += thruster.MaxEffectiveThrust;
                            if (thruster.CubeGrid != controller.CubeGrid)
                                forwardSubThrust.Add(thruster); forwardThrust.Add(thruster); break;

                        case Base6Directions.Direction.Left:
                            ntLeft += thruster.MaxEffectiveThrust; rightThrust.Add(thruster); break;

                        case Base6Directions.Direction.Right:
                            ntRight += thruster.MaxEffectiveThrust; leftThrust.Add(thruster); break;

                        case Base6Directions.Direction.Up:
                            ntDown += thruster.MaxEffectiveThrust;
                            if (thruster.CubeGrid != controller.CubeGrid) downSubThrust.Add(thruster);
                            downThrust.Add(thruster); break;

                        case Base6Directions.Direction.Down:
                            ntUp += thruster.MaxEffectiveThrust;
                            if (thruster.CubeGrid != controller.CubeGrid) upSubThrust.Add(thruster);
                            upThrust.Add(thruster); break;

                        default:
                            break;
                    }
                }
            }

            private List<IMyThrust> GetThrusterDir(Dir dir)
            {
                if (dir == Dir.Up)
                    return upThrust;
                else if (dir == Dir.Down)
                    return downThrust;
                else if (dir == Dir.Left)
                    return leftThrust;
                else if (dir == Dir.Right)
                    return rightThrust;
                else if (dir == Dir.Forward)
                    return forwardThrust;
                else if (dir == Dir.Backward)
                    return backwardThrust;
                else if (dir == Dir.All)
                    return allThrust;
                return null;
            }

            public void SetThrust(Dir dir, float thrust, bool enable)
            {
                var actualList = GetThrusterDir(dir);
                IMyThrust[] thrusters = new IMyThrust[actualList.Count];
                actualList.CopyTo(thrusters);

                foreach (var thruster in thrusters)
                {
                    if (thruster == null)
                    {
                        actualList.Remove(thruster); continue;
                    }
                    thruster.Enabled = (thrust > 0) || enable;
                    thruster.ThrustOverridePercentage = thrust;
                }
            }

            private Base6Directions.Direction GetDirection(IMyShipController reference, IMyThrust block)
            {
                return reference.WorldMatrix.GetClosestDirection(block.WorldMatrix.Forward);
            }
        }

        public enum Dir
        {
            Up,
            Down,
            Left,
            Right,
            Forward,
            Backward,
            All
        }

        // credit Wicorel: block orientation by corners
        public struct OrientedBoundingBoxFaces
        {
            public Vector3D[] Corners;
            public Vector3D Position;
            Vector3D localMax;
            Vector3D localMin;

            static int[] PointsLookupRight = { 1, 3, 5, 7 };
            static int[] PointsLookupLeft = { 0, 2, 4, 6 };

            static int[] PointsLookupTop = { 2, 3, 6, 7 };
            static int[] PointsLookupBottom = { 0, 1, 4, 5 };

            static int[] PointsLookupBack = { 4, 5, 6, 7 };
            static int[] PointsLookupFront = { 0, 1, 2, 3 };

            static int[][] PointsLookup = {
    PointsLookupRight, PointsLookupLeft,
    PointsLookupTop, PointsLookupBottom,
    PointsLookupBack, PointsLookupFront
  };

            public const int LookupRight = 0;
            public const int LookupLeft = 1;
            public const int LookupTop = 2;
            public const int LookupBottom = 3;
            public const int LookupBack = 4;
            public const int LookupFront = 5;

            public OrientedBoundingBoxFaces(IMyTerminalBlock block)
            {
                Corners = new Vector3D[8];
                if (block == null)
                {
                    Position = new Vector3D();
                    localMin = new Vector3D();
                    localMax = new Vector3D();
                    return;
                }

                localMin = new Vector3D(block.CubeGrid.Min) - new Vector3D(0.5, 0.5, 0.5);
                localMin *= block.CubeGrid.GridSize;
                localMax = new Vector3D(block.CubeGrid.Max) + new Vector3D(0.5, 0.5, 0.5);
                localMax *= block.CubeGrid.GridSize;

                var blockOrient = block.WorldMatrix.GetOrientation();
                var matrix = block.CubeGrid.WorldMatrix.GetOrientation() * MatrixD.Transpose(blockOrient);

                Vector3D.TransformNormal(ref localMin, ref matrix, out localMin);
                Vector3D.TransformNormal(ref localMax, ref matrix, out localMax);

                var tmpMin = Vector3D.Min(localMin, localMax);
                localMax = Vector3D.Max(localMin, localMax);
                localMin = tmpMin;

                var center = block.CubeGrid.GetPosition();

                Vector3D tmp2;
                Vector3D tmp3;
                tmp2 = localMin;
                Vector3D.TransformNormal(ref tmp2, ref blockOrient, out tmp2);
                tmp2 += center;

                tmp3 = localMax;
                Vector3D.TransformNormal(ref tmp3, ref blockOrient, out tmp3);
                tmp3 += center;

                BoundingBox bb = new BoundingBox(tmp2, tmp3);
                Position = bb.Center;

                Vector3D tmp;
                for (int i = 0; i < 8; i++)
                {
                    tmp.X = ((i & 1) == 0 ? localMin : localMax).X;
                    tmp.Y = ((i & 2) == 0 ? localMin : localMax).Y;
                    tmp.Z = ((i & 4) == 0 ? localMin : localMax).Z;
                    Vector3D.TransformNormal(ref tmp, ref blockOrient, out tmp);
                    tmp += center;
                    Corners[i] = tmp;
                }
            }

            public void GetFaceCorners(int face, Vector3D[] points, int index = 0)
            {
                face %= PointsLookup.Length;
                for (int i = 0; i < PointsLookup[face].Length; i++)
                {
                    points[index++] = Corners[PointsLookup[face][i]];
                }
            }
        }

        public class Progs
        {
            public string RUN { get; set; }
            public string OUT { get; set; }
        }

        public void debug(string t, bool add = false)
        {
            var list = new List<IMyTerminalBlock>();
            GridTerminalSystem.SearchBlocksOfName("Debug", list);

            IMyTextSurface scr = null;

            foreach (var b in list)
            {
                if (b is IMyTextSurfaceProvider)
                {
                    var s = b as IMyTextSurfaceProvider;

                    if (s.SurfaceCount > 0)
                    {
                        scr = s.GetSurface(lcdSur(b.CustomName, s.SurfaceCount, "debug"));
                    }
                }
            }

            if (scr != null)
            {
                scr.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
                scr.FontColor = getColor("255:215:0");
                scr.BackgroundColor = getColor("0:0:0");
                if (!add) scr.WriteText(t); else scr.WriteText(t + "\n", true);
            }
        }
    }
}
