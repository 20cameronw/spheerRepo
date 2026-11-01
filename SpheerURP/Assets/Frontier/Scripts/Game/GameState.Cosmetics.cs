using System;
using System.Collections.Generic;
using System.Linq;

namespace Frontier
{
    public partial class GameState
    {
        public static readonly string[] BuildingThemes = { "original", "ember", "jade", "cyan", "violet", "galaxy" };
        const string ThemeSetting = "building_theme";

        public string BuildingTheme
        {
            get
            {
                string selected = settings?.FirstOrDefault(entry => entry != null && entry.key == ThemeSetting)?.value;
                return Array.IndexOf(BuildingThemes, selected) >= 0 ? selected : "original";
            }
        }

        public bool OwnsBuildingTheme(string id)
        {
            return id == "original" || HasSetting("theme_owned_" + id);
        }

        public int BuildingThemePrice(string id)
        {
            if (Array.IndexOf(BuildingThemes, id) < 0) return -1;
            if (OwnsBuildingTheme(id)) return 0;
            var rules = db.shardRules?.data;
            if (rules == null) return -1;
            return id == "galaxy" ? (int)Math.Min(rules.cosmeticMaxShards, (long)rules.cosmeticMinShards * 4)
                : rules.cosmeticMinShards;
        }

        public bool PurchaseBuildingTheme(string id, int maximumPrice, out string message)
        {
            int price = BuildingThemePrice(id);
            if (SaveIsReadOnly || price < 0 || maximumPrice < price)
            {
                message = UiText.Get("theme.unavailable");
                return false;
            }
            if (ShardBalance < price)
            {
                message = UiText.Get("crew.purchase.shards-missing");
                return false;
            }
            var candidate = SaveSerializer.Instance.DeserializeV2(SaveSerializer.Instance.Serialize(BuildSave())).Normalize();
            int remaining = price;
            foreach (var entry in candidate.wallet)
            {
                if (entry == null || entry.currency != ShardCurrency || entry.amount <= 0) continue;
                int spend = Math.Min(entry.amount, remaining);
                entry.amount -= spend;
                remaining -= spend;
            }
            candidate.settings.RemoveAll(entry => entry != null &&
                (entry.key == ThemeSetting || entry.key == "theme_owned_" + id));
            candidate.settings.Add(new SettingEntry { key = ThemeSetting, value = id });
            candidate.settings.Add(new SettingEntry { key = "theme_owned_" + id, value = "1" });
            if (!saves.CommitSnapshot(candidate))
            {
                message = UiText.Get("theme.save-failed", saves.LastError);
                return false;
            }
            wallet = candidate.wallet;
            settings = candidate.settings;
            world.Touch();
            Changed?.Invoke();
            message = UiText.Get("theme.applied", UiText.Get("theme." + id));
            return true;
        }
    }
}
