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
