using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public partial class UIRoot
    {
        VisualElement dragGhost, dragSource;
        int inventoryDragPointer;

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
            OpenModal("warehouse", "warehouse", warehouse.def.displayName.ToUpper(), UiKit.Cyan,
                body => BuildWarehouse(body, warehouse), true);
            planetView.Frozen = true;
            world.Selected = anchor;
        }

        void OpenRelay(PlacedStructure relay, Transform anchor)
        {
            OpenModal("orbital-relay", "satellite", relay.def.displayName.ToUpper(), UiKit.Cyan,
                body => BuildRelay(body, relay), true);
            planetView.Frozen = true;
            world.Selected = anchor;
        }

        void BuildRelay(VisualElement body, PlacedStructure relay)
        {
            int radius = relay.def.CapacityAt(relay.level);
            body.Add(UiKit.Text(gs.planet.Name + " · LV " + relay.level + " · " +
                (radius > 0 ? radius + " LY beam" : "system beam"), "fr-muted"));
            body.Add(UiKit.Text("Pull stacks from operational storage on other visited worlds. Each pull costs energy based on stack size and distance; only fuel in your backpack can pay. Fuel Depot reserves stay isolated.", "fr-tiny"));
            var sources = gs.RelayWarehouses(relay);
            if (sources.Count == 0)
            {
                body.Add(UiKit.Text("No remote warehouses are in range. Build storage on another visited world, or upgrade the relay to extend its beam.", "fr-small"));
                return;
            }

            foreach (var source in sources)
            {
                var sourcePlanet = gs.universe.Get(source.planetId);
                if (sourcePlanet == null) continue;
                double distance = gs.RelayDistanceLy(relay, source);
                string range = distance <= 0.0 ? "SAME SYSTEM" : distance.ToString("0.#") + " LY";
                var card = Card(UiKit.Cyan);
                card.Add(UiKit.Text(sourcePlanet.Name + " · " + source.def.displayName.ToUpperInvariant(), "fr-h2"));
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
                    row.Add(ResourceIconChip(resource, resource.displayName + " ×" + stack.count));
                    string action = amount > 0 ? "PULL " + amount + " · " + energy + " ENERGY" : "BACKPACK FULL";
                    var pull = IconBtn(action, "pack", UiKit.Cyan, () =>
                    {
                        int moved = gs.WithdrawThroughRelay(relay, source, slotIndex, amount);
                        if (moved > 0) { Sfx.Good(); Toast("Relayed " + moved + " " + resource.displayName, UiKit.Cyan); }
                        else Toast("Transfer blocked: check backpack space and carried fuel.", UiKit.Amber);
                        RefreshModal();
                    });
                    UiKit.SetEnabledLook(pull, amount > 0 && energy <= gs.FuelOnBoard.Energy);
                    row.Add(pull);
                    card.Add(row);
                }
                if (!hasStacks) card.Add(UiKit.Text("No stored resources", "fr-tiny"));
                body.Add(card);
            }
        }

        void BuildWarehouse(VisualElement body, PlacedStructure warehouse)
        {
            SyncPack();
            var storage = gs.StorageFor(warehouse);
            int capacity = warehouse.def.StorageCapacity(warehouse.level);
            int used = storage.slots.FindAll(s => s.count > 0).Count;
            body.Add(UiKit.Text(gs.planet.Name + "  -  LV " + warehouse.level + "  -  " + used + " / " + capacity + " storage slots", "fr-muted"));
            bool fuelOnly = warehouse.def.kind == StructureKind.FuelDepot;
            bool cargoDepot = warehouse.def.kind == StructureKind.CargoDepot;
            bool autoHarvester = warehouse.def.kind == StructureKind.AutoHarvester;
            bool cargoPoolActive = gs.StructureOfKind(gs.planet, StructureKind.CargoDepot) != null;
            body.Add(UiKit.Text(fuelOnly
                ? "Fuel Depot stores ship-fuel resources only. Its upgraded racks hold larger fuel stacks; reserves stay on this planet and never pay travel costs."
                : autoHarvester
                    ? "This hopper stores the Auto-Harvester's offline output. A local Cargo Depot connects its materials to this planet's shared build and craft pool."
                    : cargoDepot
                    ? "This shared buffer joins operational Warehouses and Auto-Harvester hoppers on this planet. Build and craft costs draw from the pool automatically; travel still uses your backpack."
                    : cargoPoolActive
                        ? "Connected to this planet's Cargo Depot. Build and craft costs can draw from this storage automatically."
                        : "Drag a stack between the grids. Resources stay on this planet. Costs use the backpack until a Cargo Depot connects its storage.", "fr-tiny"));
            if (autoHarvester)
                body.Add(UiKit.Text("OFFLINE " + warehouse.def.EffectAt(warehouse.level).ToString("0.#") +
                    " run-eq/hr  ·  " + warehouse.def.CapacityAt(warehouse.level) + "h cap", "fr-small"));

            var packSlots = new List<VisualElement>();
            var storageSlots = new List<VisualElement>();
            body.Add(SectionLabel(fuelOnly ? "FUEL RESERVE" : autoHarvester ? "HARVESTER HOPPER" :
                cargoDepot ? "SHARED BUFFER" : "WAREHOUSE"));
            var storageGrid = UiKit.Div("fr-grid");
            for (int i = 0; i < capacity; i++)
            {
                int index = i;
                var stack = storage.slots[index];
                var resource = gs.db.FindResource(stack.resourceId);
                var slot = Slot(resource, stack.count, false);
                BindInventoryDrag(slot, resource, stack.count, () =>
                {
                    if (resource != null) Toast(resource.displayName + ": " + stack.count + " stored", UiKit.Ui(resource.color));
                }, position =>
                {
                    int target = SlotAt(storageSlots, position);
                    if (target >= 0)
                    {
                        if (!gs.MoveStoredSlot(warehouse, index, target) && target != index)
                            Toast("That stack is full.", UiKit.Red);
                        return;
                    }
                    target = SlotAt(packSlots, position);
                    if (target < 0 || resource == null) return;
                    if (packRes[target] != null && packRes[target] != resource)
                    {
                        Toast("Drop on an empty slot or the same resource.", UiKit.Red); return;
                    }
                    int room = Mathf.Max(0, resource.stackSize - packCnt[target]);
                    int moved = gs.Withdraw(warehouse, index, room, false);
                    if (moved == 0) { Toast("Backpack slot is full.", UiKit.Red); return; }
                    packRes[target] = resource; packCnt[target] += moved;
                    Sfx.Click(); gs.Notify();
                });
                storageSlots.Add(slot); storageGrid.Add(slot);
            }
            SizeInventoryGrid(storageGrid, storageSlots);
            body.Add(storageGrid);
            body.Add(SectionLabel("BACKPACK  -  " + gs.SlotsUsed + " / " + gs.Slots));
            var packGrid = UiKit.Div("fr-grid");
            for (int i = 0; i < packRes.Length; i++)
            {
                int index = i;
                var resource = packRes[index];
                var slot = Slot(resource, packCnt[index], false);
                BindInventoryDrag(slot, resource, packCnt[index], () =>
                {
                    if (resource != null) Toast(resource.displayName + ": " + packCnt[index] + " carried", UiKit.Ui(resource.color));
                }, position =>
                {
                    int target = SlotAt(storageSlots, position);
                    if (target >= 0 && resource != null)
                    {
                        if (!gs.CanStore(warehouse, resource))
                        {
                            Toast("Fuel depots only accept resources that power the ship.", UiKit.Red); return;
                        }
                        int moved = gs.Deposit(warehouse, target, resource, packCnt[index], false);
                        if (moved == 0) { Toast(fuelOnly ? "Fuel slot is full or contains another resource." : "Storage slot is full or contains another resource.", UiKit.Red); return; }
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
