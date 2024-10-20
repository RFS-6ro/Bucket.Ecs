// ----------------------------------------------------------------------------
// The Proprietary or MIT License
// Copyright (c) 20224-2024 RFS_6ro <rfs6ro@gmail.com>
// ----------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;

namespace BucketEcs.Collections
{
    //TODO: While this class looks like a great solution to not care about the memory management - it is heavy for GC. Should be replaced
    public class SafeGrowingArray<T>
    {
        public SafeGrowingArray(int capacity)
        {
            _data = new T[capacity];
        }
 
        private T[] _data;

        public int Capacity
        {
            /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _data.Length;
        }

        public ref T this[int index]
        {
            /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_data == null)
                {
                    _data = new T[index];
                }

                if (index >= _data.Length)
                {
                    var len = _data.Length;

                    if (len == 0) len = index + 1;
                    else len <<= 1;

                    while (len <= index) 
                    {
                        len <<= 1;
                    }

                    Array.Resize(ref _data, len);
                }

                return ref _data[index];
            }
        }
    }
}
