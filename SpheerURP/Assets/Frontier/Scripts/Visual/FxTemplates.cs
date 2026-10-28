using System;
using UnityEngine;

namespace Frontier
{
    public static class FxTemplates
    {
        public static GameObject Create(string name, Transform parent)
        {
            var prefab = Load<GameObject>(name);
            var instance = UnityEngine.Object.Instantiate(prefab, parent, false);
            instance.name = name;
            return instance;
        }

        public static T Load<T>(string name) where T : UnityEngine.Object
        {
            var asset = Resources.Load<T>("Frontier/FX/" + name);
            if (asset == null) throw new InvalidOperationException("Missing Frontier FX asset: " + name);
            return asset;
        }
    }
}
