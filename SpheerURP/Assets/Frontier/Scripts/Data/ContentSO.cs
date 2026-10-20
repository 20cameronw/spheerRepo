using System;
using System.Collections.Generic;
using UnityEngine;
using EItem = Frontier.Economy.ItemAmount;

namespace Frontier
{
    /// <summary>
    /// Base of every ScriptableObject the economy importer fills. The importer writes numbers, ids and structure (the catalog owns them) and
    /// never overwrites hand-authored presentation (colours, icons, model keys, text that already has content); a designer can also pin any
    /// field of one asset by listing its name in <see cref="lockedFields"/>. See docs/CONTENT.md.
    /// </summary>
    public abstract class ContentSO : ScriptableObject
    {
        [Tooltip("Field names the importer must never write on this asset (it also skips them in the drift test).")]
        public string[] lockedFields = new string[0];

        public bool IsLocked(string field)
        {
            if (lockedFields == null) return false;
            foreach (string f in lockedFields) if (string.Equals(f, field, StringComparison.Ordinal)) return true;
            return false;
        }
    }

    public static class ContentText
    {
        public static string Get(string category, string id, string field, string english)
        {
            // English authoring remains live; translating must never modify catalog ids or serialized gameplay definitions.
            return UiText.LocaleCode == "en" || string.IsNullOrEmpty(english) ? english
                : UiText.Template("content." + category + "." + id + "." + field);
        }

        public static string Label(string id)
        {
            return string.IsNullOrEmpty(id) ? UiText.Get("common.unknown") : Get("label", id, "name", EnglishLabel(id));
        }

        static string EnglishLabel(string value)
        {
            var words = new List<string>();
            var word = new System.Text.StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (current == '_')
                {
                    if (word.Length > 0) { words.Add(word.ToString()); word.Length = 0; }
                    continue;
                }
                bool newWord = i > 0 && char.IsUpper(current) &&
                    (char.IsLower(value[i - 1]) || char.IsDigit(value[i - 1]));
                if (newWord && word.Length > 0) { words.Add(word.ToString()); word.Length = 0; }
                word.Append(current);
            }
            if (word.Length > 0) words.Add(word.ToString());
            for (int i = 0; i < words.Count; i++)
                words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1).ToLowerInvariant();
            return string.Join(" ", words);
        }

        public static IEnumerable<KeyValuePair<string, string>> EnglishEntries(GameDatabase db, ObjectiveCatalogSO objectives)
        {
            KeyValuePair<string, string> Entry(string category, string id, string field, string text)
            {
                return new KeyValuePair<string, string>("content." + category + "." + id + "." + field, text);
            }
            foreach (var resource in db.resources)
            {
                yield return Entry("resource", resource.id, "name", resource.displayName);
                yield return Entry("resource", resource.id, "description", resource.description);
                yield return Entry("resource", resource.id, "detail", resource.DetailTextEnglish);
            }
            foreach (var structure in db.structures)
            {
                yield return Entry("structure", structure.id, "name", structure.displayName);
                yield return Entry("structure", structure.id, "description", structure.description);
            }
            foreach (var recipe in db.recipes) yield return Entry("recipe", recipe.id, "name", recipe.displayName);
            foreach (var enemy in db.enemies) yield return Entry("enemy", enemy.id, "name", enemy.displayName);
            foreach (var upgrade in db.upgrades)
            {
                yield return Entry("upgrade", upgrade.id, "name", upgrade.displayName);
                yield return Entry("upgrade", upgrade.id, "description", upgrade.description);
                if (upgrade.levels != null)
                    foreach (var level in upgrade.levels)
                        yield return Entry("upgrade", upgrade.id, "level-" + level.level, level.displayName);
                if (upgrade.levelNames != null)
                    for (int i = 0; i < upgrade.levelNames.Length; i++)
                        yield return Entry("upgrade", upgrade.id, "legacy-level-" + i, upgrade.levelNames[i]);
            }
            var labels = new HashSet<string>(StringComparer.Ordinal);
            foreach (var recipe in db.recipes)
            {
                labels.Add(recipe.kind.ToString());
                labels.Add(recipe.stationId);
                labels.Add(recipe.unlockResearch);
            }
            foreach (var research in db.research ?? Array.Empty<ResearchNodeSO>())
            {
                yield return Entry("research", research.data.id, "name", research.data.displayName);
                yield return Entry("research", research.data.id, "description", research.data.description);
                labels.Add(research.data.branch);
                foreach (string id in research.data.prereqs ?? Array.Empty<string>()) labels.Add(id);
                foreach (string id in research.data.unlocks ?? Array.Empty<string>()) labels.Add(id);
            }
            foreach (var ladder in db.toolLadders ?? Array.Empty<ToolLadderSO>())
            {
                yield return Entry("tool", ladder.data.id, "name", ladder.data.displayName);
                foreach (var tier in ladder.data.tiers)
                {
                    yield return Entry("tool", ladder.data.id, "tier-" + tier.tier, tier.displayName);
                    labels.Add(tier.researchPrereq);
                }
            }
            var orderedLabels = new List<string>(labels);
            orderedLabels.Sort(StringComparer.Ordinal);
            foreach (string id in orderedLabels)
                if (!string.IsNullOrEmpty(id)) yield return Entry("label", id, "name", EnglishLabel(id));
            foreach (var obstacle in db.obstacleKinds ?? Array.Empty<ObstacleKindSO>())
            {
                yield return Entry("obstacle", obstacle.data.id, "name", obstacle.data.displayName);
                yield return Entry("obstacle", obstacle.data.id, "description", obstacle.data.description);
            }
            foreach (var planet in db.planets)
            {
                yield return Entry("planet", planet.id, "description", planet.description);
                for (int i = 0; i < planet.spots.Count; i++)
                    yield return Entry("planet", planet.id, "spot-" + i, planet.spots[i].label);
            }
            if (db.formula != null)
                for (int i = 0; i < db.formula.modifiers.Count; i++)
                {
                    var modifier = db.formula.modifiers[i];
                    yield return Entry("wave-modifier", i.ToString(System.Globalization.CultureInfo.InvariantCulture), "name", modifier.name);
                    yield return Entry("wave-modifier", i.ToString(System.Globalization.CultureInfo.InvariantCulture), "description", modifier.description);
                }
            if (objectives != null)
                foreach (var objective in objectives.entries)
                {
                    yield return Entry("objective", objective.id, "title", objective.title);
                    yield return Entry("objective", objective.id, "description", objective.description);
                }
        }
    }

    /// <summary>Conversions between the ScriptableObject cost lists and the id based economy types.</summary>
    public static class ItemAmounts
    {
        public static EItem[] ToIds(List<ItemAmount> items)
        {
            var result = new EItem[items == null ? 0 : items.Count];
            for (int i = 0; i < result.Length; i++) result[i] = new EItem(items[i].resource != null ? items[i].resource.id : "", items[i].amount);
            return result;
        }

        public static EItem ToId(ItemAmount item) { return new EItem(item.resource != null ? item.resource.id : "", item.amount); }

        /// <summary>The items with the resources found by id; an unknown id yields a null resource (the validators report it).</summary>
        public static List<ItemAmount> FromIds(EItem[] items, Func<string, ResourceSO> find)
        {
            var list = new List<ItemAmount>();
            if (items == null) return list;
            foreach (var item in items) list.Add(new ItemAmount(find(item.resourceId), item.amount));
            return list;
        }

        public static string NullIfEmpty(string text) { return string.IsNullOrEmpty(text) ? null : text; }

        public static T[] CloneOrNull<T>(T[] array) { return array == null || array.Length == 0 ? null : (T[])array.Clone(); }
    }
}
