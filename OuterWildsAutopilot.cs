using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using System;
using UnityEngine;
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
        // The major astral bodies that we may easily collide with
        static AstroObject.Name[] RelevantAB = {  AstroObject.Name.Sun, AstroObject.Name.CaveTwin, AstroObject.Name.TowerTwin , AstroObject.Name.TimberHearth,
            AstroObject.Name.BrittleHollow,  AstroObject.Name.GiantsDeep,  AstroObject.Name.DarkBramble,  AstroObject.Name.Comet};


        [HarmonyPrefix]
        [HarmonyPatch(typeof(Autopilot), nameof(Autopilot.FlyToDestination))]

        //If the boolean returned from the method is true the original method is still run
        //If false, the original method will be skipped
        public static bool Autopilot_FlyToDestination_Prefix(Autopilot __instance, ReferenceFrame referenceFrame)
        {
            // Get positions
            Vector3 destPos = referenceFrame.GetPosition();

            AstroObject.Name destinationName = referenceFrame.GetAstroObject().GetAstroObjectName();

            // Loop through the most important astral bodies
            for (int it = 0; it < RelevantAB.Length; it++)
            {
                AstroObject aObj = Locator.GetAstroObject(RelevantAB[it]);

                AstroObject.Name name = aObj.GetAstroObjectName();
                OuterWildsAutopilot.Instance.ModHelper.Console.WriteLine(AstroObject.AstroObjectNameToString(name));

                // Skip self check
                if (destinationName == name)
                    continue;

                // Get radiouses
                GravityVolume gravVol = aObj.GetGravityVolume();

                // Outer wilds uses the player as the zero coordinate of the world, so we use Vector3.zero
                // Stop autopilot if we would collide if going in a straight line
                if (LineCircleCollisionCheck(Vector3.zero, destPos, gravVol.GetCenterPosition(), gravVol._upperSurfaceRadius))
                {
                    return false;
                }
            }


            return true;
        }

        static private bool LineCircleCollisionCheck(Vector3 point1, Vector3 point2, Vector3 circlePos, float rad)
        {
            // Direction of the line
            Vector3 lineDir = point2 - point1;

            // Vector from circle center to line start
            Vector3 toCircle = point1 - circlePos;

            float a = Vector3.Dot(lineDir, lineDir);
            float b = 2f * Vector3.Dot(toCircle, lineDir);
            float c = Vector3.Dot(toCircle, toCircle) - rad * rad;

            // Quadratic equation
            float discriminant = b * b - 4f * a * c;

            // No intersection
            if (discriminant < 0f)
                return false;

            // Compute intersection points (t values)
            float sqrtD = Mathf.Sqrt(discriminant);
            float t1 = (-b - sqrtD) / (2f * a);
            float t2 = (-b + sqrtD) / (2f * a);

            // Check if either intersection lies within the segment [0,1]
            if ((t1 >= 0f && t1 <= 1f) || (t2 >= 0f && t2 <= 1f))
                return true;

            return false;
        }



        [HarmonyPrefix]
        [HarmonyPatch(typeof(Autopilot), nameof(Autopilot.ReadTranslationalInput))]

        // We will now check if the ship will collide with any astral body and if it will, stop the autopilot
        public static bool Autopilot_ReadTranslationalInput_Prefix(Autopilot __instance, ref Vector3 __result)
        {
			// Ship unusable, return to original function
			//if (__instance._isShipAutopilot && !__instance._shipResources.AreThrustersUsable())
			//	return true;
		
			
			return true;
		}      
    }
}
