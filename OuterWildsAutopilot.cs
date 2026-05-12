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

        private bool _redirectShip = false;

        private float _curTimerSafetyCheck = 0.0f;
        private float _timerSafetyCheck = 1.0f;

        private Vector3 _pathOffset;

        // Because of gravity, we will consider routes that just scrape by also dangerous
        private float safetyRadIncrease = 1.25f;

        // The major astral bodies that we may easily collide with
        static AstroObject.Name[] RelevantAB = {  AstroObject.Name.Sun, AstroObject.Name.CaveTwin, AstroObject.Name.TowerTwin , AstroObject.Name.TimberHearth,
            AstroObject.Name.BrittleHollow,  AstroObject.Name.GiantsDeep,  AstroObject.Name.DarkBramble,  AstroObject.Name.Comet};

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


        // Checks if a straight line would result in the ship colliding with an astral bodie
        public bool IsCurrPathDangerous(Autopilot __instance, out AstroObject colAtroBody)
        {
            // Get positions
            Vector3 destPos = __instance._referenceFrame.GetPosition();

            AstroObject.Name destinationName = __instance._referenceFrame.GetAstroObject().GetAstroObjectName();

            // Loop through the most important astral bodies
            for (int it = 0; it < RelevantAB.Length; it++)
            {
                AstroObject aObj = Locator.GetAstroObject(RelevantAB[it]);

                AstroObject.Name name = aObj.GetAstroObjectName();

                // Skip self check
                if (destinationName == name)
                    continue;

                // Get radiouses
                GravityVolume gravVol = aObj.GetGravityVolume();

                // Outer wilds uses the player as the zero coordinate of the world, so we use Vector3.zero
                // Simple collision check
                if (LineCircleCollisionCheck(Vector3.zero, destPos, gravVol.GetCenterPosition(), gravVol._upperSurfaceRadius * safetyRadIncrease))
                {
                    colAtroBody = aObj;
                    return true;
                }
            }

            colAtroBody = null;
            return false;
        }

        static public bool LineCircleCollisionCheck(Vector3 point1, Vector3 point2, Vector3 circlePos, float rad)
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

        public void SetRedirectShip(bool state)
        {
            _redirectShip = state;
        }

        public bool IsRedirectShip()
        {
            return _redirectShip; 
        }

        public float GetTimerSafetyCheck()
        {
            return _timerSafetyCheck;
        }

        public float GetCurTimerSafetyCheck()
        {
            return _curTimerSafetyCheck;
        }

        public float GetSafetyRadIncrease()
        {
            return safetyRadIncrease;
        }

        public void SetCurTimerSafetyCheck(float val)
        {
            _curTimerSafetyCheck = val;
        }

        // Remember, all math is based on we always being the (0,0,0)
        public void SetPathOffset(Autopilot __instance, Vector3 obstacle,float rad)
        {
            Vector3 dest = __instance._referenceFrame.GetPosition();

            Vector3 obsNorm = obstacle.normalized;

            // Pick any vector not parallel to the one we have
            Vector3 helper = Mathf.Abs(obsNorm.z) < 0.999f
                        ? Vector3.forward
                        : Vector3.right;


            // Get the first perpendicular vector
            Vector3 perp1 = Vector3.Cross(obsNorm, helper).normalized;

            // Will be done later in case original vector is not valid
            Vector3 perp2 = Vector3.Cross(obsNorm, perp1);  // 90º of the first perp


            float distObs = obstacle.magnitude;
            float distDest = dest.magnitude;

            // Maintain the same triangle ratio
            float offset = (distDest * rad) / distObs;

            _pathOffset = perp1 * offset;
        }
    }


    [HarmonyPatch]
    public class AutopilotPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Autopilot), nameof(Autopilot.FlyToDestination))]

        //If the boolean returned from the method is true the original method is still run
        //If false, the original method will be skipped
        public static bool Autopilot_FlyToDestination_Prefix(Autopilot __instance, ReferenceFrame referenceFrame)
        {
            // New route started, dont redirect yet
            OuterWildsAutopilot.Instance.SetRedirectShip(false);

            return true;
        }

            [HarmonyPrefix]
        [HarmonyPatch(typeof(Autopilot), nameof(Autopilot.ReadTranslationalInput))]

        // We will now check if the ship will collide with any astral body and if it will, stop the autopilot
        public static bool Autopilot_ReadTranslationalInput_Prefix(Autopilot __instance, ref Vector3 __result)
        {
            // Ship unusable, return to original function
            if (__instance._isShipAutopilot && !__instance._shipResources.AreThrustersUsable())
				return true;

            OuterWildsAutopilot OWAutopilot = OuterWildsAutopilot.Instance;
         

            // Check every x time if route is dangerous
            if (!OWAutopilot.IsRedirectShip())
            {
                float curVal = OWAutopilot.GetCurTimerSafetyCheck();
                curVal += Time.deltaTime;
                OWAutopilot.SetCurTimerSafetyCheck(curVal);

                bool danger = false;

                AstroObject colAtroBody = null;

                if (curVal >= OWAutopilot.GetTimerSafetyCheck())
                    danger = OWAutopilot.IsCurrPathDangerous(__instance,out colAtroBody);

                if(danger)
                {
                    OWAutopilot.SetRedirectShip(true);
                    OWAutopilot.SetCurTimerSafetyCheck(0.0f);

                    Vector3 obs = colAtroBody.GetGravityVolume().GetCenterPosition();
                    float rad = colAtroBody.GetGravityVolume()._upperSurfaceRadius;
                    OWAutopilot.SetPathOffset(__instance, obs, rad * OWAutopilot.GetSafetyRadIncrease());
                }
            }
            // Code logic for handling the new route
            else 
            {
               
            }
            
            
            return true;
        }
    }
}
