// ----------------------------------------------------------------------------
// The Proprietary or MIT License
// Copyright (c) 20224-2024 RFS_6ro <rfs6ro@gmail.com>
// ----------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

namespace BucketEcs.Collections
{
    public class BitArray
    {
        public BitArray(int capacity)
        {
            if (capacity <= 0) capacity = 128; // _ CONFIG:
            _bits = new int[capacity];
            _bitsSet = 0;
        }
 
        private const int BITS_PER_ARRAY_ELEMENT = 32;
        
        private int[] _bits;
        private int _bitsSet = 0;

        public bool IsEmpty => _bitsSet == 0;

        public void Clone(BitArray origin)
        {
            if (origin._bits.Length > _bits.Length)
            {
                Array.Resize(ref _bits, origin._bits.Length);
            }
            Array.Copy(origin._bits, _bits, origin._bits.Length);
            _bitsSet = origin._bitsSet;

            if (_bits.Length > origin._bits.Length)
            {
                Array.Fill(_bits, 0, origin._bits.Length, _bits.Length - origin._bits.Length);
            }
        }

        public void Set(int id)
        {
            int index = id / BITS_PER_ARRAY_ELEMENT;
            int startingBit = id % BITS_PER_ARRAY_ELEMENT;

            EnsureCapacity(id);

            int bitsContainer = _bits[index];

            bitsContainer |= (1 << startingBit);
                
            _bits[index] = bitsContainer;

            Interlocked.Increment(ref _bitsSet);
        }

        public void ResetAll()
        {
            _bitsSet = 0;
            Array.Fill(_bits, 0);
        }

        public void Reset(int id)
        {
            int index = id / BITS_PER_ARRAY_ELEMENT;
            int startingBit = id % BITS_PER_ARRAY_ELEMENT;

            EnsureCapacity(id);

            int bitsContainer = _bits[index];

            bitsContainer &= ~(1 << startingBit);
                
            _bits[index] = bitsContainer;

            Interlocked.Decrement(ref _bitsSet);
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureCapacity(int newLength)
        {
            int length = (newLength + BITS_PER_ARRAY_ELEMENT + 1) / BITS_PER_ARRAY_ELEMENT + 1;
            if (length <= 0) length = 10; // _ CONFIG:
            if (length >= _bits.Length) 
            {
                var len = _bits.Length;

                while (len <= length) 
                {
                    len <<= 1;
                }
                Array.Resize(ref _bits, len);
            }
        }

        public bool Equals(BitArray other)
        {
            if (_bits == null && other._bits == null) return true;
            if (_bits == null || other._bits == null) return false;
            if (_bitsSet !=other._bitsSet) return false;

            int minLength = Mathf.Min(_bits.Length, other._bits.Length);

            for (int i = 0; i < minLength; i++)
            {
                if (_bits[i] != other._bits[i]) return false;
            }

            //Iterate over the rest of the arrays. Length could be different in case of accidental resize. The content matters more.
            //If any array is more in length - we need to check that the rest of the bits are 0.
            for (int i = minLength; i < _bits.Length; i++)
            {
                if (_bits[i] != 0) return false;
            }
            for (int i = minLength; i < other._bits.Length; i++)
            {
                if (other._bits[i] != 0) return false;
            }

            return true;
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            int hash = 17;
            for (int i = 0; i < _bits.Length; i++)
            {
                int value = _bits[i];
                unchecked
                {
                    hash = hash * 31 + value;
                }
            }

            return hash;
        }

        public bool this[int id]
        {
            /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get 
            {
                int index = id / BITS_PER_ARRAY_ELEMENT;
                int startingBit = id % BITS_PER_ARRAY_ELEMENT;

                if (index >= _bits.Length) return false;

                int bitsContainer = _bits[index];

                return (bitsContainer & (1 << startingBit)) != 0;
            }
        }
    }
}
