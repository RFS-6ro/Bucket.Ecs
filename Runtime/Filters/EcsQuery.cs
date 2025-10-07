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
    
    public unsafe class EcsQuery
    {
        private EcsFilter _filter;

        internal EcsWorld _world;
        internal UnmanagedComponentsStorage _unmanagedComponentsStorage;
        internal DynamicComponentsStorage _managedComponents;
        internal UnsafeArray* _migrationTable;
        internal Entity* _entities;

        private EcsQuery(EcsFilter filter)
        {
            _filter = filter;
        }

        public static EcsQuery WithFilter(EcsFilter filter) => new EcsQuery(filter);

#region Enumeration
        public AllEntitiesEnumerator ForEachEntity() => new AllEntitiesEnumerator(this, _filter);

        public struct AllEntitiesEnumerator
        {
            private EcsQuery _query;
            private EcsFilter _filter;

            public AllEntitiesEnumerator(EcsQuery query, EcsFilter filter)
            {
                _query = query;
                _filter = filter;
            }

            public EcsFilter.Enumerator GetEnumerator()
            {
                return _filter.GetQueryEnumerator(_query);
            }
        }
#endregion

#region Access methods
        [Inline(256)]
        public Entity GetEntityId(in EntityAddress entityAddress)
        {
            return *(_entities + (short)entityAddress.entityIndex);
        }

        [Inline(256)]
        public bool Has<TComponent>(in EntityAddress entityAddress)
            where TComponent : struct, IEcsComponentBase
        {
            return _managedComponents.Has<TComponent>((short)entityAddress.entityIndex);
        }

        [Inline(256)]
        public bool TryGet<TComponent>(in EntityAddress entityAddress, out TComponent component)
            where TComponent : struct, IEcsComponent
        {
            component = default;
            if (Has<TComponent>(in entityAddress) == false) return false;
            
            component = Get<TComponent>(in entityAddress);

            return true;
        }

        [Inline(256)]
        public TComponent Get<TComponent>(in EntityAddress entityAddress)
            where TComponent : struct, IEcsComponent
        {
            return _managedComponents.Get<TComponent>((short)entityAddress.entityIndex);
        }

        [Inline(256)]
        public ref TComponent GetRef<TComponent>(in EntityAddress entityAddress)
            where TComponent : struct, IEcsComponent
        {
            return ref _managedComponents.Get<TComponent>((short)entityAddress.entityIndex);
        }

        [Inline(256)]
        public ref TComponent GetSharedRef<TComponent>(in EntityAddress entityAddress)
            where TComponent : struct, IEcsSharedComponent
        {
            return ref _managedComponents.GetShared<TComponent>((short)entityAddress.entityIndex);
        }

        [Inline(256)]
        public bool TryAdd<TComponent>(in EntityAddress entityAddress, TComponent component)
            where TComponent : struct, IEcsComponent
        {
            if (Has<TComponent>(in entityAddress)) return false;

            _managedComponents.Add<TComponent>(component, (short)entityAddress.entityIndex);

            return true;
        }

        [Inline(256)]
        public bool TryAdd<TComponent>(in EntityAddress entityAddress)
            where TComponent : struct, IEcsComponent
        {
            if (Has<TComponent>(in entityAddress)) return false;

            Add<TComponent>(in entityAddress);

            return true;
        }

        [Inline(256)]
        public bool TryAddTag<TComponent>(in EntityAddress entityAddress)
            where TComponent : struct, IEcsTagComponent
        {
            if (Has<TComponent>(in entityAddress)) return false;

            AddTag<TComponent>(in entityAddress);

            return true;
        }

        [Inline(256)]
        public bool TryAddShared<TComponent>(in EntityAddress entityAddress)
            where TComponent : struct, IEcsSharedComponent
        {
            if (Has<TComponent>(in entityAddress)) return false;

            AddShared<TComponent>(in entityAddress);

            return true;
        }

        [Inline(256)]
        public bool TryAddShared<TComponent>(in EntityAddress entityAddress, TComponent component)
            where TComponent : struct, IEcsSharedComponent
        {
            if (Has<TComponent>(in entityAddress)) return false;

            _managedComponents.AddShared<TComponent>(component, (short)entityAddress.entityIndex);

            return true;
        }

        [Inline(256)]
        public void Add<TComponent>(in EntityAddress entityAddress)
            where TComponent : struct, IEcsComponent
        {
            _managedComponents.Add<TComponent>((short)entityAddress.entityIndex);
        }

        [Inline(256)]
        public void AddTag<TComponent>(in EntityAddress entityAddress)
            where TComponent : struct, IEcsTagComponent
        {
            _managedComponents.AddTag<TComponent>((short)entityAddress.entityIndex);
        }

        [Inline(256)]
        public void AddShared<TComponent>(in EntityAddress entityAddress)
            where TComponent : struct, IEcsSharedComponent
        {
            _managedComponents.AddShared<TComponent>((short)entityAddress.entityIndex);
        }

        [Inline(256)]
        public ref TComponent AddRef<TComponent>(in EntityAddress entityAddress)
            where TComponent : struct, IEcsComponent
        {
            TryAdd<TComponent>(in entityAddress);
            return ref GetRef<TComponent>(in entityAddress);
        }

        [Inline(256)]
        public void Del<TComponent>(in EntityAddress entityAddress)
            where TComponent : struct, IEcsComponentBase
        {
            _managedComponents.Del<TComponent>((short)entityAddress.entityIndex);
        }



        [Inline(256)]
        public bool HasUnmanaged<TComponent>(in EntityAddress entityAddress)
            where TComponent : unmanaged, IEcsUnmanagedComponent
        {
            short componentIndex = EcsUnmanagedComponentDescriptor<TComponent>.TypeIndex;
            return _unmanagedComponentsStorage.Has<TComponent>((short)entityAddress.entityIndex, componentIndex);
        }

        [Inline(256)]
        public bool TryGetUnmanaged<TComponent>(in EntityAddress entityAddress, out TComponent component)
            where TComponent : unmanaged, IEcsUnmanagedComponent
        {
            component = default;
            if (HasUnmanaged<TComponent>(in entityAddress) == false) return false;
            
            component = GetUnmanaged<TComponent>(in entityAddress);

            return true;
        }

        [Inline(256)]
        public TComponent GetUnmanaged<TComponent>(in EntityAddress entityAddress)
            where TComponent : unmanaged, IEcsUnmanagedComponent
        {
            short componentIndex = EcsUnmanagedComponentDescriptor<TComponent>.TypeIndex;
            if (_unmanagedComponentsStorage.Has<TComponent>((short)entityAddress.entityIndex, componentIndex) == false)
            {
                BExceptionThrower.EntityHasNoComponent($"Entity has no component of type <{typeof(TComponent)}>");
            }

            return _unmanagedComponentsStorage.Read<TComponent>((short)entityAddress.entityIndex, componentIndex);
        }

        [Inline(256)]
        public ref TComponent GetUnmanagedRef<TComponent>(in EntityAddress entityAddress)
            where TComponent : unmanaged, IEcsUnmanagedComponent
        {
            short componentIndex = EcsUnmanagedComponentDescriptor<TComponent>.TypeIndex;
            if (_unmanagedComponentsStorage.Has<TComponent>((short)entityAddress.entityIndex, componentIndex) == false)
            {
                BExceptionThrower.EntityHasNoComponent($"Entity has no component of type <{typeof(TComponent)}>");
            }

            return ref _unmanagedComponentsStorage.ReadRef<TComponent>((short)entityAddress.entityIndex, componentIndex);
        }

        [Inline(256)]
        public bool TrySetUnmanaged<TComponent>(in EntityAddress entityAddress, TComponent component)
            where TComponent : unmanaged, IEcsUnmanagedComponent
        {
            if (HasUnmanaged<TComponent>(in entityAddress) == false) return false;
        
            SetUnmanaged<TComponent>(in entityAddress, component);

            return true;
        }

        [Inline(256)]
        public void SetUnmanaged<TComponent>(in EntityAddress entityAddress, TComponent component)
            where TComponent : unmanaged, IEcsUnmanagedComponent
        {
            short componentIndex = EcsUnmanagedComponentDescriptor<TComponent>.TypeIndex;
            if (_unmanagedComponentsStorage.Has<TComponent>((short)entityAddress.entityIndex, componentIndex) == false)
            {
                BExceptionThrower.EntityHasNoComponent($"Entity has no component of type <{typeof(TComponent)}>");
            }

            _unmanagedComponentsStorage.Write<TComponent>((short)entityAddress.entityIndex, componentIndex, in component);
        }


        [WriteAccess]
        [Inline(256)]
        public void AddUnmanaged<TComponent>(in EntityAddress entityAddress)
            where TComponent : unmanaged, IEcsUnmanagedComponent
        {
            short componentIndex = EcsUnmanagedComponentDescriptor<TComponent>.TypeIndex;
            if (_unmanagedComponentsStorage.Has<TComponent>((short)entityAddress.entityIndex, componentIndex))
            {
                BExceptionThrower.ComponentAlreadyAttached($"Entity already has component of type <{typeof(TComponent)}>");
            }

            ref EntityMigrationData migrationData = ref GetOrCreateMigrationData(in entityAddress);
            migrationData.Add<TComponent>();
        }

        [WriteAccess]
        [Inline(256)]
        public void AddUnmanaged<TComponent>(in EntityAddress entityAddress, TComponent component)
            where TComponent : unmanaged, IEcsUnmanagedComponent
        {
            short componentIndex = EcsUnmanagedComponentDescriptor<TComponent>.TypeIndex;
            if (_unmanagedComponentsStorage.Has<TComponent>((short)entityAddress.entityIndex, componentIndex))
            {
                BExceptionThrower.ComponentAlreadyAttached($"Entity already has component of type <{typeof(TComponent)}>");
            }

            ref EntityMigrationData migrationData = ref GetOrCreateMigrationData(in entityAddress);
            migrationData.Add<TComponent>(component);
        }

        [WriteAccess]
        [Inline(256)]
        public void DelUnmanaged<TComponent>(in EntityAddress entityAddress)
            where TComponent : unmanaged, IEcsUnmanagedComponent
        {
            short componentIndex = EcsUnmanagedComponentDescriptor<TComponent>.TypeIndex;
            if (_unmanagedComponentsStorage.Has<TComponent>((short)entityAddress.entityIndex, componentIndex) == false)
            {
                BExceptionThrower.EntityHasNoComponent($"Entity has no component of type <{typeof(TComponent)}>");
            }

            ref EntityMigrationData migrationData = ref GetOrCreateMigrationData(in entityAddress);
            migrationData.Del<TComponent>();
        }

        [WriteAccess]
        [Inline(256)]
        private ref EntityMigrationData GetOrCreateMigrationData(in EntityAddress entityAddress)
        {
            short index = (short)entityAddress.entityIndex;
            if (UnsafeArray.Get<EntityMigrationData>(_migrationTable, index).IsCreated == false)
            {
                UnsafeArray.GetRef<EntityMigrationData>(_migrationTable, index).Create();
            }

            return ref UnsafeArray.GetRef<EntityMigrationData>(_migrationTable, index);
        }
#endregion
    }
}