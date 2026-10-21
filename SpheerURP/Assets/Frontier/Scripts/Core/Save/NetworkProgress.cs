using System;
using System.Collections.Generic;

namespace Frontier
{
    [Serializable]
    public sealed class PlanetLink
    {
        public string fromPlanetId, toPlanetId;
    }

    [Serializable]
    public sealed class NetworkProgress
    {
        public List<PlanetLink> links = new List<PlanetLink>();
        public bool completed;
        public string archivePlanetId = "";
    }
}
