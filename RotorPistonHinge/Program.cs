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
        /*
        * R e a d m e
        * ----------- Updated 21/02/2022
        * Cockpit Piston, Rotor & Hinge Controller
        * By Patrick Hansen / Tekk  - https://www.youtube.com/patrickhansen101
        * 
        * Script for controlling up to 6 pistons/rotors/hinges or groups thereof, using normal control inputs while in a cockpit
        * 
        * The comments should explain what to do, the script should throw errors on the PB if you've done something wrong!
        */

        //*** If you want to reverse individual pistons'/rotors' movement, add "rev" to their custom data ***

        //Cockpit/remote name, if the name is invalid/empty then the first controller found will be used.
        string Controller_name = "Cockpit";

        //Block/group names (eg. "Piston 2"), leave empty (eg. "") to disable that output
        string AD_Name = "ADPistons";     //Name of A & D                  Block / Block group
        string WS_Name = "";                     //Name of W & S                  Block / Block group
        string QE_Name = "";                      //Name of Q & E                  Block / Block group
        string SpaceC_Name = "";                 //Name of Space & C              Block / Block group
        string MouseY_Name = "";                //Name of MouseX/UpDown Arrow    Block / Block group
        string MouseX_Name = "";                //Name of MouseY/LeftRight Arrow Block / Block group    

        //Speed multipliers - negative values invert direction
        double AD_Movespeed = 10.0;           //A & D speed multiplier.
        double WS_Movespeed = -2.0;         //W & S speed multiplier.
        double QE_Movespeed = 10.0;          //Q & E speed multiplier.
        double SpaceC_Movespeed = 10.0;      //Space & C speed multiplier.
        double MouseY_Movespeed = 0.1;       //MouseX speed multiplier.
        double MouseX_Movespeed = -0.1;      //MouseY speed multiplier.

        //Acceleration values
        bool Use_Piston_Acceleration = true;    //should Piston acceleration be used? (true/false)
        double PistonAcc = 10.0;                         //Piston acceleration in m/s / second (m/s^2)

        bool Use_Rotor_Acceleration = false;    //should Rotor acceleration be used? (true/false)
        double RotorAcc = 60.0;                         //Rotor acceleration in RPM/second








        //DON'T Change anything below this line :) ---------------------------------------------------------------------------------------------------

        bool Use_AD = false;
        bool Use_WS = false;
        bool Use_QE = false;
        bool Use_SpaceC = false;
        bool Use_MouseX = false;
        bool Use_MouseY = false;

        bool ADPist = false;
        bool WSPist = false;
        bool QEPist = false;
        bool SCPist = false;
        bool MYPist = false;
        bool MXPist = false;

        bool isTurretController = false;

        bool ControlDropped = false;

        IMyTerminalBlock controller;
        List<IMyTerminalBlock> controllers = new List<IMyTerminalBlock>();
        List<IMyPistonBase> ADPistons = new List<IMyPistonBase>();
        List<IMyPistonBase> WSPistons = new List<IMyPistonBase>();
        List<IMyPistonBase> QEPistons = new List<IMyPistonBase>();
        List<IMyPistonBase> SpaceCPistons = new List<IMyPistonBase>();
        List<IMyPistonBase> MouseXPistons = new List<IMyPistonBase>();
        List<IMyPistonBase> MouseYPistons = new List<IMyPistonBase>();

        List<IMyMotorStator> ADRotors = new List<IMyMotorStator>();
        List<IMyMotorStator> WSRotors = new List<IMyMotorStator>();
        List<IMyMotorStator> QERotors = new List<IMyMotorStator>();
        List<IMyMotorStator> SpaceCRotors = new List<IMyMotorStator>();
        List<IMyMotorStator> MouseXRotors = new List<IMyMotorStator>();
        List<IMyMotorStator> MouseYRotors = new List<IMyMotorStator>();

        List<IMyPistonBase> AllPists = new List<IMyPistonBase>();
        List<IMyMotorStator> AllRots = new List<IMyMotorStator>();


        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;

            if (AD_Name.Trim(' ').Length > 0) { Use_AD = true; }
            if (WS_Name.Trim(' ').Length > 0) { Use_WS = true; }
            if (QE_Name.Trim(' ').Length > 0) { Use_QE = true; }
            if (SpaceC_Name.Trim(' ').Length > 0) { Use_SpaceC = true; }
            if (MouseY_Name.Trim(' ').Length > 0) { Use_MouseY = true; }
            if (MouseX_Name.Trim(' ').Length > 0) { Use_MouseX = true; }

            if (GridTerminalSystem.GetBlockWithName(Controller_name) != null)
            {
                controller = GridTerminalSystem.GetBlockWithName(Controller_name);
            }
            else
            {
                GridTerminalSystem.GetBlocksOfType<IMyShipController>(controllers);
                if (controllers.Count > 0)
                {
                    controller = controllers[0];
                }
                else
                {
                    GridTerminalSystem.GetBlocksOfType<IMyTurretControlBlock>(controllers);
                    if (controllers.Count > 0)
                        controller = controllers[0];
                    else
                        throw new System.InvalidOperationException("No controllers found!\nAdd one and recompile!");
                }
            }

            isTurretController = controller.DefinitionDisplayNameText.Contains("Turret");

            if (Use_AD) { if (IsPiston(AD_Name)) { GetPistons(AD_Name, ADPistons); ADPist = true; } else { GetRotors(AD_Name, ADRotors); } }
            if (Use_WS) { if (IsPiston(WS_Name)) { GetPistons(WS_Name, WSPistons); WSPist = true; } else { GetRotors(WS_Name, WSRotors); } }
            if (Use_QE) { if (IsPiston(QE_Name)) { GetPistons(QE_Name, QEPistons); QEPist = true; } else { GetRotors(QE_Name, QERotors); } }
            if (Use_SpaceC) { if (IsPiston(SpaceC_Name)) { GetPistons(SpaceC_Name, SpaceCPistons); SCPist = true; } else { GetRotors(SpaceC_Name, SpaceCRotors); } }
            if (Use_MouseY) { if (IsPiston(MouseY_Name)) { GetPistons(MouseY_Name, MouseYPistons); MXPist = true; } else { GetRotors(MouseY_Name, MouseYRotors); } }
            if (Use_MouseX) { if (IsPiston(MouseX_Name)) { GetPistons(MouseX_Name, MouseXPistons); MYPist = true; } else { GetRotors(MouseX_Name, MouseXRotors); } }
        }


        public void Main(string argument, UpdateType updateSource)
        {
            if (IsControlled())
            {
                if (ControlDropped) ControlDropped = false;
                if (Use_AD)
                {
                    if (ADPist) { for (int i = 0; i < ADPistons.Count; i++) { AccMod(ADPistons[i], GetMoveIndicator().X * (ADPistons[i].CustomData.ToLower().Contains("rev") ? -(float)AD_Movespeed : (float)AD_Movespeed)); } }
                    else { for (int i = 0; i < ADRotors.Count; i++) { AccMod(ADRotors[i], GetMoveIndicator().X * (ADRotors[i].CustomData.ToLower().Contains("rev") ? -(float)AD_Movespeed : (float)AD_Movespeed)); } }
                }
                if (Use_WS)
                {
                    if (WSPist) { for (int i = 0; i < WSPistons.Count; i++) { AccMod(WSPistons[i], GetMoveIndicator().Z * (WSPistons[i].CustomData.ToLower().Contains("rev") ? -(float)WS_Movespeed : (float)WS_Movespeed)); } }
                    else { for (int i = 0; i < WSRotors.Count; i++) { AccMod(WSRotors[i], GetMoveIndicator().Z * (WSRotors[i].CustomData.ToLower().Contains("rev") ? -(float)WS_Movespeed : (float)WS_Movespeed)); } }
                }
                if (Use_QE)
                {
                    if (QEPist) { for (int i = 0; i < QEPistons.Count; i++) { AccMod(QEPistons[i], GetRollIndicator() * (QEPistons[i].CustomData.ToLower().Contains("rev") ? -(float)QE_Movespeed : (float)QE_Movespeed)); } }
                    else { for (int i = 0; i < QERotors.Count; i++) { AccMod(QERotors[i], GetRollIndicator() * (QERotors[i].CustomData.ToLower().Contains("rev") ? -(float)QE_Movespeed : (float)QE_Movespeed)); } }
                }
                if (Use_SpaceC)
                {
                    if (SCPist) { for (int i = 0; i < SpaceCPistons.Count; i++) { AccMod(SpaceCPistons[i], GetMoveIndicator().Y * (SpaceCPistons[i].CustomData.ToLower().Contains("rev") ? -(float)SpaceC_Movespeed : (float)SpaceC_Movespeed)); } }
                    else { for (int i = 0; i < SpaceCRotors.Count; i++) { AccMod(SpaceCRotors[i], GetMoveIndicator().Y * (SpaceCRotors[i].CustomData.ToLower().Contains("rev") ? -(float)SpaceC_Movespeed : (float)SpaceC_Movespeed)); } }
                }
                if (Use_MouseX)
                {
                    if (MYPist) { for (int i = 0; i < MouseXPistons.Count; i++) { AccMod(MouseXPistons[i], GetRotationIndicator().Y * (MouseXPistons[i].CustomData.ToLower().Contains("rev") ? -(float)MouseX_Movespeed : (float)MouseX_Movespeed)); } }
                    else { for (int i = 0; i < MouseXRotors.Count; i++) { AccMod(MouseXRotors[i], GetRotationIndicator().Y * (MouseXRotors[i].CustomData.ToLower().Contains("rev") ? -(float)MouseX_Movespeed : (float)MouseX_Movespeed)); } }
                }
                if (Use_MouseY)
                {
                    if (MXPist) { for (int i = 0; i < MouseYPistons.Count; i++) { AccMod(MouseYPistons[i], GetRotationIndicator().X * (MouseYPistons[i].CustomData.ToLower().Contains("rev") ? -(float)MouseY_Movespeed : (float)MouseY_Movespeed)); } }
                    else { for (int i = 0; i < MouseYRotors.Count; i++) { AccMod(MouseYRotors[i], GetRotationIndicator().X * (MouseYRotors[i].CustomData.ToLower().Contains("rev") ? -(float)MouseY_Movespeed : (float)MouseY_Movespeed)); } }
                }
            }
            else
            {
                if (!ControlDropped)
                {
                    ControlDropped = true;
                    foreach (IMyPistonBase pist in AllPists)
                        pist.Velocity = 0;
                    foreach (IMyMotorStator rot in AllRots)
                        rot.TargetVelocityRPM = 0;
                }
            }
        }

        public Vector3 GetMoveIndicator()
        {
            if (isTurretController)
                return ((IMyTurretControlBlock)controller).MoveIndicator;
            else
                return ((IMyShipController)controller).MoveIndicator;
        }

        public Vector2 GetRotationIndicator()
        {
            if (isTurretController)
                return ((IMyTurretControlBlock)controller).RotationIndicator;
            else
                return ((IMyShipController)controller).RotationIndicator;
        }

        public float GetRollIndicator()
        {
            if (isTurretController)
                return ((IMyTurretControlBlock)controller).RollIndicator;
            else
                return ((IMyShipController)controller).RollIndicator;
        }

        public bool IsControlled()
        {
            if (isTurretController)
                return ((IMyTurretControlBlock)controller).IsUnderControl;
            else
                return ((IMyShipController)controller).IsUnderControl;
        }

        public bool IsPiston(string name)
        {
            if (GridTerminalSystem.GetBlockWithName(name) != null)
            {
                return GridTerminalSystem.GetBlockWithName(name).GetType().ToString().Contains("Piston");
            }
            else
            {
                if (GridTerminalSystem.GetBlockGroupWithName(name) != null)
                {
                    IMyBlockGroup tempGroup = GridTerminalSystem.GetBlockGroupWithName(name);
                    List<IMyTerminalBlock> tempList = new List<IMyTerminalBlock>();
                    tempGroup.GetBlocks(tempList);
                    if (tempList.Count > 0) { return tempList[0].GetType().ToString().Contains("Piston"); } else { throw new System.InvalidOperationException(name + " not found, correct or clear name"); }
                }
                else { throw new System.InvalidOperationException(name + " not found, correct or clear name"); }
            }
        }

        public void GetPistons(string name, List<IMyPistonBase> PistList)
        {
            if (GridTerminalSystem.GetBlockWithName(name) as IMyPistonBase != null)
            {
                PistList.Add(GridTerminalSystem.GetBlockWithName(name) as IMyPistonBase);
                AllPists.Add(GridTerminalSystem.GetBlockWithName(name) as IMyPistonBase);
            }
            else
            {
                if (GridTerminalSystem.GetBlockGroupWithName(name) != null)
                {
                    IMyBlockGroup tempGroup = GridTerminalSystem.GetBlockGroupWithName(name);
                    List<IMyTerminalBlock> tempList = new List<IMyTerminalBlock>();
                    tempGroup.GetBlocks(tempList);
                    for (int i = 0; i < tempList.Count; i++)
                    {
                        PistList.Add(tempList[i] as IMyPistonBase);
                        AllPists.Add(tempList[i] as IMyPistonBase);
                    }
                    if (PistList.Count == 0) { throw new System.InvalidOperationException(name + " not found, correct or clear name"); }
                }
                else
                {
                    throw new System.InvalidOperationException(name + " not found, correct or clear name");
                }
            }
        }

        public void GetRotors(string name, List<IMyMotorStator> RotList)
        {
            if (GridTerminalSystem.GetBlockWithName(name) as IMyMotorStator != null)
            {
                RotList.Add(GridTerminalSystem.GetBlockWithName(name) as IMyMotorStator);
                AllRots.Add(GridTerminalSystem.GetBlockWithName(name) as IMyMotorStator);
            }
            else
            {
                if (GridTerminalSystem.GetBlockGroupWithName(name) != null)
                {
                    IMyBlockGroup tempGroup = GridTerminalSystem.GetBlockGroupWithName(name);
                    List<IMyTerminalBlock> tempList = new List<IMyTerminalBlock>();
                    tempGroup.GetBlocks(tempList);
                    for (int i = 0; i < tempList.Count; i++)
                    {
                        RotList.Add(tempList[i] as IMyMotorStator);
                        AllRots.Add(tempList[i] as IMyMotorStator);
                    }
                    if (RotList.Count == 0) { throw new System.InvalidOperationException(name + " not found, correct or clear name"); }
                }
                else { throw new System.InvalidOperationException(name + " not found, correct or clear name"); }
            }
        }

        public void AccMod(IMyPistonBase pist, float vel)
        {
            if (Use_Piston_Acceleration)
            {
                float CurVel = pist.Velocity;
                if (CurVel < vel)
                {
                    if (CurVel + (PistonAcc / 60) < vel)
                        pist.Velocity = CurVel + ((float)PistonAcc / 60);
                    else
                        pist.Velocity = vel;
                }
                else
                {
                    if (CurVel - (PistonAcc / 60) > vel)
                        pist.Velocity = CurVel - ((float)PistonAcc / 60);
                    else
                        pist.Velocity = vel;
                }
            }
            else
            {
                pist.Velocity = vel;
            }
        }

        public void AccMod(IMyMotorStator rot, float vel)
        {
            if (Use_Rotor_Acceleration)
            {
                float CurVel = rot.TargetVelocityRPM;
                if (CurVel < vel)
                {
                    if (CurVel + (RotorAcc / 60) < vel)
                        rot.TargetVelocityRPM = CurVel + ((float)RotorAcc / 60);
                    else
                        rot.TargetVelocityRPM = vel;
                }
                else
                {
                    if (CurVel - (RotorAcc / 60) > vel)
                        rot.TargetVelocityRPM = CurVel - ((float)RotorAcc / 60);
                    else
                        rot.TargetVelocityRPM = vel;
                }
            }
            else
            {
                rot.TargetVelocityRPM = vel;
            }
        }
    }
}
