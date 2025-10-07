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
    using System;

    public class ManagedDynamicComponentStorage<TComponent> : DynamicComponentStorageBase
        where TComponent : struct, IEcsComponent
    {
        private TComponent[] _data;

        public ManagedDynamicComponentStorage(int size)
        {
            _data = new TComponent[size];
        }
        
        [Inline(256)]
        public ref TComponent GetRef(short entityIndex)
        {
            return ref _data[entityIndex];
        }

        [Inline(256)]
        public override DynamicComponentStorageBase CreateNew()
        {
            return new ManagedDynamicComponentStorage<TComponent>(_data.Length);
        }

        public override void CopyFrom(short oldEntityIndex, short newEntityIndex, DynamicComponentStorageBase other, int length = 1)
        {
            var otherStorage = other as ManagedDynamicComponentStorage<TComponent>;
            Array.Copy(otherStorage._data, oldEntityIndex, _data, newEntityIndex, length);
        }
    }
}
