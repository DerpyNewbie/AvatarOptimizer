using System;
using System.Collections.Generic;
using System.Reflection;
using JetBrains.Annotations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Anatawa12.AvatarOptimizer.PrefabSafeSet
{
    internal static class PrefabSafeSetRuntimeUtil
    {
        public static T[] ResizeArray<T>(T[] source, int size)
        {        
            var result = new T[size];
            Array.Copy(source, result, Math.Min(size, source.Length));
            return result;
        }

#if UNITY_EDITOR
        private static readonly Type OnBeforeSerializeImplType;

        static PrefabSafeSetRuntimeUtil()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                OnBeforeSerializeImplType =
                    assembly.GetType("Anatawa12.AvatarOptimizer.PrefabSafeSet.OnBeforeSerializeImpl`2");
                if (OnBeforeSerializeImplType != null) return;
            }
        }

        public static MethodInfo GetOnBeforeSerializeCallbackMethod(Type tType, Type tLayerType, Type setType)
        {
            var implType = OnBeforeSerializeImplType.MakeGenericType(tType, tLayerType);
            return implType.GetMethod("Impl", BindingFlags.Public | BindingFlags.Static, null, new[] { setType }, null);
        }
#endif
    }


    /// <summary>
    /// The serializable class to express hashset.
    /// using array will make prefab modifications too big so I made this class
    /// </summary>
    /// <typeparam name="T">Element Type</typeparam>
    /// <typeparam name="TLayer">Layer Type</typeparam>
    [Serializable]
    internal class PrefabSafeSet<T, TLayer> : ISerializationCallbackReceiver where TLayer : PrefabLayer<T>
    {
        [SerializeField] internal T[] mainSet = Array.Empty<T>();
        [SerializeField] internal TLayer[] prefabLayers = Array.Empty<TLayer>();

#if UNITY_EDITOR
        [SerializeField, HideInInspector] internal T fakeSlot;
        internal readonly Object OuterObject;
        internal T[] CheckedCurrentLayerRemoves;
        internal T[] CheckedCurrentLayerAdditions;
        private static MethodInfo _onBeforeSerializeCallback = PrefabSafeSetRuntimeUtil
            .GetOnBeforeSerializeCallbackMethod(typeof(T), typeof(TLayer), typeof(PrefabSafeSet<T, TLayer>));
#endif

        protected PrefabSafeSet(Object outerObject)
        {
#if UNITY_EDITOR
            if (!outerObject) throw new ArgumentNullException(nameof(outerObject));
            OuterObject = outerObject;
#endif
        }

        public HashSet<T> GetAsSet()
        {
            var result = new HashSet<T>(mainSet);
            foreach (var layer in prefabLayers)
                layer.ApplyTo(result);
            return result;
        }

        public List<T> GetAsList()
        {
            var set = new HashSet<T>(mainSet);
            var result = new List<T>(mainSet);
            foreach (var layer in prefabLayers)
                layer.ApplyTo(set, result);
            return result;
        }

        public void OnBeforeSerialize()
        {
#if UNITY_EDITOR
            _onBeforeSerializeCallback.Invoke(null, new object[] {this});
#endif
        }

        public void OnAfterDeserialize()
        {
            // there's nothing to do after deserialization.
        }
    }

    [Serializable]
    internal abstract class PrefabLayer<T>
    {
        // if some value is in both removes and additions, the values should be added
        [SerializeField] internal T[] removes = Array.Empty<T>();
        [SerializeField] internal T[] additions = Array.Empty<T>();

        public void ApplyTo(HashSet<T> result, [CanBeNull] List<T> list = null)
        {
            foreach (var remove in removes)
                if (result.Remove(remove))
                    list?.Remove(remove);
            foreach (var addition in additions)
                if (result.Add(addition))
                    list?.Add(addition);
        }
    }
    
    internal readonly struct ListSet<T>
    {
        [NotNull] private readonly List<T> _list;
        [NotNull] private readonly HashSet<T> _set;
        public ListSet(T[] initialize)
        {
            _list = new List<T>(initialize);
            _set = new HashSet<T>(initialize);
        }

        public void AddRange(IEnumerable<T> values)
        {
            foreach (var value in values)
                if (value != null && _set.Add(value))
                    _list?.Add(value);
        }

        public void RemoveRange(IEnumerable<T> values)
        {
            foreach (var value in values)
                if (value != null && _set.Remove(value))
                    _list?.Remove(value);
        }

        public T[] ToArray() => _list.ToArray();
    }
}
