using System;
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

    public unsafe struct EcsUnmanagedFilter
    {
        internal BitSet* _filterIncludeBits;
        internal BitSet* _filterExcludeBits;
        internal BitSet* _dependenciesBits;

        private UnsafeList* _matchingArchetypes;

        public bool IsValid => _filterIncludeBits != null && _filterExcludeBits != null && _dependenciesBits != null;

        public EcsUnmanagedFilter(int componentsCount)
        {
            _filterIncludeBits = BitSet.Allocate(componentsCount);
            _filterExcludeBits = BitSet.Allocate(componentsCount);
            _dependenciesBits = BitSet.Allocate(componentsCount * Config.FilterBitsPerDependency);
            
            _matchingArchetypes = UnsafeList.Allocate<int>(Config.ExpectedAmountOfArchetypesInFilter);
        }

        [Inline(256)]
        public Mask GetMask()
        {
            return new Mask(_filterIncludeBits, _filterExcludeBits, _dependenciesBits);
        }

        [Inline(256)]
        public void ClearArchetypes()
        {
            UnsafeList.Clear(_matchingArchetypes);
        }

        [Inline(256)]
        public void TryAddArchetype(ArchetypeId id, BitSet* componentsMask)
        {
            foreach ((int bit, bool set) in BitSet.GetEnumerator(componentsMask))
            {
                if (BitSet.IsSet(_filterExcludeBits, bit) && set) return;
                if (BitSet.IsSet(_filterIncludeBits, bit) && !set) return;
            }
            UnsafeList.Add(_matchingArchetypes, (int)id);
        }
        
        [Inline(256)]
        public void RemoveArchetype(ArchetypeId id)
        {
            UnsafeList.RemoveUnordered(_matchingArchetypes, (int)id);
            // _matchingArchetypes.SetValue(false, (int)id);
        }
        
        [Inline(256)]
        public void Dispose()
        {
            if (_filterIncludeBits != null)
            {
                BitSet.Free(_filterIncludeBits);
                _filterIncludeBits = null;
            }
            if (_filterExcludeBits != null)
            {
                BitSet.Free(_filterExcludeBits);
                _filterExcludeBits = null;
            }
            if (_dependenciesBits != null)
            {
                BitSet.Free(_dependenciesBits);
                _dependenciesBits = null;
            }
            if (_matchingArchetypes != null)
            {
                UnsafeList.Free(_matchingArchetypes);
                _matchingArchetypes = null;
            }
        }
        
        [Inline(256)]
        public UnsafeList.Enumerator<int> GetEnumerator()
        {
            return UnsafeList.GetEnumerator<int>(_matchingArchetypes);
        }

        public ref struct Mask
        {
            private BitSet* _includeBits;
            private BitSet* _excludeBits;
            private BitSet* _dependenciesBits;

            public Mask
            ( 
                BitSet* includeBits, 
                BitSet* excludeBits, 
                BitSet* dependenciesBits
            )
            {
                _includeBits = includeBits;
                _excludeBits = excludeBits;
                _dependenciesBits = dependenciesBits;
            }

            // public Mask WithTag<TTag>()
            //     where TTag : unmanaged, IEcsTagComponent
            // {
            //     int index = EcsTagComponentDescriptor<TTag>.TypeIndex;
            //
            //     _tagBits.SetValue(true, index);
            //     _tagBits.SetValue(false, index + 1);
            //
            //     return this;
            // }
            //
            // public Mask WithoutTag<TTag>()
            //     where TTag : unmanaged, IEcsTagComponent
            // {
            //     int index = EcsTagComponentDescriptor<TTag>.TypeIndex;
            //     
            //     _tagBits.SetValue(true, index + 1);
            //     _tagBits.SetValue(false, index);
            //
            //     return this;
            // }
            
            [Inline(256)]
            public Mask With<TComponent>()
                where TComponent : unmanaged, IEcsUnmanagedComponent
            {
                short index = EcsUnmanagedComponentDescriptor<TComponent>.TypeIndex;

                BitSet.Set(_includeBits, index);
                BitSet.Clear(_excludeBits, index);

                return this;
            }
            
            [Inline(256)]
            public Mask Without<TComponent>()
                where TComponent : unmanaged, IEcsUnmanagedComponent
            {
                short index = EcsUnmanagedComponentDescriptor<TComponent>.TypeIndex;
                
                BitSet.Clear(_includeBits, index);
                BitSet.Set(_excludeBits, index);
                BitSet.Clear(_dependenciesBits, index * Config.FilterBitsPerDependency);
                BitSet.Clear(_dependenciesBits, index * Config.FilterBitsPerDependency + 1);

                return this;
            }
            
            [Inline(256)]
            public Mask WithAspect<TAspect>()
                where TAspect : unmanaged, IEcsUnmanagedAspect
            {
                (new TAspect()).Define(this);
                return this;
            }
            
            [Inline(256)]
            public Mask ReadOnly<TComponent>()
                where TComponent : unmanaged, IEcsUnmanagedComponent
            {
                int index = EcsUnmanagedComponentDescriptor<TComponent>.TypeIndex;
                
                BitSet.Set(_includeBits, index);
                BitSet.Clear(_excludeBits, index);
                BitSet.Set(_dependenciesBits, index * Config.FilterBitsPerDependency);
                BitSet.Clear(_dependenciesBits, index * Config.FilterBitsPerDependency + 1);
                
                return With<TComponent>();
            }
            
            [Inline(256)]
            public Mask ReadWrite<TComponent>()
                where TComponent : unmanaged, IEcsUnmanagedComponent
            {
                int index = EcsUnmanagedComponentDescriptor<TComponent>.TypeIndex;
                
                BitSet.Set(_includeBits, index);
                BitSet.Clear(_excludeBits, index);
                BitSet.Set(_dependenciesBits, index * Config.FilterBitsPerDependency);
                BitSet.Set(_dependenciesBits, index * Config.FilterBitsPerDependency + 1);
                
                return With<TComponent>();
            }
        }
    }
}