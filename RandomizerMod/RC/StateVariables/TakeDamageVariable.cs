﻿using ItemChanger.Extensions;
using RandomizerCore.Logic;
using RandomizerCore.Logic.StateLogic;

namespace RandomizerMod.RC.StateVariables
{
    public class TakeDamageVariable : StateSplittingVariable
    {
        public override string Name { get; }
        public int amount;
        public bool canDreamgate;
        public bool canRegen;
        public StateBool overcharmed;
        public StateBool hasTakenDamage;
        public StateInt spentHP;
        public StateInt spentBlueHP;
        /* someday, today it was too cursed
        public StateInt spentSoul;
        public StateInt spentReserveSoul;
        public Term focus;
        */
        public Term dreamnail;
        public Term essence;
        public Term maskShards;
        public EquipCharmVariable hiveblood;
        public EquipCharmVariable lbHeart;
        public EquipCharmVariable lbCore;
        public EquipCharmVariable joni;
        public EquipCharmVariable heart;

        public const string Prefix = "$TAKEDAMAGE";

        public TakeDamageVariable(string name)
        {
            Name = name;
        }

        public static bool TryMatch(LogicManager lm, string term, out LogicVariable variable)
        {
            if (VariableResolver.TryMatchPrefix(term, Prefix, out string[] parameters))
            {
                int amount = parameters.Length == 0 ? 1 : int.Parse(parameters[0]);
                variable = new TakeDamageVariable(term)
                {
                    amount = amount,
                    canDreamgate = !parameters.Contains("noDG"),
                    canRegen = !parameters.Contains("noRegen"),
                    overcharmed = lm.StateManager.GetBool("OVERCHARMED"),
                    hasTakenDamage = lm.StateManager.GetBool("HASTAKENDAMAGE"),
                    spentHP = lm.StateManager.GetInt("SPENTHP"),
                    spentBlueHP = lm.StateManager.GetInt("SPENTBLUEHP"),
                    dreamnail = lm.GetTerm("DREAMNAIL"),
                    essence = lm.GetTerm("ESSENCE"),
                    maskShards = lm.GetTerm("MASKSHARDS"),
                    hiveblood = (EquipCharmVariable)lm.GetVariable(EquipCharmVariable.GetName("Hiveblood")),
                    lbHeart = (EquipCharmVariable)lm.GetVariable(EquipCharmVariable.GetName("Lifeblood_Heart")),
                    lbCore = (EquipCharmVariable)lm.GetVariable(EquipCharmVariable.GetName("Lifeblood_Core")),
                    joni = (EquipCharmVariable)lm.GetVariable(EquipCharmVariable.GetName("Joni's_Blessing")),
                    heart = (EquipCharmVariable)lm.GetVariable(EquipCharmVariable.GetName("Fragile_Heart")),
                };
                return true;
            }

            variable = default;
            return false;
        }



        public override IEnumerable<Term> GetTerms()
        {
            yield return dreamnail;
            yield return essence;
            yield return maskShards;
            foreach (Term t in hiveblood.GetTerms()) yield return t;
            foreach (Term t in lbHeart.GetTerms()) yield return t;
            foreach (Term t in lbCore.GetTerms()) yield return t;
            foreach (Term t in joni.GetTerms()) yield return t;
            foreach (Term t in heart.GetTerms()) yield return t;
        }

        public override int GetValue(object sender, ProgressionManager pm, StateUnion? localState)
        {
            if (localState is null) return FALSE;

            for (int i = 0; i < localState.Count; i++)
            {
                if (Survives(pm, localState[i], amount)) return TRUE;
            }
            for (int i = 0; i < localState.Count; i++)
            {
                if (SurvivesWithCharmOptimization(pm, localState[i], amount)) return TRUE;
            }

            return FALSE;
        }

        public override IEnumerable<LazyStateBuilder>? ModifyState(object sender, ProgressionManager pm, LazyStateBuilder state)
        {
            if (state.GetBool(hasTakenDamage) || !pm.Has(lbCore.canBenchTerm))
            {
                if (TakeDamage(pm, ref state, amount))
                {
                    DisableUnequippedHealthCharms(ref state);
                    return state.Yield();
                }
                return null;
            }
            else
            {
                return GenerateCharmLoadouts(pm, state);
            }
        }

        private void DisableUnequippedHealthCharms(ref LazyStateBuilder state)
        {
            if (!state.GetBool(hiveblood.charmBool)) state.SetBool(hiveblood.anticharmBool, true);
            if (!state.GetBool(lbHeart.charmBool)) state.SetBool(lbHeart.anticharmBool, true);
            if (!state.GetBool(lbCore.charmBool)) state.SetBool(lbCore.anticharmBool, true);
            if (!state.GetBool(joni.charmBool)) state.SetBool(joni.anticharmBool, true);
            if (!state.GetBool(heart.charmBool)) state.SetBool(heart.anticharmBool, true);
        }

        public IEnumerable<LazyStateBuilder> GenerateCharmLoadouts(ProgressionManager pm, LazyStateBuilder state)
        {
            int availableNotches = pm.Get(lbCore.notchesTerm) - state.GetInt(lbCore.usedNotchesInt);
            if (availableNotches <= 0) yield break;

            List<int> notchCosts = ((RandoModContext)pm.ctx).notchCosts;
            List<EquipCharmVariable> helper = new();
            if (canRegen) AddECV(hiveblood);
            AddECV(lbHeart);
            AddECV(heart);
            AddECV(lbCore);
            AddECV(joni);
            helper.Sort((p, q) => notchCosts[p.charmID - 1] - notchCosts[q.charmID - 1]);

            int pow = 1 << helper.Count;
            for (int i = 0; i < pow; i++)
            {
                LazyStateBuilder next = new(state);
                for (int j = 0; j < helper.Count; j++)
                {
                    if ((i & (1 << j)) == (1 << j))
                    {
                        if (!helper[j].ModifyState(null, pm, ref next)) goto SKIP;
                    }
                }
                if (TakeDamage(pm, ref next, amount))
                {
                    DisableUnequippedHealthCharms(ref next);
                    yield return next;
                }
                SKIP: continue;
            }

            void AddECV(EquipCharmVariable ecv)
            {
                if (pm.Has(ecv.charmTerm))
                {
                    helper.Add(ecv);
                }
            }
        }

        // TODO: fix monotonicity issue with hits remaining

        public bool TakeDamage(ProgressionManager pm, ref LazyStateBuilder state, int damage)
        {
            bool dg = canDreamgate && pm.Has(dreamnail, 2) && pm.Has(essence);
            bool oc = state.GetBool(overcharmed);
            if (canRegen)
            {
                if (dg) return Survives(pm, state, damage);
                else if (state.GetBool(hiveblood.charmBool))
                {
                    if (!oc) return Survives(pm, state, damage);
                    else damage -= damage / 2;
                }
            }

            int blueHP = -state.GetInt(spentBlueHP) + (state.GetBool(lbHeart.charmBool) ? 2 : 0) + (state.GetBool(lbCore.charmBool) ? 4 : 0);
            int hp = pm.Get(maskShards) / 4 + (state.GetBool(heart.charmBool) ? 2 : 0);
            if (state.GetBool(joni.charmBool)) hp = (int)(hp * 1.4f);
            int hits = oc ? blueHP / 2 + (hp - 1) / 2 : blueHP + hp - 1;

            if (!oc && blueHP >= damage || blueHP >= 2 * damage)
            {
                state.Increment(spentBlueHP, !oc ? damage : 2 * damage);
                state.SetBool(hasTakenDamage, true);
                return true;
            }
            
            if (hits >= damage)
            {
                if (blueHP > 0)
                {
                    state.Increment(spentBlueHP, blueHP);
                    damage -= oc ? blueHP / 2 : blueHP;
                    hits -= oc ? blueHP / 2 : blueHP;
                }
                state.Increment(spentHP, !oc ? damage : 2 * damage);
                state.SetBool(hasTakenDamage, true);
                return true;
            }
            return false;
        }

        public bool Survives<T>(ProgressionManager pm, T state, int damage) where T : IState
        {
            int hits = (state.GetBool(joni.charmBool) ? (int)(pm.Get(maskShards) / 4 * 1.4f) : pm.Get(maskShards) / 4) - state.GetInt(spentHP) - 1;
            bool oc = state.GetBool(overcharmed);
            if (oc) hits /= 2;
            if (hits >= damage) return true; // do this check first to skip the charm checks less likely to be relevant
            if (canRegen && canDreamgate && pm.Has(dreamnail, 2) && pm.Has(essence) && hits > 0) return true;
            if (canRegen && state.GetBool(hiveblood.charmBool))
            {
                if (!oc && hits > 0) return true;
                else
                {
                    hits = (state.GetBool(joni.charmBool) ? (int)(pm.Get(maskShards) / 4 * 1.4f) : pm.Get(maskShards) / 4) - state.GetInt(spentHP) - 2;
                }
            }

            if (state.GetBool(heart.charmBool)) hits += oc ? 1 : 2;
            int blueHP = (state.GetBool(lbHeart.charmBool) ? 2 : 0) + (state.GetBool(lbCore.charmBool) ? 4 : 0) - state.GetInt(spentBlueHP);
            hits += oc ? blueHP / 2 : blueHP;
            return hits >= damage;
        }

        // Returns whether the damage amount can be survived with the right charms.
        // Assumes the damage amount cannot be survived with no charms.
        // Skips checks and returns false if damage has previously been taken.
        private bool SurvivesWithCharmOptimization(ProgressionManager pm, State state, int damage)
        {
            if (state.GetBool(hasTakenDamage) || state.GetBool(overcharmed) || !pm.Has(lbCore.canBenchTerm)) return false;

            switch (damage)
            {
                case 1:
                    if (lbCore.TryEquip(pm, state) != EquipCharmVariable.EquipResult.None) return true;
                    if (heart.TryEquip(pm, state) != EquipCharmVariable.EquipResult.None) return true;
                    if (lbHeart.TryEquip(pm, state) == EquipCharmVariable.EquipResult.Nonovercharm) return true;
                    if (joni.TryEquip(pm, state) == EquipCharmVariable.EquipResult.Nonovercharm) return true;
                    return false;
                case 2:
                    if (lbCore.TryEquip(pm, state) != EquipCharmVariable.EquipResult.None) return true;
                    if (heart.TryEquip(pm, state) == EquipCharmVariable.EquipResult.Nonovercharm) return true;
                    if (lbHeart.TryEquip(pm, state) == EquipCharmVariable.EquipResult.Nonovercharm) return true;
                    if (pm.Get(maskShards) > 7 && (
                        joni.TryEquip(pm, state) == EquipCharmVariable.EquipResult.Nonovercharm ||
                        canRegen && hiveblood.TryEquip(pm, state) == EquipCharmVariable.EquipResult.Nonovercharm)) return true;
                    return false;
                case 3:
                    if (lbCore.TryEquip(pm, state) switch
                    {
                        EquipCharmVariable.EquipResult.Nonovercharm => true,
                        EquipCharmVariable.EquipResult.Overcharm => pm.Get(maskShards) > 11,
                        _ => false,
                    }) return true;
                    if (pm.Get(maskShards) > 7 && (
                        heart.TryEquip(pm, state) == EquipCharmVariable.EquipResult.Nonovercharm || 
                        lbHeart.TryEquip(pm, state) == EquipCharmVariable.EquipResult.Nonovercharm ||
                        canRegen && hiveblood.TryEquip(pm ,state) == EquipCharmVariable.EquipResult.Nonovercharm)) return true;
                    if (pm.Get(maskShards) > 11 && joni.TryEquip(pm, state) == EquipCharmVariable.EquipResult.Nonovercharm) return true;
                    return false;
            }
            return false;
        }
    }
}