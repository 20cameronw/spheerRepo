#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace Frontier.EditorTools
{
    public sealed class FrontierLocalization : IPreprocessBuildWithReport
    {
        const string DirectoryPath = "Assets/Frontier/Localization";
        public int callbackOrder { get { return 0; } }

        [MenuItem("Frontier/Localization/Sync English Catalog")]
        public static void SyncEnglishCatalog()
        {
            var source = Resources.Load<UiStringTableSO>("Frontier/UiStrings");
            if (source == null) throw new InvalidOperationException("Frontier UI catalog is missing.");
            source.Validate();
            if (source.locale != "en") throw new InvalidOperationException("The source UI catalog must remain English.");
            var entries = new List<UiStringTableSO.Entry>(source.entries);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries) keys.Add(entry.key);
            foreach (var entry in Frontier.Universe.PlanetBlurb.EnglishEntries())
                if (keys.Add(entry.Key))
                    entries.Add(new UiStringTableSO.Entry { key = entry.Key, text = entry.Value });
            foreach (var entry in ContentText.EnglishEntries(GameDatabase.Load(), Resources.Load<ObjectiveCatalogSO>("Frontier/ObjectiveCatalog")))
                if (!string.IsNullOrWhiteSpace(entry.Value) && keys.Add(entry.Key))
                    entries.Add(new UiStringTableSO.Entry { key = entry.Key, text = entry.Value });
            source.entries = entries.ToArray();
            source.Validate();
            EditorUtility.SetDirty(source);

            Directory.CreateDirectory(DirectoryPath);
            AssetDatabase.Refresh();
            var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
            bool newSettings = settings == null;
            if (newSettings)
            {
                settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                AssetDatabase.CreateAsset(settings, DirectoryPath + "/LocalizationSettings.asset");
                LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            }

            Locale english = null;
            foreach (var locale in LocalizationEditorSettings.GetLocales())
                if (locale.Identifier.Code == "en") english = locale;
            if (english == null)
            {
                english = Locale.CreateLocale("en");
                AssetDatabase.CreateAsset(english, DirectoryPath + "/English.asset");
                LocalizationEditorSettings.AddLocale(english);
            }
            if (newSettings)
            {
                LocalizationSettings.ProjectLocale = english;
                EditorUtility.SetDirty(settings);
            }

            var collection = LocalizationEditorSettings.GetStringTableCollection(UiText.CollectionName);
            if (collection == null)
                collection = LocalizationEditorSettings.CreateStringTableCollection(UiText.CollectionName,
                    DirectoryPath, new[] { english });
            var table = collection.GetTable(english.Identifier) as StringTable;
            if (table == null) table = (StringTable)collection.AddNewTable(english.Identifier);
            foreach (var entry in source.entries)
            {
                var target = table.GetEntry(entry.key) ?? table.AddEntry(entry.key, entry.text);
                target.Value = entry.text;
                target.IsSmart = false;
            }
            LocalizationEditorSettings.SetPreloadTableFlag(table, true);
            var addressables = AddressableAssetSettingsDefaultObject.Settings;
            if (addressables == null) throw new InvalidOperationException("Localization Addressables settings are missing.");
            addressables.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
            EditorUtility.SetDirty(addressables);
            EditorUtility.SetDirty(table);
            EditorUtility.SetDirty(collection.SharedData);
            EditorUtility.SetDirty(collection);
            AssetDatabase.SaveAssets();
            ValidateEnglishCatalog();
            Debug.Log("Frontier: synchronized " + source.entries.Length + " English localization strings; other locales and stable keys retained.");
        }

        public static void ValidateEnglishCatalog()
        {
            var source = Resources.Load<UiStringTableSO>("Frontier/UiStrings");
            if (source == null) throw new BuildFailedException("Frontier UI catalog is missing.");
            source.Validate();
            var sourceKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in source.entries) sourceKeys.Add(entry.key);
            foreach (var entry in Frontier.Universe.PlanetBlurb.EnglishEntries())
            {
                if (!sourceKeys.Contains(entry.Key))
                    throw new BuildFailedException("Generated-world localization is missing: " + entry.Key +
                        ". Run Frontier > Localization > Sync English Catalog.");
            }
            foreach (var entry in ContentText.EnglishEntries(GameDatabase.Load(), Resources.Load<ObjectiveCatalogSO>("Frontier/ObjectiveCatalog")))
                if (!string.IsNullOrWhiteSpace(entry.Value) && !sourceKeys.Contains(entry.Key))
                    throw new BuildFailedException("Authored-content localization is missing: " + entry.Key +
                        ". Run Frontier > Localization > Sync English Catalog.");
            if (LocalizationEditorSettings.ActiveLocalizationSettings == null)
                throw new BuildFailedException("Frontier localization settings are missing.");
            var addressables = AddressableAssetSettingsDefaultObject.Settings;
            if (addressables == null ||
                addressables.BuildAddressablesWithPlayerBuild != AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer)
                throw new BuildFailedException("Frontier localization content must build with the player.");
            var collection = LocalizationEditorSettings.GetStringTableCollection(UiText.CollectionName);
            var english = collection == null ? null : collection.GetTable(new LocaleIdentifier("en")) as StringTable;
            if (english == null || !LocalizationEditorSettings.GetPreloadTableFlag(english))
                throw new BuildFailedException("Frontier English localization table must exist and be preloaded.");
            foreach (var entry in source.entries)
            {
                var target = english.GetEntry(entry.key);
                if (target == null || target.IsSmart || target.Value != entry.text)
                    throw new BuildFailedException("English localization drift: " + entry.key +
                        ". Run Frontier > Localization > Sync English Catalog.");
            }
            foreach (var locale in LocalizationEditorSettings.GetLocales())
            {
                var localized = collection.GetTable(locale.Identifier) as StringTable;
                if (localized == null || !LocalizationEditorSettings.GetPreloadTableFlag(localized))
                    throw new BuildFailedException("Frontier language must have a complete preloaded UI table: " + locale.Identifier.Code);
                try { UiText.ValidateLocalizedTable(localized); }
                catch (InvalidOperationException exception)
                {
                    throw new BuildFailedException("Frontier language " + locale.Identifier.Code + ": " + exception.Message);
                }
            }
        }

        public void OnPreprocessBuild(BuildReport report) { ValidateEnglishCatalog(); }

        [MenuItem("Frontier/Localization/Build Local Tables")]
        public static void BuildLocalTables()
        {
            SyncEnglishCatalog();
            AddressableAssetSettings.BuildPlayerContent(out var result);
            if (result == null || !string.IsNullOrEmpty(result.Error))
                throw new BuildFailedException("Frontier localization bundle build failed: " + result?.Error);
            Debug.Log("Frontier: local localization bundles built successfully.");
        }
    }
}
#endif
