using System.Collections.Generic;
using UnsafeCollections.Collections.Native;
using UnsafeCollections.Collections.Unsafe;

namespace Bucket.Ecs.v3
{
#if UNITY
    using AllowUnsafePtr = Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestrictionAttribute;
    using WriteAccess = Unity.Collections.LowLevel.Unsafe.WriteAccessRequiredAttribute;
#else
    using AllowUnsafePtr = UnityAttribute;
    using WriteAccess = UnityAttribute;
#endif
    using Inline = System.Runtime.CompilerServices.MethodImplAttribute;

    public sealed partial class EcsWorld
    {
        private List<EcsFilter> _mainThreadFilters;
        private NativeArray<EcsUnmanagedFilter> _multiThreadFilters;

        [Inline(256)]
        private unsafe void InitFilters()
        {
            _mainThreadFilters = new List<EcsFilter>(Config.ExpectedFiltersAmount);
            _multiThreadFilters = new NativeArray<EcsUnmanagedFilter>(RUNTIME_REFERENCES.MultiThreadSystemsCount);
        }

        [Inline(256)]
        public unsafe EcsFilter.Mask CreateFilter()
        {
            if (_mainThreadFilters == null) BExceptionThrower.ObjectIsDisposed();
            
            EcsFilter filter = new EcsFilter(this);

            _mainThreadFilters.Add(filter);

            return filter.GetMask();
        }

        [Inline(256)]
        internal unsafe void RefreshArchetypesForFilter(ref EcsFilter filter)
        {
            foreach (var archetypeIndex in this)
            {
                ref var archetype = ref _archetypes[archetypeIndex];
                filter.TryAddArchetype(archetype.Id, archetype.ComponentsMask);
            }
        }

        [Inline(256)]
        internal unsafe EcsUnmanagedFilter GetOrCreateUnmanagedFilter(int systemId)
        {
            if (_multiThreadFilters.IsCreated == false) BExceptionThrower.ObjectIsDisposed();

            if (_multiThreadFilters[systemId].IsValid == false)
            {
                _multiThreadFilters[systemId] = new EcsUnmanagedFilter(RUNTIME_REFERENCES.UnmanagedComponentsCount);
            }

            return _multiThreadFilters[systemId];
        }

        [Inline(256)]
        internal unsafe void InitializeUnmanagedFilterWithAliveArchetypes(int systemId)
        {
            _multiThreadFilters[systemId].ClearArchetypes();
            foreach (var archetypeIndex in this)
            {
                ref var archetype = ref _archetypes[archetypeIndex];
                _multiThreadFilters[systemId].TryAddArchetype(archetype.Id, archetype.ComponentsMask);
            }
        }

        [Inline(256)]
        internal unsafe void OnArchetypeCreated(ArchetypeId id, BitSet* componentsMask)
        {
            for (var index = 0; index < _mainThreadFilters.Count; index++)
            {
                _mainThreadFilters[index].TryAddArchetype(id, componentsMask);
            }
            for (var index = 0; index < _multiThreadFilters.Length; index++)
            {
                if (_multiThreadFilters[index].IsValid)
                {
                    _multiThreadFilters[index].TryAddArchetype(id, componentsMask);
                }
            }
        }

        [Inline(256)]
        internal unsafe void OnArchetypeRecycled(ArchetypeId id)
        {
            for (var index = 0; index < _mainThreadFilters.Count; index++)
            {
                _mainThreadFilters[index].RemoveArchetype(id);
            }
            for (var index = 0; index < _multiThreadFilters.Length; index++)
            {
                if (_multiThreadFilters[index].IsValid)
                {
                    _multiThreadFilters[index].RemoveArchetype(id);
                }
            }
        }

        [Inline(256)]
        private unsafe void ReleaseFilters()
        {
            if (_mainThreadFilters != null)
            {
                for (var index = 0; index < _mainThreadFilters.Count; index++)
                {
                    _mainThreadFilters[index].Dispose();
                }

                _mainThreadFilters.Clear();
                _mainThreadFilters = null;
            }

            if (_multiThreadFilters.IsCreated)
            {
                for (var index = 0; index < _multiThreadFilters.Length; index++)
                {
                    _multiThreadFilters[index].Dispose();
                }

                _multiThreadFilters.Dispose();
            }
        }
    }
}