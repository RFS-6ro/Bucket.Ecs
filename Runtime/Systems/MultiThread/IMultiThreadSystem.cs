#if UNITY && B_USE_JOB_SYSTEM
using Unity.Jobs;
#endif

namespace Bucket.Ecs.v3
{
#if !B_USE_JOB_SYSTEM
    public interface IJob
    {
        void Execute();
    }
#endif

    public interface ISystem
    {
        CommandsScheduler CommandsScheduler { get; set; }
        void Run(in double deltaTime);
    }
    
    public interface ISystemFilter
    {
        void GetFilterMask(EcsUnmanagedFilter.Mask mask);
    }

    public interface IChunkSystem : ISystemFilter
    {
        CommandsScheduler CommandsScheduler { get; set; }
        void Run(in double deltaTime, in UnmanagedChunkData chunkData);
    }
}
