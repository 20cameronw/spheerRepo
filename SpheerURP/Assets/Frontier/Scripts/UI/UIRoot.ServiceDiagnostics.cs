using System;
using Frontier.Services;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public partial class UIRoot
    {
        bool adTestBusy;
        string adTestStatus;

        void BuildServiceDiagnostics(VisualElement body)
        {
            var card = Card(UiKit.Cyan);
            card.name = "service-diagnostics";
            card.Add(UiKit.Text(UiText.Get("debug.services.title"), "fr-h2"));
            card.Add(UiKit.Text(UiText.Get("debug.services.ads-disabled"), "fr-muted"));
            var diagnostics = services == null ? null : services.AdDiagnostics;
            card.Add(UiKit.Text(diagnostics != null ? diagnostics.Status.Resolve(UiText.Get)
                : UiText.Get("debug.services.ads-unavailable"), "fr-muted"));
            if (!string.IsNullOrEmpty(adTestStatus)) card.Add(UiKit.Text(adTestStatus, "fr-muted"));
            var open = IconBtn(UiText.Get(adTestBusy ? "debug.services.connecting" : "debug.services.open-ads"),
                "play", UiKit.Cyan, OpenAdTestSuite);
            open.name = "debug-ad-test-suite";
            UiKit.SetEnabledLook(open, !adTestBusy && diagnostics != null && diagnostics.CanOpenTestSuite);
            card.Add(open);
            card.Add(UiKit.Text(UiText.Get("ranks.setup-required"), "fr-muted"));
            body.Add(card);
        }

        async void OpenAdTestSuite()
        {
            if (adTestBusy || services == null || services.AdDiagnostics == null) return;
            adTestBusy = true;
            adTestStatus = UiText.Get("debug.services.connecting");
            RefreshModal();
            try
            {
                var result = await services.AdDiagnostics.OpenTestSuiteAsync();
                adTestStatus = result.succeeded ? UiText.Get("debug.services.opened") : result.text.Resolve(UiText.Get);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                adTestStatus = UiText.Get("debug.services.failed");
            }
            finally
            {
                adTestBusy = false;
                if (menuPage == "debug") RefreshModal();
            }
        }
    }
}
