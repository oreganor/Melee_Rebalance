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

        public static MRattackmode[] Attackmodes { get; set; }

        private static bool worldloaded = false;

        private static HarmonyInstance harmony;

        public static Texture2D LowChance;

        public static Texture2D NoChance;

        public static string LowChanceDesc;

        public static string NoChanceDesc;

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
                Attackmodes[i]= new MRattackmode(ContentFinder<Texture2D>.Get(Constants.Icontexpath + Constants.Icontexname[i]),
                    Constants.Labelstring[i].Translate(), string.Format(Constants.Descstring[i].Translate(),Constants.Methresholds[i],
                    Constants.Mechances[i]), Constants.Methresholds[i], Constants.Mechances[i], i);
            }
            LowChance = ContentFinder<Texture2D>.Get(Constants.Icontexpath + Constants.LowChanceTex);
            NoChance = ContentFinder<Texture2D>.Get(Constants.Icontexpath + Constants.NoChanceTex);
            LowChanceDesc = string.Format(Constants.LowChanceDesc.Translate());
            NoChanceDesc = string.Format(Constants.NoChanceDesc.Translate());
        }

        static MainController()
        {
            //Initializing Monitorized pawn list
            Pawntokens = new List<MRpawntoken>();

            //Registering Assembly into Harmony database
            harmony = HarmonyInstance.Create("net.oreganor.rimworld.mod.meleerebalance");

            //Applying Detours
            var original = typeof(Verb_MeleeAttack).GetMethod("TryCastShot", Constants.BindF);
            var detour = typeof(VerbMeleeTryCastShotPatch).GetMethod("Prefix");
            harmony.Patch(original, new HarmonyMethod(detour), new HarmonyMethod(null));

            //We check for the pressence of Defensive Positions
            original = null;
            foreach (ModMetaData current in ModsConfig.ActiveModsInLoadOrder.ToList<ModMetaData>())
            {
                if (current.Identifier.Equals(Constants.DefensivePositionsFolder))
                {
                    Log.Warning(string.Concat(new object[]
                    {
                    "Melee Rebalance: Defensive Possitions Mod active. Adapting Detours."
                    }));
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        Type type = assembly.GetType("DefensivePositions.DraftControllerDetour");
                        if (type != null)
                        {
                            Log.Warning(string.Concat(new object[]
                            {
                                    "Melee Rebalance: Found right spot inside Defensive Positions"
                            }));
                            original = type.GetMethod("_GetGizmos", Constants.BindF);
                            if (original != null)
                            {
                                Log.Warning(string.Concat(new object[]
                                {
                                    "Melee Rebalance: Detours successfully adapted to Defensive Positions'"
                                }));
                                break;
                            }
                        }
                    }
                }
            }
            if (original == null)
            {
                original = typeof(Pawn_DraftController).GetMethod("GetGizmos", Constants.BindF);
                detour = typeof(DraftControllerGetGizmosPatch).GetMethod("Postfix");
            }
            else
            {
                detour = typeof(DraftControllerDPDetourGetGizmosPatch).GetMethod("Postfix");
            }
             harmony.Patch(original, new HarmonyMethod(null), new HarmonyMethod(detour));

            //Adding the controller to the scene
            GameObject controllercontainer = new GameObject(Constants.ControllerName);
            UnityEngine.Object.DontDestroyOnLoad(controllercontainer);
            controllercontainer.AddComponent<MainController>();

        }

        public static void ResetParryCounter(Pawn pawn)
        {
            // We scan the list for a reference to the pawn, cleaning all null references
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
        public float ehc; // Effective Hit Chance of the last melee attack
        public MRattackmode amode;
        public Pawn pawn;
        public MRpawntoken(Pawn pawn)
        {
            this.counter = 0;
            this.ehc = 1f;
            this.amode = MainController.GetNextAttackMode(null);
            this.pawn = pawn;
        }
        public void NextAttackMode()
        {
            this.amode = MainController.GetNextAttackMode(amode);
            return;
        }
        public void ChooseAttackMode(Pawn target)
        {   
            if (pawn.RaceProps.Humanlike)
            {
                // Rational pawns will try to use modes that have a reallistic chance to apply its effect
                // Pending generalization: 0 kill, 1 capture, 2 stun, 3 disarm
                // If we have a decent chance to capture, we go for it 1st
                if (MainController.Attackmodes[1].HasGoodChances(ehc))
                {
                    amode = MainController.Attackmodes[1];
                    return;
                }
                // Target driven decissions
                if (target != null)
                {
                    // We try to stun if the target isn't
                    if (MainController.Attackmodes[2].HasGoodChances(ehc))
                    {
                        if (target.stances != null && !target.stances.stunner.Stunned )
                        {
                            amode = MainController.Attackmodes[2];
                            return;
                        }
                    }
                    // We try to disarm if the target carries a weapon
                    if (MainController.Attackmodes[3].HasGoodChances(ehc))
                    {
                        if (target.equipment != null && target.equipment.Primary != null && target.equipment.Primary.def.IsWeapon)
                        {
                            amode = MainController.Attackmodes[3];
                            return;
                        }
                    }
                }
            }
            amode = MainController.Attackmodes[0];
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
        public bool CanTriggerSE(float ehc)
        {
            if(ehc >= threshold)
            {
                return true;
            }
            return false;
        }
        public bool HasGoodChances(float ehc)
        {
            if(CanTriggerSE(ehc))
            {
                if((ehc * chance) >= Constants.LowSpecialEffectChance)
                {
                    return true;
                }
            }
            return false;
        }
    }

    static class Constants
    {
        public const float MaxParryChance = 0.9f;
        public const float ParryReduction = 0.5f;
        public const float ParryCounterPenalty = 0.20f;
        public const float LowSpecialEffectChance = 1f/6f;
        public const string ControllerName = "MeleeRebalanceController";
        public const int Maxspecialeffects = 4;
        public const string Icontexpath = "Commands/";
        public static string[] Icontexname = { "UI_Kill", "UI_Capture", "UI_Stun", "UI_Disarm" };
        public static string[] Labelstring = {"Meleerebalance_KillLabel", "Meleerebalance_CaptureLabel",
            "Meleerebalance_StunLabel", "Meleerebalance_DisarmLabel"};
        public static string[] Descstring = {"Meleerebalance_KillDesc", "Meleerebalance_CaptureDesc",
            "Meleerebalance_StunDesc", "Meleerebalance_DisarmDesc"};
        public const string NoChanceDesc = "Meleerebalance_NoChanceDesc";
        public const string LowChanceDesc = "Meleerebalance_LowChanceDesc";
        public static float[] Methresholds = { 0.15f, 0.55f, 0.25f, 0.45f };
        public static float[] Mechances = { 1f / 4f, 1f / 4f, 1f / 2.5f, 1f / 3f };
        public const int CommandGroupKey = 23128736;
        public static BindingFlags BindF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
        public const string DefensivePositionsFolder = "DefensivePositions";
        public const string LowChanceTex = "UI_Overlay_LowChance";
        public const string NoChanceTex = "UI_Overlay_NoChance";
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
            MRpawntoken atoken = MainController.GetPawnToken(attacker);
            if (surprise)
            {
                atoken.ehc = 1f;
                return 1;
            }
            // Attacks against immobile/downed targets or things that aren't pawns ALWAYS hit and can't trigger special effects
            Pawn tpawn = defender as Pawn;
            if (defender.def.category != ThingCategory.Pawn || tpawn.Downed || tpawn.GetPosture() != PawnPosture.Standing)
            {
                atoken.ehc = 1f;
                return 1;
            }
            // Against regular targets the effective melee skill of the defender reduces multiplicatively the chance to hit of the attacker.
            // Max Parry Chances are capped at MaxParryChance.
            float roll = Rand.Value;
            float abh = attacker.GetStatValue(StatDefOf.MeleeHitChance, true);
            float dbh = tpawn.GetStatValue(StatDefOf.MeleeHitChance, true);
            MRpawntoken dtoken = MainController.GetPawnToken(tpawn);
            // We reduce the base defensive skill based on the registered parry counters for target pawn.
            dbh -= Constants.ParryCounterPenalty * (float)dtoken.counter;
            if (dbh < 0f)
            {
                dbh = 0f;
            }
            else if (dbh > Constants.MaxParryChance)
            {
                dbh = Constants.MaxParryChance;
            }
            atoken.ehc = abh * (1f - dbh);

            //Log.Warning(string.Concat(new object[]
            //    {
            //    attacker,"(BHC ",abh,") tried to hit ",tpawn,"(BHC ",dbh,") with effective hit chance ",atoken.ehc," and rolled ",roll
            //    }));

            // If the attacker is NOT a player controller entity we choose the attack mode
            if (!attacker.IsColonistPlayerControlled)
            {
                atoken.ChooseAttackMode(tpawn);
            }
            if (roll > abh)
            {
                return 0;
            }
            else
            {
                // The attempt was not a miss so we increase the parry counters of the target
                dtoken.counter++;
                // Now we check for critical effects
                if ((atoken.ehc > atoken.amode.threshold) && (roll <= (atoken.ehc * atoken.amode.chance)))
                {
                    return 3;
                }
                if (roll <= atoken.ehc)
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

    public class AttackModeCommand : Command_Action
    {
        private MRpawntoken Token;

        public AttackModeCommand(MRpawntoken token)
        {
            this.Token = token;
            this.icon = token.amode.icontex;
            this.defaultLabel = token.amode.label;
            this.defaultDesc = token.amode.desc;
            this.hotKey = KeyBindingDefOf.Misc7;
            this.activateSound = SoundDef.Named("Click");
            this.action = token.NextAttackMode;
            this.groupKey = Constants.CommandGroupKey;
        }

        public void UpdateMode(MRattackmode amode)
        {
            this.Token.amode = amode;
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft)
        {
            GizmoResult result;
            // We add an extra overlay based on last effective hit chance stored into the token compared with the threshold of the current and with the
            // chance to trigger the special effect. Descriptions have to be updated BEFORE calling the base method.
            Rect rect = new Rect(topLeft.x, topLeft.y, this.Width, 75f);
            // If the pawn isn't drafted we take the chance to reset ehc to remove the overlay
            if (!Token.pawn.Drafted)
            {
                Token.ehc = 1f;
            }
            if (Token.amode.CanTriggerSE(Token.ehc))
            {
                if(!Token.amode.HasGoodChances(Token.ehc))
                {
                    // Low chance to trigger Special Effect
                    this.defaultDesc += MainController.LowChanceDesc;
                    result = base.GizmoOnGUI(topLeft);
                    GUI.DrawTexture(rect, MainController.LowChance);

                }
                else
                {
                    result = base.GizmoOnGUI(topLeft);
                }
            }
            else
            {
                // No chance to trigger Special Effect
                this.defaultDesc += MainController.NoChanceDesc;
                result = base.GizmoOnGUI(topLeft);
                GUI.DrawTexture(rect, MainController.NoChance);
            }
            return result;
        }

        public override bool InheritInteractionsFrom(Gizmo other)
        {
            // When grouped pawns receive an order the resulting state will be applied to all of them
            // So first thing to do after a click is equalizing the sate of all of them
            // And then execute the default action on each
            (other as AttackModeCommand).UpdateMode(this.Token.amode);
            return true;
        }
    }

    // Pawn_DraftController.GetGizmos() detour
    // We use a Postfix that adds Gizmos to what Vanilla has generated (Ideally Other Harmony Powered Mods also)
    //[HarmonyPatch(typeof(Pawn_DraftController))]
    //[HarmonyPatch("GetGizmos")]
    public static class DraftControllerGetGizmosPatch
    {
        public static void Postfix(Pawn_DraftController __instance, ref IEnumerable<Gizmo> __result)
        {
            // We add the command toggle corresponding to the token in the value
            MRpawntoken token = MainController.GetPawnToken(__instance.pawn);
            AttackModeCommand optF = new AttackModeCommand(token);
            List<Gizmo> list = __result.ToList<Gizmo>();
            list.Add(optF);
            __result = (IEnumerable<Gizmo>)list;
        }
    }

    // The patch version for Defensive Positions Detour, which is a static method
    public static class DraftControllerDPDetourGetGizmosPatch
    {
        public static void Postfix(Pawn_DraftController controller, ref IEnumerable<Gizmo> __result)
        {
            // We add the command toggle corresponding to the token in the value
            MRpawntoken token = MainController.GetPawnToken(controller.pawn);
            AttackModeCommand optF = new AttackModeCommand(token);
            List<Gizmo> list = __result.ToList<Gizmo>();
            list.Add(optF);
            __result = (IEnumerable<Gizmo>)list;
        }
    }
}
