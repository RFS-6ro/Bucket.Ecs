namespace Bucket.Ecs.v3
{
#if UNITY
#else
    using AllowUnsafePtr = UnityAttribute;
    using WriteAccess = UnityAttribute;
#endif
    public interface IEcsFeature
    {
        void Register(EcsSystems ecsSystems);
    }
}
