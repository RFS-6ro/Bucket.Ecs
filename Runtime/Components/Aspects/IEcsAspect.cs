namespace Bucket.Ecs.v3
{
    public interface IEcsAspect
    {
        void Define(EcsFilter.Mask mask);
    }
}