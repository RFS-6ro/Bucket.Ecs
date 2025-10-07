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

    public unsafe interface ISystemScheduler
    {
        void Schedule(in MultiThreadSystemInfo systemInfo, in double deltaTime);
        void Complete();
    }

    public unsafe class SystemScheduler : ISystemScheduler
    {
        private readonly EcsWorld _world;
        private readonly IThreadPool _threadPool;

        public SystemScheduler(EcsWorld world, IThreadPool threadPool)
        {
            _world = world;
            _threadPool = threadPool;
        }

        [Inline(256)]
        public void Schedule(in MultiThreadSystemInfo systemInfo, in double deltaTime)
        {
            byte* contextDataRaw = null;
            if (systemInfo.ContextTypeIndex != -1)
            {
                contextDataRaw = _world.GetSystemContext(systemInfo.ContextTypeIndex);
            }

            CommandsScheduler commandsScheduler = new CommandsScheduler(_world.SchedulledCommandsQueue);
            // Get affected archetypes from filter
            foreach (ArchetypeId archetypeId in systemInfo.Filter)
            {
                ref Archetype archetype = ref _world.GetArchetype(archetypeId);
                foreach (ArchetypeChunk archetypeChunk in archetype)
                {
                    UnmanagedChunkData data = archetypeChunk.GetUnmanagedChunkData(contextDataRaw, systemInfo.ContextTypeIndex);
                    data._dependenciesBits = systemInfo.Filter._dependenciesBits;
                    data._filterIncludeBits = systemInfo.Filter._filterIncludeBits;
                    _threadPool.Queue(new MultiThreadSystemExecutionHelper.ScheduledSystemState
                    (
                        in data, in deltaTime, systemInfo.RunMethodPtr, in commandsScheduler
                    ));
                }
            }
        }

        [Inline(256)]
        public void Complete()
        {
            _threadPool.WaitForAll();
        }
    }
}
