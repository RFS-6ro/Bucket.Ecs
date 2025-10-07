using UnsafeCollections.Collections.Native;

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

    public unsafe static class NativeCollectionsUtils
    {
        public static NativeArray<T> Resize<T>(this ref NativeArray<T> obj, int newSize) where T : unmanaged
        {
            var newObj = new NativeArray<T>(newSize);
            NativeArray<T>.Copy(obj, newObj, obj.Length);
            obj.Dispose();
            return newObj;
        }
    }
}