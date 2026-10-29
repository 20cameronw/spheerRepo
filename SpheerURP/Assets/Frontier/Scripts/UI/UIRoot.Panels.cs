using System;
using System.Collections.Generic;
using Frontier.Economy;
using Frontier.Services;
using UnityEngine;
using UnityEngine.UIElements;
using UBiome = Frontier.Universe.Biome;
using USize = Frontier.Universe.SizeClass;

namespace Frontier
{
    public partial class UIRoot
    {
        VisualElement modal;
        ScrollView modalScroll;
        Action<VisualElement> modalBuild;
        string modalKey;
        enum ResearchPage { Tech, Improvements }
        enum ShipPage { Cargo, Tools, Systems }
        ResearchPage researchPage;
        ShipPage shipPage;
        PlacedStructure confirmBuilderFinish;
        int confirmBuilderPurchaseOwned = -1;

        // ------------------------------------------------------------ modal core
        void OpenModal(string key, string icon, string title, Color accent, Action<VisualElement> build, bool wide = true)
        {
            if (modalKey == key) { CloseModal(); return; }
            ClosePopup();
            CloseModal();
            CloseMap();
            ExitBuild();
            modalKey = key; modalBuild = build;
            Color c = UiKit.Ui(accent);

            var backdropEl = UiKit.Div("fr-backdrop");
            backdropEl.pickingMode = PickingMode.Position;
            backdropEl.RegisterCallback<PointerDownEvent>(e => { if (e.target == backdropEl) CloseModal(); });
            modal = backdropEl;

            var panel = new FrontierModal { Wide = wide };
            UiKit.SetAccent(panel, c);
            var head = UiKit.Div("fr-modal-head");
            head.Add(Icons.Make(icon, c));
            head.Add(UiKit.Text(title, "fr-modal-title"));
            head.Add(Icons.IconButton("close", UiKit.Muted, CloseModal));
            panel.Add(head);

            modalScroll = UiKit.VerticalScroll("fr-modal-scroll");
            panel.Add(modalScroll);
            backdropEl.Add(panel);
            modalLayer.Add(backdropEl);
            if (scrimLayer != null) scrimLayer.style.display = DisplayStyle.Flex;
            build(modalScroll.contentContainer);
        }

        void CloseModal()
        {
            accountDeletionPlayerId = null;
            confirmAscension = false;
            confirmBuilderFinish = null;
            confirmBuilderPurchaseOwned = -1;
            EndInventoryDrag();
            if (scrimLayer != null) scrimLayer.style.display = DisplayStyle.None;
            if (modalKey == "warehouse")
            {
                planetView.Frozen = false;
                world.Selected = null;
            }
            if (modal != null) modal.RemoveFromHierarchy();
            modal = null; modalScroll = null; modalBuild = null; modalKey = null;
        }

        void RefreshModal()
        {
            if (modal == null || modalBuild == null || modalScroll == null) return;
            var offset = modalScroll.scrollOffset;
            EndInventoryDrag();
            modalScroll.contentContainer.Clear();
            modalBuild(modalScroll.contentContainer);
            var scroll = modalScroll;
            scroll.schedule.Execute(() => scroll.scrollOffset = offset).StartingIn(1);
        }

        void OpenShop() { if (defense.Active || busy) return; OpenModal("shop", "build", UiText.Get("nav.build"), UiKit.Lime, BuildShop, true); }
        void OpenResearch() { OpenResearch(ResearchPage.Tech); }
        void OpenResearch(ResearchPage page)
        {
            if (defense.Active || busy) return;
            bool samePage = researchPage == page;
            researchPage = page;
            if (modalKey == "research" && !samePage) { RefreshModal(); return; }
            OpenModal("research", "research", UiText.Get("nav.command"), UiKit.Magenta, BuildResearch, true);
        }
        void OpenBuilders() { if (defense.Active || busy) return; OpenModal("builders", "build", UiText.Get("builders.jobs-title"), UiKit.Cyan, BuildBuilders, true); }
        void OpenPack() { OpenShip(ShipPage.Cargo); }
        void OpenShip(ShipPage page)
        {
            if (defense.Active || busy) return;
            bool samePage = shipPage == page;
            shipPage = page;
            if (modalKey == "pack" && !samePage) { RefreshModal(); return; }
            OpenModal("pack", "ship", UiText.Get("nav.backpack"), UiKit.Amber, BuildShip, true);
        }

        // ------------------------------------------------------------ build shop
        int shopTab;

        VisualElement TabButton(string icon, string label, bool on, Color accent, Action onClick)
        {
            var t = new FrontierTab { Accent = accent, Selected = on, Icon = icon, Caption = label };
            t.clicked += () => { confirmBuilderFinish = null; confirmBuilderPurchaseOwned = -1; onClick(); RefreshModal(); };
            return t;
        }

        void BuildShop(VisualElement body)
        {
            var tabs = UiKit.Div("fr-tabs");
            tabs.Add(TabButton("refinery", UiText.Get("shop.economy"), shopTab == 0, UiKit.Lime, () => shopTab = 0));
            tabs.Add(TabButton("mine", UiText.Get("shop.gather"), shopTab == 1, UiKit.Lime, () => shopTab = 1));
            tabs.Add(TabButton("shield", UiText.Get("shop.defense"), shopTab == 2, UiKit.Lime, () => shopTab = 2));
            tabs.Add(TabButton("research", UiText.Get("shop.utility"), shopTab == 3, UiKit.Lime, () => shopTab = 3));
            body.Add(tabs);
            var note = UiKit.Text(UiText.Get("shop.summary", gs.planet.Name, gs.FreePads(gs.planet),
                gs.planet.BuildingSlots, gs.BusyBuilderCount, gs.BuilderCount), "fr-muted");
            note.style.marginBottom = UiKit.Space.S12;
            body.Add(note);
            switch (shopTab)
            {
                case 0: ShopSection(body, StructureKind.Refinery, StructureKind.Fabricator, StructureKind.Foundry,
                    StructureKind.ChemReactor, StructureKind.QuantumLab, StructureKind.Alarm, StructureKind.Warehouse,
                    StructureKind.FuelDepot); break;
                case 1: ShopSection(body, StructureKind.Lumber, StructureKind.Mine, StructureKind.Drill,
                    StructureKind.SiphonRig, StructureKind.Excavator, StructureKind.SalvageYard); break;
                case 2: ShopSection(body, StructureKind.Gunner, StructureKind.Laser); break;
                default: ShopSection(body, StructureKind.ResearchLab, StructureKind.RadarTower, StructureKind.ColonyHub,
                    StructureKind.Satellite, StructureKind.Terraformer, StructureKind.WarpGate, StructureKind.DysonCollector); break;
            }
        }

        void ShopSection(VisualElement body, params StructureKind[] kinds)
        {
            var list = new List<StructureSO>();
            foreach (var sd in gs.db.structures)
                if (sd != null && Array.IndexOf(kinds, sd.kind) >= 0) list.Add(sd);
            if (list.Count == 0) { body.Add(UiKit.Text(UiText.Get("shop.empty"), "fr-muted")); return; }
            int guideIndex = list.FindIndex(def => IsFirstSessionBuildTarget(def.kind));
            if (guideIndex > 0)
            {
                var next = list[guideIndex];
                list.RemoveAt(guideIndex);
                list.Insert(0, next);
            }
            foreach (var sd in list)
            {
                var def = sd;
                Color c = UiKit.Ui(def.color);
                var card = Card(c);
                card.name = "shop-structure-" + def.id;
                var top = UiKit.Div("fr-card-head");
                top.Add(Icons.Make(StructureIcon(def.kind), c, UiKit.Size.IconXl));
                var col = UiKit.Div("fr-card-text"); col.pickingMode = PickingMode.Ignore;
                col.Add(UiKit.Text(def.LocalizedName.ToUpper(), "fr-h2"));
                col.Add(UiKit.Text(def.LocalizedDescription, "fr-tiny"));
                top.Add(col);
                int have = gs.CountOnPlanet(gs.planet, def);
                var count = UiKit.Text(def.maxPerPlanet > 0 ? have + "/" + def.maxPerPlanet : "x" + have, "fr-small");
                count.style.marginLeft = UiKit.Space.S12;
                top.Add(count);
                card.Add(top);

                PlacedStructure activeJob = null;
                foreach (var structure in gs.StructuresOn(gs.planet))
                    if (structure.def == def && gs.IsBuilderBusy(structure)) { activeJob = structure; break; }
                if (activeJob != null)
                {
                    card.Add(Note(gs.BuilderJobStatus(activeJob), UiKit.Cyan));
                    BuildBuilderFinishAction(card, activeJob, RefreshModal);
                }
                else if (gs.AtLimit(gs.planet, def)) card.Add(Note(UiText.Get("shop.built"), UiKit.Lime));
                else if (gs.BuildLockReason(def) != null) card.Add(Note(gs.BuildLockReason(def), UiKit.Amber));
                else if (!gs.CanBuildHere(def)) card.Add(Note(UiText.Get("shop.no-harvest"), UiKit.Amber));
                else
                {
                    card.Add(ProjectCosts(def.buildCost, Project("build", def.id)));
                    AddProjectAction(card, Project("build", def.id));
                    bool crewAvailable = gs.StructureJobSeconds(def, 1) <= 0 || gs.AvailableBuilderCount > 0;
                    if (!crewAvailable) card.Add(Note(gs.BuilderAvailabilityReason(), UiKit.Amber));
                    bool can = gs.HasPlanetResources(def.buildCost) && crewAvailable;
                    var place = IconBtn(UiText.Get("shop.place"), "build", UiKit.Lime, () => { CloseModal(); EnterBuild(def); });
                    place.name = "shop-place-" + def.id;
                    UiKit.SetEnabledLook(place, can && !gs.SaveIsReadOnly && gs.FreePads(gs.planet) > 0);
                    var actions = UiKit.Div("fr-row");
                    place.style.flexGrow = 1;
                    place.style.flexBasis = 0;
                    actions.Add(place);
                    int shardCost = gs.StructureShardCost(def);
                    var buy = IconBtn(UiText.Get("shop.shards.price", shardCost), "crystal", UiKit.Magenta,
                        () => { CloseModal(); EnterBuild(def, shardCost); });
                    buy.name = "shop-shards-" + def.id;
                    buy.tooltip = UiText.Get("shop.shards.help", shardCost);
                    buy.style.marginLeft = UiKit.Space.S8;
                    buy.style.flexShrink = 0;
                    UiKit.SetEnabledLook(buy, !gs.SaveIsReadOnly && shardCost >= 0 &&
                        gs.ShardBalance >= shardCost && gs.FreePads(gs.planet) > 0);
                    actions.Add(buy);
                    if (can && IsFirstSessionBuildTarget(def.kind))
                        AddFirstSessionCue(card, CurrentFirstSessionGuideStep);
                    card.Add(actions);
                }
                body.Add(card);
            }
        }

        /// <summary>A short status line inside a card (built, locked, maxed).</summary>
        static Label Note(string text, Color color)
        {
            var l = UiKit.Text(text, "fr-small", color);
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.flexShrink = 1f;
            l.style.minWidth = 0f;
            l.style.marginTop = UiKit.Space.S8;
            return l;
        }

        // ------------------------------------------------------------ menu (leaderboard, tutorial, about, settings)
        void OpenMenu() { if (defense.Active || busy) return; menuPage = ""; OpenModal("menu", "menu", UiText.Get("nav.menu"), UiKit.Cyan, BuildMenu, true); }

        string menuPage = "";

        /// <summary>The DEBUG tile and its cheat tools exist only in the editor and development builds.</summary>
        public static bool DebugToolsAvailable
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (debugToolsOverride.HasValue) return debugToolsOverride.Value;
#endif
                return Debug.isDebugBuild || Application.isEditor;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static bool? debugToolsOverride;
#endif

        void BuildMenu(VisualElement body)
        {
            if (menuPage != "settings") accountDeletionPlayerId = null;
            if (menuPage != "ascension") confirmAscension = false;
            if (menuPage == "debug" && !DebugToolsAvailable) menuPage = "";
            if (menuPage != "")
            {
                var back = IconBtn(UiText.Get("common.back"), "back", UiKit.Muted, () => { menuPage = ""; RefreshModal(); });
                back.AddToClassList("fr-btn-ghost");
                back.AddToClassList("fr-back");
                body.Add(back);
                switch (menuPage)
                {
                    case "debug": BuildDebug(body); break;
                    case "ranks": BuildLeaderboard(body); break;
                    case "leaderboard": BuildOnlineLeaderboard(body); break;
                    case "shards": BuildShardWallet(body); break;
                    case "goals": BuildGoals(body); break;
                    case "ascension": BuildAscension(body); break;
                    case "tutorial": BuildTutorial(body); break;
                    case "about": BuildAbout(body); break;
                    case "atlas": BuildAtlas(body); break;
                    default: BuildSettings(body); break;
                }
                return;
            }
            var grid = UiKit.Div("fr-menu-grid");
            grid.Add(MenuTile("star", UiText.Get("menu.records"), UiKit.Lime, "ranks"));
            grid.Add(MenuTile("ranks", UiText.Get("menu.leaderboard"), UiKit.Cyan, "leaderboard"));
            grid.Add(MenuTile("crystal", UiText.Get("menu.shards"), UiKit.Magenta, "shards"));
            grid.Add(MenuTile("star", UiText.Get("menu.goals"), UiKit.Amber, "goals"));
            if (gs.AscensionAvailable) grid.Add(MenuTile("play", UiText.Get("menu.ascension"), UiKit.Magenta, "ascension"));
            grid.Add(MenuTile("map", UiText.Get("menu.atlas"), UiKit.Cyan, "atlas"));
            grid.Add(MenuTile("tutorial", UiText.Get("menu.tutorial"), UiKit.Cyan, "tutorial"));
            grid.Add(MenuTile("about", UiText.Get("menu.about"), UiKit.Magenta, "about"));
            grid.Add(MenuTile("gear", UiText.Get("menu.settings"), UiKit.Cyan, "settings"));
            if (DebugToolsAvailable) grid.Add(MenuTile("tools", UiText.Get("menu.debug"), UiKit.Red, "debug"));
            body.Add(grid);
            body.Add(IconBtn(UiText.Get("project.title"), "star", UiKit.Cyan, OpenProjectPanel));
        }

        VisualElement MenuTile(string icon, string label, Color color, string page)
        {
            Color c = UiKit.Ui(color);
            var b = new Button();
            b.name = "menu-" + page;
            b.AddToClassList("fr-menu-tile");
            UiKit.SetAccent(b, c);
            b.Add(Icons.Make(icon, c));
            b.Add(UiKit.Text(label, "fr-menu-tile-label"));
            b.clicked += () =>
            {
                Sfx.Click();
                menuPage = page;
                RefreshModal();
                if (page == "leaderboard" && services != null && services.Consent.Current.hasDecision &&
                    services.Consent.Current.onlineServicesAllowed)
                    LoadOnlineLeaderboard();
            };
            return b;
        }

        void StatRow(VisualElement body, string icon, Color color, string label, string value)
        {
            body.Add(new FrontierStatRow { Tint = color, Icon = icon, Caption = label, Value = value });
        }

        void BuildLeaderboard(VisualElement body)
        {
            BuildEraProgress(body);
            if (gs.AscensionAvailable)
                body.Add(IconBtn(UiText.Get("ranks.ascension"), "play", UiKit.Magenta, () => { menuPage = "ascension"; RefreshModal(); }));
            body.Add(IconBtn(UiText.Get("ranks.goals"), "star", UiKit.Amber, () => { menuPage = "goals"; RefreshModal(); }));
            ScoreBreakdown score = gs.FrontierScore;
            var scoreCard = Card(UiKit.Lime);
            scoreCard.Add(UiKit.Text(UiText.Get("ranks.score"), "fr-h2"));
            StatRow(scoreCard, "star", UiKit.Lime, UiText.Get("ranks.total"), score.total.ToString("N0"));
            StatRow(scoreCard, "map", UiKit.Cyan, UiText.Get("ranks.reach"), score.reach.ToString("N0"));
            StatRow(scoreCard, "map", UiKit.Amber, UiText.Get("ranks.worlds"), score.worlds.ToString("N0"));
            StatRow(scoreCard, "base", UiKit.Lime, UiText.Get("ranks.industry"), score.industry.ToString("N0"));
            StatRow(scoreCard, "research", UiKit.Magenta, UiText.Get("ranks.technology"), score.tech.ToString("N0"));
            StatRow(scoreCard, "alien", UiKit.Red, UiText.Get("ranks.defense"), score.defense.ToString("N0"));
            StatRow(scoreCard, "map", UiKit.Cyan, UiText.Get("ranks.atlas"), score.atlas.ToString("N0"));
            if (score.multiplier != 1.0)
                StatRow(scoreCard, "star", UiKit.Magenta, UiText.Get("ranks.multiplier"), score.multiplier.ToString("0.0#"));
            body.Add(scoreCard);

            var card = Card(UiKit.Lime);
            card.Add(UiKit.Text(UiText.Get("ranks.record"), "fr-h2"));
            StatRow(card, "star", UiKit.Magenta, UiText.Get("ranks.level"), gs.PlayerLevel.ToString());
            StatRow(card, "alien", UiKit.Red, UiText.Get("ranks.waves"), gs.totalWavesCleared.ToString());
            StatRow(card, "base", UiKit.Cyan, UiText.Get("ranks.structures"), gs.structures.Count.ToString());
            var planets = new HashSet<string>();
            foreach (var s in gs.structures) planets.Add(s.planetId);
            StatRow(card, "map", UiKit.Amber, UiText.Get("ranks.bases"), planets.Count.ToString());
            body.Add(card);
        }

        void BuildShardWallet(VisualElement body)
        {
            var balance = Card(UiKit.Magenta);
            balance.Add(UiKit.Text(UiText.Get("shards.title"), "fr-h2"));
            balance.Add(UiKit.Text(UiText.Get("shards.balance", gs.ShardBalance.ToString("N0")), "fr-h1", UiKit.Magenta));
            balance.Add(UiKit.Text(UiText.Get("shards.uses-help"), "fr-muted"));
            body.Add(balance);
            BuildShardAdOffer(body);
            BuildBuilders(body);
        }

        void BuildOnlineLeaderboard(VisualElement body)
        {
            body.Add(SectionLabel(UiText.Get("ranks.online.title")));
            if (services == null || !services.Leaderboards.IsConfigured)
            {
                var setup = UiKit.Text(UiText.Get("ranks.setup-required"), "fr-muted");
                setup.name = "leaderboard-setup-status";
                body.Add(setup);
                return;
            }
            if (!services.Consent.Current.hasDecision || !services.Consent.Current.onlineServicesAllowed)
            {
                body.Add(UiKit.Text(UiText.Get("ranks.enable"), "fr-muted"));
                var enable = IconBtn(UiText.Get("ranks.opt-in"), "ranks", UiKit.Cyan, () =>
                {
                    var current = services.Consent.Current;
                    SaveConsentPreferences(current.analyticsAllowed, current.adsAllowed, true);
                    LoadOnlineLeaderboard();
                });
                enable.name = "leaderboard-enable";
                body.Add(enable);
                return;
            }

            body.Add(UiKit.Text(UiText.Get("ranks.online.help"), "fr-muted"));
            if (!string.IsNullOrEmpty(leaderboardStatus))
                body.Add(UiKit.Text(leaderboardStatus, "fr-muted"));
            if (onlinePlayerEntry.HasValue)
            {
                var entry = onlinePlayerEntry.Value;
                var own = UiKit.Text(UiText.Get("ranks.your-rank", entry.rank, entry.score.ToString("N0")), "fr-muted");
                own.name = "leaderboard-player-rank";
                body.Add(own);
            }

            if (onlineLeaderboardEntries != null)
            {
                BuildLeaderboardPodium(body);
                body.Add(SectionLabel(UiText.Get("ranks.top20")));
                for (int i = 0; i < onlineLeaderboardEntries.Count; i++)
                {
                    LeaderboardEntry entry = onlineLeaderboardEntries[i];
                    if (entry.rank <= 3) continue;
                    var row = UiKit.Div("fr-stat-row");
                    row.Add(UiKit.Text(UiText.Get("ranks.entry", entry.rank, entry.usesFallbackName
                        ? UiText.Get("service.leaderboard.unknown-pilot") : entry.displayName), "fr-small"));
                    row.Add(UiKit.Div("fr-spacer"));
                    row.Add(UiKit.Text(entry.score.ToString("N0"), "fr-small"));
                    body.Add(row);
                }
                if (onlineLeaderboardEntries.Count == 0)
                    body.Add(UiKit.Text(UiText.Get("ranks.empty"), "fr-muted"));
            }

            if (leaderboardFailure.HasValue)
            {
                var retry = IconBtn(UiText.Get("ranks.retry"), "ranks", UiKit.Cyan, LoadOnlineLeaderboard);
                retry.name = "leaderboard-retry";
                UiKit.SetEnabledLook(retry, !leaderboardLoading);
                body.Add(retry);
            }
        }

        void BuildLeaderboardPodium(VisualElement body)
        {
            if (onlineLeaderboardEntries == null || onlineLeaderboardEntries.Count == 0) return;
            body.Add(SectionLabel(UiText.Get("ranks.podium.title")));
            var podium = UiKit.Div("fr-row");
            podium.name = "leaderboard-podium";
            podium.style.alignItems = Align.FlexEnd;
            podium.style.justifyContent = Justify.Center;
            int[] order = { 1, 0, 2 };
            foreach (int index in order)
            {
                if (index >= onlineLeaderboardEntries.Count) continue;
                LeaderboardEntry entry = onlineLeaderboardEntries[index];
                Color accent = entry.rank == 1 ? UiKit.Amber : UiKit.Cyan;
                var card = Card(accent);
                card.style.flexGrow = 1f;
                card.style.minWidth = 0f;
                card.style.marginLeft = UiKit.Space.S4;
                card.style.marginRight = UiKit.Space.S4;
                card.style.minHeight = entry.rank == 1 ? 112f : 88f;
                card.Add(UiKit.Text(UiText.Get("ranks.podium.place", entry.rank), "fr-small", accent));
                card.Add(UiKit.Text(entry.usesFallbackName ? UiText.Get("service.leaderboard.unknown-pilot") : entry.displayName, "fr-tiny"));
                card.Add(UiKit.Text(entry.score.ToString("N0"), "fr-h2"));
                podium.Add(card);
            }
            body.Add(podium);
        }

        void BuildEraProgress(VisualElement body)
        {
            var era = gs.Era;
            var card = Card(UiKit.Cyan);
            card.name = "era-progress";
            card.Add(UiKit.Text(UiText.Get("ranks.era", ContentText.EraName(era).ToUpperInvariant()), "fr-h2"));
            card.Add(UiKit.Text(UiText.Get("ranks.era.help"), "fr-tiny"));
            if (era == FrontierEra.Ascension)
            {
                card.Add(UiKit.Text(UiText.Get("ranks.era.mastery"), "fr-muted"));
                card.Add(IconBtn(UiText.Get("guide.action.atlas"), "research", UiKit.Cyan, OpenGuideAtlas));
            }
            else
            {
                card.Add(UiKit.Text(UiText.Get("ranks.era.next", ContentText.EraName(era + 1).ToUpperInvariant()), "fr-small"));
                foreach (var requirement in EraRules.NextRequirements(era, gs.EraProgress))
                {
                    var row = UiKit.Div("fr-row");
                    var requirementIcon = Icons.Make(requirement.Complete ? "check" : "lock",
                        requirement.Complete ? UiKit.Lime : UiKit.Muted, UiKit.Size.IconSm);
                    requirementIcon.style.marginRight = UiKit.Space.S12;
                    row.Add(requirementIcon);
                    row.Add(UiKit.Text(ContentText.Get("era-requirement", requirement.Id, "label", requirement.Label), "fr-small"));
                    card.Add(row);
                }
                Action action;
                string label, icon;
                if (era == FrontierEra.Landfall) { action = OpenMap; label = UiText.Get("guide.action.map"); icon = "map"; }
                else if (era == FrontierEra.Interstellar) { action = OpenShop; label = UiText.Get("guide.action.build-shop"); icon = "build"; }
                else
                {
                    action = OpenResearch;
                    label = UiText.Get("ranks.era.command");
                    icon = "research";
                }
                var next = IconBtn(label, icon, UiKit.Cyan, action);
                next.name = "era-next-action";
                card.Add(next);
            }
            body.Add(card);
        }

        IReadOnlyList<LeaderboardEntry> onlineLeaderboardEntries;
        LeaderboardEntry? onlinePlayerEntry;
        string leaderboardStatusValue;
        ServiceMessage? leaderboardFailure;
        ServiceStatus leaderboardFailureStatus;
        string leaderboardStatus
        {
            get { return leaderboardFailure.HasValue
                ? LeaderboardError(leaderboardFailureStatus, leaderboardFailure.Value.Resolve(UiText.Get)) : leaderboardStatusValue; }
            set { leaderboardStatusValue = value; leaderboardFailure = null; }
        }
        bool leaderboardLoading;

        async void LoadOnlineLeaderboard()
        {
            if (leaderboardLoading || services == null || services.AccountDeletionBusy ||
                !services.Consent.Current.hasDecision || !services.Consent.Current.onlineServicesAllowed) return;
            var activeServices = services;
            long score = gs.FrontierScore.total;
            leaderboardLoading = true;
            onlineLeaderboardEntries = null;
            onlinePlayerEntry = null;
            leaderboardStatus = UiText.Get("ranks.connecting");
            RefreshModal();
            bool withdrawn = false;
            void Observe(ConsentPreferences preferences)
            {
                if (!preferences.hasDecision || !preferences.onlineServicesAllowed) withdrawn = true;
            }
            activeServices.Consent.PreferencesChanged += Observe;
            void Fail(ServiceStatus status, ServiceMessage text)
            {
                leaderboardStatus = LeaderboardError(status, text.Resolve(UiText.Get));
                leaderboardFailure = text;
                leaderboardFailureStatus = status;
            }
            try
            {
                var signIn = await activeServices.Authentication.SignInAnonymouslyAsync();
                if (this == null || services != activeServices || withdrawn) return;
                if (!signIn.succeeded) { Fail(signIn.status, signIn.text); return; }
                if (activeServices.Authentication.CurrentPlayerId != signIn.value.playerId)
                {
                    Fail(ServiceStatus.InvalidRequest, ServiceMessage.Get("service.deletion.review"));
                    return;
                }
                var submitted = await activeServices.Leaderboards.SubmitScoreAsync(activeServices.LeaderboardId, score);
                if (this == null || services != activeServices || withdrawn) return;
                if (!submitted.succeeded) { Fail(submitted.status, submitted.text); return; }
                if (activeServices.Authentication.CurrentPlayerId != signIn.value.playerId)
                {
                    Fail(ServiceStatus.InvalidRequest, ServiceMessage.Get("service.deletion.review"));
                    return;
                }
                onlinePlayerEntry = submitted.value;
                var scores = await activeServices.Leaderboards.GetScoresAsync(activeServices.LeaderboardId, 20);
                if (this == null || services != activeServices || withdrawn) return;
                if (activeServices.Authentication.CurrentPlayerId != signIn.value.playerId)
                {
                    onlinePlayerEntry = null;
                    Fail(ServiceStatus.InvalidRequest, ServiceMessage.Get("service.deletion.review"));
                    return;
                }
                if (!scores.succeeded) { Fail(scores.status, scores.text); return; }
                onlineLeaderboardEntries = scores.value;
                leaderboardStatus = UiText.Get("ranks.connected");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Fail(ServiceStatus.Failed, ServiceMessage.Literal(exception.Message));
            }
            finally
            {
                activeServices.Consent.PreferencesChanged -= Observe;
                leaderboardLoading = false;
                if (withdrawn) { onlineLeaderboardEntries = null; onlinePlayerEntry = null; leaderboardStatus = null; }
                if (this != null && menuPage == "leaderboard") RefreshModal();
            }
        }

        static string LeaderboardError(ServiceStatus status, string details)
        {
            switch (status)
            {
                case ServiceStatus.NotConfigured:
                    return UiText.Get("ranks.error.not-configured");
                case ServiceStatus.ConsentRequired:
                    return UiText.Get("ranks.error.consent");
                case ServiceStatus.InvalidRequest:
                    return UiText.Get("ranks.error.request", details);
                case ServiceStatus.Failed:
                    return UiText.Get("ranks.error.failed", details);
                default:
                    return details;
            }
        }

        void BuildTutorial(VisualElement body)
        {
            TutorialStep(body, "build", UiKit.Lime, UiText.Get("tutorial.build.title"), UiText.Get("tutorial.build.body"));
            TutorialStep(body, "wood", UiKit.Amber, UiText.Get("tutorial.gather.title"), UiText.Get("tutorial.gather.body"));
            TutorialStep(body, "pack", UiKit.Cyan, UiText.Get("tutorial.storage.title"), UiText.Get("tutorial.storage.body"));
            TutorialStep(body, "fuel", UiKit.Amber, UiText.Get("tutorial.refine.title"), UiText.Get("tutorial.refine.body"));
            TutorialStep(body, "alarm", UiKit.Magenta, UiText.Get("tutorial.defend.title"), UiText.Get("tutorial.defend.body"));
            TutorialStep(body, "research_lab", UiKit.Magenta, UiText.Get("tutorial.research.title"), UiText.Get("tutorial.research.body"));
            TutorialStep(body, "research", UiKit.Magenta, UiText.Get("tutorial.upgrade.title"), UiText.Get("tutorial.upgrade.body"));
            TutorialStep(body, "map", UiKit.Cyan, UiText.Get("tutorial.travel.title"), UiText.Get("tutorial.travel.body"));
            TutorialStep(body, "base", UiKit.Lime, UiText.Get("tutorial.expand.title"), UiText.Get("tutorial.expand.body"));
            TutorialStep(body, "map", UiKit.Cyan, UiText.Get("tutorial.map.title"), UiText.Get("tutorial.map.body"));
            TutorialStep(body, "warp", UiKit.Cyan, UiText.Get("network.title"), UiText.Get("network.help"));
        }

        void TutorialStep(VisualElement body, string icon, Color color, string title, string text)
        {
            var card = Card(color);
            var row = UiKit.Div("fr-card-head");
            row.style.alignItems = Align.FlexStart;
            row.Add(Icons.Make(icon, UiKit.Ui(color), UiKit.Size.IconLg));
            var col = UiKit.Div("fr-card-text"); col.pickingMode = PickingMode.Ignore;
            col.Add(UiKit.Text(title, "fr-h2"));
            col.Add(UiKit.Text(text, "fr-muted"));
            row.Add(col);
            card.Add(row);
            body.Add(card);
        }

        void BuildAbout(VisualElement body)
        {
            var card = Card(UiKit.Magenta);
            card.Add(UiKit.Text(UiText.Get("about.title"), "fr-title", UiKit.Magenta));
            var blurb = UiKit.Text(UiText.Get("about.body"), "fr-muted");
            blurb.style.marginTop = UiKit.Space.S8;
            card.Add(blurb);
            var version = UiKit.Text(UiText.Get("about.version", Application.version), "fr-tiny");
            version.style.marginTop = UiKit.Space.S8;
            card.Add(version);
            body.Add(card);
        }

        bool confirmReset;

        void BuildSettings(VisualElement body)
        {
            BuildLanguageSettings(body);
            var snd = IconBtn(UiText.Get(Sfx.Muted ? "settings.sound.off" : "settings.sound.on"), Sfx.Muted ? "mute" : "sound", Sfx.Muted ? UiKit.Red : UiKit.Lime, () =>
            {
                Sfx.Muted = !Sfx.Muted;
                PlayerPrefs.SetInt("fr_muted", Sfx.Muted ? 1 : 0);
                RefreshModal();
            });
            snd.style.marginTop = 0;
            body.Add(snd);
            BuildAudioVolume(body, "settings.audio.effects", "settings-effects-volume", Sfx.EffectsVolume,
                value => Sfx.EffectsVolume = value);
            BuildAudioVolume(body, "settings.audio.music", "settings-music-volume", Sfx.MusicVolume,
                value => Sfx.MusicVolume = value);
            var haptics = IconBtn(UiText.Get(HarvestHaptics.Enabled ? "settings.haptics.on" : "settings.haptics.off"), "tools", UiKit.Cyan, () =>
            {
                HarvestHaptics.Enabled = !HarvestHaptics.Enabled;
                RefreshModal();
            });
            haptics.name = "settings-haptics";
            UiKit.SetEnabledLook(haptics, HarvestHaptics.IsSupported);
            body.Add(haptics);
            body.Add(UiKit.Text(UiText.Get(HarvestHaptics.IsSupported
                ? "settings.haptics.help" : "settings.haptics.unavailable"), "fr-tiny"));
            var notifications = IconBtn(UiText.Get(NotificationPreferences.Enabled
                ? "notifications.on" : "notifications.off"), "gear", UiKit.Cyan, () =>
            {
                NotificationPreferences.Enabled = !NotificationPreferences.Enabled;
                RefreshModal();
            });
            notifications.name = "settings-notifications";
            UiKit.SetEnabledLook(notifications, services != null && services.Notifications.IsConfigured);
            body.Add(notifications);
            body.Add(UiKit.Text(UiText.Get(services != null && services.Notifications.IsConfigured
                ? "notifications.help" : "notifications.unavailable"), "fr-muted"));
            if (!string.IsNullOrEmpty(NotificationPreferences.LastError))
                body.Add(UiKit.Text(UiText.Get("notifications.error",
                    NotificationPreferences.LastError), "fr-small", UiKit.Amber));
            var preset = PresentationQuality.Preset;
            var quality = IconBtn(UiText.Get("settings.quality",
                UiText.Get("settings.quality-preset." + preset.ToString().ToLowerInvariant())), "gear", UiKit.Cyan, () =>
            {
                PresentationQuality.Preset = (QualityPreset)(((int)PresentationQuality.Preset + 1) % 4);
                RefreshModal();
            });
            quality.name = "settings-quality";
            body.Add(quality);
            body.Add(UiKit.Text(UiText.Get("settings.quality.help",
                UiText.Get("settings.quality-tier." + PresentationQuality.Budget.Tier.ToString().ToLowerInvariant())), "fr-tiny"));
            var fps = IconBtn(UiText.Get("settings.frame-rate", PresentationQuality.FrameRate == 0
                ? UiText.Get("settings.automatic") : UiText.Get("settings.fps", PresentationQuality.FrameRate)),
                "bolt", UiKit.Cyan, () =>
            {
                int current = PresentationQuality.FrameRate;
                PresentationQuality.FrameRate = current == 0 ? 30 : current == 30 ? 60 : 0;
                RefreshModal();
            });
            fps.name = "settings-frame-rate";
            body.Add(fps);
            body.Add(UiKit.Text(UiText.Get("settings.frame-rate.help", PresentationQuality.TargetFrameRate), "fr-tiny"));
            body.Add(IconBtn(UiText.Get(world.ShowStructureLabels ? "settings.labels.always" : "settings.labels.selected"), "base", UiKit.Cyan, () =>
            {
                world.ShowStructureLabels = !world.ShowStructureLabels;
                PlayerPrefs.SetInt("fr_structure_labels", world.ShowStructureLabels ? 1 : 0);
                PlayerPrefs.Save();
                RefreshModal();
            }));
            BuildTravelSettings(body);
            BuildConsentSettings(body);
            BuildAccountDeletionSettings(body);

            var reset = IconBtn(UiText.Get(confirmReset ? "settings.reset.confirm" : "settings.reset"), "close", UiKit.Red, () =>
            {
                if (!confirmReset) { confirmReset = true; RefreshModal(); return; }
                confirmReset = false;
                if (!gs.TryResetProgress(out string resetMessage))
                {
                    RefreshModal();
                    Toast(resetMessage, UiKit.Red);
                    return;
                }
                CloseModal();
                planetView.Show(gs.planet, true, gs.TerraformerStageOn(gs.planet));
                selected = gs.planet;
            });
            reset.style.marginTop = UiKit.Space.S24;
            reset.name = "settings-reset";
            body.Add(reset);
        }

        void BuildLanguageSettings(VisualElement body)
        {
            body.Add(SectionLabel(UiText.Get("settings.language")));
            var locales = UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales.Locales;
            var names = new List<string>();
            int current = 0;
            for (int i = 0; i < locales.Count; i++)
            {
                names.Add(locales[i].LocaleName);
                if (locales[i].Identifier.Code == UiText.LocaleCode) current = i;
            }
            var language = new DropdownField(names, current) { name = "settings-language" };
            language.AddToClassList("fr-slider");
            language.style.minHeight = UiKit.Size.Touch;
            language.tooltip = UiText.Get("settings.language");
            language.SetEnabled(locales.Count > 1 && !UiText.IsChangingLocale);
            language.RegisterValueChangedCallback(e =>
            {
                int index = names.IndexOf(e.newValue);
                if (index >= 0 && !busy && !defense.Active)
                    StartCoroutine(ChangeLanguage(locales[index].Identifier.Code));
            });
            body.Add(language);
            body.Add(UiKit.Text(UiText.Get(locales.Count > 1
                ? "settings.language.help" : "settings.language.only-english"), "fr-tiny"));
        }

        System.Collections.IEnumerator ChangeLanguage(string code)
        {
            busy = true;
            root.SetEnabled(false);
            string error = null;
            try { yield return UiText.SelectLocale(code, message => error = message); }
            finally
            {
                busy = false;
                root.SetEnabled(true);
            }
            if (error != null)
            {
                RefreshModal();
                Toast(error, UiKit.Red);
                yield break;
            }
            hud.RemoveFromHierarchy();
            BuildHud();
            firstSessionGuideVisibilityKnown = false;
            BuildFirstSessionGuide();
            int waveHudIndex = root.IndexOf(waveHud);
            waveHud.RemoveFromHierarchy();
            BuildWaveHud();
            root.Insert(waveHudIndex, waveHud);
            if (modal != null)
                modal.Q<Label>(className: "fr-modal-title").text = UiText.Get("nav.menu");
            world.RefreshLocalizedLabels();
            ApplyLayout(true);
            OnStateChanged();
        }

        void BuildAudioVolume(VisualElement body, string labelKey, string name, float initial, Action<float> changed)
        {
            body.Add(SectionLabel(UiText.Get(labelKey)));
            var row = UiKit.Div("fr-row");
            var slider = new Slider(0f, 1f) { name = name, value = initial };
            slider.AddToClassList("fr-slider");
            slider.AddToClassList("fr-audio-slider");
            slider.tooltip = UiText.Get(labelKey);
            slider.style.minHeight = UiKit.Size.Touch;
            slider.style.minWidth = 0;
            var value = UiKit.Text(UiText.Get("settings.audio.percent", Mathf.RoundToInt(initial * 100f)), "fr-small");
            value.style.minWidth = 100;
            value.style.unityTextAlign = TextAnchor.MiddleRight;
            slider.RegisterValueChangedCallback(e =>
            {
                changed(e.newValue);
                value.text = UiText.Get("settings.audio.percent", Mathf.RoundToInt(e.newValue * 100f));
            });
            row.Add(slider);
            row.Add(value);
            body.Add(row);
        }

        void BuildConsentSettings(VisualElement body)
        {
            if (services == null || services.Consent == null) return;
            ConsentPreferences saved = services.Consent.Current;
            bool analyticsAllowed = saved.hasDecision && saved.analyticsAllowed;
            bool adsAllowed = saved.hasDecision && saved.adsAllowed;
            bool onlineServicesAllowed = saved.hasDecision && saved.onlineServicesAllowed;

            body.Add(SectionLabel(UiText.Get("privacy.title")));
            body.Add(UiKit.Text(UiText.Get("privacy.help"), "fr-muted"));
            if (!string.IsNullOrEmpty(services.PrivacyPolicyUrl))
                body.Add(IconBtn(UiText.Get("privacy.policy"), "about", UiKit.Cyan,
                    () => Application.OpenURL(services.PrivacyPolicyUrl)));

            ConsentChoice(body, UiText.Get("privacy.analytics"), analyticsAllowed, value =>
            {
                analyticsAllowed = value;
                SaveConsentPreferences(analyticsAllowed, adsAllowed, onlineServicesAllowed);
            });
            ConsentChoice(body, UiText.Get("privacy.ads"), adsAllowed, value =>
            {
                adsAllowed = value;
                SaveConsentPreferences(analyticsAllowed, adsAllowed, onlineServicesAllowed);
            });
            ConsentChoice(body, UiText.Get("privacy.online"), onlineServicesAllowed, value =>
            {
                onlineServicesAllowed = value;
                SaveConsentPreferences(analyticsAllowed, adsAllowed, onlineServicesAllowed);
            });

            BuildAccountRecoverySettings(body, onlineServicesAllowed);
        }

        bool accountRecoveryBusy;
        string accountDeletionPlayerId, accountDeletionStatusValue;
        ServiceMessage? accountDeletionFailure;
        string accountDeletionStatus
        {
            get { return accountDeletionFailure.HasValue
                ? accountDeletionFailure.Value.Resolve(UiText.Get) : accountDeletionStatusValue; }
            set { accountDeletionStatusValue = value; accountDeletionFailure = null; }
        }
        bool accountDeletionIsError;

        void BuildAccountDeletionSettings(VisualElement body)
        {
            body.Add(SectionLabel(UiText.Get("deletion.title")));
            body.Add(UiKit.Text(UiText.Get("deletion.help"), "fr-muted"));
            if (!string.IsNullOrEmpty(accountDeletionStatus))
                body.Add(UiKit.Text(accountDeletionStatus, "fr-small", accountDeletionIsError ? UiKit.Amber : UiKit.Cyan));
            if (services == null || !services.Authentication.IsConfigured)
            {
                body.Add(UiKit.Text(UiText.Get("deletion.unavailable"), "fr-small"));
                return;
            }
            string playerId = services.Authentication.CurrentPlayerId;
            if (string.IsNullOrEmpty(playerId))
            {
                accountDeletionPlayerId = null;
                body.Add(UiKit.Text(UiText.Get("deletion.sign-in"), "fr-small"));
                return;
            }
            if (accountDeletionPlayerId != playerId) accountDeletionPlayerId = null;
            bool confirming = accountDeletionPlayerId != null;
            var remove = IconBtn(UiText.Get(services.AccountDeletionBusy ? "deletion.busy"
                : confirming ? "deletion.confirm" : "deletion.action"), "close", UiKit.Red, RequestAccountDeletion);
            remove.name = "settings-delete-account";
            UiKit.SetEnabledLook(remove, !accountRecoveryBusy && !cloudSaveBusy && !leaderboardLoading && !services.AccountDeletionBusy);
            body.Add(remove);
            if (confirming)
            {
                body.Add(UiKit.Text(UiText.Get("deletion.warning"), "fr-small", UiKit.Amber));
                var cancel = UiKit.Btn(UiText.Get("deletion.cancel"), UiKit.Muted, () =>
                {
                    accountDeletionPlayerId = null;
                    RefreshModal();
                });
                cancel.name = "settings-delete-account-cancel";
                body.Add(cancel);
            }
        }

        async void RequestAccountDeletion()
        {
            if (services == null || accountRecoveryBusy || cloudSaveBusy || leaderboardLoading || services.AccountDeletionBusy) return;
            string playerId = services.Authentication.CurrentPlayerId;
            if (string.IsNullOrEmpty(playerId)) { accountDeletionPlayerId = null; RefreshModal(); return; }
            if (accountDeletionPlayerId != playerId)
            {
                accountDeletionPlayerId = playerId;
                RefreshModal();
                return;
            }
            accountDeletionPlayerId = null;
            accountRecoveryBusy = true;
            cloudSaveBusy = true;
            accountDeletionStatus = UiText.Get("deletion.busy");
            accountDeletionIsError = false;
            RefreshModal();
            ServiceResult result = await services.DeleteCurrentAccountAndSaveAsync(playerId);
            if (this == null) return;
            accountRecoveryBusy = false;
            cloudSaveBusy = false;
            accountDeletionIsError = !result.succeeded;
            accountDeletionStatus = result.succeeded ? UiText.Get("deletion.done") : result.text.Resolve(UiText.Get);
            if (!result.succeeded) accountDeletionFailure = result.text;
            cloudSaveChecked = false;
            cloudBackupConfirm = cloudRestoreConfirm = localRestoreConfirm = false;
            cloudSnapshot = localCloudSnapshot = null;
            cloudRecord = default;
            cloudSaveStatus = accountRecoveryStatus = null;
            onlineLeaderboardEntries = null;
            onlinePlayerEntry = null;
            leaderboardStatus = null;
            RefreshModal();
        }

        string accountRecoveryStatusValue;
        ServiceMessage? accountRecoveryFailure;
        string accountRecoveryStatus
        {
            get { return accountRecoveryFailure.HasValue
                ? accountRecoveryFailure.Value.Resolve(UiText.Get) : accountRecoveryStatusValue; }
            set { accountRecoveryStatusValue = value; accountRecoveryFailure = null; }
        }
        bool cloudSaveBusy;
        bool cloudSaveChecked;
        bool cloudBackupConfirm;
        bool cloudRestoreConfirm;
        bool localRestoreConfirm;
        bool cloudSaveStatusIsError;
        string cloudSaveStatusValue;
        ServiceMessage? cloudSaveFailure;
        string cloudSaveStatus
        {
            get { return cloudSaveFailure.HasValue ? cloudSaveFailure.Value.Resolve(UiText.Get) : cloudSaveStatusValue; }
            set { cloudSaveStatusValue = value; cloudSaveFailure = null; }
        }
        CloudSaveRecord cloudRecord;
        SaveV2 localCloudSnapshot;
        SaveV2 cloudSnapshot;

        void BuildAccountRecoverySettings(VisualElement body, bool onlineServicesAllowed)
        {
            body.Add(SectionLabel(UiText.Get("recovery.title")));
            body.Add(UiKit.Text(UiText.Get("recovery.help"), "fr-muted"));

            if (!onlineServicesAllowed)
            {
                body.Add(UiKit.Text(UiText.Get("recovery.enable"), "fr-small"));
                if (!string.IsNullOrEmpty(cloudSaveStatus))
                    body.Add(UiKit.Text(cloudSaveStatus, "fr-small",
                        cloudSaveStatusIsError ? UiKit.Amber : UiKit.Cyan));
                BuildLocalRestoreBackupSettings(body);
                return;
            }

            if (services.Authentication.CanRecoverAccount)
            {
                body.Add(UiKit.Text(UiText.Get("recovery.provider", services.Authentication.RecoveryProviderName), "fr-small"));
                if (!string.IsNullOrEmpty(accountRecoveryStatus))
                    body.Add(UiKit.Text(accountRecoveryStatus, "fr-small"));

                var protect = IconBtn(UiText.Get(accountRecoveryBusy ? "recovery.connecting" : "recovery.protect"),
                    "about", UiKit.Cyan, ProtectCurrentSave);
                UiKit.SetEnabledLook(protect, !accountRecoveryBusy);
                body.Add(protect);

                var recover = IconBtn(UiText.Get(accountRecoveryBusy ? "recovery.connecting" : "recovery.sign-in"),
                    "about", UiKit.Magenta, SignInToRecoverSave);
                UiKit.SetEnabledLook(recover, !accountRecoveryBusy);
                body.Add(recover);
            }
            else body.Add(UiKit.Text(UiText.Get("recovery.unavailable"), "fr-small"));

            BuildCloudSaveSettings(body);
        }

        async void ProtectCurrentSave()
        {
            if (accountRecoveryBusy || services == null) return;
            accountRecoveryBusy = true;
            accountRecoveryStatus = UiText.Get("recovery.preparing");
            RefreshModal();

            ServiceResult<AuthIdentity> current = await services.Authentication.SignInAnonymouslyAsync();
            if (this == null) return;
            if (!current.succeeded)
            {
                FinishAccountRecovery(current.text);
                return;
            }
            if (!current.value.isAnonymous)
            {
                FinishAccountRecovery(UiText.Get("recovery.already-linked"));
                return;
            }

            accountRecoveryStatus = UiText.Get("recovery.link-prompt");
            RefreshModal();
            ServiceResult<AuthIdentity> linked = await services.Authentication.LinkRecoveryAccountAsync();
            if (this == null) return;
            if (linked.succeeded)
                FinishAccountRecovery(UiText.Get("recovery.protected", services.Authentication.RecoveryProviderName));
            else FinishAccountRecovery(linked.text);
        }

        async void SignInToRecoverSave()
        {
            if (accountRecoveryBusy || services == null) return;
            accountRecoveryBusy = true;
            accountRecoveryStatus = UiText.Get("recovery.sign-in-prompt");
            RefreshModal();

            ServiceResult<AuthIdentity> recovered =
                await services.Authentication.SignInWithRecoveryAccountAsync();
            if (this == null) return;
            if (recovered.succeeded)
                FinishAccountRecovery(UiText.Get("recovery.signed-in", services.Authentication.RecoveryProviderName));
            else FinishAccountRecovery(recovered.text);
        }

        void FinishAccountRecovery(string message)
        {
            accountRecoveryStatus = message;
            CompleteAccountRecovery();
        }

        void FinishAccountRecovery(ServiceMessage message)
        {
            accountRecoveryStatus = message.Resolve(UiText.Get);
            accountRecoveryFailure = message;
            CompleteAccountRecovery();
        }

        void CompleteAccountRecovery()
        {
            accountRecoveryBusy = false;
            cloudSaveChecked = false;
            cloudBackupConfirm = false;
            cloudRestoreConfirm = false;
            RefreshModal();
        }

        void BuildCloudSaveSettings(VisualElement body)
        {
            body.Add(SectionLabel(UiText.Get("cloud.title")));
            body.Add(UiKit.Text(UiText.Get("cloud.help"), "fr-muted"));

            if (!string.IsNullOrEmpty(cloudSaveStatus))
                body.Add(UiKit.Text(cloudSaveStatus, "fr-small",
                    cloudSaveStatusIsError ? UiKit.Amber : UiKit.Cyan));

            if (!services.CloudSave.IsConfigured)
            {
                body.Add(UiKit.Text(UiText.Get("cloud.unavailable"), "fr-small"));
                BuildLocalRestoreBackupSettings(body);
                return;
            }

            var compare = IconBtn(UiText.Get(cloudSaveBusy ? "cloud.checking" : "cloud.compare"),
                "map", UiKit.Cyan, CompareCloudSave);
            UiKit.SetEnabledLook(compare, !cloudSaveBusy);
            body.Add(compare);

            if (cloudSaveChecked)
            {
                if (localCloudSnapshot != null)
                    body.Add(UiKit.Text(UiText.Get("cloud.device-summary", CloudSaveSummary(localCloudSnapshot)), "fr-small"));

                if (!cloudRecord.hasValue)
                {
                    body.Add(UiKit.Text(UiText.Get("cloud.empty"), "fr-small"));
                    if (localCloudSnapshot != null)
                    {
                        var create = IconBtn(UiText.Get(cloudSaveBusy ? "cloud.saving" :
                                cloudBackupConfirm ? "cloud.create.confirm" : "cloud.create"),
                            "about", cloudBackupConfirm ? UiKit.Amber : UiKit.Cyan, RequestCloudBackup);
                        UiKit.SetEnabledLook(create, !cloudSaveBusy);
                        body.Add(create);
                    }
                }
                else if (cloudSnapshot == null)
                {
                    body.Add(UiKit.Text(UiText.Get("cloud.invalid"), "fr-small", UiKit.Amber));
                }
                else
                {
                    body.Add(UiKit.Text(UiText.Get("cloud.cloud-summary", CloudSaveSummary(cloudSnapshot)), "fr-small"));
                    string relation = localCloudSnapshot != null &&
                        localCloudSnapshot.saveId == cloudSnapshot.saveId &&
                        localCloudSnapshot.revision == cloudSnapshot.revision
                        ? UiText.Get("cloud.same")
                        : UiText.Get("cloud.different");
                    body.Add(UiKit.Text(relation, "fr-small", UiKit.Amber));

                    bool safeCloudVersion = gs.CanRestoreCloudSaveSnapshot(cloudSnapshot, out string compatibilityMessage);
                    if (localCloudSnapshot != null && safeCloudVersion)
                    {
                        var backup = IconBtn(UiText.Get(cloudBackupConfirm
                                ? "cloud.backup.confirm"
                                : "cloud.backup"),
                            "about", cloudBackupConfirm ? UiKit.Amber : UiKit.Cyan, RequestCloudBackup);
                        UiKit.SetEnabledLook(backup, !cloudSaveBusy);
                        body.Add(backup);
                    }
                    else if (!safeCloudVersion)
                        body.Add(UiKit.Text(compatibilityMessage, "fr-small", UiKit.Amber));

                    if (safeCloudVersion)
                    {
                        var restore = IconBtn(UiText.Get(cloudRestoreConfirm
                                ? "cloud.restore.confirm"
                                : "cloud.restore"),
                            "back", cloudRestoreConfirm ? UiKit.Red : UiKit.Magenta, RequestCloudRestore);
                        UiKit.SetEnabledLook(restore, !cloudSaveBusy);
                        body.Add(restore);
                    }
                }
            }

            BuildLocalRestoreBackupSettings(body);
        }

        void BuildLocalRestoreBackupSettings(VisualElement body)
        {
            if (!gs.HasCloudRestoreBackup) return;
            body.Add(UiKit.Text(UiText.Get("cloud.undo.help"), "fr-small"));
            var undo = IconBtn(UiText.Get(localRestoreConfirm
                    ? "cloud.undo.confirm"
                    : "cloud.undo"),
                "back", localRestoreConfirm ? UiKit.Red : UiKit.Muted, RequestLocalRestore);
            UiKit.SetEnabledLook(undo, !cloudSaveBusy);
            body.Add(undo);
        }

        static string CloudSaveSummary(SaveV2 save)
        {
            return UiText.Get("cloud.snapshot", save.player.xp, save.worlds.Count, save.revision,
                string.IsNullOrEmpty(save.location.currentPlanetId) ? UiText.Get("cloud.world.unknown") : save.location.currentPlanetId);
        }

        async void CompareCloudSave()
        {
            if (cloudSaveBusy || services == null) return;
            cloudSaveBusy = true;
            cloudSaveStatus = UiText.Get("cloud.comparing");
            cloudSaveStatusIsError = false;
            cloudBackupConfirm = false;
            cloudRestoreConfirm = false;
            cloudSaveChecked = false;
            RefreshModal();

            try
            {
                if (!gs.TryCreateCloudSaveSnapshot(out localCloudSnapshot, out string localError))
                {
                    FinishCloudSaveAction(localError, true);
                    return;
                }

                var signedIn = await services.Authentication.SignInAnonymouslyAsync();
                if (this == null) return;
                if (!signedIn.succeeded)
                {
                    FinishCloudSaveAction(signedIn.text);
                    return;
                }
                ServiceResult<CloudSaveRecord> result = await services.CloudSave.ReadAsync();
                if (this == null) return;
                if (!result.succeeded)
                {
                    FinishCloudSaveAction(result.text);
                    return;
                }

                cloudRecord = result.value;
                cloudSaveChecked = true;
                cloudSnapshot = null;
                if (cloudRecord.hasValue && !TryParseCloudSave(cloudRecord.payload, out cloudSnapshot, out string parseError))
                {
                    FinishCloudSaveAction(parseError, true);
                    return;
                }

                FinishCloudSaveAction(cloudRecord.hasValue
                    ? UiText.Get("cloud.compared")
                    : UiText.Get("cloud.compared-empty"), false);
            }
            catch (Exception exception)
            {
                FinishCloudSaveAction(UiText.Get("cloud.compare-failed", exception.Message), true);
            }
        }

        static bool TryParseCloudSave(string payload, out SaveV2 save, out string message)
        {
            save = null;
            message = "";
            try
            {
                if (!JsonShape.IsCompleteObject(payload))
                {
                    message = UiText.Get("cloud.incomplete");
                    return false;
                }
                int schema = SaveSerializer.Instance.ReadSchemaVersion(payload);
                if (schema != SaveSchema.Current)
                {
                    message = UiText.Get("cloud.schema", schema, SaveSchema.Current);
                    return false;
                }
                save = SaveSerializer.Instance.DeserializeV2(payload).Normalize();
                return true;
            }
            catch (Exception exception)
            {
                message = UiText.Get("cloud.validation-failed", exception.Message);
                return false;
            }
        }

        void RequestCloudBackup()
        {
            if (cloudSaveBusy || !cloudSaveChecked || localCloudSnapshot == null) return;
            if (!cloudBackupConfirm)
            {
                cloudBackupConfirm = true;
                cloudSaveStatus = cloudRecord.hasValue
                    ? UiText.Get("cloud.backup.warning")
                    : UiText.Get("cloud.create.warning");
                cloudSaveStatusIsError = false;
                RefreshModal();
                return;
            }
            cloudBackupConfirm = false;
            BackupCurrentSaveToCloud();
        }

        async void BackupCurrentSaveToCloud()
        {
            if (cloudSaveBusy || services == null) return;
            cloudSaveBusy = true;
            cloudSaveStatus = UiText.Get("cloud.backing-up");
            cloudSaveStatusIsError = false;
            RefreshModal();

            try
            {
                if (!gs.TryCreateCloudSaveSnapshot(out localCloudSnapshot, out string localError))
                {
                    FinishCloudSaveAction(localError, true);
                    return;
                }
                ServiceResult<CloudSaveRecord> current = await services.CloudSave.ReadAsync();
                if (this == null) return;
                if (!current.succeeded)
                {
                    FinishCloudSaveAction(current.text);
                    return;
                }
                if (!SameCloudRecord(cloudRecord, current.value))
                {
                    cloudRecord = current.value;
                    cloudSaveChecked = false;
                    cloudSnapshot = null;
                    FinishCloudSaveAction(UiText.Get("cloud.backup.changed"), true);
                    return;
                }

                string writeLock = current.value.hasValue ? current.value.writeLock : "";
                ServiceResult<CloudSaveRecord> written = await services.CloudSave.WriteAsync(
                    SaveSerializer.Instance.Serialize(localCloudSnapshot), writeLock);
                if (this == null) return;
                if (!written.succeeded)
                {
                    FinishCloudSaveAction(written.text);
                    return;
                }

                cloudRecord = written.value;
                cloudSnapshot = localCloudSnapshot;
                cloudSaveChecked = true;
                FinishCloudSaveAction(UiText.Get("cloud.backed-up"), false);
            }
            catch (Exception exception)
            {
                FinishCloudSaveAction(UiText.Get("cloud.backup-failed", exception.Message), true);
            }
        }

        void RequestCloudRestore()
        {
            if (cloudSaveBusy || !cloudSaveChecked || !cloudRecord.hasValue || cloudSnapshot == null) return;
            if (!cloudRestoreConfirm)
            {
                cloudRestoreConfirm = true;
                cloudSaveStatus = UiText.Get("cloud.restore.warning");
                cloudSaveStatusIsError = false;
                RefreshModal();
                return;
            }
            cloudRestoreConfirm = false;
            RestoreCloudToDevice();
        }

        async void RestoreCloudToDevice()
        {
            if (cloudSaveBusy || services == null) return;
            cloudSaveBusy = true;
            cloudSaveStatus = UiText.Get("cloud.restoring");
            cloudSaveStatusIsError = false;
            RefreshModal();

            try
            {
                ServiceResult<CloudSaveRecord> current = await services.CloudSave.ReadAsync();
                if (this == null) return;
                if (!current.succeeded)
                {
                    FinishCloudSaveAction(current.text);
                    return;
                }
                if (!SameCloudRecord(cloudRecord, current.value))
                {
                    cloudRecord = current.value;
                    cloudSaveChecked = false;
                    cloudSnapshot = null;
                    FinishCloudSaveAction(UiText.Get("cloud.restore.changed"), true);
                    return;
                }

                if (!gs.TryRestoreCloudSave(cloudSnapshot, out string message))
                {
                    FinishCloudSaveAction(message, true);
                    return;
                }
                cloudSaveChecked = false;
                localRestoreConfirm = false;
                FinishCloudSaveAction(message, false);
            }
            catch (Exception exception)
            {
                FinishCloudSaveAction(UiText.Get("cloud.restore-failed", exception.Message), true);
            }
        }

        void RequestLocalRestore()
        {
            if (cloudSaveBusy) return;
            if (!localRestoreConfirm)
            {
                localRestoreConfirm = true;
                cloudSaveStatus = UiText.Get("cloud.undo.warning");
                cloudSaveStatusIsError = false;
                RefreshModal();
                return;
            }
            localRestoreConfirm = false;
            bool restored = gs.TryRestorePreviousLocalSave(out string message);
            cloudSaveChecked = false;
            FinishCloudSaveAction(message, !restored);
        }

        static bool SameCloudRecord(CloudSaveRecord first, CloudSaveRecord second)
        {
            return first.hasValue == second.hasValue &&
                string.Equals(first.payload, second.payload, StringComparison.Ordinal) &&
                string.Equals(first.writeLock, second.writeLock, StringComparison.Ordinal);
        }

        void FinishCloudSaveAction(string message, bool isError)
        {
            cloudSaveBusy = false;
            cloudSaveStatus = message;
            cloudSaveStatusIsError = isError;
            RefreshModal();
        }

        void FinishCloudSaveAction(ServiceMessage message)
        {
            cloudSaveBusy = false;
            cloudSaveStatus = message.Resolve(UiText.Get);
            cloudSaveFailure = message;
            cloudSaveStatusIsError = true;
            RefreshModal();
        }

        static void ConsentChoice(VisualElement body, string label, bool initialValue, Action<bool> onChanged)
        {
            bool value = initialValue;
            var button = new Button();
            button.AddToClassList("fr-consent-choice");
            button.Add(UiKit.Text(label, "fr-small"));
            var state = UiKit.Text(UiText.Get(value ? "privacy.on" : "privacy.off"), "fr-consent-choice-state");
            state.style.color = value ? UiKit.Lime : UiKit.Muted;
            button.Add(state);
            button.clicked += () =>
            {
                value = !value;
                state.text = UiText.Get(value ? "privacy.on" : "privacy.off");
                state.style.color = value ? UiKit.Lime : UiKit.Muted;
                onChanged(value);
            };
            body.Add(button);
        }

        void SaveConsentPreferences(bool analyticsAllowed, bool adsAllowed, bool onlineServicesAllowed)
        {
            services.Consent.SavePreferences(analyticsAllowed, adsAllowed, onlineServicesAllowed);
            if (!onlineServicesAllowed)
            {
                onlineLeaderboardEntries = null;
                onlinePlayerEntry = null;
                leaderboardStatus = null;
            }
            Sfx.Good();
            Toast(UiText.Get("privacy.saved"), UiKit.Cyan);
        }

        // ------------------------------------------------------------ research and ship
        void BuildResearch(VisualElement body)
        {
            var tabs = UiKit.Div("fr-tabs");
            tabs.name = "research-navigation";
            tabs.Add(TabButton("research", UiText.Get("research.tab.tech"), researchPage == ResearchPage.Tech, UiKit.Magenta,
                () => researchPage = ResearchPage.Tech));
            tabs.Add(TabButton("shield", UiText.Get("research.tab.improvements"), researchPage == ResearchPage.Improvements, UiKit.Magenta,
                () => researchPage = ResearchPage.Improvements));
            body.Add(tabs);
            BuildXpProgress(body, true);
            BuildProgressionResourceHelp(body);
            if (researchPage == ResearchPage.Tech) BuildTech(body);
            else
            {
                BuildUpgrades(body, UpgradeCategory.Defense);
                BuildUpgrades(body, UpgradeCategory.Logistics);
            }
        }

        void BuildShip(VisualElement body)
        {
            var tabs = UiKit.Div("fr-tabs");
            tabs.name = "ship-navigation";
            tabs.Add(TabButton("pack", UiText.Get("ship.tab.cargo"), shipPage == ShipPage.Cargo, UiKit.Amber,
                () => shipPage = ShipPage.Cargo));
            tabs.Add(TabButton("tools", UiText.Get("command.tools"), shipPage == ShipPage.Tools, UiKit.Amber,
                () => shipPage = ShipPage.Tools));
            tabs.Add(TabButton("ship", UiText.Get("ship.tab.systems"), shipPage == ShipPage.Systems, UiKit.Amber,
                () => shipPage = ShipPage.Systems));
            body.Add(tabs);
            if (shipPage == ShipPage.Cargo)
            {
                BuildPack(body);
                BuildProgressionResourceHelp(body);
                BuildUpgrades(body, UpgradeCategory.Ship, u => u.effect == UpgradeEffect.BackpackSlots);
            }
            else if (shipPage == ShipPage.Tools)
            {
                BuildXpProgress(body, true);
                BuildProgressionResourceHelp(body);
                BuildTools(body);
            }
            else
            {
                BuildFuelReserves(body);
                body.Add(UiKit.Text(UiText.Get("fuel.help"), "fr-muted"));
                BuildProgressionResourceHelp(body);
                BuildUpgrades(body, UpgradeCategory.Ship, u => u.effect == UpgradeEffect.FuelTankSlots);
                BuildUpgrades(body, UpgradeCategory.Ship, u => u.effect != UpgradeEffect.BackpackSlots && u.effect != UpgradeEffect.FuelTankSlots);
            }
        }

        void BuildProgressionResourceHelp(VisualElement body)
        {
            body.Add(UiKit.Text(UiText.Get(gs.CargoDepotActive ? "progression.resources.connected" :
                "progression.resources.cargo"), "fr-tiny"));
        }

        void BuildBuilders(VisualElement body)
        {
            body.Add(UiKit.Text(UiText.Get("builders.help"), "fr-muted"));
            var jobs = UiKit.Div();
            jobs.name = "builder-jobs";
            body.Add(jobs);
            void RefreshJobs()
            {
                jobs.Clear();
                var summary = UiKit.Text(UiText.Get("builders.summary", gs.BusyBuilderCount, gs.BuilderCount, gs.ShardBalance), "fr-small");
                jobs.Add(summary);
                summary.schedule.Execute(() => summary.text = UiText.Get("builders.summary",
                    gs.BusyBuilderCount, gs.BuilderCount, gs.ShardBalance)).Every(1000);
                int count = 0;
                foreach (var structure in gs.structures)
                {
                    if (!gs.IsBuilderBusy(structure)) continue;
                    count++;
                    var job = structure;
                    var planet = gs.universe.Get(job.planetId);
                    if (planet == null)
                    {
                        jobs.Add(Note(UiText.Get("builders.unknown-world", job.planetId), UiKit.Amber));
                        continue;
                    }
                    var card = Card(UiKit.Cyan);
                    card.Add(UiKit.Text(UiText.Get("builders.structure", job.def.LocalizedName, planet.Name), "fr-h2"));
                    var status = UiKit.Text(gs.BuilderJobStatus(job), "fr-small");
                    card.Add(status);
                    BuildBuilderFinishAction(card, job, RefreshModal, status);
                    BuildConstructionOffer(card, job);
                    jobs.Add(card);
                }
                foreach (var delta in gs.world.All)
                {
                    foreach (var clearance in delta.obstacleJobs)
                    {
                        var planet = gs.universe.Get(delta.planetId);
                        if (planet == null)
                        {
                            jobs.Add(Note(UiText.Get("builders.unknown-world", delta.planetId), UiKit.Amber));
                            count++;
                            continue;
                        }
                        int pad = clearance.pad;
                        var card = Card(UiKit.Amber);
                        card.Add(UiKit.Text(UiText.Get("builders.clearance", planet.Name, pad + 1), "fr-h2"));
                        var status = UiKit.Text("", "fr-small");
                        card.Add(status);
                        void UpdateClearance()
                        {
                            int remaining = gs.ObstacleClearSecondsRemaining(planet, pad);
                            status.text = UiText.Get(remaining > 0 ? "obstacle.clearing" : "obstacle.completing", FormatJobDuration(remaining));
                        }
                        UpdateClearance();
                        card.schedule.Execute(UpdateClearance).Every(1000);
                        card.Add(IconBtn(UiText.Get(planet.Id == gs.planet.Id ? "builders.manage-clearance" : "builders.view-world"),
                            "map", UiKit.Amber, () =>
                            {
                                CloseModal();
                                if (planet.Id == gs.planet.Id)
                                {
                                    if (!world.SimulateObstacleTap(pad)) Toast(UiText.Get("builders.clearance-unavailable"), UiKit.Amber);
                                }
                                else
                                {
                                    OpenMap();
                                    SelectMapWorld(planet);
                                }
                            }));
                        jobs.Add(card);
                        count++;
                    }
                }
                if (count == 0) jobs.Add(Note(UiText.Get("builders.empty"), UiKit.Cyan));
                BuildCrewRecruitment(jobs);
                if (gs.SaveIsReadOnly) jobs.Add(Note(UiText.Get("builders.read-only"), UiKit.Amber));
            }
            RefreshJobs();
        }

        void BuildBuilderFinishAction(VisualElement body, PlacedStructure job, Action refresh, Label status = null)
        {
            int cost = gs.BuilderSkipCost(job);
            bool confirming = confirmBuilderFinish == job;
            string key = cost == 0 ? confirming ? "builders.confirm-free" : "builders.finish-free" :
                confirming ? "builders.confirm-paid" : "builders.finish-paid";
            var finish = IconBtn(UiText.Get(key, cost), "crystal", confirming ? UiKit.Amber : UiKit.Magenta, () =>
            {
                if (confirmBuilderFinish != job)
                {
                    confirmBuilderPurchaseOwned = -1;
                    confirmBuilderFinish = job;
                    refresh();
                    return;
                }
                confirmBuilderFinish = null;
                var result = gs.FinishBuilderJob(job, cost, out string message);
                if (result == BuilderFinishResult.Rejected) Sfx.Bad();
                else Sfx.Good();
                Toast(message, result == BuilderFinishResult.Saved ? UiKit.Lime :
                    result == BuilderFinishResult.PendingSave ? UiKit.Amber : UiKit.Red);
                refresh();
            });
            finish.name = "builder-finish-" + job.planetId + "-" + job.slot;
            var text = finish.Q<Label>(className: "fr-btn-label");
            void UpdateFinish()
            {
                if (status != null) status.text = gs.BuilderJobStatus(job);
                cost = gs.BuilderSkipCost(job);
                bool armed = confirmBuilderFinish == job;
                string currentKey = cost == 0 ? armed ? "builders.confirm-free" : "builders.finish-free" :
                    armed ? "builders.confirm-paid" : "builders.finish-paid";
                text.text = UiText.Get(currentKey, cost);
                UiKit.SetEnabledLook(finish, gs.IsBuilderBusy(job) && !gs.SaveIsReadOnly && gs.ShardBalance >= cost &&
                    gs.db.shardRules != null && gs.db.shardRules.data != null);
            }
            UpdateFinish();
            finish.schedule.Execute(UpdateFinish).Every(1000);
            body.Add(finish);
        }

        void BuildCrewRecruitment(VisualElement body)
        {
            var rules = gs.db.shardRules != null ? gs.db.shardRules.data : null;
            if (rules == null) return;
            int owned = gs.BuilderCount;
            int price = gs.NextBuilderPurchaseCost;
            var card = Card(UiKit.Cyan);
            card.Add(UiKit.Text(UiText.Get("builders.recruit-title"), "fr-small"));
            card.Add(UiKit.Text(UiText.Get("builders.recruit-help"), "fr-tiny"));
            if (price < 0)
                card.Add(Note(UiText.Get(owned >= rules.maxBuilders ? "builders.cap-reached" : "builders.earn-second"), UiKit.Muted));
            else
            {
                bool confirming = confirmBuilderPurchaseOwned == owned;
                card.Add(UiKit.Text(UiText.Get("builders.recruit-offer", owned + 1, rules.maxBuilders), "fr-small"));
                var buy = IconBtn(UiText.Get(confirming ? "builders.recruit-confirm" : "builders.recruit", price),
                    "build", confirming ? UiKit.Amber : UiKit.Cyan, () =>
                    {
                        if (confirmBuilderPurchaseOwned != owned)
                        {
                            confirmBuilderFinish = null;
                            confirmBuilderPurchaseOwned = owned;
                            RefreshModal();
                            return;
                        }
                        confirmBuilderPurchaseOwned = -1;
                        var result = gs.PurchaseBuilder(owned, price, out string message);
                        if (result == BuilderPurchaseResult.Rejected) Sfx.Bad();
                        else Sfx.Good();
                        Toast(message, result == BuilderPurchaseResult.Saved ? UiKit.Lime :
                            result == BuilderPurchaseResult.PendingSave ? UiKit.Amber : UiKit.Red);
                        RefreshModal();
                    });
                buy.name = "builder-recruit";
                UiKit.SetEnabledLook(buy, !gs.SaveIsReadOnly && gs.ShardBalance >= price);
                card.Add(buy);
            }
            body.Add(card);
        }

        void BuildTools(VisualElement body)
        {
            if (gs.db.UsesImportedEconomy)
            {
                BuildCatalogTools(body);
                return;
            }
            foreach (var upgrade in gs.db.upgrades)
            {
                if (upgrade != null && upgrade.category == UpgradeCategory.Tools)
                    BuildLegacyTool(body, upgrade);
            }
        }

        void BuildLegacyTool(VisualElement body, UpgradeSO u2)
        {
            body.Add(SectionLabel(u2.LocalizedName.ToUpper()));
            int lvl = gs.Level(u2);
            body.Add(UiKit.Text(u2.LocalizedDescription, "fr-muted"));
            var equipped = UiKit.Text(UiText.Get("tools.equipped-summary", gs.ToolName(u2.tool),
                gs.ToolDamage(u2.tool), gs.ToolMultiplier(u2.tool)), "fr-small");
            equipped.style.marginTop = UiKit.Space.S8; equipped.style.marginBottom = UiKit.Space.S12;
            body.Add(equipped);
            for (int l = 0; l <= u2.maxLevel; l++)
            {
                int level = l;
                bool owned = l <= lvl, next = l == lvl + 1;
                var card = Card(UiKit.Magenta, l == lvl);
                if (!owned && !next) card.AddToClassList("fr-card-dim");
                var top = UiKit.Div("fr-row");
                top.Add(UiKit.Text(u2.NameAtLevel(l), "fr-h2"));
                top.Add(UiKit.Div("fr-spacer"));
                top.Add(UiKit.Text(UiText.Get(owned ? (l == lvl ? "tools.equipped" : "tools.owned")
                    : next ? "tools.next-state" : "tools.locked"), "fr-tiny", l == lvl ? UiKit.Lime : (Color?)null));
                card.Add(top);
                card.Add(UiKit.Text(UiText.Get("tools.level-summary", 1f + FrontierSettings.I.toolDamagePerLevel * l,
                    (1f + u2.valuePerLevel * l) * gs.CatalogToolYieldMultiplier(u2.tool)), "fr-tiny"));
                if (next)
                {
                    var cost = u2.CostAt(lvl);
                    card.Add(ProjectCosts(cost, Project("upgrade", u2.id, lvl + 1)));
                    AddProjectAction(card, Project("upgrade", u2.id, lvl + 1));
                    var buy = IconBtn(UiText.Get("command.upgrade"), "up", UiKit.Magenta, () =>
                    {
                        if (gs.Buy(u2)) { Sfx.Good(); Toast(UiText.Get("tools.purchased", u2.NameAtLevel(gs.Level(u2))), UiKit.Ui(u2.color)); planetView.Punch(); }
                        else Sfx.Bad();
                    });
                    UiKit.SetEnabledLook(buy, gs.CanBuy(u2));
                    card.Add(buy);
                }
                body.Add(card);
            }
        }

        void BuildUpgrades(VisualElement body, UpgradeCategory cat, Predicate<UpgradeSO> include = null)
        {
            {
                foreach (var up in gs.db.upgrades)
                {
                    if (up == null || up.category != cat || (include != null && !include(up))) continue;
                    var u = up;
                    int lvl = gs.Level(u);
                    bool max = lvl >= u.maxLevel;
                    var card = Card(UiKit.Magenta);
                    card.name = "upgrade-" + u.id;
                    var top = UiKit.Div("fr-row");
                    top.Add(UiKit.Text(u.NameAtLevel(lvl), "fr-h2"));
                    top.Add(UiKit.Div("fr-spacer"));
                    top.Add(UiKit.Text(UiText.Get("command.level", lvl, u.maxLevel), "fr-small"));
                    card.Add(top);
                    card.Add(UiKit.Text(u.LocalizedDescription, "fr-muted"));
                    card.Add(UiKit.Text(UiText.Get(max ? "command.effect.current" : "command.effect.next",
                        UpgradeEffectText(u, lvl), max ? "" : UpgradeEffectText(u, lvl + 1)), "fr-tiny"));
                    if (!max)
                    {
                        var cost = u.CostAt(lvl);
                        card.Add(ProjectCosts(cost, Project("upgrade", u.id, lvl + 1)));
                        AddProjectAction(card, Project("upgrade", u.id, lvl + 1));
                        var buy = IconBtn(UiText.Get("command.upgrade"), "up", UiKit.Magenta, () =>
                        {
                            if (gs.Buy(u)) { Sfx.Good(); Toast(UiText.Get("tools.purchased", u.NameAtLevel(gs.Level(u))), UiKit.Ui(u.color)); planetView.Punch(); }
                            else Sfx.Bad();
                        });
                        if (gs.UpgradeLockReason(u) != null) card.Add(Note(gs.UpgradeLockReason(u), UiKit.Amber));
                        UiKit.SetEnabledLook(buy, gs.CanBuy(u));
                        card.Add(buy);
                    }
                    else card.Add(Note(UiText.Get("command.maxed"), UiKit.Lime));
                    body.Add(card);
                }
            }
        }

        // ------------------------------------------------------------ carried cargo
        const int PackColumns = 9;

        void BuildPack(VisualElement body)
        {
            SyncPack();
            body.Add(UiKit.Text(UiText.Get("ship.cargo.help"), "fr-tiny"));
            var fuel = IconBtn(UiText.Get("fuel.open"), "fuel", UiKit.Amber, OpenFuelTanks);
            fuel.name = "pack-open-tanks";
            body.Add(fuel);
            bool empty = gs.SlotsUsed == 0;
            body.Add(UiKit.Text(UiText.Get(empty ? "pack.capacity" : "pack.capacity.rearrange", gs.SlotsUsed, gs.Slots), "fr-muted"));
            if (empty)
            {
                body.Add(UiKit.Text(UiText.Get("pack.empty.title"), "fr-h2"));
                body.Add(UiKit.Text(UiText.Get("pack.empty.help"), "fr-muted"));
                var harvest = AvailableLocalGatherer();
                var gather = IconBtn(harvest != null ? UiText.Get("pack.open-gatherer", harvest.def.LocalizedName.ToUpperInvariant())
                    : UiText.Get("guide.action.gather-shop"),
                    harvest != null ? StructureIcon(harvest.def.kind) : "build", UiKit.Lime, RunEmptyPackAction);
                gather.name = "empty-pack-action";
                body.Add(gather);
            }
            var grid = UiKit.Div("fr-grid");
            grid.style.marginTop = UiKit.Space.S8;

            var slots = new List<VisualElement>();
            for (int i = 0; i < packRes.Length; i++) slots.Add(PackSlot(i, slots));

            int rows = Mathf.CeilToInt(slots.Count / (float)PackColumns);
            int padded = slots.Count;
            while (padded < rows * PackColumns) { slots.Add(Slot(null, 0, true)); padded++; }
            foreach (var s in slots) grid.Add(s);

            SizeInventoryGrid(grid, slots);
            body.Add(grid);

            var detail = Card(UiKit.Amber);
            detail.style.marginTop = UiKit.Space.S16;
            detail.style.minHeight = 200;
            if (packSel >= 0 && packSel < packRes.Length && packRes[packSel] != null)
            {
                var r = packRes[packSel];
                Color c = UiKit.Ui(r.color);
                var row = UiKit.Div("fr-card-head");
                row.Add(Icons.Make(Icons.ForResource(r), c, UiKit.Size.IconXl));
                var col = UiKit.Div("fr-card-text"); col.pickingMode = PickingMode.Ignore;
                col.Add(UiKit.Text(r.LocalizedName.ToUpper(), "fr-h2"));
                col.Add(UiKit.Text(UiText.Get("pack.slot-count", packCnt[packSel], r.stackSize), "fr-small"));
                col.Add(UiKit.Text(UiText.Get("pack.total-count", gs.Count(r)), "fr-small"));
                row.Add(col);
                detail.Add(row);
                if (!string.IsNullOrEmpty(r.LocalizedDescription))
                {
                    var desc = UiKit.Text(r.LocalizedDescription, "fr-muted");
                    desc.style.marginTop = UiKit.Space.S8;
                    detail.Add(desc);
                }
            }
            else if (!empty) detail.Add(UiKit.Text(UiText.Get("pack.select-help"), "fr-muted"));
            if (!empty) body.Add(detail);
            var salvage = gs.ObstacleRewardsOn(gs.planet);
            if (salvage.Count > 0)
            {
                body.Add(SectionLabel(UiText.Get("pack.salvage.title")));
                foreach (var reward in salvage)
                {
                    ResourceSO resource = gs.db.FindResource(reward.resourceId);
                    if (resource == null) continue;
                    var row = UiKit.Div("fr-row");
                    row.AddToClassList("fr-popup-line");
                    row.Add(Icons.Make(Icons.ForResource(resource), UiKit.Ui(resource.color), UiKit.Size.IconLg));
                    var text = UiKit.Div("fr-card-text");
                    text.Add(UiKit.Text(resource.LocalizedName, "fr-small"));
                    text.Add(UiKit.Text(UiText.Get("pack.salvage.waiting", reward.amount, gs.planet.Name), "fr-tiny"));
                    row.Add(text);
                    int pad = reward.pad;
                    var claim = IconBtn(UiText.Get("pack.salvage.claim"), "pack", UiKit.Amber, () =>
                    {
                        if (gs.ClaimObstacleReward(gs.planet, pad))
                        {
                            Sfx.Good();
                            Toast(UiText.Get("pack.salvage.added"), UiKit.Lime);
                        }
                        else
                        {
                            Sfx.Bad();
                            Toast(UiText.Get("pack.salvage.full"), UiKit.Red);
                        }
                    });
                    UiKit.SetEnabledLook(claim, gs.FreeSpace(resource) > 0);
                    row.Add(claim);
                    body.Add(row);
                }
            }
        }

        ResourceSO[] packRes = new ResourceSO[0];
        int[] packCnt = new int[0];
        int packSel = -1;

        // Keeps the arranged slot layout consistent with the real inventory totals.
        void SyncPack()
        {
            int n = gs.Slots;
            if (packRes.Length != n)
            {
                Array.Resize(ref packRes, n);
                Array.Resize(ref packCnt, n);
            }
            foreach (var r in gs.db.resources)
            {
                int total = gs.IsTankFuel(r.id) ? 0 : gs.Count(r), sum = 0;
                for (int i = 0; i < n; i++) if (packRes[i] == r) sum += packCnt[i];
                for (int i = n - 1; i >= 0 && sum > total; i--)
                {
                    if (packRes[i] != r) continue;
                    int take = Mathf.Min(packCnt[i], sum - total);
                    packCnt[i] -= take; sum -= take;
                    if (packCnt[i] <= 0) { packRes[i] = null; packCnt[i] = 0; }
                }
                for (int i = 0; i < n && sum < total; i++)
                {
                    if (packRes[i] != r || packCnt[i] >= r.stackSize) continue;
                    int add = Mathf.Min(r.stackSize - packCnt[i], total - sum);
                    packCnt[i] += add; sum += add;
                }
                for (int i = 0; i < n && sum < total; i++)
                {
                    if (packRes[i] != null) continue;
                    int add = Mathf.Min(r.stackSize, total - sum);
                    packRes[i] = r; packCnt[i] = add; sum += add;
                }
            }
            for (int i = 0; i < n; i++) if (packRes[i] != null && packCnt[i] <= 0) packRes[i] = null;
            if (packSel >= n || (packSel >= 0 && packRes[packSel] == null)) packSel = -1;
        }

        VisualElement PackSlot(int index, List<VisualElement> all)
        {
            var s = Slot(packRes[index], packCnt[index], false);
            if (index == packSel)
            {
                Color c = packRes[index] != null ? UiKit.Ui(packRes[index].color) : UiKit.Cyan;
                UiKit.Border(s, Color.white, 5f);
                UiKit.Halo(s, c, 18f, 0.6f);
            }
            BindInventoryDrag(s, packRes[index], packCnt[index], () =>
            {
                packSel = (packRes[index] != null && packSel != index) ? index : -1;
                Sfx.Click(); RefreshModal();
            }, position =>
            {
                int target = SlotAt(all, position);
                if (target >= 0 && target < packRes.Length && target != index && packRes[index] != null)
                {
                    MovePackSlot(index, target); packSel = target; Sfx.Click();
                }
                RefreshModal();
            });
            return s;
        }

        void MovePackSlot(int from, int to)
        {
            if (packRes[to] == packRes[from] && packRes[to] != null)
            {
                int room = packRes[to].stackSize - packCnt[to];
                int move = Mathf.Min(room, packCnt[from]);
                packCnt[to] += move; packCnt[from] -= move;
                if (packCnt[from] <= 0) { packRes[from] = null; packCnt[from] = 0; }
                return;
            }
            var r = packRes[to]; packRes[to] = packRes[from]; packRes[from] = r;
            int c = packCnt[to]; packCnt[to] = packCnt[from]; packCnt[from] = c;
        }

        // ------------------------------------------------------------ debug
        int debugAmount = 100;

        void BuildDebug(VisualElement body)
        {
            body.Add(UiKit.Text(UiText.Get("debug.header"), "fr-muted", UiKit.Red));
            BuildServiceDiagnostics(body);

            var give = Card(UiKit.Amber);
            give.Add(UiKit.Text(UiText.Get("debug.items"), "fr-h2"));
            var amt = UiKit.Div("fr-tabs");
            amt.style.marginTop = UiKit.Space.S8;
            foreach (int a in new[] { 10, 100, 1000 })
            {
                int v = a;
                amt.Add(TabButton("star", UiText.Get("debug.amount", a), debugAmount == v, UiKit.Amber, () => debugAmount = v));
            }
            give.Add(amt);
            var res = UiKit.Div("fr-row-wrap");
            foreach (var r in gs.db.resources)
            {
                var rr = r;
                var b = IconBtn(r.LocalizedName.ToUpper(), Icons.ForResource(r), UiKit.Ui(r.color), () => { gs.Add(rr, debugAmount); Sfx.Good(); RefreshModal(); });
                b.AddToClassList("fr-btn-mini");
                b.style.marginBottom = UiKit.Space.S8;
                res.Add(b);
            }
            give.Add(res);
            give.Add(IconBtn(UiText.Get("debug.give-each", debugAmount), "pack", UiKit.Lime, () =>
            {
                foreach (var r in gs.db.resources) gs.Add(r, debugAmount, false);
                gs.Notify(); Sfx.Good(); RefreshModal();
            }));
            body.Add(give);

            var prog = Card(UiKit.Magenta);
            prog.Add(UiKit.Text(UiText.Get("debug.progress"), "fr-h2"));
            prog.Add(UiKit.Text(UiText.Get("debug.player-level", gs.PlayerLevel), "fr-small"));
            prog.Add(IconBtn(UiText.Get("debug.level-one"), "star", UiKit.Magenta, () => { gs.DebugSetLevel(gs.PlayerLevel + 1); Sfx.Good(); RefreshModal(); }));
            prog.Add(IconBtn(UiText.Get("debug.level-ten"), "star", UiKit.Magenta, () => { gs.DebugSetLevel(gs.PlayerLevel + 10); Sfx.Good(); RefreshModal(); }));
            prog.Add(IconBtn(UiText.Get("debug.max-upgrades"), "up", UiKit.Lime, () =>
            {
                foreach (var u in gs.db.upgrades) if (u != null) gs.DebugSetUpgrade(u, u.maxLevel);
                Sfx.Good(); RefreshModal();
            }));
            prog.Add(IconBtn(UiText.Get("debug.reset-upgrades"), "close", UiKit.Red, () =>
            {
                foreach (var u in gs.db.upgrades) if (u != null) gs.DebugSetUpgrade(u, 0);
                Sfx.Click(); RefreshModal();
            }));
            body.Add(prog);
            BuildTravelDebug(body);

            var tp = Card(UiKit.Cyan);
            tp.Add(UiKit.Text(UiText.Get("debug.teleport-free"), "fr-h2"));
            foreach (var p in gs.universe.AuthoredPlanets)
            {
                var pl = p;
                tp.Add(IconBtn(pl.Name.ToUpper() + "  -  " + pl.SystemName, "map", pl.Id == gs.planet.Id ? UiKit.Lime : UiKit.Cyan, () => DebugTeleportTo(pl)));
            }
            body.Add(tp);
            body.Add(BuildGeneratedPicker());
        }

        // ------------------------------------------------------------ debug: generated worlds
        static readonly string[] DebugBiomeLabels = { "debug.biome.any", "debug.biome.lush", "debug.biome.mineral", "debug.biome.glacial", "debug.biome.volcanic", "debug.biome.fungal", "debug.biome.crystal", "debug.biome.obsidian", "debug.biome.shattered", "debug.biome.desert", "debug.biome.oceanic", "debug.biome.gas-giant", "debug.biome.barren" };
        static readonly string[] DebugSizeLabels = { "debug.size.any", "debug.size.dwarf", "debug.size.small", "debug.size.medium", "debug.size.large", "debug.size.giant", "debug.size.colossal" };
        static readonly int[] DebugTierChoices = { 0, 2, 4, 6, 8, 12, 20 };
        /// <summary>The menu searches this many sectors around home at most, so a rare combination cannot freeze the game.</summary>
        const int DebugSearchSectors = 3000;
        int debugBiome, debugSize, debugTier, debugSkip;

        VisualElement BuildGeneratedPicker()
        {
            var gen = Card(UiKit.Lime);
            gen.Add(UiKit.Text(UiText.Get("debug.generated-world"), "fr-h2"));
            gen.Add(UiKit.Text(UiText.Get("debug.generated-help"), "fr-tiny"));
            gen.Add(DebugChoiceRow("debug.biome.label", DebugBiomeLabels, debugBiome, i => debugBiome = i));
            gen.Add(DebugChoiceRow("debug.size.label", DebugSizeLabels, debugSize, i => debugSize = i));
            var tiers = new string[DebugTierChoices.Length];
            for (int i = 0; i < tiers.Length; i++) tiers[i] = UiText.Get("debug.tier-choice", DebugTierChoices[i]);
            gen.Add(DebugChoiceRow("debug.minimum-tier", tiers, debugTier, i => debugTier = i, localizeLabels: false));
            gen.Add(IconBtn(UiText.Get("debug.teleport"), "map", UiKit.Lime, () => DebugTeleportToGenerated(false)));
            gen.Add(IconBtn(UiText.Get("debug.next-match"), "map", UiKit.Cyan, () => DebugTeleportToGenerated(true)));
            var here = UiKit.Text(UiText.Get("debug.now-on", gs.planet.Name, gs.planet.Id), "fr-tiny");
            here.style.marginTop = UiKit.Space.S12;
            gen.Add(here);
            gen.Add(IconBtn(UiText.Get("debug.dump-descriptor"), "tools", UiKit.Amber, DebugDumpDescriptor));
            return gen;
        }

        VisualElement DebugChoiceRow(string title, string[] labels, int selectedIndex, Action<int> pick, bool localizeLabels = true)
        {
            var box = UiKit.Div();
            box.Add(SectionLabel(UiText.Get(title)));
            var row = UiKit.Div("fr-row-wrap");
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                string label = localizeLabels ? UiText.Get(labels[i]) : labels[i];
                row.Add(DebugChoice(label, i == selectedIndex, () => { pick(index); debugSkip = 0; RefreshModal(); }));
            }
            box.Add(row);
            return box;
        }

        Button DebugChoice(string label, bool on, Action click)
        {
            var b = new Button();
            b.AddToClassList("fr-tab");
            if (on) b.AddToClassList("fr-tab-on");
            UiKit.SetAccent(b, UiKit.Lime);
            // the tab style is for equal width rows; these wrap
            b.style.flexGrow = 0; b.style.flexShrink = 0; b.style.flexBasis = StyleKeyword.Auto;
            b.style.minWidth = UiKit.Size.Touch;
            b.style.paddingLeft = UiKit.Space.S12; b.style.paddingRight = UiKit.Space.S12;
            b.style.marginBottom = UiKit.Space.S8;
            b.Add(UiKit.Text(label, "fr-tab-label"));
            b.clicked += () => { Sfx.Click(); click(); };
            return b;
        }

        void DebugTeleportToGenerated(bool next)
        {
            debugSkip = next ? debugSkip + 1 : 0;
            UBiome? biome = debugBiome == 0 ? (UBiome?)null : (UBiome)(debugBiome - 1);
            USize? size = debugSize == 0 ? (USize?)null : (USize)(debugSize - 1);
            var found = gs.universe.FindGenerated(biome, size, DebugTierChoices[debugTier], debugSkip, DebugSearchSectors);
            if (found == null)
            {
                debugSkip = 0;
                Sfx.Bad();
                Toast(UiText.Get("debug.no-matching-world"), UiKit.Red);
                return;
            }
            Sfx.Good();
            DebugTeleportTo(found);
            Toast(UiText.Get("debug.world-found", found.Name, found.BiomeName, found.SizeName.ToLower(), found.Tier), UiKit.Cyan);
        }

        /// <summary>Writes a readable description of the current world's descriptor to the log and the clipboard.</summary>
        void DebugDumpDescriptor()
        {
            string text = gs.planet.Dump();
            Debug.Log("Frontier: planet descriptor\n" + text);
            GUIUtility.systemCopyBuffer = text;
            Sfx.Click();
            Toast(UiText.Get("debug.descriptor-copied"), UiKit.Amber);
        }

        /// <summary>Moves the player to a world for free and shows it (debug tools).</summary>
        void DebugTeleportTo(PlanetData target)
        {
            CloseModal();
            gs.DebugTeleport(target);
            planetView.Show(target, true, gs.TerraformerStageOn(target));
            selected = target;
        }

        VisualElement Slot(ResourceSO r, int count, bool locked)
        {
            var s = UiKit.Div("fr-slot");
            if (locked) s.AddToClassList("fr-slot-locked");
            if (r != null)
            {
                s.Add(Icons.Make(Icons.ForResource(r), UiKit.Ui(r.color)));
                s.Add(UiKit.Text(r.fuelEnergy > 0
                    ? UiText.Get("fuel.slot-thousands", (long)count * r.fuelEnergy) : count.ToString(), "fr-slot-count"));
                s.tooltip = r.LocalizedName + " " + UiKit.Amount(r, count);
            }
            return s;
        }
    }
}
