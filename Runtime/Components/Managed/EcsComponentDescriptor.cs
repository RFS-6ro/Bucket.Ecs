namespace Bucket.Ecs.v3
{
    public static class EcsComponentDescriptor<TComponent> where TComponent : struct, IEcsComponentBase
    {
        private static short _typeIndex = -1;

        public static short TypeIndex => _typeIndex == -1 ? Init() : _typeIndex;

        private static short Init()
        {
            _typeIndex = (short)ManagedComponentIndexRegistry.GetComponentIndex<TComponent>(new DescriptorWrapper());
            return _typeIndex;
        }

        class DescriptorWrapper : ManagedComponentIndexRegistry.IResettableDescriptor
        {
            public void ResetLocal() => EcsComponentDescriptor<TComponent>.ResetLocalStatic();
        }

        private static void ResetLocalStatic()
        {
            _typeIndex = -1;
        }
    }
}
