// ----------------------------------------------------------------------------
// The Proprietary or MIT License
// Copyright (c) 20224-2024 RFS_6ro <rfs6ro@gmail.com>
// ----------------------------------------------------------------------------

using BucketEcs.Collections;
using System.Runtime.CompilerServices;

namespace BucketEcs
{
    public interface IStorageAccess<T>
        where T : struct, IEcsComponent
    {
        ref readonly T RO(in EntityIndex contextId);

        ref T RW(in EntityIndex contextId);
    }

    public readonly struct StorageAccess<T> : IStorageAccess<T>
        where T : struct, IEcsComponent
    {
        public StorageAccess(ComponentStorage<T> storage, ChunkIndex containerIndex)
        {
            _data = storage.GetContainer(containerIndex).components;
        }

        private readonly SafeGrowingArray<T> _data;

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly T RO(in EntityIndex contextId)
        {
            return ref _data[(ushort)contextId];
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T RW(in EntityIndex contextId)
        {
            return ref _data[(ushort)contextId];
        }
    }
}
