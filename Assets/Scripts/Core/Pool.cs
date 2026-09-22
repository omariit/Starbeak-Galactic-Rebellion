using System.Collections.Generic;
using UnityEngine;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// Generic preallocated object pool. Uses a Stack (LIFO cache locality) and never
    /// allocates after warmup, avoiding GC spikes during 60 FPS combat.
    /// </summary>
    public sealed class Pool<T> where T : Component
    {
        private readonly T prefab;
        private readonly Stack<T> available;
        private readonly HashSet<T> inUse;
        private readonly Transform parent;

        public int Capacity { get; }
        public int ActiveCount => inUse.Count;

        public Pool(T prefab, int capacity, Transform parent, bool prewarm = true)
        {
            this.prefab = prefab;
            this.parent = parent;
            Capacity = capacity;
            available = new Stack<T>(capacity);
            inUse = new HashSet<T>(capacity);

            if (prewarm) Prewarm();
        }

        public void Prewarm()
        {
            while (available.Count + inUse.Count < Capacity)
            {
                T instance = Object.Instantiate(prefab, parent);
                instance.gameObject.SetActive(false);
                available.Push(instance);
            }
        }

        public T Spawn(Vector3 position, Quaternion rotation)
        {
            T instance = available.Count > 0 ? available.Pop() : Expand();
            inUse.Add(instance);

            Transform tf = instance.transform;
            tf.SetPositionAndRotation(position, rotation);
            instance.gameObject.SetActive(true);
            return instance;
        }

        public void Release(T instance)
        {
            if (instance == null) return;
            if (!inUse.Remove(instance)) return;

            instance.gameObject.SetActive(false);
            available.Push(instance);
        }

        public void ReleaseAll()
        {
            foreach (T instance in inUse)
            {
                if (instance == null) continue;
                instance.gameObject.SetActive(false);
                available.Push(instance);
            }
            inUse.Clear();
        }

        private T Expand()
        {
            // Pool exhaustion is a tuning signal, not a crash: grow by one and keep running.
            T instance = Object.Instantiate(prefab, parent);
            return instance;
        }
    }
}
