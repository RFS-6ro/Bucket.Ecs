using System;
using UnsafeCollections.Collections.Native;
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

    public unsafe struct BucketTempArray : IDisposable
    {
        private UnsafeArray* _inner;

        public bool IsValid { [Inline(256)] get => _inner != null; }

        [Inline(256)]
        public BucketTempArray(UnsafeArray* inner)
        {
            _inner = inner;
        }

        [Inline(256)]
        public void Dispose()
        {
            if (IsValid)
            {
                UnsafeArray.Free(_inner);
                _inner = null;
            }
        }
    }

    public unsafe class BucketAllocator
    {
        private UnsafeList* _tempArrays;
        private UnsafeList* _arraysToFree;

        public static int TempMemoryLeakInFrames = 3;

        public BucketAllocator()
        {
            _tempArrays = UnsafeList.Allocate<(BucketTempArray, int)>(128); // TODO: size from config
            _arraysToFree = UnsafeList.Allocate<(BucketTempArray, int)>(128); // TODO: size from config
        }

        public BucketTempArray AllocTempArray<T>(int size)
            where T : unmanaged
        {
            UnsafeArray* arr = UnsafeArray.Allocate<T>(size);
            BucketTempArray bucketArray = new BucketTempArray(arr);
            UnsafeList.Add(_tempArrays, (bucketArray, 0));
            return bucketArray;
        }

        public void OnUpdate()
        {
            for (int i = UnsafeList.GetCount(_tempArrays) - 1; i >= 0 ; i--)
            {
                ref var tempArray = ref UnsafeList.GetRef<(BucketTempArray array, int framesInUse)>(_tempArrays, i);
                if (tempArray.array.IsValid == false)
                {
                    UnsafeList.Add(_arraysToFree, tempArray);
                }
                else
                {
                    tempArray.framesInUse++;
                    if (tempArray.framesInUse > TempMemoryLeakInFrames)
                    {
                        BLogger.Error($"Temp array is used more, than {TempMemoryLeakInFrames} frames. Possible memory leak detected.");
                    }
                }
            }

            for (int i = UnsafeList.GetCount(_arraysToFree) - 1; i >= 0 ; i--)
            {
                (BucketTempArray, int) array = UnsafeList.Get<(BucketTempArray, int)>(_arraysToFree, i);
                UnsafeList.Remove(_tempArrays, array);
            }

            UnsafeList.Clear(_arraysToFree);
        }
        
        public void Dispose()
        {
            if (_tempArrays != null)
            {
                for (int i = UnsafeList.GetCount(_tempArrays) - 1; i >= 0 ; i--)
                {
                    ref var tempArray = ref UnsafeList.GetRef<(BucketTempArray array, int framesInUse)>(_tempArrays, i);
                    if (tempArray.array.IsValid)
                    {
                        tempArray.array.Dispose();
                    }
                }
                UnsafeList.Free(_tempArrays);
                _tempArrays = null;
            }

            if (_arraysToFree != null)
            {
                UnsafeList.Free(_arraysToFree);
                _arraysToFree = null;
            }
        }
    }
}
