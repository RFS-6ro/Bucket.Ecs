// ----------------------------------------------------------------------------
// The Proprietary or MIT License
// Copyright (c) 20224-2024 RFS_6ro <rfs6ro@gmail.com>
// ----------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace BucketEcs.Collections
{
    public class RecycleArray<T>
    {
        public RecycleArray(int capacity)
        {
            if (capacity == 0) capacity = 128;

            _isAlive = new BitArray(capacity);
            _data = new T[capacity];
            _count = 0;
            _dataCount = 0;

            _recycledData = new int[capacity];
            _recycledDataCount = 0;
        }
 
        private T[] _data;
        private int _count;
        private int _dataCount;
        private int[] _recycledData;
        private int _recycledDataCount;

        private BitArray _isAlive;

        public int Capacity => _data.Length;
        public int Count => _count;

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Allocate(bool resetData = false)
        {
            Interlocked.Increment(ref _count);

            int newDataIndex;
            if (_recycledDataCount > 0)
            {
                newDataIndex = _recycledData[--_recycledDataCount];
            }
            else
            {
                if (_dataCount == _data.Length)
                {
                    int newSize;
                    if (_dataCount == 0) newSize = 10;
                    else newSize = _dataCount << 1;
                    Array.Resize(ref _data, newSize);
                    _isAlive.EnsureCapacity(newSize);
                }

                newDataIndex = _dataCount++;
            }
            
            _isAlive.Set(newDataIndex);
            if (resetData) _data[newDataIndex] = default;

            return newDataIndex;
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Recycle(int index, bool resetData = false)
        {
            Interlocked.Decrement(ref _count);

            if (_recycledDataCount == _recycledData.Length) 
            {
                int newSize;
                if (_recycledDataCount == 0) newSize = 10;
                else newSize = _recycledDataCount << 1;
                Array.Resize(ref _recycledData, newSize);
                _isAlive.EnsureCapacity(newSize);
            }

            if (resetData) _data[index] = default;

            _isAlive.Reset(index);

            _recycledData[_recycledDataCount] = index;
            _recycledDataCount++;
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAlive(int index)
        {
            if (index >= _dataCount) return false;
            return _isAlive[index];
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecycleAll(bool resetData = true)
        {
            _count = 0;
            _dataCount = 0;
            _isAlive.ResetAll();
            if (resetData) Array.Fill(_data, default);
        }

        public int GetAll(ref int[] data)
        {
            if (data == null) 
            {
                data = new int[_count];
            }
            
            if (data.Length < _count)
            {
                Array.Resize(ref data, _count);
            }

            var j = 0;
            for (int i = 0; i < _data.Length; i++) 
            {
                if (_isAlive[i])
                {
                    data[j] = i;
                    j++;
                }
            }

            return _count;
        }

        public void ForEach(Action<bool, int, T> action)
        {
            for (int i = 0; i < _data.Length; i++) 
            {
                action?.Invoke(_isAlive[i], i, _data[i]);
            }
        }

        public ref T this[int index]
        {
            /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _data[index];
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator() => new Enumerator(this);

        public struct Enumerator
        {
            public Enumerator(RecycleArray<T> array) 
            {
                _count = array._data.Length;
                _array = array;
                _idx = -1;
            }
 
            private readonly RecycleArray<T> _array;
            private readonly int _count;
            private int _idx;

            public ref T Current 
            {
                /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref _array[_idx];
            }

            /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() 
            {
                do
                {
                    if (++_idx >= _count) return false;
                    
                    bool isAlive = _array.IsAlive(_idx);

                    if (isAlive) return true;

                } while (_idx < _count);

                return false;
            }
        }
    }
}
