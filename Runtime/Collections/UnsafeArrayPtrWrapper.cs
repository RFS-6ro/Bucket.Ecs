using System;
using UnsafeCollections.Collections.Unsafe;

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

    public unsafe struct UnsafeArrayPtrWrapper : IDisposable
    {
        [AllowUnsafePtr] private UnsafeArray* _array;
        private int _length;

        public bool IsCreated { [Inline(256)] get => _array != null; }
        public int Length { [Inline(256)] get => _length; }

        public UnsafeArrayPtrWrapper(UnsafeArray* array)
        {
            _array = array;
            _length = UnsafeArray.GetLength(_array);
        }

        [Inline(256)]
        public UnsafeArray* GetInternal() => _array;

        [Inline(256)]
        public void Dispose()
        {
            if (_array != null)
            {
                UnsafeArray.Free(_array);
                _array = null;
            }
        }
    }
}
