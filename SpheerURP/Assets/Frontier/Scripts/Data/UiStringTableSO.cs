using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Localization;
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
        public const string LocalePreferenceKey = "fr_locale";
        static UiStringTableSO table;
        static StringTable localizedTable;
        public static bool IsChangingLocale { get; private set; }

        public static bool UsesUnityLocalization { get { return localizedTable != null; } }
        public static string LocaleCode { get { return localizedTable != null ? localizedTable.LocaleIdentifier.Code : Source.locale; } }

        static UiStringTableSO Source
        {
            get
            {
                if (table == null)
                {
                    table = Resources.Load<UiStringTableSO>("Frontier/UiStrings");
                    if (table == null) throw new InvalidOperationException("Frontier UI string table is missing.");
                }
                return table;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { table = null; localizedTable = null; IsChangingLocale = false; }

        public static IEnumerator InitializeLocalization()
        {
            localizedTable = null;
            var initialization = LocalizationSettings.InitializationOperation;
            yield return initialization;
            if (initialization.Status != AsyncOperationStatus.Succeeded)
                throw new InvalidOperationException("Frontier localization initialization failed.", initialization.OperationException);
            if (LocalizationSettings.SelectedLocale == null)
                throw new InvalidOperationException("Frontier localization has no selected locale.");
            string saved = PlayerPrefs.GetString(LocalePreferenceKey, "");
            var locale = string.IsNullOrEmpty(saved) ? LocalizationSettings.SelectedLocale
                : LocalizationSettings.AvailableLocales.GetLocale(saved);
            if (locale == null)
            {
                Debug.LogWarning("Frontier: saved language is no longer installed: " + saved + ". Using the startup locale.");
                locale = LocalizationSettings.SelectedLocale;
                PlayerPrefs.DeleteKey(LocalePreferenceKey);
                PlayerPrefs.Save();
            }
            var load = LocalizationSettings.StringDatabase.GetTableAsync(CollectionName, locale);
            yield return load;
            if (load.Status != AsyncOperationStatus.Succeeded || load.Result == null)
                throw new InvalidOperationException("Frontier UI localization table is missing. Run Frontier > Localization > Sync English Catalog.",
                    load.OperationException);
            ValidateLocalizedTable(load.Result);
            localizedTable = load.Result;
            LocalizationSettings.SelectedLocale = locale;
        }

        public static void ValidateLocalizedTable(StringTable candidate)
        {
            if (candidate == null) throw new InvalidOperationException("Frontier UI localization table is missing.");
            foreach (var source in Source.entries)
            {
                var entry = candidate.GetEntry(source.key);
                if (entry == null || string.IsNullOrWhiteSpace(entry.Value) || entry.IsSmart)
                    throw new InvalidOperationException("Frontier UI localization is missing a plain-text entry: " + source.key);
            }
        }

        public static IEnumerator SelectLocale(string code, Action<string> completed)
        {
            if (completed == null) throw new ArgumentNullException(nameof(completed));
            if (IsChangingLocale)
            {
                completed(Get("settings.language.busy"));
                yield break;
            }
            var locale = string.IsNullOrWhiteSpace(code) ? null : LocalizationSettings.AvailableLocales.GetLocale(code);
            if (locale == null)
            {
                completed(Get("settings.language.unavailable"));
                yield break;
            }
            IsChangingLocale = true;
            string error = null;
            try
            {
                var load = LocalizationSettings.StringDatabase.GetTableAsync(CollectionName, locale);
                yield return load;
                if (load.Status != AsyncOperationStatus.Succeeded || load.Result == null)
                    error = Get("settings.language.failed");
                else
                {
                    try { ValidateLocalizedTable(load.Result); }
                    catch (InvalidOperationException exception)
                    {
                        Debug.LogWarning(exception.Message);
                        error = Get("settings.language.failed");
                    }
                    if (error == null)
                    {
                        localizedTable = load.Result;
                        LocalizationSettings.SelectedLocale = locale;
                        PlayerPrefs.SetString(LocalePreferenceKey, locale.Identifier.Code);
                        PlayerPrefs.Save();
                    }
                }
            }
            finally { IsChangingLocale = false; }
            completed(error);
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
            return Source.Get(key, arguments);
        }
    }
}
