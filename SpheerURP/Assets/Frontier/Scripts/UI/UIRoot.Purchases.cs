using System;
using System.Collections.Generic;
using System.Linq;
using Frontier.Services;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public partial class UIRoot
    {
        IReadOnlyList<StoreProduct> shardStoreProducts;
        bool shardStoreBusy;
        string shardStoreStatus;

        void BuildShardStore(VisualElement body)
        {
            body.Add(SectionLabel(UiText.Get("shards.store.title")));
            body.Add(UiKit.Text(UiText.Get("shards.store.help"), "fr-muted"));
            bool configured = services != null && services.Purchases.IsConfigured;
            var status = UiKit.Text(!configured ? UiText.Get("shards.store.setup") :
                shardStoreStatus ?? UiText.Get("shards.store.connect-help"), "fr-small");
            status.name = "shard-store-status";
            status.style.whiteSpace = WhiteSpace.Normal;
            status.style.minWidth = 0;
            status.style.flexShrink = 1;
            body.Add(status);
            if (configured)
            {
                var connect = IconBtn(UiText.Get(shardStoreBusy ? "shards.store.busy" : "shards.store.connect"),
                    "ship", UiKit.Cyan, LoadShardStore);
                connect.name = "shard-store-connect";
                UiKit.SetEnabledLook(connect, !shardStoreBusy && !gs.SaveIsReadOnly);
                body.Add(connect);
            }
            var packs = gs.db.shardRules != null && gs.db.shardRules.data != null ? gs.db.shardRules.data.packs : null;
            if (packs == null) return;
            foreach (var pack in packs)
            {
                var product = shardStoreProducts == null ? default :
                    shardStoreProducts.FirstOrDefault(p => p.id == pack.id);
                string id = pack.id;
                bool available = configured && !string.IsNullOrWhiteSpace(product.localizedPrice);
                var row = UiKit.Div("fr-row");
                row.name = "shard-pack-" + id;
                row.style.flexWrap = Wrap.Wrap;
                row.style.marginTop = UiKit.Space.S12;
                var amount = UiKit.Text(UiText.Get("shards.store.amount", pack.shards.ToString("N0")), "fr-h2", UiKit.Magenta);
                amount.style.flexGrow = 1;
                row.Add(amount);
                var buy = UiKit.Btn(available ? UiText.Get("shards.store.buy", product.localizedPrice) :
                    UiText.Get("shards.store.unavailable"), UiKit.Magenta, () => BuyShardPack(id));
                buy.name = "shard-pack-buy-" + id;
                UiKit.SetEnabledLook(buy, available && !shardStoreBusy && !gs.SaveIsReadOnly);
                row.Add(buy);
                body.Add(row);
            }
        }

        async void LoadShardStore()
        {
            if (shardStoreBusy || services == null || !services.Purchases.IsConfigured || gs.SaveIsReadOnly) return;
            var active = services;
            var state = gs;
            shardStoreBusy = true;
            shardStoreProducts = null;
            shardStoreStatus = UiText.Get("shards.store.busy");
            RefreshModal();
            try
            {
                var ids = gs.db.shardRules.data.packs.Select(pack => pack.id).ToArray();
                var products = await active.Purchases.FetchProductsAsync(ids);
                if (this == null || services != active || gs != state) return;
                if (!products.succeeded) { shardStoreStatus = products.text.Resolve(UiText.Get); return; }
                var recovered = await active.Purchases.RestoreAsync();
                if (this == null || services != active || gs != state) return;
                if (!recovered.succeeded) { shardStoreStatus = recovered.text.Resolve(UiText.Get); return; }
                shardStoreProducts = products.value;
                shardStoreStatus = UiText.Get("shards.store.ready");
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Frontier: shard store connection failed (" + exception.GetType().Name + ").");
                shardStoreStatus = UiText.Get("purchase.connect-failed");
            }
            finally
            {
                shardStoreBusy = false;
                if (this != null && menuPage == "shards") RefreshModal();
            }
        }

        async void BuyShardPack(string id)
        {
            if (shardStoreBusy || services == null || !services.Purchases.IsConfigured ||
                gs.SaveIsReadOnly || shardStoreProducts == null ||
                !shardStoreProducts.Any(p => p.id == id && !string.IsNullOrWhiteSpace(p.localizedPrice))) return;
            var active = services;
            var state = gs;
            shardStoreBusy = true;
            shardStoreStatus = UiText.Get("shards.store.busy");
            RefreshModal();
            try
            {
                var result = await active.Purchases.PurchaseAsync(id);
                if (this == null || services != active || gs != state) return;
                shardStoreStatus = result.succeeded ? UiText.Get("shards.store.delivered") : result.text.Resolve(UiText.Get);
                // Recover/verify outstanding transactions before allowing another payment after an uncertain result.
                if (!result.succeeded) shardStoreProducts = null;
                Toast(shardStoreStatus, result.succeeded ? UiKit.Lime : UiKit.Amber);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Frontier: shard store payment failed (" + exception.GetType().Name + ").");
                shardStoreProducts = null;
                shardStoreStatus = UiText.Get("purchase.unsettled");
            }
            finally
            {
                shardStoreBusy = false;
                if (this != null && menuPage == "shards") RefreshModal();
            }
        }
    }
}
