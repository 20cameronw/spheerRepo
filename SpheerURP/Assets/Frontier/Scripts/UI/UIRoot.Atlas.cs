using System;
using System.Collections.Generic;
using System.Linq;
using Frontier.Economy;
using Frontier.Universe;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public partial class UIRoot
    {
        int atlasTab;
        ResourceCategory atlasCategory;
        string atlasResourceId;
        string atlasWorldId;

        void BuildAtlas(VisualElement body)
        {
            var tabs = UiKit.Div();
            var firstTabRow = UiKit.Div("fr-tabs");
            firstTabRow.Add(TabButton("core", UiText.Get("atlas.resources"), atlasTab == 0, UiKit.Cyan, () => atlasTab = 0));
            firstTabRow.Add(TabButton("refinery", UiText.Get("atlas.recipes"), atlasTab == 1, UiKit.Cyan, () => atlasTab = 1));
            tabs.Add(firstTabRow);
            var secondTabRow = UiKit.Div("fr-tabs");
            secondTabRow.Add(TabButton("map", UiText.Get("atlas.worlds"), atlasTab == 2, UiKit.Cyan, () => atlasTab = 2));
            secondTabRow.Add(TabButton("alien", UiText.Get("atlas.bestiary"), atlasTab == 3, UiKit.Cyan, () => atlasTab = 3));
            tabs.Add(secondTabRow);
            var thirdTabRow = UiKit.Div("fr-tabs");
            thirdTabRow.Add(TabButton("build", UiText.Get("atlas.structures"), atlasTab == 4, UiKit.Cyan, () => atlasTab = 4));
            tabs.Add(thirdTabRow);
            body.Add(tabs);

            body.Add(UiKit.Text(UiText.Get("atlas.notes"), "fr-tiny", UiKit.Muted));
            body.Add(UiKit.Text(UiText.Get("atlas.summary.resources-recipes",
                gs.AtlasDiscoveredCount("resource"), gs.AtlasCatalogCount("resource"),
                gs.AtlasDiscoveredCount("recipe"), gs.AtlasCatalogCount("recipe")), "fr-small"));
            body.Add(UiKit.Text(UiText.Get("atlas.summary.structures", gs.AtlasDiscoveredCount("structure"), gs.AtlasCatalogCount("structure")), "fr-small"));
            body.Add(UiKit.Text(UiText.Get("atlas.summary.aliens", gs.AtlasDiscoveredCount("enemy"), gs.AtlasCatalogCount("enemy")), "fr-small"));
            body.Add(UiKit.Text(UiText.Get("atlas.help"), "fr-muted"));
            switch (atlasTab)
            {
                case 0: BuildAtlasResources(body); break;
                case 1: BuildAtlasRecipes(body); break;
                case 2: BuildAtlasWorlds(body); break;
                case 3: BuildAtlasBestiary(body); break;
                default: BuildAtlasStructures(body); break;
            }
        }

        List<ResourceSO> AtlasResources(ResourceCategory category)
        {
            var resources = new List<ResourceSO>();
            if (gs.db.resources == null) return resources;
            foreach (var resource in gs.db.resources)
                if (resource != null && resource.category == category) resources.Add(resource);
            resources.Sort((a, b) => string.Compare(a.displayName, b.displayName, StringComparison.Ordinal));
            return resources;
        }

        void BuildAtlasResources(VisualElement body)
        {
            var categoryLabel = UiKit.Text(UiText.Get("atlas.resource.family"), "fr-tiny");
            categoryLabel.style.marginTop = UiKit.Space.S12;
            body.Add(categoryLabel);
            var category = new DropdownField();
            category.choices = Enum.GetNames(typeof(ResourceCategory))
                .Select(name => UiText.Get("resource.category." + name.ToLowerInvariant())).ToList();
            category.index = (int)atlasCategory;
            category.RegisterValueChangedCallback(e =>
            {
                int index = category.choices.IndexOf(e.newValue);
                if (index < 0) return;
                atlasCategory = (ResourceCategory)index;
                var matching = AtlasResources(atlasCategory);
                atlasResourceId = matching.Count > 0 ? matching[0].id : null;
                RefreshModal();
            });
            body.Add(category);

            var resources = AtlasResources(atlasCategory);
            if (resources.Count == 0)
            {
                body.Add(UiKit.Text(UiText.Get("atlas.resource.empty"), "fr-muted"));
                return;
            }

            int selectedIndex = resources.FindIndex(r => r.id == atlasResourceId);
            if (selectedIndex < 0) selectedIndex = 0;
            atlasResourceId = resources[selectedIndex].id;
            var resourceLabel = UiKit.Text(UiText.Get("atlas.resource"), "fr-tiny");
            resourceLabel.style.marginTop = UiKit.Space.S12;
            body.Add(resourceLabel);
            var resourcePicker = new DropdownField();
            resourcePicker.choices = resources.Select(r => r.displayName).ToList();
            resourcePicker.index = selectedIndex;
            resourcePicker.RegisterValueChangedCallback(e =>
            {
                int index = resourcePicker.choices.IndexOf(e.newValue);
                if (index < 0) return;
                atlasResourceId = resources[index].id;
                RefreshModal();
            });
            body.Add(resourcePicker);
            BuildAtlasResourceDetails(body, resources[selectedIndex]);
        }

        void BuildAtlasResourceDetails(VisualElement body, ResourceSO resource)
        {
            var card = Card(resource.color, true);
            var header = UiKit.Div("fr-card-head");
            header.Add(Icons.Make(Icons.ForResource(resource), UiKit.Ui(resource.color), UiKit.Size.IconXl));
            var heading = UiKit.Div("fr-card-text");
            heading.Add(UiKit.Text(resource.displayName.ToUpper(), "fr-h2"));
            heading.Add(UiKit.Text(UiText.Get("atlas.resource.summary",
                UiText.Get("resource.category." + resource.category.ToString().ToLowerInvariant()), resource.tier, resource.stackSize), "fr-tiny"));
            header.Add(heading);
            card.Add(header);
            card.Add(UiKit.Text(
                UiText.Get(gs.IsAtlasDiscovered("resource", resource.id)
                    ? "atlas.resource.discovered"
                    : "atlas.resource.undiscovered"),
                "fr-tiny",
                gs.IsAtlasDiscovered("resource", resource.id) ? UiKit.Lime : UiKit.Muted));
            if (!string.IsNullOrEmpty(resource.DetailText)) card.Add(UiKit.Text(resource.DetailText, "fr-muted"));

            card.Add(SectionLabel(UiText.Get("atlas.resource.where")));
            int habitatCount = 0;
            if (resource.IsRaw && gs.db.habitats != null && gs.db.habitats.rows != null)
            {
                foreach (var habitat in gs.db.habitats.rows)
                {
                    if (habitat == null || habitat.resourceId != resource.id) continue;
                    string area = habitat.belt ? UiText.Get("atlas.resource.belt") : PrettyName(habitat.biome.ToString());
                    card.Add(UiKit.Text(UiText.Get("atlas.resource.habitat", area, PrettyName(habitat.method.ToString()), habitat.minTier), "fr-small"));
                    habitatCount++;
                }
            }
            if (habitatCount == 0 && resource.harvestMethods != null)
                foreach (var method in resource.harvestMethods)
                    card.Add(UiKit.Text(UiText.Get("atlas.resource.harvest", PrettyName(method.ToString())), "fr-small"));
            if (habitatCount == 0 && (resource.harvestMethods == null || resource.harvestMethods.Length == 0))
                card.Add(UiKit.Text(UiText.Get("atlas.resource.processed"), "fr-small"));

            if (resource.IsRaw)
            {
                card.Add(SectionLabel(UiText.Get("atlas.resource.samples")));
                var samples = AtlasSurveyedWorldSamples(resource);
                if (samples.Count == 0) card.Add(UiKit.Text(UiText.Get("atlas.resource.samples.empty"), "fr-small"));
                for (int i = 0; i < samples.Count && i < 3; i++)
                    card.Add(UiKit.Text(UiText.Get("atlas.resource.sample", samples[i].planet.Name, samples[i].distance), "fr-small"));
            }

            body.Add(card);
            BuildAtlasRelatedRecipes(body, resource);
            BuildAtlasBuildUses(body, resource);
            if (resource.IsRaw)
            {
                var find = IconBtn(UiText.Get("atlas.resource.find"), "map", UiKit.Cyan, () =>
                {
                    CloseModal();
                    OpenMapForResource(resource.id);
                });
                body.Add(find);
            }
        }

        List<(PlanetData planet, double distance)> AtlasSurveyedWorldSamples(ResourceSO resource)
        {
            var samples = new List<(PlanetData planet, double distance)>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var home = gs.universe.Home;
            if (gs.planet != null) AddAtlasSample(gs.planet, resource, home, seen, samples);
            if (gs.world != null)
                foreach (var record in gs.world.All)
                {
                    if (record == null || record.visits <= 0) continue;
                    AddAtlasSample(gs.universe.Get(record.planetId), resource, home, seen, samples);
                }
            samples.Sort((a, b) => a.distance.CompareTo(b.distance));
            return samples;
        }

        static void AddAtlasSample(PlanetData planet, ResourceSO resource, PlanetData home, HashSet<string> seen,
            List<(PlanetData planet, double distance)> samples)
        {
            if (planet == null || !seen.Add(planet.Id) || planet.Resources == null ||
                !planet.Resources.Any(candidate => candidate != null && candidate.id == resource.id)) return;
            double dx = planet.Star.X - home.Star.X;
            double dy = planet.Star.Y - home.Star.Y;
            samples.Add((planet, Math.Sqrt(dx * dx + dy * dy)));
        }

        void BuildAtlasBuildUses(VisualElement body, ResourceSO resource)
        {
            var uses = new List<string>();
            if (gs.db.structures != null)
            {
                foreach (var structure in gs.db.structures)
                    if (structure != null && structure.buildCost != null &&
                        structure.buildCost.Exists(item => item.resource == resource))
                        uses.Add(UiText.Get("atlas.resource.use-structure", structure.displayName));
            }
            if (gs.db.upgrades != null)
            {
                foreach (var upgrade in gs.db.upgrades)
                {
                    if (upgrade == null) continue;
                    for (int level = 0; level < upgrade.MaxLevel; level++)
                    {
                        if (upgrade.CostAt(level).Exists(item => item.resource == resource))
                            uses.Add(UiText.Get("atlas.resource.use-upgrade", upgrade.NameAtLevel(level + 1)));
                    }
                }
            }
            if (uses.Count == 0) return;
            body.Add(SectionLabel(UiText.Get("atlas.resource.uses")));
            foreach (var use in uses) body.Add(UiKit.Text(use, "fr-small"));
        }

        void BuildAtlasRelatedRecipes(VisualElement body, ResourceSO resource)
        {
            bool headingAdded = false;
            if (gs.db.recipes == null) return;
            foreach (var recipe in gs.db.recipes)
            {
                if (recipe == null) continue;
                bool uses = recipe.inputs != null && recipe.inputs.Exists(i => i.resource == resource);
                bool makes = recipe.outputs != null && recipe.outputs.Exists(i => i.resource == resource);
                if (!uses && !makes) continue;
                if (!headingAdded)
                {
                    body.Add(SectionLabel(UiText.Get("atlas.resource.related")));
                    headingAdded = true;
                }
                AddAtlasRecipeCard(body, recipe);
            }
            if (!headingAdded) body.Add(UiKit.Text(UiText.Get("atlas.resource.related.empty"), "fr-muted"));
        }

        void BuildAtlasRecipes(VisualElement body)
        {
            if (gs.db.recipes == null || gs.db.recipes.Length == 0)
            {
                body.Add(UiKit.Text(UiText.Get("atlas.recipes.empty"), "fr-muted"));
                return;
            }
            body.Add(UiKit.Text(UiText.Get("atlas.recipes.help"), "fr-muted"));
            var recipes = gs.db.recipes.Where(r => r != null)
                .OrderBy(r => r.tier).ThenBy(r => r.kind).ThenBy(r => r.displayName, StringComparer.Ordinal);
            foreach (var recipe in recipes) AddAtlasRecipeCard(body, recipe);
        }

        void AddAtlasRecipeCard(VisualElement body, RecipeSO recipe)
        {
            var card = Card(UiKit.Cyan);
            card.Add(UiKit.Text(recipe.displayName.ToUpper(), "fr-h2"));
            string gate = UiText.Get(string.IsNullOrEmpty(recipe.unlockResearch) ? "atlas.recipe.gate" : "atlas.recipe.gate-research",
                PrettyName(recipe.kind.ToString()), recipe.tier, PrettyName(recipe.stationId),
                recipe.stationLevel, PrettyName(recipe.unlockResearch));
            card.Add(UiKit.Text(gate, "fr-tiny"));
            bool discovered = gs.IsAtlasDiscovered("recipe", recipe.id);
            card.Add(UiKit.Text(
                UiText.Get(discovered ? "atlas.recipe.crafted" : "atlas.recipe.uncrafted"),
                "fr-tiny",
                discovered ? UiKit.Lime : UiKit.Muted));

            var flow = UiKit.Div("fr-recipe-flow");
            var inputs = UiKit.Div("fr-recipe-side");
            inputs.Add(UiKit.Text(UiText.Get("atlas.recipe.input"), "fr-tiny"));
            if (recipe.inputs != null)
                foreach (var item in recipe.inputs)
                    if (item.resource != null) inputs.Add(RecipeResource(item.resource, item.amount));
            flow.Add(inputs);
            var arrow = UiKit.Text(">", "fr-h2", UiKit.Cyan);
            arrow.AddToClassList("fr-recipe-arrow");
            flow.Add(arrow);
            var outputs = UiKit.Div("fr-recipe-side");
            outputs.Add(UiKit.Text(UiText.Get("atlas.recipe.output"), "fr-tiny"));
            if (recipe.outputs != null)
                foreach (var item in recipe.outputs)
                    if (item.resource != null) outputs.Add(RecipeResource(item.resource, item.amount));
            flow.Add(outputs);
            card.Add(flow);
            body.Add(card);
        }

        void BuildAtlasWorlds(VisualElement body)
        {
            var planets = AtlasKnownWorlds();
            if (planets.Count == 0)
            {
                body.Add(UiKit.Text(UiText.Get("atlas.worlds.empty"), "fr-muted"));
                return;
            }
            int selectedIndex = planets.FindIndex(p => p.Id == atlasWorldId);
            if (selectedIndex < 0) selectedIndex = 0;
            atlasWorldId = planets[selectedIndex].Id;
            var picker = new DropdownField();
            picker.choices = planets.Select(p => p.Name).ToList();
            picker.index = selectedIndex;
            picker.RegisterValueChangedCallback(e =>
            {
                int index = picker.choices.IndexOf(e.newValue);
                if (index < 0) return;
                atlasWorldId = planets[index].Id;
                RefreshModal();
            });
            body.Add(UiKit.Text(UiText.Get("atlas.worlds.help"), "fr-tiny"));
            body.Add(picker);
            BuildAtlasWorldDetails(body, planets[selectedIndex]);
        }

        List<PlanetData> AtlasKnownWorlds()
        {
            var planets = new List<PlanetData>();
            var known = new HashSet<string>();
            if (gs.world != null)
            {
                foreach (var record in gs.world.All)
                {
                    if (record == null || record.visits <= 0 || !known.Add(record.planetId)) continue;
                    var planet = gs.universe.Get(record.planetId);
                    if (planet != null) planets.Add(planet);
                }
            }
            if (gs.planet != null && known.Add(gs.planet.Id)) planets.Add(gs.planet);
            planets.Sort((a, b) =>
            {
                if (a.Id == gs.planet.Id) return -1;
                if (b.Id == gs.planet.Id) return 1;
                return string.Compare(a.Name, b.Name, StringComparison.Ordinal);
            });
            return planets;
        }

        void BuildAtlasWorldDetails(VisualElement body, PlanetData planet)
        {
            var card = Card(UiKit.Cyan, true);
            card.Add(UiKit.Text(planet.Name.ToUpper(), "fr-h2"));
            card.Add(UiKit.Text(UiText.Get("atlas.world.summary", planet.SystemName, planet.BiomeName, planet.Tier, planet.Descriptor.Kind), "fr-small"));
            card.Add(UiKit.Text(UiText.Get("atlas.world.hazards", planet.Danger,
                planet.Hazards == Hazards.None ? UiText.Get("atlas.world.hazards.none") : planet.Hazards.ToString()), "fr-tiny"));
            card.Add(SectionLabel(UiText.Get("atlas.world.resources")));
            foreach (var resource in planet.Resources)
            {
                if (resource == null) continue;
                var action = new Button();
                action.AddToClassList("fr-btn");
                action.Add(Icons.Make(Icons.ForResource(resource), UiKit.Ui(resource.color), UiKit.Size.IconMd));
                action.Add(UiKit.Text(resource.displayName, "fr-btn-label"));
                action.clicked += () =>
                {
                    atlasCategory = resource.category;
                    atlasResourceId = resource.id;
                    atlasTab = 0;
                    RefreshModal();
                };
                card.Add(action);
            }
            body.Add(card);
        }

        void BuildAtlasStructures(VisualElement body)
        {
            if (gs.db.structures != null)
            {
                foreach (var structure in gs.db.structures)
                {
                    if (structure == null) continue;
                    bool discovered = !structure.Implemented || gs.IsAtlasDiscovered("structure", structure.kind.ToString());
                    var card = Card(structure.color);
                    var header = UiKit.Div("fr-card-head");
                    header.Add(Icons.Make(discovered ? StructureIcon(structure.kind) : "lock",
                        discovered ? UiKit.Ui(structure.color) : UiKit.Muted, UiKit.Size.IconLg));
                    var text = UiKit.Div("fr-card-text");
                    text.Add(UiKit.Text(discovered ? structure.displayName.ToUpper() : UiText.Get("atlas.structure.unknown"), "fr-h2"));
                    text.Add(UiKit.Text(discovered ? UiText.Get("atlas.structure.summary", structure.role, structure.tier,
                        UiText.Get(structure.Implemented ? "atlas.structure.active" : "atlas.structure.catalog-only"))
                        : UiText.Get("atlas.structure.identify"), "fr-tiny"));
                    header.Add(text);
                    card.Add(header);
                    if (structure.Implemented)
                        card.Add(UiKit.Text(UiText.Get(discovered ? "atlas.structure.built" : "atlas.structure.unbuilt"),
                            "fr-tiny", discovered ? UiKit.Lime : UiKit.Muted));
                    if (discovered) card.Add(UiKit.Text(structure.description, "fr-muted"));
                    if (!structure.Implemented)
                        card.Add(Note(UiText.Get("atlas.structure.unimplemented"), UiKit.Amber));
                    body.Add(card);
                }
            }
        }

        void OpenAtlasResource(ResourceSO resource)
        {
            if (resource == null) return;
            atlasCategory = resource.category;
            atlasResourceId = resource.id;
            atlasTab = 0;
            if (resource.IsRaw)
            {
                CloseModal();
                OpenMapForResource(resource.id);
                return;
            }
            menuPage = "atlas";
            OpenModal("menu", "menu", UiText.Get("nav.menu"), UiKit.Cyan, BuildMenu, true);
        }

        void BuildAtlasBestiary(VisualElement body)
        {
            if (gs.db.enemies == null || gs.db.enemies.Length == 0)
            {
                body.Add(UiKit.Text(UiText.Get("atlas.bestiary.empty"), "fr-muted"));
                return;
            }
            foreach (var enemy in gs.db.enemies)
            {
                if (enemy == null) continue;
                bool discovered = gs.IsAtlasDiscovered("enemy", enemy.id);
                var card = Card(discovered ? enemy.color : UiKit.Muted);
                var header = UiKit.Div("fr-card-head");
                header.Add(Icons.Make("alien",
                    discovered ? UiKit.Ui(enemy.color) : new Color(0.16f, 0.2f, 0.27f), UiKit.Size.IconLg));
                var text = UiKit.Div("fr-card-text");
                text.Add(UiKit.Text(discovered ? enemy.displayName.ToUpper() : UiText.Get("atlas.enemy.unknown"), "fr-h2"));
                text.Add(UiKit.Text(discovered ? UiText.Get("atlas.enemy.summary", enemy.minWave, enemy.threat)
                    : UiText.Get("atlas.enemy.reveal"), "fr-tiny"));
                header.Add(text);
                card.Add(header);
                if (!discovered) { body.Add(card); continue; }
                var stats = UiKit.Div("fr-row-wrap");
                stats.Add(UiKit.Chip("heart", UiKit.Red, UiText.Get("atlas.enemy.health", enemy.health)));
                stats.Add(UiKit.Chip("alien", UiKit.Magenta, UiText.Get("atlas.enemy.damage", enemy.damage)));
                if (enemy.rank != AlienRank.Standard)
                    stats.Add(UiKit.Chip("alien", UiKit.Amber, UiText.Get("wave.rank." + enemy.rank.ToString().ToLowerInvariant())));
                if (enemy.armor > 0f)
                    stats.Add(UiKit.Chip("shield", UiKit.Cyan, UiText.Get("atlas.enemy.armor", Mathf.RoundToInt(enemy.armor * 100f))));
                card.Add(stats);
                if (enemy.rank == AlienRank.Boss)
                    card.Add(UiKit.Text(UiText.Get("atlas.enemy.boss-return", enemy.bossInterval), "fr-small"));
                if (enemy.bounty != null && enemy.bounty.Count > 0)
                {
                    card.Add(UiKit.Text(UiText.Get("atlas.enemy.reward"), "fr-tiny"));
                    foreach (var item in enemy.bounty)
                        if (item.resource != null) card.Add(RecipeResource(item.resource, item.amount));
                }
                body.Add(card);
            }
        }

        static string PrettyName(string value)
        {
            if (string.IsNullOrEmpty(value)) return UiText.Get("common.unknown");
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
            var parts = words.ToArray();
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Length > 0)
                    parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1).ToLowerInvariant();
            return string.Join(" ", parts);
        }
    }
}
