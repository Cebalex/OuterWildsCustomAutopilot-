using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using System;
using System.Reflection;

namespace OuterWildsAutopilot
{
    public class OuterWildsAutopilot : ModBehaviour
    {
        public static OuterWildsAutopilot Instance;

        // You won't be able to access OWML's mod helper in Awake.
        public void Awake()
        {
            Instance = this;

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
        }

        public void Start()
        {
            // Starting here, you'll have access to OWML's mod helper.
            ModHelper.Console.WriteLine($"My mod {nameof(OuterWildsAutopilot)} is loaded!", MessageType.Success);

            new Harmony("Alya.OuterWildsAutopilot").PatchAll(Assembly.GetExecutingAssembly());

            // Example of accessing game code.
            OnCompleteSceneLoad(OWScene.TitleScreen, OWScene.TitleScreen); // We start on title screen
            LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;
        }

        public void OnCompleteSceneLoad(OWScene previousScene, OWScene newScene)
        {
            if (newScene != OWScene.SolarSystem) return;
            ModHelper.Console.WriteLine("Loaded into solar system!", MessageType.Success);
        }
    }


    [HarmonyPatch]
    public class AutopilotPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DeathManager), nameof(DeathManager.KillPlayer))]

        //If the boolean returned from the method is true the original method is still run
        //If false, the original method will be skipped
        public static bool DeathManager_KillPlayer_Prefix()
        {
            OuterWildsAutopilot.Instance.ModHelper.Console.WriteLine("The player has died! oh no!");
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(QuantumMoon), nameof(QuantumMoon.ChangeQuantumState))]
        public static void QuantumMoon_ChangeQuantumState_Postfix(QuantumMoon __instance)
        {
            OuterWildsAutopilot.Instance.ModHelper.Console.WriteLine($"The quantum moon is now at state index: {__instance._stateIndex}!");
        }
    }
}
