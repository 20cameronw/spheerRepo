using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public partial class UIRoot
    {
        void OpenResourceSource(ResourceSO resource)
        {
            if (busy || defense.Active) return;
            var source = gs.FindResourceSource(resource);
            CloseAll();
            if (source.kind == ResourceSourceKind.Structure)
            {
                if (world.SelectStructure(source.structure))
                {
                    if (source.recipe != null && popupScroll != null)
                    {
                        var scroll = popupScroll;
                        var recipeCard = scroll.Q("recipe-" + source.recipe.id);
                        if (recipeCard != null) scroll.schedule.Execute(() => scroll.ScrollTo(recipeCard)).StartingIn(1);
                    }
                    return;
                }
                Debug.LogError("Frontier: resource producer has no world anchor: " + source.structure.def.id);
            }
            if (source.kind == ResourceSourceKind.Build)
            {
                OpenBuildDefinition(source.build);
                return;
            }
            if (source.kind == ResourceSourceKind.Planet)
            {
                OpenMapForResource(source.resource.id);
                SelectMapWorld(source.planet);
                CenterOn(source.planet);
                Toast(UiText.Get("resource.source.planet", source.resource.LocalizedName, source.planet.Name), UiKit.Cyan);
                return;
            }
            OpenAtlasResource(resource);
            Toast(UiText.Get("resource.source.unavailable", resource.LocalizedName), UiKit.Amber);
        }

        void OpenBuildDefinition(StructureSO definition)
        {
            CloseAll();
            switch (definition.kind)
            {
                case StructureKind.ResearchLab:
                case StructureKind.RadarTower:
                case StructureKind.ColonyHub:
                case StructureKind.Satellite:
                case StructureKind.Terraformer:
                case StructureKind.WarpGate:
                case StructureKind.DysonCollector: shopTab = 3; break;
                case StructureKind.Gunner:
                case StructureKind.Laser: shopTab = 2; break;
                default: shopTab = definition.kind.IsGather() ? 1 : 0; break;
            }
            OpenShop();
            var scroll = modalScroll;
            scroll.schedule.Execute(() =>
            {
                var card = scroll.Q("shop-structure-" + definition.id);
                if (card != null) scroll.ScrollTo(card);
            }).StartingIn(1);
        }
    }
}
