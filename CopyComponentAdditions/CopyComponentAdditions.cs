using Elements.Assets;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.Store;
using FrooxEngine.UIX;
using HarmonyLib;
using ResoniteModLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;

namespace CopyComponentAdditions
{
    public class CopyComponentAdditions : ResoniteMod
    {
        public static ModConfiguration Config;


        public override string Author => "989onan";

        public override string Link => "https://github.com/989onan/CopyComponentAdditions";

        public override string Name => "CopyComponentAdditions";

        public override string Version => "0.0.0";


        public override void OnEngineInit()
        {
            Harmony harmony = new Harmony($"{Author}.{Name}");
            Msg("patching methods");

            //AccessTools.GetDeclaredMethods(typeof(SlotComponentReceiver))

            //my terrible way of doing things - @989onan
            /*
            Type internal_type_async = null;
            MethodInfo internal_sync_method = null;

            MethodInfo MethodThing = typeof(SlotComponentReceiver).FirstMethod((i) => {
                Msg(i.Name);


                return i.GetMethodBody().LocalVariables.FirstOrDefault((j) =>
                {

                    Msg(j.LocalType);
                    if(j.LocalType.ToString().Contains("DisplayClass1_1"))
                    {
                        internal_type_async = j.LocalType;
                        return true;
                    }
                    return false;
                }, null) != null;
            });
            if(internal_type_async != null) 
            {
                //FieldInfo hello = internal_type_async.GetAllNestedTypes().FirstOrDefault((i) => i.Name == "IAsyncStateMachine.MoveNext()");
                foreach (MethodInfo item in ((Type)internal_type_async.GetMembers(BindingFlags.NonPublic)[0]).EnumerateAllInstanceMethods())
                {
                    if (item.Name == "MoveNext")
                    {
                        internal_sync_method = item;
                    }
                }
            }*/
            //99% credit to NepuShiro!
            Type stateMachineType = typeof(SlotComponentReceiver).InnerTypes().FirstOrDefault(x => x.Name.StartsWith("<>") && x.InnerTypes().Any());

            MethodInfo stolencodethingy = AccessTools.GetDeclaredMethods(stateMachineType).FirstOrDefault(x => x.Name == "<TryReceive>b__1");


            MethodInfo method = AccessTools.AsyncMoveNext(AccessTools.FirstMethod(stateMachineType, x => x.ReturnType == typeof(Task)));

            harmony.Patch(stolencodethingy, transpiler: new HarmonyMethod(DynBonePatch.Transpiler2));

            harmony.Patch(method, transpiler: new HarmonyMethod(DynBonePatch.Transpiler));


            harmony.PatchAll();

        }

        //99% credit to NepuShiro!
        public static class DynBonePatch
        {
            public static List<CodeInstruction> codes3 = new List<CodeInstruction>(); //points to loading the target slot field (syncref<slot>) within the code for pressing the copy component button context menu.
            public static FieldInfo component_field = null; //points to the "Component" field within the async method which stores the component being copied.
            public static FieldInfo load_target = null; //points to the "Target" field within the SlotReciever which stores the syncref<slot> of the slot the component is being copied to.

            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                Msg("starting evaluation of code");
                List<CodeInstruction> codes = instructions.ToList();
                bool first_result = false;
                int insert_point = -1;
                for (int i = 0; i < instructions.Count(); i++)
                {
                    CodeInstruction code = codes[i];
                    if (code.operand?.ToString().Contains("GetResult") == true)
                    {
                        first_result = true;
                        continue;
                    }
                    if (first_result && code.opcode == OpCodes.Dup)
                    {
                        insert_point = i;
                        break;
                    }
                }

                Msg("Insert point is: " + insert_point.ToString());
                Msg("Code is: " + codes[insert_point]);


                List<CodeInstruction> codes2 =
                [
                    codes[8],//load "this" of the class that can load the component being copied.
                    codes[8],codes[9],codes[10],//load "this" of the class that can load the target slot syncref.                    
                    //new CodeInstruction(OpCodes.Ldarg_0),
                    //new CodeInstruction(OpCodes.Castclass, typeof(Object)),
                    new CodeInstruction(OpCodes.Call, ((Delegate)DynBonePatch.patch_context_menu).Method),
                    new CodeInstruction(OpCodes.Dup)
                ];

                codes.InsertRange(insert_point + 1, codes2);

                return codes.AsEnumerable();
            }

            public static IEnumerable<CodeInstruction> Transpiler2(IEnumerable<CodeInstruction> instructions)
            {

                //codes3.AddRange([instructions.ToList()[5], instructions.ToList()[6]]);
                component_field = (FieldInfo)instructions.ToList()[6].operand; //yoink the field info for loading in the component being copied field under the compiler generated class.
                load_target = (FieldInfo)instructions.ToList()[3].operand; //yoink the loadfield instruction for SyncRef<Slot> Target.
                return instructions;
            }

            public static void patch_context_menu(ContextMenu menu, object __instance, object __instance2)
            {
                try
                {
                    Msg("attach component 1");
                    FrooxEngine.Component component = (FrooxEngine.Component)component_field.GetValue(__instance);
                    SyncRef<Slot> target = (SyncRef<Slot>)load_target.GetValue(__instance2);
                    //Msg("attach component 1");
                    Msg("component type is: " + component.GetType().Name);
                    if (component.GetType().IsAssignableTo(typeof(DynamicBoneChain)))
                    {
                        DynamicBoneChain componentdyn = component as DynamicBoneChain;
                        ContextMenuItem newitem = menu.AddItem("Copy Dynamic Bone Linked", ((Uri)(null)), new colorX(1, 1, 1, 1));

                        newitem.Button.LocalPressed += (IButton b, ButtonEventData e) => {
                            DynamicBoneChain newcomponent = target.Target.AttachComponent<DynamicBoneChain>();
                            newcomponent.CollideWithBody.DriveFrom(componentdyn.CollideWithBody, true);
                            newcomponent.CollideWithHead.DriveFrom(componentdyn.CollideWithHead, true);
                            newcomponent.CollideWithLeftHand.DriveFrom(componentdyn.CollideWithLeftHand, true);
                            newcomponent.CollideWithOwnBody.DriveFrom(componentdyn.CollideWithOwnBody, true);
                            newcomponent.CollideWithRightHand.DriveFrom(componentdyn.CollideWithRightHand, true);
                            newcomponent.Damping.DriveFrom(componentdyn.Damping, true);
                            newcomponent.Stiffness.DriveFrom(componentdyn.Stiffness, true);
                            newcomponent.Elasticity.DriveFrom(componentdyn.Elasticity, true);
                            newcomponent.Inertia.DriveFrom(componentdyn.Inertia, true);
                            newcomponent.InertiaForce.DriveFrom(componentdyn.InertiaForce, true);
                            newcomponent.GrabPriority.DriveFrom(componentdyn.GrabPriority, true);
                            newcomponent.GrabSlipping.DriveFrom(componentdyn.GrabSlipping, true);
                            newcomponent.GrabRadiusTolerance.DriveFrom(componentdyn.GrabRadiusTolerance, true);
                            newcomponent.BaseBoneRadius.DriveFrom(componentdyn.BaseBoneRadius, true);
                            newcomponent.AllowSteal.DriveFrom(componentdyn.AllowSteal, true);
                            newcomponent.ActiveUserRootOnly.DriveFrom(componentdyn.ActiveUserRootOnly, true);
                            newcomponent.DynamicPlayerCollision.DriveFrom(componentdyn.DynamicPlayerCollision, true);
                            newcomponent.Gravity.DriveFrom(componentdyn.Gravity, true);
                            newcomponent.GravitySpace.Default.DriveFrom(componentdyn.GravitySpace.Default, true);
                            newcomponent.GravitySpace.LocalSpace.DriveFrom(componentdyn.GravitySpace.LocalSpace, true);
                            newcomponent.GravitySpace.OverrideRootSpace.DriveFrom(componentdyn.GravitySpace.OverrideRootSpace, true);
                            newcomponent.GravitySpace.UseParentSpace.DriveFrom(componentdyn.GravitySpace.UseParentSpace, true);
                            newcomponent.IgnoreOwnLeftHand.DriveFrom(componentdyn.IgnoreOwnLeftHand, true);
                            newcomponent.IgnoreOwnRightHand.DriveFrom(componentdyn.IgnoreOwnRightHand, true);
                            newcomponent.IgnoreGrabOnFirstBone.DriveFrom(componentdyn.IgnoreGrabOnFirstBone, true);
                            newcomponent.IsGrabbable.DriveFrom(componentdyn.IsGrabbable, true);
                            newcomponent.GrabVibration.DriveFrom(componentdyn.GrabVibration, true);
                            newcomponent.GrabTerminalBones.DriveFrom(componentdyn.GrabTerminalBones, true);
                            newcomponent.MaxStretchRatio.DriveFrom(componentdyn.MaxStretchRatio, true);
                            newcomponent.LocalForce.DriveFrom(componentdyn.LocalForce, true);
                            newcomponent.Stiffness.DriveFrom(componentdyn.Stiffness, true);
                            newcomponent.StretchRestoreSpeed.DriveFrom(componentdyn.StretchRestoreSpeed, true);
                            newcomponent.UseUserGravityDirection.DriveFrom(componentdyn.UseUserGravityDirection, true);
                            newcomponent.UseLocalUserSpace.DriveFrom(componentdyn.UseLocalUserSpace, true);




                        };
                    }
                }
                catch (Exception)
                {
                    //idc, pass.
                }

            }


        }


    }
}