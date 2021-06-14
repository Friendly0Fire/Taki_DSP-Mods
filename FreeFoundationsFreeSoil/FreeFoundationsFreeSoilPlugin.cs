using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Security;
using System.Security.Permissions;
using BepInEx.Configuration;

[module: UnverifiableCode]
#pragma warning disable 618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore 618
namespace FreeFoundationsFreeSoil
{
    [BepInPlugin(ModGuid, ModName, ModVer)]
    [BepInProcess("DSPGAME.exe")]
    public class FreeFoundationsFreeSoilPlugin : BaseUnityPlugin
    {
        public const string ModGuid = "ca.radiantlabs.FreeFoundationsFreeSoil";
        public const string ModName = "FreeFoundationsFreeSoil";
        public const string ModVer = "1.0.0";

        public static bool FreeFoundationEnabled { get; private set; } = true;
        public static bool FreeSoilEnabled { get; private set; } = true;
        public static bool CollectSoilEnabled { get; private set; } = true;


        public static Harmony HarmonyInstance;

        public static bool BuildRangeEnabled { get; private set; } = true;
        public static int BuildRange { get; private set; } = 160;
        public const int BuildRangeMin = 80;
        public const int BuildRangeMax = 250;


        public static bool FoundationSizeEnabled { get; private set; } = true;
        public static int FoundationSize { get; private set; } = 20;
        public const int FoundationSizeMin = 10;
        public const int FoundationSizeMax = 30;

        public void Awake()
        {
            FreeFoundationEnabled = Config.Bind("Free Foundations", "Enable Free Foundations", true, "If enabled, you do not need Foundations to build them.").Value;
            FreeSoilEnabled = Config.Bind("Free Soil", "Enable Free Soil", true, "If enabled, you do not need Soil to build Foundations.").Value;
            CollectSoilEnabled = Config.Bind("Free Soil", "Enable Soil Collect", true, "If enabled, you will collect soil as usual. If disabled, it will not collect Soil.").Value;

            BuildRangeEnabled = Config.Bind("Build Range", "Enable Higher Build Range", true, "Enables/Disables the functionality to gain higher Build-Range").Value;
            BuildRange = Config.Bind("Build Range", "Build Range", BuildRange, new ConfigDescription("Vanilla value is 80. Mod max is 250. Default is 160.", new AcceptableValueRange<int>(BuildRangeMin, BuildRangeMax))).Value;

            FoundationSizeEnabled = Config.Bind("Foundation Size", "Enable Larger Foundation Brushes", true, "Enables/Disables the functionality to gain larger foundation brushes").Value;
            FoundationSize = Config.Bind("Foundation Size", "Foundation Size", FoundationSize, new ConfigDescription("Vanilla value is 10. Mod max is 30. Default is 20.", new AcceptableValueRange<int>(FoundationSizeMin, FoundationSizeMax))).Value;

            HarmonyInstance = new Harmony(ModGuid);

            HarmonyInstance.PatchAll(typeof(FreeFoundationsFreeSoilPatch));

            HarmonyInstance.PatchAll(typeof(BuildingRangePatchImport));
            HarmonyInstance.PatchAll(typeof(BuildingRangePatchExport));
        }

        public void OnDestroy()
        {
            HarmonyInstance.UnpatchAll(ModGuid);
        }

    }

    [HarmonyPatch]
    public class BuildingRangePatchImport
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerAction_Build), "Init")]
        public static void SizePatch(PlayerAction_Build __instance)
        {
            if (FreeFoundationsFreeSoilPlugin.FoundationSizeEnabled) {
                int size = FreeFoundationsFreeSoilPlugin.FoundationSize * FreeFoundationsFreeSoilPlugin.FoundationSize;
                __instance.reformTool.cursorIndices = new int[size];
                __instance.reformTool.cursorPoints = new Vector3[size];
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Mecha), "Import")]
        public static void Postfix(Mecha __instance)
        {
            if (FreeFoundationsFreeSoilPlugin.BuildRangeEnabled) {
                __instance.buildArea = (float)FreeFoundationsFreeSoilPlugin.BuildRange;
            }
        }
    }
    public class BuildingRangePatchExport
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Mecha), "Export")]
        public static void Prefix(Mecha __instance)
        {
            if (FreeFoundationsFreeSoilPlugin.BuildRangeEnabled) {
                __instance.buildArea = 80f;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Mecha), "Export")]
        public static void Postfix(Mecha __instance)
        {
            if (FreeFoundationsFreeSoilPlugin.BuildRangeEnabled) {
                __instance.buildArea = (float)FreeFoundationsFreeSoilPlugin.BuildRange;
            }
        }
    }

    [HarmonyPatch]
    public class FreeFoundationsFreeSoilPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(BuildTool_Reform), "ReformAction")]
        public static bool ReformActionPrefix(ref BuildTool_Reform __instance, ref Vector3 ___lastReformPoint)
        {
            bool flag = false;
            int[] consumeRegister = GameMain.statistics.production.factoryStatPool[__instance.factory.index].consumeRegister;
            if (__instance.brushSize < 1) {
                __instance.brushSize = 1;
            }
            else if (__instance.brushSize > FreeFoundationsFreeSoilPlugin.FoundationSize) {
                __instance.brushSize = FreeFoundationsFreeSoilPlugin.FoundationSize;
            }
            if ((__instance.reformCenterPoint - __instance.player.position).sqrMagnitude > __instance.player.mecha.buildArea * __instance.player.mecha.buildArea) {
                if (!VFInput.onGUIOperate) {
                    __instance.actionBuild.model.cursorText = "目标超出范围".Translate();
                    __instance.actionBuild.model.cursorState = -1;
                    UICursor.SetCursor(ECursor.Ban);
                }
            }
            else {
                if (!VFInput.onGUIOperate) {
                    UICursor.SetCursor(ECursor.Reform);
                }
                bool flag2 = false;
                if (VFInput._cursorPlusKey.onDown) {
                    if (__instance.brushSize < FreeFoundationsFreeSoilPlugin.FoundationSize) {
                        __instance.brushSize++;
                        flag2 = true;
                        for (int i = 0; i < __instance.brushSize * __instance.brushSize; i++) {
                            __instance.cursorIndices[i] = -1;
                        }
                    }
                }
                else if (VFInput._cursorMinusKey.onDown && __instance.brushSize > 1) {
                    __instance.brushSize--;
                    flag2 = true;
                }
                float radius = 0.990946f * (float)__instance.brushSize;
                int num = __instance.factory.ComputeFlattenTerrainReform(__instance.cursorPoints, __instance.reformCenterPoint, radius, __instance.cursorPointCount);
                if (__instance.cursorValid && !VFInput.onGUIOperate) {
                    if (num > 0) {
                        __instance.actionBuild.model.cursorText = "沙土消耗".Translate() + " " + num + " " + "个沙土".Translate() + "\n" + "改造大小".Translate() + __instance.brushSize + "x" + __instance.brushSize;
                    }
                    else if (num == 0) {
                        __instance.actionBuild.model.cursorText = "改造大小".Translate() + __instance.brushSize + "x" + __instance.brushSize;
                    }
                    else {
                        int num2 = -num;
                        __instance.actionBuild.model.cursorText = "沙土获得".Translate() + " " + num2 + " " + "个沙土".Translate() + "\n" + "改造大小".Translate() + __instance.brushSize + "x" + __instance.brushSize;
                    }
                    if (VFInput._buildConfirm.pressing) {
                        bool onDown = VFInput._buildConfirm.onDown;
                        if (onDown) {
                            __instance.drawing = true;
                        }
                        if (__instance.drawing) {
                            flag = true;
                            if ((___lastReformPoint.x != __instance.reformCenterPoint.x || ___lastReformPoint.y != __instance.reformCenterPoint.y || ___lastReformPoint.z != __instance.reformCenterPoint.z || flag2) && !VFInput.onGUI) {
                                __instance.factory.FlattenTerrainReform(__instance.reformCenterPoint, radius, __instance.brushSize, __instance.buryVeins);
                                VFAudio.Create("reform-terrain", null, __instance.reformCenterPoint, play: true, 4);
                                int num4 = __instance.brushSize * __instance.brushSize;
                                for (int j = 0; j < num4; j++) {
                                    int num5 = __instance.cursorIndices[j];
                                    PlatformSystem platformSystem = __instance.factory.platformSystem;
                                    if (num5 >= 0) {
                                        int reformType = platformSystem.GetReformType(num5);
                                        int reformColor = platformSystem.GetReformColor(num5);
                                        if (reformType != __instance.brushType || reformColor != __instance.brushColor) {
                                            __instance.factory.platformSystem.SetReformType(num5, __instance.brushType);
                                            __instance.factory.platformSystem.SetReformColor(num5, __instance.brushColor);
                                        }
                                    }
                                }
                                if (__instance.player.inhandItemCount > 0) {
                                    __instance.player.UseHandItems(__instance.cursorPointCount);
                                }
                                int itemId = __instance.handItem.ID;
                                consumeRegister[itemId] += __instance.cursorPointCount;
                                GameMain.gameScenario.NotifyOnBuild(__instance.player.planetId, __instance.handItem.ID, 0);
                            }
                            ___lastReformPoint = __instance.reformCenterPoint;
                        }
                        else {
                            ___lastReformPoint = Vector3.zero;
                        }
                    }
                    else {
                        __instance.drawing = false;
                        ___lastReformPoint = Vector3.zero;
                    }
                }
            }
            if (!flag) {
                __instance.drawing = flag;
            }
            return false;
        }
    }
}
