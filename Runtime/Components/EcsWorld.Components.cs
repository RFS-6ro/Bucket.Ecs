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
    using System.Collections.Generic;
    using System;

    public sealed partial class EcsWorld
    {
        private Dictionary<Type, object> _sharedComponents;

        [Inline(256)]
        private unsafe void InitComponents()
        {
            _sharedComponents = new Dictionary<Type, object>();
        }

        [Inline(256)]
        public SharedComponentReference<TComponent> GetOrCreateSharedComponent<TComponent>()
            where TComponent : struct, IEcsSharedComponent
        {
            Type sharedComponentType = typeof(TComponent);
            if (_sharedComponents.TryGetValue(sharedComponentType, out object reference))
            {
                return (SharedComponentReference<TComponent>)reference;
            }

            var sharedComponentReference = new SharedComponentReference<TComponent>();
            _sharedComponents.Add(sharedComponentType, sharedComponentReference);
            return sharedComponentReference;
        }

        [Inline(256)]
        private unsafe void ReleaseComponents()
        {
        }
    }
}
