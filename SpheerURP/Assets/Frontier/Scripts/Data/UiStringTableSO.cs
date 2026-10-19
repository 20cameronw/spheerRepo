using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Frontier
{
    [CreateAssetMenu(fileName = "UiStrings", menuName = "Frontier/UI String Table")]
    public sealed class UiStringTableSO : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string key;
            [TextArea] public string text;
        }

        public string locale = "en";
        public Entry[] entries = Array.Empty<Entry>();
        Dictionary<string, string> index;

        public void Validate()
        {
            index = null;
            var candidate = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(locale)) throw new InvalidOperationException("UI string table has no locale.");
            if (entries == null) throw new InvalidOperationException("UI string table has no entries.");
            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.key) || string.IsNullOrWhiteSpace(entry.text))
                    throw new InvalidOperationException("UI string table contains an empty key or text.");
                if (candidate.ContainsKey(entry.key))
                    throw new InvalidOperationException("Duplicate UI string key: " + entry.key);
                candidate.Add(entry.key, entry.text);
            }
            index = candidate;
        }

        public string Get(string key, params object[] arguments)
        {
            if (index == null) Validate();
            if (key == null || !index.TryGetValue(key, out var text))
                throw new KeyNotFoundException("Missing UI string: " + key);
            return string.Format(CultureInfo.CurrentCulture, text, arguments);
        }

        void OnEnable() { index = null; }
        void OnValidate() { index = null; }
    }

    public static class UiText
    {
        public const string CollectionName = "Frontier UI";
        static UiStringTableSO table;
        static StringTable localizedTable;

        public static bool UsesUnityLocalization { get { return localizedTable != null; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { table = null; localizedTable = null; }

        public static IEnumerator InitializeLocalization()
        {
            localizedTable = null;
            var initialization = LocalizationSettings.InitializationOperation;
            yield return initialization;
            if (initialization.Status != AsyncOperationStatus.Succeeded)
                throw new InvalidOperationException("Frontier localization initialization failed.", initialization.OperationException);
            if (LocalizationSettings.SelectedLocale == null)
                throw new InvalidOperationException("Frontier localization has no selected locale.");
            var load = LocalizationSettings.StringDatabase.GetTableAsync(CollectionName);
            yield return load;
            if (load.Status != AsyncOperationStatus.Succeeded || load.Result == null)
                throw new InvalidOperationException("Frontier UI localization table is missing. Run Frontier > Localization > Sync English Catalog.",
                    load.OperationException);
            localizedTable = load.Result;
        }

        public static string Get(string key, params object[] arguments)
        {
            if (localizedTable != null)
            {
                var entry = key == null ? null : localizedTable.GetEntry(key);
                if (entry == null || string.IsNullOrWhiteSpace(entry.Value))
                    throw new KeyNotFoundException("Missing localized UI string: " + key);
                return string.Format(CultureInfo.CurrentCulture, entry.Value, arguments);
            }
            if (table == null)
            {
                table = Resources.Load<UiStringTableSO>("Frontier/UiStrings");
                if (table == null) throw new InvalidOperationException("Frontier UI string table is missing.");
            }
            return table.Get(key, arguments);
        }
    }
}
