namespace Bucket.Ecs.v3
{
    public static class EcsMultiThreadSystemContextDescriptor<TContext>
        where TContext : unmanaged, IMultiThreadSystemContext
    {
        private static short _typeIndex = -1;

        public static short TypeIndex => _typeIndex == -1 ? Init() : _typeIndex;

        private static short Init()
        {
            _typeIndex = MultiThreadSystemContextIndexRegistry.GetContextIndex<TContext>(new DescriptorWrapper());
            return _typeIndex;
        }

        class DescriptorWrapper : MultiThreadSystemContextIndexRegistry.IResettableDescriptor
        {
            public void ResetLocal() => EcsMultiThreadSystemContextDescriptor<TContext>.ResetLocalStatic();
        }

        private static void ResetLocalStatic()
        {
            _typeIndex = -1;
        }
    }
}
