using System;
using System.Collections.Generic;
using System.Reflection;

namespace Frontier.Hyperspace
{
    public sealed partial class HyperspaceTuning
    {
        static readonly FieldInfo[] KeyFields = BuildKeyFields();

        static FieldInfo[] BuildKeyFields()
        {
            var list = new List<FieldInfo>();
            foreach (FieldInfo f in typeof(HyperspaceTuning).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.FieldType == typeof(float) || f.FieldType == typeof(int) || f.FieldType == typeof(float[])) list.Add(f);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return list.ToArray();
        }

        /// <summary>
        /// Every tunable as a key string (field name; per-type arrays as "name.Drone"). Needed by Remote Config; because it uses reflection the host must keep this type
        /// from being stripped (link.xml: &lt;type fullname="Frontier.Hyperspace.HyperspaceTuning" preserve="all"/&gt;).
        /// </summary>
        public static string[] AllKeys()
        {
            var keys = new List<string>();
            foreach (FieldInfo f in KeyFields)
            {
                if (f.FieldType == typeof(float[]))
                {
                    for (int i = 1; i < EntityTypeCount; i++) keys.Add(f.Name + "." + ((EntityType)i).ToString());
                }
                else keys.Add(f.Name);
            }
            return keys.ToArray();
        }

        public bool TryGet(string key, out float value)
        {
            value = 0f;
            FieldInfo f;
            int index;
            if (!Locate(key, out f, out index)) return false;
            object o = f.GetValue(this);
            if (f.FieldType == typeof(float)) value = (float)o;
            else if (f.FieldType == typeof(int)) value = (int)o;
            else value = ((float[])o)[index];
            return true;
        }

        public bool TrySet(string key, float value)
        {
            if (!WithinRange(value)) return false;
            FieldInfo f;
            int index;
            if (!Locate(key, out f, out index)) return false;
            if (f.FieldType == typeof(float)) f.SetValue(this, value);
            else if (f.FieldType == typeof(int)) f.SetValue(this, (int)(value >= 0f ? (double)value + 0.5 : (double)value - 0.5));
            else ((float[])f.GetValue(this))[index] = value;
            return true;
        }

        static bool WithinRange(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }

        static bool Locate(string key, out FieldInfo field, out int index)
        {
            field = null;
            index = 0;
            if (string.IsNullOrEmpty(key)) return false;
            string name = key;
            string sub = null;
            int dot = key.IndexOf('.');
            if (dot > 0)
            {
                name = key.Substring(0, dot);
                sub = key.Substring(dot + 1);
            }
            foreach (FieldInfo f in KeyFields)
            {
                if (!string.Equals(f.Name, name, StringComparison.Ordinal)) continue;
                if (f.FieldType == typeof(float[]))
                {
                    if (sub == null) return false;
                    int parsed;
                    if (int.TryParse(sub, out parsed) && parsed >= 0 && parsed < EntityTypeCount) index = parsed;
                    else if (Enum.IsDefined(typeof(EntityType), sub)) index = (int)(EntityType)Enum.Parse(typeof(EntityType), sub);
                    else return false;
                }
                else if (sub != null) return false;
                field = f;
                return true;
            }
            return false;
        }

        public HyperspaceTuning Clone()
        {
            var c = (HyperspaceTuning)MemberwiseClone();
            c.enemyRadius = (float[])enemyRadius.Clone();
            c.enemyHalfDepth = (float[])enemyHalfDepth.Clone();
            c.enemyHp = (float[])enemyHp.Clone();
            c.enemyDamage = (float[])enemyDamage.Clone();
            c.enemyScore = (float[])enemyScore.Clone();
            c.enemyScrap = (float[])enemyScrap.Clone();
            c.enemyCost = (float[])enemyCost.Clone();
            return c;
        }

        // ---------------------------------------------------------------- derived values
        /// <summary>Power rating of a ship: sqrt(effective hull x damage per second) scaled by handling and bomb count; starter = 1.</summary>
        public float ShipPower(ShipStats s)
        {
            double effHp = s.hull + (double)s.shield * powerShieldHitValue;
            double dps = (double)s.damage * s.fireRate * (1.0 + (double)powerPierceDps * s.pierce);
            double core = HMath.Sqrt((effHp / powerHullRef) * (dps / powerDpsRef));
            double handling = 1.0 + powerHandlingWeight * ((double)s.handling - 1.0);
            double bombs = 1.0 + (double)powerBombWeight * (s.bombCapacity - 1);
            return (float)(core * handling * bombs);
        }

        /// <summary>The power a ship is expected to have on a route of this tier (used so a strong ship is not punished, only challenged a little).</summary>
        public float ExpectedPower(int tier)
        {
            return (float)(expectedPowerBase + (double)expectedPowerPerTier * HMath.Clamp(tier, 0, 12));
        }

        /// <summary>Clearance the fairness proof keeps around hazards at a tier (fairnessMargin at tier 0, fairnessMarginDeep from marginRampTiers on).</summary>
        public float FairnessMarginFor(int tier)
        {
            double f = HMath.Clamp01((double)tier / (marginRampTiers > 1f ? marginRampTiers : 1f));
            return (float)HMath.Lerp(fairnessMargin, fairnessMarginDeep, f);
        }

        /// <summary>Spare room left in the narrowest gate at a tier.</summary>
        public float GapSlackFor(int tier)
        {
            double f = HMath.Clamp01((double)tier / (marginRampTiers > 1f ? marginRampTiers : 1f));
            return (float)HMath.Lerp(gapSlack, gapSlackDeep, f);
        }

        /// <summary>Gate opening multiplier for a tier (1 at tier 0 down to gapScaleMin).</summary>
        public float GapScaleFor(int tier) { return (float)HMath.Max(gapScaleMin, 1.0 - (double)gapScalePerTier * HMath.Clamp(tier, 0, 12)); }

        /// <summary>Time compression of a template's z offsets for a tier (1 at tier 0 down to timeScaleMin).</summary>
        public float TimeScaleFor(int tier) { return (float)HMath.Max(timeScaleMin, 1.0 - (double)timeScalePerTier * HMath.Clamp(tier, 0, 12)); }

        public float MaxShipSpeed(float handling) { return (float)((double)baseMaxSpeed * handling); }

        /// <summary>Lateral speed the fairness contract allows a template to demand (starter ship, safety fraction applied).</summary>
        public float FairSpeed { get { return (float)((double)baseMaxSpeed * fairnessSpeedFraction); } }

        // ---------------------------------------------------------------- tier scaling of aimed threats (used by the run and by the bot)
        /// <summary>Seconds a turret shows its lock ring before firing: 0.85 s at tier 0 down to 0.6 s at tier 12 (always above reactionBudget + escape time).</summary>
        public float TurretTelegraphFor(int tier) { return (float)HMath.Lerp(turretTelegraph, turretTelegraphMin, HMath.Clamp01((double)tier / 12.0)); }

        public float BoltSpeedFor(int tier) { return (float)(boltSpeed * (1.0 + (double)boltSpeedPerTier * HMath.Clamp(tier, 0, 12))); }

        public int FanCountFor(int tier) { return tier >= turretFanTier ? turretFanCount + 2 : turretFanCount; }

        public float SeekerCommitFor(int tier) { return (float)HMath.Lerp(seekerCommitSeconds, seekerCommitMin, HMath.Clamp01((double)tier / 12.0)); }

        public float SeekerSpeedScale(int tier) { return (float)(1.0 + (double)seekerSpeedPerTier * HMath.Clamp(tier, 0, 12)); }

        public float EnemyRadius(EntityType t) { return enemyRadius[(int)t]; }
    }
}
