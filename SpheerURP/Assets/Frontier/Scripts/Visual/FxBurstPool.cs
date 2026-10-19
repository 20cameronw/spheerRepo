using UnityEngine;

namespace Frontier
{
    public static class FxBurstPool
    {
        const int Capacity = 24;
        static readonly ParticleSystem[] Systems = new ParticleSystem[Capacity];
        static int count, cursor;
        static GameObject root;

        public static int PoolCount { get { return count; } }

        public static void Emit(Vector3 position, Color color, int amount, float speed, float size)
        {
            if (root == null)
            {
                count = 0;
                cursor = 0;
                root = new GameObject("Frontier FX Burst Pool");
                root.AddComponent<FxBurstPoolLifetime>();
            }

            ParticleSystem system = Acquire();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.transform.position = position;
            var main = system.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = color;
            main.maxParticles = Mathf.Max(64, amount);
            system.Play(true);
            system.Emit(amount);
        }

        static ParticleSystem Acquire()
        {
            for (int offset = 0; offset < count; offset++)
            {
                int index = (cursor + offset) % count;
                if (Systems[index] == null)
                {
                    Systems[index] = FxTemplates.Create("Burst", root.transform).GetComponent<ParticleSystem>();
                    cursor = (index + 1) % Capacity;
                    return Systems[index];
                }
                if (Systems[index].IsAlive(true)) continue;
                cursor = (index + 1) % Mathf.Max(1, count);
                return Systems[index];
            }

            if (count < Capacity)
            {
                var system = FxTemplates.Create("Burst", root.transform).GetComponent<ParticleSystem>();
                Systems[count++] = system;
                cursor = count % Capacity;
                return system;
            }

            int steal = cursor;
            cursor = (cursor + 1) % Capacity;
            return Systems[steal];
        }

        static void Release(GameObject owner)
        {
            if (root != owner) return;
            root = null;
            count = 0;
            cursor = 0;
            for (int i = 0; i < Systems.Length; i++) Systems[i] = null;
        }

        sealed class FxBurstPoolLifetime : MonoBehaviour
        {
            void OnDestroy() { Release(gameObject); }
        }
    }
}
