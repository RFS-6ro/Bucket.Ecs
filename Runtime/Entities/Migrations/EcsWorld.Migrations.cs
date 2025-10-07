using UnsafeCollections.Collections.Unsafe;
using UnsafeCollections.Collections.Native;

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
        private unsafe BitSet* _migrationComponentsMask;

        [Inline(256)]
        private unsafe void InitMigrationSupport()
        {
            _migrationComponentsMask = BitSet.Allocate(RUNTIME_REFERENCES.UnmanagedComponentsCount);
        }

        [Inline(256)]
        public void RunSyncPoint()
        {
            foreach (var archetypeIndex in this)
            {
                ref var archetype = ref _archetypes[archetypeIndex];
                foreach (ref var chunk in archetype)
                {
                    ApplyMigrationTable(ref archetype, ref chunk);
                }

                archetype.RemoveMarkedEntities();
                archetype.Rebalance();
            }
        }

        private unsafe void ApplyMigrationTable(ref Archetype archetype, ref ArchetypeChunk chunk)
        {
            EntityMigrationData* migrationTablePtr = UnsafeArray.GetPtr<EntityMigrationData>(chunk.GetMigrationTable(), 0);

            if (migrationTablePtr == null) return;

            for (short entityIndex = 0; entityIndex < chunk.Count; entityIndex++)
            {
                ref EntityMigrationData entityMigrationData = ref *(migrationTablePtr + entityIndex);
                if (entityMigrationData.IsCreated == false) continue;

                BitSet.CopyFrom(_migrationComponentsMask, archetype.ComponentsMask);
                entityMigrationData.ApplyComponentMask(_migrationComponentsMask);

                var newArchetypeId = GetOrCreateArchetype(_migrationComponentsMask);
                EntityAddress address = CreateEntity(newArchetypeId);
                ref var newChunk = ref _archetypes[(int)address.archetype].GetChunk(address.chunkIndex);
                
                var sourceStorage = chunk.GetUnmanagedComponentsStorage();
                var destinationStorage = newChunk.GetUnmanagedComponentsStorage();
                var newEntityIndex = (short)address.entityIndex;
                sourceStorage.MigrateAllComponents(entityIndex, ref destinationStorage, newEntityIndex);

                entityMigrationData.ApplyComponentsValues(newEntityIndex, ref destinationStorage);
                chunk.ManagedComponents.CopyTo(entityIndex, newEntityIndex, newChunk.ManagedComponents);

                chunk.MarkToRemove(address.entityIndex);

                entityMigrationData.Dispose();
            }
        }

        [Inline(256)]
        private unsafe void ReleaseMigrationSupport()
        {   
            if (_migrationComponentsMask != null)
            {
                BitSet.Free(_migrationComponentsMask);
                _migrationComponentsMask = null;
            }
        }
    }
}
