namespace Bucket.Ecs.v3
{
#if UNITY
#else
    using AllowUnsafePtr = UnityAttribute;
    using WriteAccess = UnityAttribute;
#endif
    using Inline = System.Runtime.CompilerServices.MethodImplAttribute;
    using UnsafeCollections.Collections.Native;
    using System;

    public unsafe class EntityBuilder : System.IDisposable
    {
        public delegate void OnCreateCallback(EntityContext entity);

        private readonly EcsWorld _world;

        private BitSet* _unmanagedComponentsMask;
        private BitSet* _dynamicComponentsMask;

        private ArchetypeId _archetypeId;

        internal EntityBuilder(EcsWorld world)
        {
            _world = world;
            _archetypeId = ArchetypeId.INVALID;
            _unmanagedComponentsMask = BitSet.Allocate(RUNTIME_REFERENCES.UnmanagedComponentsCount);
            _dynamicComponentsMask = BitSet.Allocate(RUNTIME_REFERENCES.ComponentsCount);
        }

        [Inline(256)]
        public EntityBuilder WithUnmanaged<T>() where T : unmanaged, IEcsUnmanagedComponent
        {
            // TODO: consider reuse the same way like in EntityMigrationData to allow set 
            BAssert.True(_unmanagedComponentsMask != null);
            BAssert.True(_archetypeId == ArchetypeId.INVALID);
            
            short componentOffset = EcsUnmanagedComponentDescriptor<T>.TypeIndex;
            BitSet.Set(_unmanagedComponentsMask, componentOffset);

            return this;
        }

        [Inline(256)]
        public EntityBuilder With<T>() where T : struct, IEcsComponentBase
        {
            // TODO: consider reuse the same way like in EntityMigrationData to allow set 
            BAssert.True(_dynamicComponentsMask != null);
            BAssert.True(_archetypeId == ArchetypeId.INVALID);
            
            short componentOffset = EcsComponentDescriptor<T>.TypeIndex;
            BitSet.Set(_dynamicComponentsMask, componentOffset);

            return this;
        }

        [Inline(256)]
        public EntityBuilder Build()
        {
            BAssert.True(_unmanagedComponentsMask != null);
            BAssert.True(_archetypeId == ArchetypeId.INVALID);
            _archetypeId = _world.GetOrCreateArchetype(_unmanagedComponentsMask);
            return this;
        }

        [Inline(256)]
        public EntityAddress Create()
        {
            BAssert.True(_unmanagedComponentsMask != null);
            BAssert.False(_archetypeId == ArchetypeId.INVALID);

            var address = _world.CreateEntity(_archetypeId);
            var managedComponents = _world.GetArchetype(_archetypeId)
                .GetChunk(address.chunkIndex).ManagedComponents;

            foreach ((int componentIndex, bool set) in BitSet.GetEnumerator(_dynamicComponentsMask))
            {
                if (set == false) continue;
                
                managedComponents.AddRaw(componentIndex, (short)address.entityIndex);
            }

            return address;
        }

        public EntityAddress Create(OnCreateCallback callback)
        {
            EntityAddress address = Create();
            callback?.Invoke(_world.CreateEntityContext(in address));
            return address;
        }

        public void Dispose()
        {
            if (_unmanagedComponentsMask != null)
            {
                BitSet.Free(_unmanagedComponentsMask);
                _unmanagedComponentsMask = null;
            }
        }
    }
}
