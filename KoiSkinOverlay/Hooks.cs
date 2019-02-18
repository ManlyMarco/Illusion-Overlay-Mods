/*
    
    Original KoiSkinOverlay by essu, the poem too

    Yea,
    though I walk through the valley of the shadow of death, I will fear no takedown
    for Thou art with me; Thy praise and Thy frogposting they comfort me.
  
*/

using System.Collections.Generic;
using System.Reflection.Emit;
using Harmony;
using UnityEngine;

namespace KoiSkinOverlayX
{
    internal static class Hooks
    {
        public static void Init()
        {
            HarmonyInstance.Create(nameof(Hooks)).PatchAll(typeof(Hooks));
        }

        #region New method

        private static Texture RebuildTextureHook(CustomTextureCreate __instance, Texture texMain)
        {
            if (__instance is CustomTextureControl)
            {
                var overlay = __instance.trfParent?.GetComponent<KoiSkinOverlayController>();
                if (overlay == null) return texMain;

                if (overlay.ChaControl.customTexCtrlFace == __instance)
                    return overlay.ApplyOverlayToTex(texMain, TexType.FaceUnder);

                if (overlay.ChaControl.customTexCtrlBody == __instance)
                    return overlay.ApplyOverlayToTex(texMain, TexType.BodyUnder);
            }
            return texMain;
        }

        [HarmonyTranspiler, HarmonyPatch(typeof(CustomTextureCreate), "RebuildTextureAndSetMaterial")]
        public static IEnumerable<CodeInstruction> tpl_CustomTextureCreate_RebuildTextureAndSetMaterial(IEnumerable<CodeInstruction> _instructions)
        {
            /*
                 After SetRenderTarget, before Graphics.Blit
                 Composite texMain + overlay, using cache if available.
             */

            var instructions = new List<CodeInstruction>(_instructions);
            var texMain = AccessTools.Field(typeof(CustomTextureCreate), "texMain");

            var inserts = new[] {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, texMain),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Hooks), nameof(RebuildTextureHook))),
                new CodeInstruction(OpCodes.Stfld, texMain),
            };

            OpCode prev = OpCodes.Add;

            for (var i = 0; i < instructions.Count; i++)
            {
                var instruction = instructions[i];
                if (instruction.opcode == OpCodes.Call)
                {
                    if (instruction.ToString() != "call Void SetRenderTarget(UnityEngine.RenderTexture)") continue;
                    if (prev != OpCodes.Ldnull) continue;
                    instructions.InsertRange(i, inserts);
                    break;
                }
                prev = instruction.opcode;
            }

            return instructions;
        }

        #endregion

        #region Old method

        [HarmonyPostfix, HarmonyPatch(typeof(CustomTextureCreate), "RebuildTextureAndSetMaterial")]
        public static void post_CustomTextureCreate_RebuildTextureAndSetMaterial(CustomTextureCreate __instance, ref Texture __result)
        {
            if (__instance is CustomTextureControl)
            {
                var overlay = __instance.trfParent?.GetComponent<KoiSkinOverlayController>();
                if (overlay == null) return;

                var createTex = __result as RenderTexture;
                if (createTex == null) return;

                if (overlay.ChaControl.customTexCtrlFace == __instance)
                    overlay.ApplyOverlayToRT(createTex, TexType.FaceOver);
                else if (overlay.ChaControl.customTexCtrlBody == __instance)
                    overlay.ApplyOverlayToRT(createTex, TexType.BodyOver);
            }
        }

        #endregion
    }
}
