namespace Bucket.Ecs.v3
{
    public static class EcsUnmanagedComponentDescriptor<TComponent>
        where TComponent : unmanaged, IEcsUnmanagedComponent
    {
        private static short _typeIndex = -1;
        private static object _lock = new object();

        public static short TypeIndex => _typeIndex == -1 ? Init() : _typeIndex;

        private static short Init()
        {
            if (_typeIndex == -1)
            {
                lock (_lock)
                {
                    if (_typeIndex == -1)
                    {
                        _typeIndex = UnmanagedComponentIndexRegistry.GetComponentIndex<TComponent>(new DescriptorWrapper());
                    }
                }
            }
            
            return _typeIndex;
        }

        class DescriptorWrapper : UnmanagedComponentIndexRegistry.IResettableDescriptor
        {
            public void ResetLocal() => EcsUnmanagedComponentDescriptor<TComponent>.ResetLocalStatic();
        }

        private static void ResetLocalStatic()
        {
            _typeIndex = -1;
        }
    }
}
