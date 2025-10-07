using System;
using UnsafeCollections.Collections.Unsafe;
using UnsafeCollections.Collections.Unsafe.Concurrent;

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

    public unsafe struct DeferredCommandCall
    {
        public void* Data;
        public int CommandId;
        public EntityAddress EntityAddress;
    }

    public unsafe struct CommandsScheduler
    {
        internal const int DestroyCommmandId = 0;
        internal const int MigrateAllCommmandId = 1;
        internal const int DestroyAllCommmandId = 2;
        internal const int CreateEntityCommmandId = 3;

        [AllowUnsafePtr] private readonly UnsafeMPSCQueue* _commands;

        internal CommandsScheduler(UnsafeMPSCQueue* commands)
        {
            _commands = commands;
        }

        public bool Destroy(EntityAddress entityAddress)
        {
            return UnsafeMPSCQueue.TryEnqueue(_commands, new DeferredCommandCall()
            {
                CommandId = DestroyCommmandId,
                EntityAddress = entityAddress,
            });
        }

        public bool MigrateAll(in UnmanagedChunkData chunkData)
        {
            return UnsafeMPSCQueue.TryEnqueue(_commands, new DeferredCommandCall()
            {
                CommandId = MigrateAllCommmandId,
                EntityAddress = new EntityAddress(chunkData.ArchetypeId, chunkData.ChunkIndex, 0)
            });
        }

        public bool DestroyAll(in UnmanagedChunkData chunkData)
        {
            return UnsafeMPSCQueue.TryEnqueue(_commands, new DeferredCommandCall()
            {
                CommandId = DestroyAllCommmandId,
                EntityAddress = new EntityAddress(chunkData.ArchetypeId, chunkData.ChunkIndex, 0)
            });
        }

        public bool CreateEntity()
        {
            return UnsafeMPSCQueue.TryEnqueue(_commands, new DeferredCommandCall()
            {
                CommandId = CreateEntityCommmandId
            });
        }

        public bool ScheduleCustom(int commandId, EntityAddress entityAddress, void* data = null)
        {
            return UnsafeMPSCQueue.TryEnqueue(_commands, new DeferredCommandCall()
            {
                Data = data,
                EntityAddress = entityAddress,
                CommandId = commandId
            });
        }
    }
}
