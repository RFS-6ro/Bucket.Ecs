namespace Bucket.Ecs.v3
{
    public interface IEcsUnmanagedAspect
    {
        void Define(EcsUnmanagedFilter.Mask mask);
    }
}