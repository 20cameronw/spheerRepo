using System;
using System.Collections.Generic;

namespace Frontier
{
    public partial class GameState
    {
        /// <summary>Operational storage structures on other visited worlds that this local relay can reach.</summary>
        public List<PlacedStructure> RelayWarehouses(PlacedStructure relay)
        {
            var result = new List<PlacedStructure>();
            foreach (var source in structures)
            {
                if (CanRelayAccess(relay, source)) result.Add(source);
            }
            result.Sort((a, b) =>
            {
                PlanetData aPlanet = universe.Get(a.planetId);
                PlanetData bPlanet = universe.Get(b.planetId);
                int distance = RelayDistanceLy(relay, a).CompareTo(RelayDistanceLy(relay, b));
                if (distance != 0) return distance;
                int name = string.CompareOrdinal(aPlanet.Name, bPlanet.Name);
                return name != 0 ? name : a.slot.CompareTo(b.slot);
            });
            return result;
        }

        /// <summary>Light-year distance between the relay and a remote warehouse's systems.</summary>
        public double RelayDistanceLy(PlacedStructure relay, PlacedStructure source)
        {
            PlanetData origin = relay == null ? null : universe.Get(relay.planetId);
            PlanetData destination = source == null ? null : universe.Get(source.planetId);
            if (origin == null || destination == null) return double.PositiveInfinity;
            if (origin.Star.Id == destination.Star.Id) return 0.0;
            double dx = origin.Star.X - destination.Star.X;
            double dy = origin.Star.Y - destination.Star.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Energy units for a remote pull: one unit per ten items, scaled by distance and the relay's authored multiplier.</summary>
        public int RelayTransferEnergyCost(PlacedStructure relay, PlacedStructure source, int amount)
        {
            if (amount <= 0) return 0;
            if (!CanRelayAccess(relay, source)) return int.MaxValue;
            double rangeFactor = 1.0 + RelayDistanceLy(relay, source) / 100.0;
            double relayFactor = Math.Max(0.01, relay.def.EffectAt(relay.level));
            double energy = Math.Ceiling(amount / 10.0 * rangeFactor * relayFactor);
            return (int)Math.Min(int.MaxValue, Math.Max(1.0, energy));
        }

        /// <summary>Pulls up to <paramref name="amount"/> from a reachable warehouse into the backpack, paying backpack energy only.</summary>
        public int WithdrawThroughRelay(PlacedStructure relay, PlacedStructure source, int slotIndex, int amount)
        {
            if (!CanRelayAccess(relay, source) || amount <= 0) return 0;
            var storage = StorageFor(source);
            if (slotIndex < 0 || slotIndex >= storage.slots.Count) return 0;
            var slot = storage.slots[slotIndex];
            ResourceSO resource = slot == null ? null : db.FindResource(slot.resourceId);
            if (resource == null || slot.count <= 0) return 0;

            int moved = Math.Min(amount, Math.Min(slot.count, backpack.FreeSpace(resource.id)));
            if (moved <= 0) return 0;
            int energyCost = RelayTransferEnergyCost(relay, source, moved);
            var payment = Rules.Pay(energyCost, FuelOnBoard);
            if (!payment.ok) return 0;

            SpendEnergy(payment);
            int added = backpack.Add(resource.id, moved);
            if (added != moved)
                throw new InvalidOperationException("Relay transfer did not fit after checking backpack space.");
            slot.count -= moved;
            if (slot.count == 0) slot.resourceId = null;
            RecordAtlasResourceAcquired(resource.id);
            world.Touch();
            Notify();
            return moved;
        }

        bool CanRelayAccess(PlacedStructure relay, PlacedStructure source)
        {
            if (relay == null || relay.def.kind != StructureKind.Satellite || relay.planetId != planet.Id ||
                !IsPlaced(relay) || !IsOperational(relay) || source == null || !IsPlaced(source) ||
                source.planetId == relay.planetId || !source.def.kind.IsStorage() ||
                source.def.kind == StructureKind.FuelDepot || !IsOperational(source) ||
                world.Visits(source.planetId) <= 0) return false;

            PlanetData origin = universe.Get(relay.planetId);
            PlanetData destination = universe.Get(source.planetId);
            if (origin == null || destination == null) return false;
            if (origin.Star.Id == destination.Star.Id) return true;

            double radius = Math.Max(0, relay.def.CapacityAt(relay.level));
            return radius > 0.0 && RelayDistanceLy(relay, source) <= radius;
        }
    }
}
