using System;
using System.Collections.Generic;
using Frontier.Economy;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public partial class UIRoot
    {
        VisualElement dragGhost, dragSource;
        int inventoryDragPointer;
        ResourceCategory? warehouseCategoryFilter;

        void OnDisable() { EndInventoryDrag(); }
        void OnApplicationFocus(bool focused) { if (!focused) EndInventoryDrag(); }

        void EndInventoryDrag()
        {
            if (dragGhost != null) dragGhost.RemoveFromHierarchy();
            dragGhost = null;
            var source = dragSource;
            dragSource = null;
            if (source != null)
            {
                source.style.opacity = 1f;
                if (source.HasPointerCapture(inventoryDragPointer)) source.ReleasePointer(inventoryDragPointer);
            }
        }

        void BindInventoryDrag(VisualElement slot, ResourceSO resource, int count, Action tap, Action<Vector2> drop)
        {
            Vector2 down = Vector2.zero;
            bool pressed = false, dragging = false;
            slot.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                EndInventoryDrag();
                pressed = true; dragging = false; down = e.position;
                dragSource = slot; inventoryDragPointer = e.pointerId;
                slot.CapturePointer(e.pointerId);
                e.StopPropagation();
            });
            slot.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!pressed || e.pointerId != inventoryDragPointer || resource == null) return;
                if (!dragging && ((Vector2)e.position - down).magnitude >= 14f)
                {
                    dragging = true;
                    slot.style.opacity = 0.5f;
                    dragGhost = UiKit.Div("fr-drag-ghost");
                    dragGhost.pickingMode = PickingMode.Ignore;
                    var icon = Icons.Make(Icons.ForResource(resource), UiKit.Ui(resource.color), UiKit.Size.IconLg);
                    icon.pickingMode = PickingMode.Ignore;
                    dragGhost.Add(icon);
                    var label = UiKit.Text(count.ToString(), "fr-drag-count");
                    label.pickingMode = PickingMode.Ignore;
                    dragGhost.Add(label);
                    root.Add(dragGhost);
                }
                if (dragGhost != null)
                {
                    Vector2 local = root.WorldToLocal(e.position);
                    dragGhost.style.left = local.x - 32f;
                    dragGhost.style.top = local.y - 80f;
                    e.StopPropagation();
                }
            });
            slot.RegisterCallback<PointerUpEvent>(e =>
            {
                if (!pressed || e.pointerId != inventoryDragPointer) return;
                pressed = false;
                bool moved = dragging;
                EndInventoryDrag();
                e.StopPropagation();
                if (moved) drop(e.position);
                else tap();
            });
            slot.RegisterCallback<PointerCancelEvent>(e => { pressed = false; EndInventoryDrag(); });
            slot.RegisterCallback<PointerCaptureOutEvent>(e =>
            {
                pressed = false;
                if (dragSource == slot) EndInventoryDrag();
            });
        }

        static int SlotAt(List<VisualElement> slots, Vector2 position)
        {
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].worldBound.Contains(position)) return i;
            return -1;
        }

        void OpenWarehouse(PlacedStructure warehouse, Transform anchor)
        {
            OpenModal("warehouse", "warehouse", warehouse.def.LocalizedName.ToUpper(), UiKit.Cyan,
                body => BuildWarehouse(body, warehouse), true);
            planetView.Frozen = true;
            world.Selected = anchor;
        }

        void OpenRelay(PlacedStructure relay, Transform anchor)
        {
            OpenModal("orbital-relay", "satellite", relay.def.LocalizedName.ToUpper(), UiKit.Cyan,
                body => BuildRelay(body, relay), true);
            planetView.Frozen = true;
            world.Selected = anchor;
        }

        void BuildRelay(VisualElement body, PlacedStructure relay)
        {
            int radius = relay.def.CapacityAt(relay.level);
            body.Add(UiKit.Text(UiText.Get("relay.summary", gs.planet.Name, relay.level,
                UiText.Get(radius > 0 ? "relay.beam.range" : "relay.beam.system", radius)), "fr-muted"));
            body.Add(UiKit.Text(UiText.Get("relay.help"), "fr-tiny"));
            var sources = gs.RelayWarehouses(relay);
            if (sources.Count == 0)
            {
                body.Add(UiKit.Text(UiText.Get("relay.empty"), "fr-small"));
                var map = IconBtn(UiText.Get("guide.action.map"), "map", UiKit.Cyan, () => { CloseModal(); OpenMap(); });
                map.name = "empty-relay-action";
                body.Add(map);
                return;
            }

            foreach (var source in sources)
            {
                var sourcePlanet = gs.universe.Get(source.planetId);
                if (sourcePlanet == null) continue;
                double distance = gs.RelayDistanceLy(relay, source);
                string range = UiText.Get(distance <= 0.0 ? "relay.range.system" : "relay.range.distance", distance);
                var card = Card(UiKit.Cyan);
                card.Add(UiKit.Text(sourcePlanet.Name + " · " + source.def.LocalizedName.ToUpperInvariant(), "fr-h2"));
                card.Add(UiKit.Text(sourcePlanet.SystemName + " · " + range, "fr-tiny"));
                var storage = gs.StorageFor(source);
                bool hasStacks = false;
                for (int i = 0; i < storage.slots.Count; i++)
                {
                    var stack = storage.slots[i];
                    var resource = stack == null ? null : gs.db.FindResource(stack.resourceId);
                    if (resource == null || stack.count <= 0) continue;
                    hasStacks = true;
                    int slotIndex = i;
                    int amount = Math.Min(stack.count, gs.FreeSpace(resource));
                    int energy = amount > 0 ? gs.RelayTransferEnergyCost(relay, source, amount) : 0;
                    var row = UiKit.Div("fr-row-wrap");
                    row.Add(ResourceIconChip(resource, resource.LocalizedName + " ×" + stack.count));
                    string action = UiText.Get(amount > 0 ? "relay.pull" : "storage.pack-full", amount, energy);
                    var pull = IconBtn(action, "pack", UiKit.Cyan, () =>
                    {
                        int moved = gs.WithdrawThroughRelay(relay, source, slotIndex, amount);
                        if (moved > 0) { Sfx.Good(); Toast(UiText.Get("relay.transferred", moved, resource.LocalizedName), UiKit.Cyan); }
                        else Toast(UiText.Get("relay.blocked"), UiKit.Amber);
                        RefreshModal();
                    });
                    UiKit.SetEnabledLook(pull, amount > 0 && energy <= gs.FuelOnBoard.Energy);
                    row.Add(pull);
                    card.Add(row);
                }
                if (!hasStacks) card.Add(UiKit.Text(UiText.Get("relay.storage-empty", sourcePlanet.Name), "fr-tiny"));
                body.Add(card);
            }
        }

        void BuildWarehouse(VisualElement body, PlacedStructure warehouse)
        {
            SyncPack();
            var storage = gs.StorageFor(warehouse);
            int capacity = warehouse.def.StorageCapacity(warehouse.level);
            int used = storage.slots.FindAll(s => s.count > 0).Count;
            body.Add(UiKit.Text(UiText.Get("storage.summary", gs.planet.Name, warehouse.level, used, capacity), "fr-muted"));
            bool fuelOnly = warehouse.def.kind == StructureKind.FuelDepot;
            bool cargoDepot = warehouse.def.kind == StructureKind.CargoDepot;
            bool autoHarvester = warehouse.def.kind == StructureKind.AutoHarvester;
            bool cargoPoolActive = gs.StructureOfKind(gs.planet, StructureKind.CargoDepot) != null;
            body.Add(UiKit.Text(UiText.Get(fuelOnly
                ? "storage.help.fuel"
                : autoHarvester
                    ? "storage.help.harvester"
                    : cargoDepot
                    ? "storage.help.cargo"
                    : cargoPoolActive
                        ? "storage.help.connected"
                        : "storage.help.warehouse"), "fr-tiny"));
            if (autoHarvester)
                body.Add(UiKit.Text(UiText.Get("storage.offline", warehouse.def.EffectAt(warehouse.level),
                    warehouse.def.CapacityAt(warehouse.level)), "fr-small"));

            var categories = (ResourceCategory[])Enum.GetValues(typeof(ResourceCategory));
            var filter = new DropdownField();
            filter.choices = new List<string> { UiText.Get("storage.filter.all") };
            foreach (var category in categories) filter.choices.Add(UiText.Get("resource.category." + category.ToString().ToLowerInvariant()));
            filter.index = warehouseCategoryFilter.HasValue
                ? Array.IndexOf(categories, warehouseCategoryFilter.Value) + 1
                : 0;
            filter.tooltip = UiText.Get("storage.filter.help");
            filter.RegisterValueChangedCallback(e =>
            {
                int index = filter.choices.IndexOf(e.newValue);
                if (index < 0) return;
                warehouseCategoryFilter = index == 0 ? (ResourceCategory?)null : categories[index - 1];
                RefreshModal();
            });
            body.Add(filter);

            var quickActions = UiKit.Div("fr-row-wrap");
            quickActions.Add(IconBtn(UiText.Get("storage.quick-store"), "pack", UiKit.Cyan, () => QuickStore(warehouse)));
            quickActions.Add(IconBtn(UiText.Get("storage.quick-withdraw"), "warehouse", UiKit.Cyan, () => QuickWithdraw(warehouse)));
            body.Add(quickActions);

            var packSlots = new List<VisualElement>();
            var storageSlots = new List<VisualElement>();
            body.Add(SectionLabel(UiText.Get(fuelOnly ? "storage.title.fuel" : autoHarvester ? "storage.title.harvester" :
                cargoDepot ? "storage.title.cargo" : "storage.title.warehouse")));
            var storageGrid = UiKit.Div("fr-grid");
            for (int i = 0; i < capacity; i++)
            {
                int index = i;
                var stack = storage.slots[index];
                var resource = gs.db.FindResource(stack.resourceId);
                var slot = Slot(resource, stack.count, false);
                if (resource != null && !PassWarehouseFilter(resource)) slot.style.display = DisplayStyle.None;
                BindInventoryDrag(slot, resource, stack.count, () =>
                {
                    if (resource != null) Toast(UiText.Get("storage.stored-count", resource.LocalizedName, stack.count), UiKit.Ui(resource.color));
                }, position =>
                {
                    int target = SlotAt(storageSlots, position);
                    if (target >= 0)
                    {
                        if (!gs.MoveStoredSlot(warehouse, index, target) && target != index)
                            Toast(UiText.Get("storage.stack-full"), UiKit.Red);
                        return;
                    }
                    target = SlotAt(packSlots, position);
                    if (target < 0 || resource == null) return;
                    if (packRes[target] != null && packRes[target] != resource)
                    {
                        Toast(UiText.Get("storage.drop-help"), UiKit.Red); return;
                    }
                    int room = Mathf.Max(0, resource.stackSize - packCnt[target]);
                    int moved = gs.Withdraw(warehouse, index, room, false);
                    if (moved == 0) { Toast(UiText.Get("storage.pack-slot-full"), UiKit.Red); return; }
                    packRes[target] = resource; packCnt[target] += moved;
                    Sfx.Click(); gs.Notify();
                });
                storageSlots.Add(slot); storageGrid.Add(slot);
            }
            SizeInventoryGrid(storageGrid, storageSlots);
            body.Add(storageGrid);
            if (used == 0)
            {
                var hint = UiKit.Text(UiText.Get(autoHarvester
                    ? "storage.empty.harvester"
                    : gs.SlotsUsed == 0
                        ? "storage.empty.both"
                        : fuelOnly
                            ? "storage.empty.fuel"
                            : "storage.empty.warehouse"), "fr-tiny");
                hint.name = "empty-storage-hint";
                body.Add(hint);
                if (!autoHarvester && gs.SlotsUsed == 0)
                {
                    var gather = IconBtn(UiText.Get("storage.find-resources"), "mine", UiKit.Lime, RunEmptyPackAction);
                    gather.name = "empty-storage-action";
                    body.Add(gather);
                }
            }
            body.Add(SectionLabel(UiText.Get("storage.pack-capacity", gs.SlotsUsed, gs.Slots)));
            var packGrid = UiKit.Div("fr-grid");
            for (int i = 0; i < packRes.Length; i++)
            {
                int index = i;
                var resource = packRes[index];
                var slot = Slot(resource, packCnt[index], false);
                if (resource != null && !PassWarehouseFilter(resource)) slot.style.display = DisplayStyle.None;
                BindInventoryDrag(slot, resource, packCnt[index], () =>
                {
                    if (resource != null) Toast(UiText.Get("storage.carried-count", resource.LocalizedName, packCnt[index]), UiKit.Ui(resource.color));
                }, position =>
                {
                    int target = SlotAt(storageSlots, position);
                    if (target >= 0 && resource != null)
                    {
                        if (!gs.CanStore(warehouse, resource))
                        {
                            Toast(UiText.Get("storage.fuel-only"), UiKit.Red); return;
                        }
                        int moved = gs.Deposit(warehouse, target, resource, packCnt[index], false);
                        if (moved == 0) { Toast(UiText.Get(fuelOnly ? "storage.fuel-slot-full" : "storage.slot-full"), UiKit.Red); return; }
                        packCnt[index] -= moved;
                        if (packCnt[index] == 0) packRes[index] = null;
                        Sfx.Click(); gs.Notify(); return;
                    }
                    target = SlotAt(packSlots, position);
                    if (target >= 0 && target != index && resource != null)
                    {
                        MovePackSlot(index, target); Sfx.Click(); RefreshModal();
                    }
                });
                packSlots.Add(slot); packGrid.Add(slot);
            }
            SizeInventoryGrid(packGrid, packSlots);
            body.Add(packGrid);
            UpgradeButton(body, warehouse, UiKit.Cyan);
        }

        bool PassWarehouseFilter(ResourceSO resource)
        {
            return resource == null || !warehouseCategoryFilter.HasValue || resource.category == warehouseCategoryFilter.Value;
        }

        void QuickStore(PlacedStructure warehouse)
        {
            int moved = gs.QuickStore(warehouse, warehouseCategoryFilter, false);
            FinishQuickTransfer(moved, "storage.quick-stored", "storage.quick-store-empty");
        }

        void QuickWithdraw(PlacedStructure warehouse)
        {
            int moved = gs.QuickWithdraw(warehouse, warehouseCategoryFilter, false);
            FinishQuickTransfer(moved, "storage.quick-withdrew", "storage.quick-withdraw-empty");
        }

        void FinishQuickTransfer(int moved, string successKey, string emptyKey)
        {
            if (moved > 0)
            {
                gs.Notify();
                Sfx.Click();
                Toast(UiText.Get(successKey, moved), UiKit.Cyan);
            }
            else Toast(UiText.Get(emptyKey), UiKit.Muted);
            RefreshModal();
        }

        static void SizeInventoryGrid(VisualElement grid, List<VisualElement> slots)
        {
            grid.RegisterCallback<GeometryChangedEvent>(e =>
            {
                float cell = Mathf.Floor((grid.contentRect.width - 1f) / PackColumns);
                if (float.IsNaN(cell) || cell < 8f) return;
                foreach (var slot in slots) { slot.style.width = cell; slot.style.height = cell; }
            });
        }
    }
}
