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
        private static List<MRpawntoken> Pawntokens { get; set; }

        private static MRattackmode[] Attackmodes { get; set; }

        private static bool worldloaded = false;

        public void FixedUpdate()
        {
            // We check if the world is loaded to trigger DefererLoader
            if (!worldloaded && Current.Game != null && Current.Game.World != null)
            {
                // Rdy to load resources
                DeferedLoader();
                worldloaded = true;
            }
        }

        private static void DeferedLoader()
        {
            //We prepare the attack mode array
            Attackmodes = new MRattackmode[Constants.Maxspecialeffects];

            //Loading Textures & translations after init
            for (int i = 0; i < Constants.Maxspecialeffects; i++)
            {
                Attackmodes[i]= new MRattackmode(ContentFinder<Texture2D>.Get(Constants.icontexpath + Constants.icontexname[i]),
                    Constants.labelstring[i].Translate(), string.Format(Constants.descstring[i].Translate(),Constants.methresholds[i],
                    Constants.mechances[i]), Constants.methresholds[i], Constants.mechances[i], i);
            }
        }

        static MainController()
        {

            //Initializing Monitorized pawn list
            Pawntokens = new List<MRpawntoken>();

            //Initializing Harmony detours
            var harmony = HarmonyInstance.Create("net.oreganor.rimworld.mod.meleerebalance");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            //Adding the controller to the scene
            GameObject controllercontainer = new GameObject(Constants.ControllerName);
            UnityEngine.Object.DontDestroyOnLoad(controllercontainer);
            controllercontainer.AddComponent<MainController>();

        }

        public static void ResetParryCounter(Pawn pawn)
        {
            // We scan the list for a reference to the pawn, cleaning all null reference
            // If we find the pawn we reset the counter
            foreach (MRpawntoken p in Pawntokens)
            {
                if (p.pawn == null)
                {
                    Pawntokens.Remove(p);
                }
                else
                {
                    if (p.pawn.Equals(pawn))
                    {
                        p.counter = 0;
                        return;
                    }
                }
            }
        }

        public static int GetParryCounter(Pawn pawn, bool increase)
        {
            // We scan the list pruning null references and increase the counter if we find the pawn in it
            // If the pawn isn't found we create a new entry on the list
            // We return the current counter
            foreach (MRpawntoken p in Pawntokens)
            {
                if (p.pawn == null)
                {
                    Pawntokens.Remove(p);
                }
                else
                {
                    if (p.pawn.Equals(pawn))
                    {
                        if (increase) p.counter++;
                        return p.counter;
                    }
                }
            }
            Pawntokens.Add(new MRpawntoken(pawn));
            return Pawntokens.Last().counter;
        }

        public static MRpawntoken GetPawnToken(Pawn pawn)
        {
            // We scan the list pruning null references and if we find the pawn in it we return the full token
            // If the pawn isn't found we create a new entry on the list with defaults
            foreach (MRpawntoken p in Pawntokens)
            {
                if (p.pawn == null)
                {
                    Pawntokens.Remove(p);
                }
                else
                {
                    if (p.pawn.Equals(pawn))
                    {
                        return p;
                    }
                }
            }
            Pawntokens.Add(new MRpawntoken(pawn));
            return Pawntokens.Last();
        }

        public static MRattackmode GetNextAttackMode(MRattackmode amode)
        {
            if (amode != null)
            {
                for (int i = 0; i < (Attackmodes.Count()-1); i++)
                {
                    if (Attackmodes[i].Equals(amode))
                    {
                        return Attackmodes[i + 1];
                    }
                }    
            }
            return Attackmodes.First();
        }
    }

    public class MRpawntoken
    {
        public int counter;
        public MRattackmode amode;
        public Pawn pawn;
        public MRpawntoken(Pawn pawn)
        {
            this.counter = 0;
            this.amode = MainController.GetNextAttackMode(null);
            this.pawn = pawn;
        }
        public void NextAttackMode()
        {
            this.amode = MainController.GetNextAttackMode(amode);
            return;
        }
    }

    public class MRattackmode
    {
        public int solver; // ATM we solve this in the Detour to simplify access to private data, but a full independent method is the right way to do this
        public float threshold;
        public float chance;
        public Texture2D icontex;
        public string label;
        public string desc;
        public MRattackmode(Texture2D icon, string label, string desc, float threshold, float chance, int solver)
        {
            this.icontex = icon;
            this.label = label;
            this.desc = desc;
            this.threshold = threshold;
            this.chance = chance;
            this.solver = solver;
        }
    }

    static class Constants
    {
        public const float MaxParryChance = 0.9f;
        public const float ParryReduction = 0.5f;
        public const float ParryCounterPenalty = 0.25f;
        public const string ControllerName = "MeleeRebalanceController";
        public const int Maxspecialeffects = 4;
        public const string icontexpath = "Commands/";
        public static string[] icontexname = { "UI_Kill", "UI_Capture", "UI_Stun", "UI_Disarm" };
        public static string[] labelstring = {"Meleerebalance_KillLabel", "Meleerebalance_CaptureLabel",
            "Meleerebalance_StunLabel", "Meleerebalance_DisarmLabel"};
        public static string[] descstring = {"Meleerebalance_KillDesc", "Meleerebalance_CaptureDesc",
            "Meleerebalance_StunDesc", "Meleerebalance_DisarmDesc"};
        public static float[] methresholds = { 0.20f, 0.40f, 0.30f, 0.30f };
        public static float[] mechances = { 1f / 3f, 1f / 4f, 1f / 3f, 1f / 3f };
    }

    // Verb_MeleeAttack Detour
    // We always return false at the Prefix to halt vanilla behaviour
    [HarmonyPatch(typeof(Verb_MeleeAttack))]
    [HarmonyPatch("TryCastShot")]
    public static class VerbMeleeTryCastShotPatch
    {

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
            // We register the attacker and reset its ParryCounters
            MainController.ResetParryCounter(__instance.CasterPawn);
            switch (ResolveMeleeAttack(__instance.CasterPawn, thing, verbMA.Field("surpriseAttack").GetValue<bool>())) // 0 Miss, 1 Hit, 2 Parry, 3 Critical
            {
                case 1: // Hit
                    __result = true;
                    ApplyNormalDamageToTarget(__instance, __instance.CasterPawn, target);
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
                    ApplyParryDamageToTarget(__instance, __instance.CasterPawn, target);
                    __result = false;
                    soundDef = SoundDefOf.MetalHitImportant;
                    break;
                case 3: // Critical
                    ApplyCriticalDamageToTarget(__instance, __instance.CasterPawn, target);
                    __result = true;
                    soundDef = SoundDefOf.Thunder_OnMap;
                    break;
                default: // Miss
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


        private static int ResolveMeleeAttack(Pawn attacker, Thing defender, bool surprise)
        {
            // We generate the result of a melee attack based on skills involved and some extra factors
            // 0: Miss, 1: Hit, 2: Parry, 3: Critical
            // Surprise attacks ALWAYS hit
            if (surprise)
            {
                return 1;
            }
            // Attacks against immobile/downed targets or things that aren't pawns ALWAYS hit and can't trigger special effects
            Pawn tpawn = defender as Pawn;
            if (defender.def.category != ThingCategory.Pawn || tpawn.Downed || tpawn.GetPosture() != PawnPosture.Standing)
            {
                return 1;
            }
            // Against regular targets the effective melee skill of the defender reduces multiplicatively the chance to hit of the attacker.
            // Max Parry Chances are capped at MaxParryChance.
            float roll = Rand.Value;
            float abh = attacker.GetStatValue(StatDefOf.MeleeHitChance, true);
            float dbh = tpawn.GetStatValue(StatDefOf.MeleeHitChance, true);
            MRpawntoken token = MainController.GetPawnToken(tpawn);
            // We reduce the base defensive skill based on the registered parry counters for target pawn.
            dbh -= Constants.ParryCounterPenalty * (float)token.counter;
            if (dbh < 0f)
            {
                dbh = 0f;
            }
            else if (dbh > Constants.MaxParryChance)
            {
                dbh = Constants.MaxParryChance;
            }
            float ehc = abh * (1f - dbh);
            if (roll > abh)
            {
                return 0;
            }
            else
            {
                // The attempt was not a miss so we increase the parry counters of the target
                token.counter++;
                // Now we check for critical effects
                token = MainController.GetPawnToken(attacker);
                if (ehc > token.amode.threshold && roll <= (ehc * token.amode.chance))
                {
                    return 3;
                }
                if (roll <= ehc)
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

        private static void ApplyNormalDamageToTarget(Verb_MeleeAttack vMA, Pawn attacker, LocalTargetInfo target)
        {
            MRpawntoken token = MainController.GetPawnToken(attacker);
            Pawn tpawn = target.Thing as Pawn;
            // Immobile targets receive full damage on normal hits (The only possible result against them) with independence of the Attack Mode selected
            bool immobile = (target.Thing.def.category != ThingCategory.Pawn) || tpawn.Downed || (tpawn.GetPosture() != PawnPosture.Standing);
            if (token.amode.solver == 3 && !immobile) return; // On Disarm Mode we do no damage at all
            var DamageInfosToApply = Traverse.Create(vMA).Method("DamageInfosToApply", new Type[] { typeof(LocalTargetInfo) });
            foreach (DamageInfo current in (DamageInfosToApply.GetValue<IEnumerable<DamageInfo>>(target)))
            {
                if (target.ThingDestroyed)
                {
                    break;
                }
                if(!immobile && (token.amode.solver==1 || token.amode.solver == 2))
                {
                    // On Capture & Stun we do half the damage on normal hits
                    current.SetAmount(Mathf.Max(1, Mathf.RoundToInt(current.Amount * 0.5f)));
                }
                target.Thing.TakeDamage(current);
            }
        }

        private static void ApplyParryDamageToTarget(Verb_MeleeAttack vMA, Pawn attacker, LocalTargetInfo target)
        {
            // The main differences between a regular hit and a parried one regarding damage are the following:
            // - If the target has an active shield with energy it will absorb the FULL damage of the attack
            // - If the target is wielding a melee weapon it will absorb all the damage of the attack suffering 
            //   Original Damage * ParryReduction * (1-Melee Skill) (minimum of 1) of damage
            // - If the target is unarmed, the damage received will be reduced by ParryReduction
            MRpawntoken token = MainController.GetPawnToken(attacker);
            if (token.amode.solver == 3) return; // On Disarm Mode we do no damage at all on parried hits
            bool rollback = false;
            Pawn tpawn = target.Thing as Pawn;
            var DamageInfosToApply = Traverse.Create(vMA).Method("DamageInfosToApply", new Type[] { typeof(LocalTargetInfo) });
            foreach (DamageInfo current in (DamageInfosToApply.GetValue<IEnumerable<DamageInfo>>(target)))
            {
                if (target.ThingDestroyed)
                {
                    break;
                }
                if (token.amode.solver == 1 || token.amode.solver == 2)
                {
                    // On Capture & Stun we do half the damage on parry hits
                    current.SetAmount(Mathf.Max(1, Mathf.RoundToInt(current.Amount * 0.5f)));
                }
                rollback = current.Def.isExplosive; // We briefly active isExplosive to make Vanilla Personal Shields able to intercept melee damage
                current.Def.isExplosive = true;
                if (PawnHasActiveAbsorber(current, tpawn)) // We have to do a double check so Apparel absorbers take precedence over wield weapon damage
                {
                    target.Thing.TakeDamage(current);
                }
                else if (tpawn.equipment != null && tpawn.equipment.Primary != null && tpawn.equipment.Primary.def.IsMeleeWeapon)
                {
                    // The target has an equiped melee weapon after a parry, it will deny any damage to the target but the weapon will suffer some damage itself
                    current.SetAmount(Mathf.Max(1, Mathf.RoundToInt(current.Amount * Constants.ParryReduction * (1f - tpawn.GetStatValue(StatDefOf.MeleeHitChance, true)))));
                    // If the current damage is enough to destroy the weapon we try to drop it instead. If there is not enough room nearby, it will be destroyed
                    if (tpawn.equipment.Primary.HitPoints <= current.Amount)
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

        private static void ApplyCriticalDamageToTarget(Verb_MeleeAttack vMA, Pawn attacker, LocalTargetInfo target)
        {
            Pawn tpawn = target.Thing as Pawn;
            var DamageInfosToApply = Traverse.Create(vMA).Method("DamageInfosToApply", new Type[] { typeof(LocalTargetInfo) });
            MRpawntoken token = MainController.GetPawnToken(attacker);
            switch (token.amode.solver)
            {
                case 0: // Kill
                    // Double damage on all applied damage
                    foreach (DamageInfo current in (DamageInfosToApply.GetValue<IEnumerable<DamageInfo>>(target)))
                    {
                        if (target.ThingDestroyed)
                        {
                            break;
                        }
                        current.SetAmount(Mathf.Max(1, Mathf.RoundToInt(current.Amount * 2f)));
                        target.Thing.TakeDamage(current);
                    }
                    break;
                case 1: // Capture
                    // We replace all damage by an anesthesize effect
                    if (target.Thing.def.category == ThingCategory.Pawn)
                    {
                        HealthUtility.TryAnesthesize(tpawn);
                    }
                    break;
                case 2: // Stun
                    // We replace ALL damage by a short stun effect instead
                    target.Thing.TakeDamage(new DamageInfo(DamageDefOf.Stun, 20));
                    break;
                case 3: // Disarm
                    // We replace ALL damage by removing currently equiped weapon
                    if (tpawn.equipment != null && tpawn.equipment.Primary != null)
                    {
                        ThingWithComps thingwithcomps = new ThingWithComps();
                        tpawn.equipment.TryDropEquipment(tpawn.equipment.Primary, out thingwithcomps, tpawn.Position);
                    }
                    break;
                default:
                    Log.Warning(string.Concat(new object[]
                        {
                            "Melee Rebalance: Critical Special effect not registered for ", attacker
                        }));
                    break;
            }
        }
    }

    // Pawn_DraftController.GetGizmos() detour
    // We use a Postfix that adds Gizmos to what Vanilla has generated (Ideally Other Harmony Powered Mods also)
    [HarmonyPatch(typeof(Pawn_DraftController))]
    [HarmonyPatch("GetGizmos")]
    public static class Pawn_DraftControllerGetGizmosPatch
    {
        public static void Postfix(Pawn_DraftController __instance, ref IEnumerable<Gizmo> __result)
        {
            // We add the command toggle corresponding to the token in the value
            Command_Action optF = new Command_Action();
            MRpawntoken token = MainController.GetPawnToken(__instance.pawn);
            optF.icon = token.amode.icontex;
            optF.defaultLabel = token.amode.label;
            optF.defaultDesc = token.amode.desc;
            optF.hotKey = KeyBindingDefOf.Misc1; //KeyCode.F;
            optF.activateSound = SoundDef.Named("Click");
            optF.action = token.NextAttackMode;
            optF.groupKey = 313123001;
            List<Gizmo> list = __result.ToList<Gizmo>();
            list.Add(optF);
            __result=(IEnumerable<Gizmo>)list;
        }
    }
}
