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
                    var icon = Icons.Make(Icons.ForResource(resource), UiKit.Ui(resource.color), 44f);
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
                    dragGhost.style.left = local.x - 28f;
                    dragGhost.style.top = local.y - 72f;
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
            OpenModal("warehouse", "base", warehouse.def.displayName.ToUpper(), UiKit.Cyan,
                body => BuildWarehouse(body, warehouse), true);
            planetView.Frozen = true;
            world.Selected = anchor;
        }

        void BuildWarehouse(VisualElement body, PlacedStructure warehouse)
        {
            SyncPack();
            var storage = gs.StorageFor(warehouse);
            int capacity = warehouse.def.StorageCapacity(warehouse.level);
            int used = storage.slots.FindAll(s => s.count > 0).Count;
            body.Add(UiKit.Text(gs.planet.displayName + "  -  LV " + warehouse.level + "  -  " + used + " / " + capacity + " storage slots", "fr-muted"));
            body.Add(UiKit.Text("Drag a stack between the grids. Resources stay on this planet. If a stack won't fit, only the amount that fits is moved.", "fr-tiny"));

            var packSlots = new List<VisualElement>();
            var storageSlots = new List<VisualElement>();
            body.Add(SectionLabel("WAREHOUSE"));
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
                        int moved = gs.Deposit(warehouse, target, resource, packCnt[index], false);
                        if (moved == 0) { Toast("Storage slot is full or contains another resource.", UiKit.Red); return; }
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
                float cell = Mathf.Floor(grid.resolvedStyle.width / PackColumns);
                if (float.IsNaN(cell) || cell < 8f) return;
                foreach (var slot in slots) { slot.style.width = cell; slot.style.height = cell; }
            });
        }
    }
}
