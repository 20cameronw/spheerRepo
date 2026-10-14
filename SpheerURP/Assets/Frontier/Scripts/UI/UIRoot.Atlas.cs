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
            firstTabRow.Add(TabButton("core", "RESOURCES", atlasTab == 0, UiKit.Cyan, () => atlasTab = 0));
            firstTabRow.Add(TabButton("refinery", "RECIPES", atlasTab == 1, UiKit.Cyan, () => atlasTab = 1));
            tabs.Add(firstTabRow);
            var secondTabRow = UiKit.Div("fr-tabs");
            secondTabRow.Add(TabButton("map", "WORLDS", atlasTab == 2, UiKit.Cyan, () => atlasTab = 2));
            secondTabRow.Add(TabButton("alien", "FIELD GUIDE", atlasTab == 3, UiKit.Cyan, () => atlasTab = 3));
            tabs.Add(secondTabRow);
            body.Add(tabs);

            body.Add(UiKit.Text("FIELD NOTES  /  CATALOG DATA", "fr-tiny", UiKit.Muted));
            body.Add(UiKit.Text("RESOURCES  " + gs.AtlasDiscoveredCount("resource") + "/" + gs.AtlasCatalogCount("resource") +
                "  ·  RECIPES  " + gs.AtlasDiscoveredCount("recipe") + "/" + gs.AtlasCatalogCount("recipe"), "fr-small"));
            body.Add(UiKit.Text("STRUCTURES  " + gs.AtlasDiscoveredCount("structure") + "/" + gs.AtlasCatalogCount("structure"), "fr-small"));
            body.Add(UiKit.Text("Find resources, craft recipes and complete builds to discover entries and earn one-time Nova Shards. World visits are tracked separately.", "fr-muted"));
            switch (atlasTab)
            {
                case 0: BuildAtlasResources(body); break;
                case 1: BuildAtlasRecipes(body); break;
                case 2: BuildAtlasWorlds(body); break;
                default: BuildAtlasFieldGuide(body); break;
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
            var categoryLabel = UiKit.Text("RESOURCE FAMILY", "fr-tiny");
            categoryLabel.style.marginTop = UiKit.Space.S12;
            body.Add(categoryLabel);
            var category = new DropdownField();
            category.choices = Enum.GetNames(typeof(ResourceCategory)).Select(PrettyName).ToList();
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
                body.Add(UiKit.Text("No resources are configured for this family.", "fr-muted"));
                return;
            }

            int selectedIndex = resources.FindIndex(r => r.id == atlasResourceId);
            if (selectedIndex < 0) selectedIndex = 0;
            atlasResourceId = resources[selectedIndex].id;
            var resourceLabel = UiKit.Text("RESOURCE", "fr-tiny");
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
            heading.Add(UiKit.Text(resource.category + "  /  TIER " + resource.tier + "  /  STACK " + resource.stackSize, "fr-tiny"));
            header.Add(heading);
            card.Add(header);
            card.Add(UiKit.Text(
                gs.IsAtlasDiscovered("resource", resource.id)
                    ? "DISCOVERED"
                    : "NOT DISCOVERED  ·  FIRST FIND EARNS SHARDS",
                "fr-tiny",
                gs.IsAtlasDiscovered("resource", resource.id) ? UiKit.Lime : UiKit.Muted));
            if (!string.IsNullOrEmpty(resource.DetailText)) card.Add(UiKit.Text(resource.DetailText, "fr-muted"));

            card.Add(SectionLabel("WHERE TO FIND IT"));
            int habitatCount = 0;
            if (resource.IsRaw && gs.db.habitats != null && gs.db.habitats.rows != null)
            {
                foreach (var habitat in gs.db.habitats.rows)
                {
                    if (habitat == null || habitat.resourceId != resource.id) continue;
                    string area = habitat.belt ? "Asteroid belt" : PrettyName(habitat.biome.ToString());
                    card.Add(UiKit.Text(area + "  ·  " + PrettyName(habitat.method.ToString()) + "  ·  tier " + habitat.minTier + "+", "fr-small"));
                    habitatCount++;
                }
            }
            if (habitatCount == 0 && resource.harvestMethods != null)
                foreach (var method in resource.harvestMethods)
                    card.Add(UiKit.Text(PrettyName(method.ToString()) + " harvest", "fr-small"));
            if (habitatCount == 0 && (resource.harvestMethods == null || resource.harvestMethods.Length == 0))
                card.Add(UiKit.Text("Produced by processing or crafting.", "fr-small"));

            body.Add(card);
            BuildAtlasRelatedRecipes(body, resource);
            BuildAtlasBuildUses(body, resource);
            if (resource.IsRaw)
            {
                var find = IconBtn("FIND SURVEYED DEPOSITS", "map", UiKit.Cyan, () =>
                {
                    CloseModal();
                    OpenMapForResource(resource.id);
                });
                body.Add(find);
            }
        }

        void BuildAtlasBuildUses(VisualElement body, ResourceSO resource)
        {
            var uses = new List<string>();
            if (gs.db.structures != null)
            {
                foreach (var structure in gs.db.structures)
                    if (structure != null && structure.buildCost != null &&
                        structure.buildCost.Exists(item => item.resource == resource))
                        uses.Add(structure.displayName + "  ·  Structure");
            }
            if (gs.db.upgrades != null)
            {
                foreach (var upgrade in gs.db.upgrades)
                {
                    if (upgrade == null) continue;
                    for (int level = 0; level < upgrade.MaxLevel; level++)
                    {
                        if (upgrade.CostAt(level).Exists(item => item.resource == resource))
                            uses.Add(upgrade.NameAtLevel(level + 1) + "  ·  Upgrade");
                    }
                }
            }
            if (uses.Count == 0) return;
            body.Add(SectionLabel("ALSO USED TO BUILD / UPGRADE"));
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
                    body.Add(SectionLabel("MADE FROM  /  USED IN"));
                    headingAdded = true;
                }
                AddAtlasRecipeCard(body, recipe);
            }
            if (!headingAdded) body.Add(UiKit.Text("No recipes reference this resource yet.", "fr-muted"));
        }

        void BuildAtlasRecipes(VisualElement body)
        {
            if (gs.db.recipes == null || gs.db.recipes.Length == 0)
            {
                body.Add(UiKit.Text("No recipes are configured.", "fr-muted"));
                return;
            }
            body.Add(UiKit.Text("Follow the input-to-output flow. Station, tier and research gates are shown on each recipe.", "fr-muted"));
            var recipes = gs.db.recipes.Where(r => r != null)
                .OrderBy(r => r.tier).ThenBy(r => r.kind).ThenBy(r => r.displayName, StringComparer.Ordinal);
            foreach (var recipe in recipes) AddAtlasRecipeCard(body, recipe);
        }

        void AddAtlasRecipeCard(VisualElement body, RecipeSO recipe)
        {
            var card = Card(UiKit.Cyan);
            card.Add(UiKit.Text(recipe.displayName.ToUpper(), "fr-h2"));
            string gate = PrettyName(recipe.kind.ToString()) + "  ·  TIER " + recipe.tier + "  ·  " +
                PrettyName(recipe.stationId) + " LV " + recipe.stationLevel;
            if (!string.IsNullOrEmpty(recipe.unlockResearch)) gate += "  ·  RESEARCH: " + PrettyName(recipe.unlockResearch);
            card.Add(UiKit.Text(gate, "fr-tiny"));
            bool discovered = gs.IsAtlasDiscovered("recipe", recipe.id);
            card.Add(UiKit.Text(
                discovered ? "CRAFTED" : "NOT CRAFTED  ·  FIRST CRAFT EARNS SHARDS",
                "fr-tiny",
                discovered ? UiKit.Lime : UiKit.Muted));

            var flow = UiKit.Div("fr-recipe-flow");
            var inputs = UiKit.Div("fr-recipe-side");
            inputs.Add(UiKit.Text("INPUT", "fr-tiny"));
            if (recipe.inputs != null)
                foreach (var item in recipe.inputs)
                    if (item.resource != null) inputs.Add(RecipeResource(item.resource, item.amount));
            flow.Add(inputs);
            var arrow = UiKit.Text(">", "fr-h2", UiKit.Cyan);
            arrow.AddToClassList("fr-recipe-arrow");
            flow.Add(arrow);
            var outputs = UiKit.Div("fr-recipe-side");
            outputs.Add(UiKit.Text("OUTPUT", "fr-tiny"));
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
                body.Add(UiKit.Text("No visited worlds are recorded yet.", "fr-muted"));
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
            body.Add(UiKit.Text("VISITED WORLDS  ·  TRACKED SEPARATELY FROM CATALOG DISCOVERIES", "fr-tiny"));
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
            card.Add(UiKit.Text(planet.SystemName + "  ·  " + planet.BiomeName + "  ·  TIER " + planet.Tier + "  ·  " + planet.Descriptor.Kind, "fr-small"));
            card.Add(UiKit.Text("DANGER " + planet.Danger.ToString("0.0") + "  ·  HAZARDS " + (planet.Hazards == Hazards.None ? "NONE" : planet.Hazards.ToString()), "fr-tiny"));
            card.Add(SectionLabel("AVAILABLE RESOURCES"));
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

        void BuildAtlasFieldGuide(VisualElement body)
        {
            body.Add(SectionLabel("STRUCTURES"));
            if (gs.db.structures != null)
            {
                foreach (var structure in gs.db.structures)
                {
                    if (structure == null) continue;
                    var card = Card(structure.color);
                    var header = UiKit.Div("fr-card-head");
                    header.Add(Icons.Make(StructureIcon(structure.kind), UiKit.Ui(structure.color), UiKit.Size.IconLg));
                    var text = UiKit.Div("fr-card-text");
                    text.Add(UiKit.Text(structure.displayName.ToUpper(), "fr-h2"));
                    text.Add(UiKit.Text(structure.role + "  ·  TIER " + structure.tier + "  ·  " + (structure.Implemented ? "GAMEPLAY ACTIVE" : "CATALOGUE ONLY"), "fr-tiny"));
                    header.Add(text);
                    card.Add(header);
                    if (structure.Implemented)
                    {
                        bool discovered = gs.IsAtlasDiscovered("structure", structure.kind.ToString());
                        card.Add(UiKit.Text(
                            discovered ? "BUILT" : "NOT BUILT  ·  FIRST COMPLETED BUILD EARNS SHARDS",
                            "fr-tiny",
                            discovered ? UiKit.Lime : UiKit.Muted));
                    }
                    card.Add(UiKit.Text(structure.description, "fr-muted"));
                    if (!structure.Implemented)
                        card.Add(Note("This definition is in the content catalog; its gameplay behavior is not implemented.", UiKit.Amber));
                    body.Add(card);
                }
            }

            body.Add(SectionLabel("ALIENS"));
            if (gs.db.enemies == null || gs.db.enemies.Length == 0)
            {
                body.Add(UiKit.Text("No alien entries are configured.", "fr-muted"));
                return;
            }
            foreach (var enemy in gs.db.enemies)
            {
                if (enemy == null) continue;
                var card = Card(enemy.color);
                var header = UiKit.Div("fr-card-head");
                var dot = UiKit.Div("fr-dot");
                dot.style.width = UiKit.Size.IconLg;
                dot.style.height = UiKit.Size.IconLg;
                dot.style.backgroundColor = UiKit.Ui(enemy.color);
                header.Add(dot);
                var text = UiKit.Div("fr-card-text");
                text.Add(UiKit.Text(enemy.displayName.ToUpper(), "fr-h2"));
                text.Add(UiKit.Text("FIRST WAVE " + enemy.minWave + "  ·  THREAT " + enemy.threat.ToString("0.0"), "fr-tiny"));
                header.Add(text);
                card.Add(header);
                var stats = UiKit.Div("fr-row-wrap");
                stats.Add(UiKit.Chip("heart", UiKit.Red, "HP " + enemy.health.ToString("0")));
                stats.Add(UiKit.Chip("alien", UiKit.Magenta, "DMG " + enemy.damage.ToString("0")));
                card.Add(stats);
                if (enemy.bounty != null && enemy.bounty.Count > 0)
                {
                    card.Add(UiKit.Text("WAVE REWARD", "fr-tiny"));
                    foreach (var item in enemy.bounty)
                        if (item.resource != null) card.Add(RecipeResource(item.resource, item.amount));
                }
                body.Add(card);
            }
        }

        static string PrettyName(string value)
        {
            if (string.IsNullOrEmpty(value)) return "Unknown";
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
