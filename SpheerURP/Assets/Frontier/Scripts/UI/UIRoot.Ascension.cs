using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public partial class UIRoot
    {
        bool confirmAscension;

        public static Color AscensionAccent(int rank)
        {
            return rank <= 0 ? UiKit.Magenta : AscensionStyle.Accent(rank);
        }

        static Label AscensionNote(string text, Color color)
        {
            var label = Note(text, color);
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        void BuildAscension(VisualElement body)
        {
            var card = Card(AscensionAccent(gs.AscensionRank));
            card.name = "ascension-progress";
            card.Add(UiKit.Text(UiText.Get("ascension.title", gs.AscensionRank), "fr-h2"));
            if (!gs.AscensionAvailable)
            {
                card.Add(UiKit.Text(UiText.Get("ascension.disabled"), "fr-muted"));
                body.Add(card);
                return;
            }
            StatRow(card, "star", UiKit.Magenta, UiText.Get("ascension.legacy-points"), gs.LegacyPoints.ToString());
            StatRow(card, "tools", UiKit.Amber, UiText.Get("ascension.harvest-yield"), "x" + gs.LegacyHarvestMultiplier.ToString("0.00"));
            StatRow(card, "ranks", UiKit.Lime, UiText.Get("ascension.score-multiplier"), "x" + gs.FrontierScore.multiplier.ToString("0.00"));
            card.Add(UiKit.Text(UiText.Get("ascension.summary"), "fr-muted"));
            var keeps = AscensionNote(UiText.Get("ascension.keeps"), UiKit.Cyan);
            keeps.name = "ascension-keeps";
            card.Add(keeps);
            card.Add(AscensionNote(UiText.Get("ascension.resets"), UiKit.Amber));
            card.Add(AscensionNote(UiText.Get("ascension.next-yield", gs.NextLegacyHarvestMultiplier,
                gs.MaximumLegacyHarvestBonus * 100), UiKit.Amber));
            card.Add(UiKit.Text(UiText.Get("ascension.recovery"), "fr-tiny"));
            body.Add(card);

            string locked = gs.AscensionLockReason();
            if (locked != null)
            {
                confirmAscension = false;
                card.Add(AscensionNote(locked, UiKit.Amber));
                if (!gs.SaveIsReadOnly)
                    card.Add(gs.AscensionNeedsResearch
                        ? IconBtn(UiText.Get("ascension.open-command"), "research", UiKit.Magenta, () => { researchTab = 2; progressionTab = 0; OpenResearch(); })
                        : IconBtn(UiText.Get("ascension.open-build"), "build", UiKit.Lime, () => { shopTab = 0; OpenShop(); }));
                return;
            }
            if (!confirmAscension)
            {
                var review = IconBtn(UiText.Get("ascension.review"), "play", UiKit.Magenta, () => { confirmAscension = true; RefreshModal(); });
                review.name = "ascension-review";
                body.Add(review);
                return;
            }
            body.Add(AscensionNote(UiText.Get("ascension.confirm-prompt"), UiKit.Amber));
            var confirm = IconBtn(UiText.Get("ascension.confirm"), "play", UiKit.Red, () =>
            {
                if (busy || defense.Active) return;
                confirmAscension = false;
                bool ascended = gs.TryAscend(out var message);
                Toast(message, ascended && !gs.saves.IsDirty ? UiKit.Lime : UiKit.Amber);
                RefreshModal();
            });
            confirm.name = "ascension-confirm";
            body.Add(confirm);
            var cancel = IconBtn(UiText.Get("ascension.stay"), "back", UiKit.Cyan, () => { confirmAscension = false; RefreshModal(); });
            cancel.name = "ascension-cancel";
            body.Add(cancel);
        }
    }
}
