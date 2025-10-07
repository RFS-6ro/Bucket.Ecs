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

    public interface IForEachSystem : ISystemFilter
    {
        CommandsScheduler CommandsScheduler { get; set; }
        void Run(in double deltaTime, short entityIndex, in UnmanagedChunkData chunkData);
    }

    public struct MultiThreadForEachSystem<TForEachSystem> : IChunkSystem
        where TForEachSystem : unmanaged, IForEachSystem
    {
        public CommandsScheduler CommandsScheduler { get; set; }

        [Inline(256)]
        public void GetFilterMask(EcsUnmanagedFilter.Mask mask)
        {
            new TForEachSystem().GetFilterMask(mask);
        }

        [Inline(256)]
        public void Run(in double deltaTime, in UnmanagedChunkData chunkData)
        {
            var internalSystem = new TForEachSystem();
            internalSystem.CommandsScheduler = CommandsScheduler;
            for (short index = 0; index < chunkData.Count; index++)
            {
                internalSystem.Run(deltaTime, index, in chunkData);
            }
        }
    }
}
