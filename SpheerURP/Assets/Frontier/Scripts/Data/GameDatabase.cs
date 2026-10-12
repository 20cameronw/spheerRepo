using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    [CreateAssetMenu(fileName = "GameDatabase", menuName = "Frontier/Game Database")]
    public class GameDatabase : ScriptableObject
    {
        public ResourceSO[] resources;
        public PlanetSO[] planets;
        public UpgradeSO[] upgrades;
        public EnemySO[] enemies;
        public RecipeSO[] recipes;
        public StructureSO[] structures;
        public WaveFormulaSO formula;

        public ResourceSO fuel;
        public ResourceSO scrap;
        public PlanetSO startPlanet;
        public List<ItemAmount> startingItems = new List<ItemAmount>();

        public int baseBackpackSlots = 9;
        public float baseReactorHealth = 100f;
        public float fuelPerMapUnit = 1f;

        /// <summary>Loads the data asset from Resources, or builds the default content in memory.</summary>
        public static GameDatabase Load()
        {
            var db = Resources.Load<GameDatabase>("Frontier/GameDatabase");
            if (db == null) db = DefaultContent.Create();
            return db;
        }

        public PlanetSO FindPlanet(string id)
        {
            foreach (var p in planets) if (p != null && p.id == id) return p;
            return null;
        }

        public ResourceSO FindResource(string id)
        {
            foreach (var r in resources) if (r != null && r.id == id) return r;
            return null;
        }

        public UpgradeSO FindUpgrade(string id)
        {
            foreach (var u in upgrades) if (u != null && u.id == id) return u;
            return null;
        }

        public StructureSO FindStructure(string id)
        {
            if (structures == null) return null;
            foreach (var s in structures) if (s != null && s.id == id) return s;
            return null;
        }

        public StructureSO StructureOfKind(StructureKind k)
        {
            if (structures == null) return null;
            foreach (var s in structures) if (s != null && s.kind == k) return s;
            return null;
        }
    }
}
