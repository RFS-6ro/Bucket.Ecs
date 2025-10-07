namespace Bucket.Ecs.v3
{
    public static class MultiThreadSystemDescriptor<TSystem>
        where TSystem : unmanaged, IChunkSystem
    {
        private static short _typeIndex = -1;

        public static short TypeIndex => _typeIndex == -1 ? Init() : _typeIndex;

        private static short Init()
        {
            _typeIndex = MultiThreadSystemsIndexRegistry.GetSystemIndex<TSystem>(new DescriptorWrapper());
            return _typeIndex;
        }

        class DescriptorWrapper : MultiThreadSystemsIndexRegistry.IResettableDescriptor
        {
            public void ResetLocal() => MultiThreadSystemDescriptor<TSystem>.ResetLocalStatic();
        }

        private static void ResetLocalStatic()
        {
            _typeIndex = -1;
        }
    }
}
