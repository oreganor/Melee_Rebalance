using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using Verse;
using Verse.AI;
using Verse.Sound;
using RimWorld;
using UnityEngine;
using Harmony;

namespace MeleeRebalance
{
    [StaticConstructorOnStartup]
    public class MainController : MonoBehaviour
    {
        private static List<ParryCounter> Parrycounters { get; set; }

        private static MainController instance;

        public static MainController Instance
        {
            get
            {
                MainController instance;
                if ((instance = MainController.instance) == null)
                {
                    instance = MainController.instance = new MainController();
                }
                return instance;
            }
        }


        static MainController()
        {
            //Initializing Harmony detours
            var harmony = HarmonyInstance.Create("net.oreganor.rimworld.mod.meleeparry");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            //Initializing Parry Counter Controller
            Parrycounters = new List<ParryCounter>();

            //Adding the controller to the scene
            GameObject gameObject = new GameObject(Constants.ControllerName);
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
            gameObject.AddComponent<MainController>();
        }

        public void ResetParryCounter(Pawn pawn)
        {
            // We scan the list for a reference to the pawn, cleaning all null reference
            // If we find the pawn we reset the counter to -1
            foreach (ParryCounter c in Parrycounters)
            {
                if (c.pawn == null)
                {
                    Parrycounters.Remove(c);
                }
                else
                {
                    if (c.pawn.Equals(pawn))
                    {
                        c.counter=0;
                        return;
                    }
                }
            }
        }

        public int GetParryCounter(Pawn pawn, bool increase)
        {
            // We scan the list pruning null references and increase the counter if we find the pawn in it
            // If the pawn isn't found we create a new entry on the list
            // We return the current counter
            foreach(ParryCounter c in Parrycounters)
            {
                if(c.pawn == null)
                {
                    Parrycounters.Remove(c);
                }
                else
                {
                    if (c.pawn.Equals(pawn))
                    {
                        if(increase) c.counter++;
                        return c.counter;
                    }
                }
            }
            Parrycounters.Add(new ParryCounter(pawn));
            return Parrycounters.Last().counter;
        }
    }

    public class ParryCounter
    {
        public int counter;
        public Pawn pawn;
        public ParryCounter(Pawn pawn)
        {
            this.counter = 0;
            this.pawn = pawn;
        }
    }

    static class Constants
    {
        public const float MaxParryChance = 0.9f;
        public const float ParryReduction = 0.5f;
        public const float ParryCounterPenalty = 0.25f;
        public const string ControllerName = "MeleeRebalanceController";
    }

    [HarmonyPatch(typeof(Verb_MeleeAttack))]
    [HarmonyPatch("TryCastShot")]
    public static class VerbMeleeTryCastShotPatch
    {

        //This is the old Prefix that patched GetHitChances Method
        //[HarmonyPatch(typeof(Verb_MeleeAttack))]
        //[HarmonyPatch("GetHitChances")]
        //public static bool Prefix(Verb_MeleeAttack __instance, ref float __result, LocalTargetInfo target)
        //{
        //    var verbMA = Traverse.Create(__instance);
        //    __result = verbMA.Field("DefaultHitChance").GetValue<float>(); // Default chances if anything else fails to evaluate
        //    if (verbMA.Field("surpriseAttack").GetValue<bool>() || verbMA.Method("IsTargetImmobile", target).GetValue<bool>())
        //    {
        //        __result = 1f; // This also checks that the target is a pawn.
        //        return false;
        //    }
        //    if (__instance.CasterPawn.skills != null)
        //    {
        //        // Against active pawns, the chance to hit is reduced by the defender's melee chance down to 1/3 of the base attack chance
        //        Pawn tpawn = target.Thing as Pawn;
        //        __result = Math.Max(__instance.CasterPawn.GetStatValue(StatDefOf.MeleeHitChance, true) / 3f,
        //            __instance.CasterPawn.GetStatValue(StatDefOf.MeleeHitChance, true) - tpawn.GetStatValue(StatDefOf.MeleeHitChance, true));
        //        Log.Warning(string.Concat(new object[]
        //            {
        //            __instance.CasterPawn,"(BHC ",__instance.CasterPawn.GetStatValue(StatDefOf.MeleeHitChance, true),
        //            ") tried to hit ",
        //            tpawn,"(BHC ",tpawn.GetStatValue(StatDefOf.MeleeHitChance, true),") with final melee hit chance ",
        //            __result,"."
        //            }));
        //    }
        //    return false;
        //}
        private static MainController parrycontroller;

        public static MainController Parrycontroller
        {
            get
            {
                GameObject temp = GameObject.Find(Constants.ControllerName);
                return (parrycontroller = temp.GetComponent<MainController>());
            }
        }

        public static bool Prefix(Verb_MeleeAttack __instance, ref bool __result)
        {
            var verbMA = Traverse.Create(__instance);
            if (__instance.CasterPawn.stances.FullBodyBusy)
            {
                __result = false;
                return false;
            }
            LocalTargetInfo target = verbMA.Field("currentTarget").GetValue<LocalTargetInfo>();
            Thing thing = target.Thing;
            if (!__instance.CanHitTarget(thing))
            {
                Log.Warning(string.Concat(new object[]
                {
                    __instance.CasterPawn,
                    " meleed ",
                    thing,
                    " from out of melee position."
                }));
            }
            __instance.CasterPawn.Drawer.rotator.Face(thing.DrawPos);
            if (!verbMA.Method("IsTargetImmobile", target).GetValue<bool>() && __instance.CasterPawn.skills != null)
            {
                __instance.CasterPawn.skills.Learn(SkillDefOf.Melee, 250f, false);
            }
            SoundDef soundDef;
            // We reset the Parry Counter of the attacker before evaluating anything else
            Parrycontroller.ResetParryCounter(__instance.CasterPawn);
            //Log.Warning(string.Concat(new object[]
            //{
            //    "Evaluating melee attack by ",__instance.CasterPawn
            //}));
            switch (ResolveMeleeAttack(__instance.CasterPawn, thing, verbMA.Field("surpriseAttack").GetValue<bool>())) // 0 Miss, 1 Hit, 2 Parry)
            {
                case 1: // Hit
                    __result = true;
                    //Log.Warning(string.Concat(new object[]
                    //{
                    //    "Hit against",target," !"
                    //}));
                    verbMA.Method("ApplyMeleeDamageToTarget", new Type[] { typeof(LocalTargetInfo) }).GetValue(target);
                        if (thing.def.category == ThingCategory.Building)
                    {
                        soundDef = verbMA.Method("SoundHitBuilding").GetValue<SoundDef>();
                    }
                    else
                    {
                        soundDef = verbMA.Method("SoundHitPawn").GetValue<SoundDef>();
                    }
                    break;
                case 2: // Parry
                    //Log.Warning(string.Concat(new object[]
                    //{
                    //    "Parry!"
                    //}));
                    ApplyParryDamageToTarget(__instance, __instance.CasterPawn, target);
                    __result = false;
                    soundDef = SoundDefOf.MetalHitImportant;
                    break;
                default: // Miss
                    //Log.Warning(string.Concat(new object[]
                    //{
                    //    "Miss!"
                    //}));
                    __result = false;
                    soundDef = verbMA.Method("SoundMiss").GetValue<SoundDef>();
                    break;
            }
            soundDef.PlayOneShot(new TargetInfo(thing.Position, __instance.CasterPawn.Map, false));
            __instance.CasterPawn.Drawer.Notify_MeleeAttackOn(thing);
            Pawn pawntemp = thing as Pawn;
            if (pawntemp != null && !pawntemp.Dead)
            {
                pawntemp.stances.StaggerFor(95);
                if (__instance.CasterPawn.MentalStateDef != MentalStateDefOf.SocialFighting || pawntemp.MentalStateDef != MentalStateDefOf.SocialFighting)
                {
                    pawntemp.mindState.meleeThreat = __instance.CasterPawn;
                    pawntemp.mindState.lastMeleeThreatHarmTick = Find.TickManager.TicksGame;
                }
            }
            __instance.CasterPawn.Drawer.rotator.FaceCell(thing.Position);
            if (__instance.CasterPawn.caller != null)
            {
                __instance.CasterPawn.caller.Notify_DidMeleeAttack();
            }
            return false;
        }

        //private const BindingFlags BindFlags = BindingFlags.Instance
        //                               | BindingFlags.Public
        //                               | BindingFlags.NonPublic
        //                               | BindingFlags.Static;

        private static int ResolveMeleeAttack(Pawn attacker, Thing defender, bool surprise)
        {
            // We generate the result of a melee attack based on skills involved and some extra factors
            // 0: Miss, 1: Hit, 2: Parry
            // Surprise attacks ALWAYS hit
            if (surprise)
            {
                //Log.Warning(string.Concat(new object[]
                //{
                //    "Suprise Attack by ",attacker,"!"
                //}));
                //return 1;
            }
            // Attacks against immobile/downed targets or things that aren't pawns ALWAYS hit
            Pawn tpawn = defender as Pawn;
            if (defender.def.category != ThingCategory.Pawn || tpawn.Downed || tpawn.GetPosture() != PawnPosture.Standing)
            {
                //Log.Warning(string.Concat(new object[]
                //{
                //    "Defender ",defender," is down/immobile"
                //}));
                return 1;
            }
            // Against regular targets the effective melee skill of the defender reduces multiplicatively the chance to hit of the attacker.
            // Max Parry Chances are capped at MaxParryChance.
            float roll = Rand.Value;
            float abh = attacker.GetStatValue(StatDefOf.MeleeHitChance, true);
            float dbh = tpawn.GetStatValue(StatDefOf.MeleeHitChance, true);
            // We reduce the effective defensive skill based on the registered parry counters for this pawn.
            dbh -= Constants.ParryCounterPenalty * (float)parrycontroller.GetParryCounter(tpawn, false);
            if (dbh < 0f)
            {
                dbh = 0f;
            } else if (dbh > Constants.MaxParryChance)
            {
                dbh = Constants.MaxParryChance;
            }
            Log.Warning(string.Concat(new object[]
                {
                attacker,"(BHC ",abh,") tried to hit ",tpawn,"(BHC ",dbh,") with effective hit chance ",abh*(1f - dbh)," and rolled ",roll
                }));
            if (roll > abh)
            {
                return 0;
            }
            else
            {
                // The attempt was either a parry or a hit, so we increase the counter for this target
                parrycontroller.GetParryCounter(tpawn, true);
                if (roll <= abh * (1f - dbh))
                {
                    return 1;
                }
            }
            return 2;
        }

        private static bool PawnHasActiveAbsorber(DamageInfo dinfo, Pawn pawn)
        {
            // We probe for a CheckPreAbsorbDamage method that returns true in the case of a 0 damage dinfo
            dinfo.SetAmount(0);
            if (pawn.apparel != null)
            {
                List<Apparel> wornApparel = pawn.apparel.WornApparel;
                for (int i = 0; i < wornApparel.Count; i++)
                {
                    if (wornApparel[i].CheckPreAbsorbDamage(dinfo))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static void ApplyParryDamageToTarget(Verb_MeleeAttack vMA, Pawn attacker, LocalTargetInfo target)
        {
            // The main differences between a regular hit and a parried one regarding damage are the following:
            // - If the target has an active shield with energy it will absorb the FULL damage of the attack
            // - If the target is wielding a melee weapon it will absorb all the damage of the attack suffering 
            //   Original Damage * ParryReduction * (1-Melee Skill) (minimum of 1) of damage
            // - If the target is unarmed, the damage received will be reduced by ParryReduction
            bool rollback = false;
            Pawn tpawn = target.Thing as Pawn;
            var DamageInfosToApply = Traverse.Create(vMA).Method("DamageInfosToApply", new Type[] { typeof(LocalTargetInfo) });
            foreach (DamageInfo current in (DamageInfosToApply.GetValue<IEnumerable<DamageInfo>>(target)))
            {
                if (target.ThingDestroyed)
                {
                    break;
                }
                rollback = current.Def.isExplosive; // We briefly active isExplosive to make Vanilla Personal Shields able to intercept melee damage
                current.Def.isExplosive = true;
                if (PawnHasActiveAbsorber(current, tpawn))
                {
                    target.Thing.TakeDamage(current);
                }
                else if (tpawn.equipment!=null && tpawn.equipment.Primary!=null && tpawn.equipment.Primary.def.IsMeleeWeapon)
                {
                    // The target has an equiped melee weapon after a parry, it will deny any damage to the target but the weapon will suffer some damage itself
                    current.SetAmount(Mathf.Max(1, Mathf.RoundToInt(current.Amount * Constants.ParryReduction * (1f-tpawn.GetStatValue(StatDefOf.MeleeHitChance,true)))));
                    // If the current damage is enough to destroy the weapon we try to drop it instead. If there is not enough room nearby, it will be destroyed
                    if(tpawn.equipment.Primary.HitPoints <= current.Amount)
                    {
                        ThingWithComps thingwithcomps = new ThingWithComps();
                        if (tpawn.equipment.TryDropEquipment(tpawn.equipment.Primary, out thingwithcomps, tpawn.Position))
                        {
                            current.Def.isExplosive = rollback;
                            break;
                        }
                    }
                    tpawn.equipment.Primary.TakeDamage(current);
                }
                else
                {
                    // Unarmed defender, it just takes reduced damage
                    current.SetAmount(Mathf.Max(1, Mathf.RoundToInt(current.Amount * Constants.ParryReduction)));
                    target.Thing.TakeDamage(current);
                }
                current.Def.isExplosive = rollback;
            }
        }
    }
}
