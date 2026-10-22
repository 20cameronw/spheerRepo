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
                shopTab = source.build.kind.IsGather() ? 1 : 0;
                OpenShop();
                var scroll = modalScroll;
                var card = scroll.Q("shop-structure-" + source.build.id);
                if (card != null) scroll.schedule.Execute(() => scroll.ScrollTo(card)).StartingIn(1);
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
    }
}
