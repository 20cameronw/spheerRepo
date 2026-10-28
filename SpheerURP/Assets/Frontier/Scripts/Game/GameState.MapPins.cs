using System;
using System.Collections.Generic;
using Frontier.Universe;

namespace Frontier
{
    public partial class GameState
    {
        readonly HashSet<string> mapDiscoverySet = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Saved canonical planet ids pinned on the star map.</summary>
        public IList<string> MapPins { get { return mapPins.AsReadOnly(); } }
        public string[] MapDiscoveryIds() { return mapDiscoveries.ToArray(); }

        public bool IsMapPinned(string planetId)
        {
            return !string.IsNullOrEmpty(planetId) && mapPins.Contains(planetId);
        }

        /// <summary>Toggles a known map destination and returns whether it is pinned after the change.</summary>
        public bool ToggleMapPin(PlanetData destination)
        {
            if (SaveIsReadOnly || destination == null || universe.Get(destination.Id) == null) return false;
            if (mapPins.Remove(destination.Id))
            {
                Notify();
                return false;
            }
            mapPins.Add(destination.Id);
            mapPins.Sort(StringComparer.Ordinal);
            Notify();
            return true;
        }

        public bool IsMapDiscovered(string systemId)
        {
            return !string.IsNullOrEmpty(systemId) && mapDiscoverySet.Contains(systemId);
        }

        /// <summary>Persists newly surveyed systems. The supplied keys must resolve in the current universe.</summary>
        public bool RecordMapDiscoveries(IEnumerable<StarSystem> systems)
        {
            if (SaveIsReadOnly || systems == null) return false;
            bool changed = false;
            foreach (StarSystem system in systems)
            {
                if (system == null || !system.Key.IsValid) continue;
                StarSystem known;
                if (!universe.Universe.TryGetSystem(system.Key, out known) || known.Id != system.Id) continue;
                if (mapDiscoverySet.Add(system.Id))
                {
                    mapDiscoveries.Add(system.Id);
                    changed = true;
                }
            }
            if (changed)
            {
                mapDiscoveries.Sort(StringComparer.Ordinal);
                saves.MarkDirty();
            }
            return changed;
        }
    }
}
