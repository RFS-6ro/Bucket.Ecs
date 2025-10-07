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

    public readonly unsafe ref struct EntityContext
    {
        private readonly EcsWorld _world;
        private readonly short _index;
        private readonly UnmanagedComponentsStorage _unmanagedComponentsStorage;
        private readonly DynamicComponentsStorage _managedComponents;
        private readonly UnsafeArray* _migrationTable;
        
        public EntityAddress EntityAddress { [Inline(256)] get; }
        public Entity EntityId { [Inline(256)] get; }
        public bool IsValid { [Inline(256)] get => EntityId.IsValid; }

        public EntityContext
        (
            EcsWorld world,
            in Entity entityId,
            in EntityAddress entityAddress,
            ref UnmanagedComponentsStorage unmanagedComponentsStorage,
            DynamicComponentsStorage managedComponents,
            UnsafeArray* migrationTable
        )
        {
            _world = world;
            EntityId = entityId;
            EntityAddress = entityAddress;
            _unmanagedComponentsStorage = unmanagedComponentsStorage;
            _managedComponents = managedComponents;
            _migrationTable = migrationTable;
            _index = (short)entityAddress.entityIndex;
        }



        [Inline(256)]
        public bool Has<TComponent>()
            where TComponent : struct, IEcsComponentBase
        {
            return _managedComponents.Has<TComponent>(_index);
        }

        [Inline(256)]
        public bool TryGet<TComponent>(out TComponent component)
            where TComponent : struct, IEcsComponent
        {
            component = default;
            if (Has<TComponent>() == false) return false;
            
            component = Get<TComponent>();

            return true;
        }

        [Inline(256)]
        public TComponent Get<TComponent>()
            where TComponent : struct, IEcsComponent
        {
            return _managedComponents.Get<TComponent>(_index);
        }

        [Inline(256)]
        public ref TComponent GetRef<TComponent>()
            where TComponent : struct, IEcsComponent
        {
            return ref _managedComponents.Get<TComponent>(_index);
        }

        [Inline(256)]
        public ref TComponent GetSharedRef<TComponent>()
            where TComponent : struct, IEcsSharedComponent
        {
            return ref _managedComponents.GetShared<TComponent>(_index);
        }

        [Inline(256)]
        public bool TryAdd<TComponent>(TComponent component)
            where TComponent : struct, IEcsComponent
        {
            if (Has<TComponent>()) return false;

            _managedComponents.Add<TComponent>(component, _index);

            return true;
        }

        [Inline(256)]
        public bool TryAdd<TComponent>()
            where TComponent : struct, IEcsComponent
        {
            if (Has<TComponent>()) return false;

            Add<TComponent>();

            return true;
        }

        [Inline(256)]
        public bool TryAddTag<TComponent>()
            where TComponent : struct, IEcsTagComponent
        {
            if (Has<TComponent>()) return false;

            AddTag<TComponent>();

            return true;
        }

        [Inline(256)]
        public bool TryAddShared<TComponent>()
            where TComponent : struct, IEcsSharedComponent
        {
            if (Has<TComponent>()) return false;

            AddShared<TComponent>();

            return true;
        }

        [Inline(256)]
        public bool TryAddShared<TComponent>(TComponent component)
            where TComponent : struct, IEcsSharedComponent
        {
            if (Has<TComponent>()) return false;

            _managedComponents.AddShared<TComponent>(component, _index);

            return true;
        }

        [Inline(256)]
        public void Add<TComponent>()
            where TComponent : struct, IEcsComponent
        {
            _managedComponents.Add<TComponent>(_index);
        }

        [Inline(256)]
        public void AddTag<TComponent>()
            where TComponent : struct, IEcsTagComponent
        {
            _managedComponents.AddTag<TComponent>(_index);
        }

        [Inline(256)]
        public void AddShared<TComponent>()
            where TComponent : struct, IEcsSharedComponent
        {
            _managedComponents.AddShared<TComponent>(_index);
        }

        [Inline(256)]
        public ref TComponent AddRef<TComponent>()
            where TComponent : struct, IEcsComponent
        {
            TryAdd<TComponent>();
            return ref GetRef<TComponent>();
        }

        [Inline(256)]
        public void Del<TComponent>()
            where TComponent : struct, IEcsComponentBase
        {
            _managedComponents.Del<TComponent>(_index);
        }



        [Inline(256)]
        public bool HasUnmanaged<TComponent>()
            where TComponent : unmanaged, IEcsUnmanagedComponent
        {
            short componentIndex = EcsUnmanagedComponentDescriptor<TComponent>.TypeIndex;
            return _unmanagedComponentsStorage.Has<TComponent>(_index, componentIndex);
        }

        [Inline(256)]
        public bool TryGetUnmanaged<TComponent>(out TComponent component)
            where TComponent : unmanaged, IEcsUnmanagedComponent
        {
            component = default;
            if (HasUnmanaged<TComponent>() == false) return false;
            
            component = GetUnmanaged<TComponent>();

            return true;
        }

        [Inline(256)]
        public TComponent GetUnmanaged<TComponent>()
            where TComponent : unmanaged, IEcsUnmanagedComponent
        {
            short componentIndex = EcsUnmanagedComponentDescriptor<TComponent>.TypeIndex;
            if (_unmanagedComponentsStorage.Has<TComponent>(_index, componentIndex) == false)
            {
                BExceptionThrower.EntityHasNoComponent($"Entity has no component of type <{typeof(TComponent)}>");
            }

            return _unmanagedComponentsStorage.Read<TComponent>(_index, componentIndex);
        }

        [Inline(256)]
        public ref TComponent GetUnmanagedRef<TComponent>()
            where TComponent : unmanaged, IEcsUnmanagedComponent
        {
            short componentIndex = EcsUnmanagedComponentDescriptor<TComponent>.TypeIndex;
            if (_unmanagedComponentsStorage.Has<TComponent>(_index, componentIndex) == false)
            {
                BExceptionThrower.EntityHasNoComponent($"Entity has no component of type <{typeof(TComponent)}>");
            }

            return ref _unmanagedComponentsStorage.ReadRef<TComponent>(_index, componentIndex);
        }

        [Inline(256)]
        public bool TrySetUnmanaged<TComponent>(TComponent component)
            where TComponent : unmanaged, IEcsUnmanagedComponent
        {
            if (HasUnmanaged<TComponent>() == false) return false;
        
            SetUnmanaged<TComponent>(component);

            return true;
        }

        [Inline(256)]
        public void SetUnmanaged<TComponent>(TComponent component)
            where TComponent : unmanaged, IEcsUnmanagedComponent
        {
            short componentIndex = EcsUnmanagedComponentDescriptor<TComponent>.TypeIndex;
            if (_unmanagedComponentsStorage.Has<TComponent>(_index, componentIndex) == false)
            {
                BExceptionThrower.EntityHasNoComponent($"Entity has no component of type <{typeof(TComponent)}>");
            }

            _unmanagedComponentsStorage.Write<TComponent>(_index, componentIndex, in component);
        }


        [WriteAccess]
        [Inline(256)]
        public void AddUnmanaged<TComponent>()
            where TComponent : unmanaged, IEcsUnmanagedComponent
        {
            short componentIndex = EcsUnmanagedComponentDescriptor<TComponent>.TypeIndex;
            if (_unmanagedComponentsStorage.Has<TComponent>(_index, componentIndex))
            {
                BExceptionThrower.ComponentAlreadyAttached($"Entity already has component of type <{typeof(TComponent)}>");
            }

            ref EntityMigrationData migrationData = ref GetOrCreateMigrationData();
            migrationData.Add<TComponent>();
        }

        [WriteAccess]
        [Inline(256)]
        public void AddUnmanaged<TComponent>(TComponent component)
            where TComponent : unmanaged, IEcsUnmanagedComponent
        {
            short componentIndex = EcsUnmanagedComponentDescriptor<TComponent>.TypeIndex;
            if (_unmanagedComponentsStorage.Has<TComponent>(_index, componentIndex))
            {
                BExceptionThrower.ComponentAlreadyAttached($"Entity already has component of type <{typeof(TComponent)}>");
            }

            ref EntityMigrationData migrationData = ref GetOrCreateMigrationData();
            migrationData.Add<TComponent>(component);
        }

        [WriteAccess]
        [Inline(256)]
        public void DelUnmanaged<TComponent>()
            where TComponent : unmanaged, IEcsUnmanagedComponent
        {
            short componentIndex = EcsUnmanagedComponentDescriptor<TComponent>.TypeIndex;
            if (_unmanagedComponentsStorage.Has<TComponent>(_index, componentIndex) == false)
            {
                BExceptionThrower.EntityHasNoComponent($"Entity has no component of type <{typeof(TComponent)}>");
            }

            ref EntityMigrationData migrationData = ref GetOrCreateMigrationData();
            migrationData.Del<TComponent>();
        }

        [WriteAccess]
        [Inline(256)]
        private ref EntityMigrationData GetOrCreateMigrationData()
        {
            if (UnsafeArray.Get<EntityMigrationData>(_migrationTable, _index).IsCreated == false)
            {
                UnsafeArray.GetRef<EntityMigrationData>(_migrationTable, _index).Create();
            }

            return ref UnsafeArray.GetRef<EntityMigrationData>(_migrationTable, _index);
        }
    }
}
