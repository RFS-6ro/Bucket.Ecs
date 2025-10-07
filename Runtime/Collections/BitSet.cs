/*
The MIT License (MIT)

Copyright (c) 2019 Fredrik Holmstrom

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using UnsafeCollections;
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

    // This is a copy of BitSet from UnsafeCollections.Collections.Unsafe.
    public unsafe struct BitSet
    {
        const int WORD_SIZE = sizeof(ulong);
        const int BUCKET_SHIFT = 6;
        const int WORD_SIZE_BITS = WORD_SIZE * 8;
        const int WORD_SIZE_BITS_MASK = WORD_SIZE_BITS - 1;
        const ulong WORD_ONE = 1UL;
        const ulong WORD_ZERO = 0UL;

        [AllowUnsafePtr]
        ulong* _bits;

        int _sizeBits;
        int _sizeBuckets;

        public static BitSet* Allocate(int size)
        {
            if (size < 1)
                throw new ArgumentOutOfRangeException(nameof(size), "Must Be Non Negative", nameof(size));
            
            int capacity = size;
            if (capacity < WORD_SIZE_BITS) capacity = WORD_SIZE_BITS;

            var sizeOfHeader = Memory.RoundToAlignment(sizeof(BitSet), WORD_SIZE);
            // Round up to nearest multiple of bucket size (64 bits)
            var sizeOfBuffer = ((capacity + WORD_SIZE_BITS_MASK) >> BUCKET_SHIFT) * sizeof(ulong);

            var ptr = Memory.MallocAndZero(sizeOfHeader + sizeOfBuffer);
            var set = (BitSet*)ptr;

            set->_sizeBits = size;
            set->_sizeBuckets = (capacity + WORD_SIZE_BITS_MASK) >> BUCKET_SHIFT;
            set->_bits = (ulong*)((byte*)ptr + sizeOfHeader);

            return set;
        }

        public static void Free(BitSet* set)
        {
            if (set == null)
                return;

            // clear memory
            *set = default;

            // free memory
            Memory.Free(set);
        }

        [Inline(256)]
        public static int GetSize(BitSet* set)
        {
            return set->_sizeBits;
        }

        [Inline(256)]
        public static int GetBucketSize(BitSet* set)
        {
            return set->_sizeBuckets;
        }

        [Inline(256)]
        public static BitSet* Resize(BitSet* oldSet, int newSize)
        {
            BitSet* newSet = Allocate(newSize);

            int bucketsToCopy = Math.Min(oldSet->_sizeBuckets, newSet->_sizeBuckets);

            // Copy
            for (var i = 0; i < bucketsToCopy; i++)
            {
                newSet->_bits[i] = oldSet->_bits[i];
            }

            // Mask and clear rest bits in the bucket
            int validBits = Math.Min(oldSet->_sizeBits, newSize);
            if (validBits != 0)
            {
                int lastBucket = (validBits - 1) >> BUCKET_SHIFT;
                int bitsInLastBucket = validBits & WORD_SIZE_BITS_MASK;

                ulong mask = bitsInLastBucket == 0
                    ? ulong.MaxValue // bucket fully valid
                    : (1UL << bitsInLastBucket) - 1UL;

                newSet->_bits[lastBucket] &= mask;
            }


            // Clear rest buckets
            for (var i = oldSet->_sizeBuckets + 1; i < newSet->_sizeBuckets; ++i)
            {
                newSet->_bits[i] = WORD_ZERO;
            }

            Free(oldSet);

            return newSet;
        }

        [Inline(256)]
        public static void Clear(BitSet* set)
        {
            Memory.ZeroMem(set->_bits, set->_sizeBuckets * WORD_SIZE);
        }

        [Inline(256)]
        public static void Set(BitSet* set, int bit, bool state)
        {
            if (state)
            {
                Set(set, bit);
            }
            else
            {
                Clear(set, bit);
            }
        }

        [Inline(256)]
        public static void Set(BitSet* set, int bit)
        {
            if ((uint)bit >= (uint)set->_sizeBits)
            {
                throw new IndexOutOfRangeException("Index out of range");
            }

            set->_bits[bit >> BUCKET_SHIFT] |= WORD_ONE << (bit & WORD_SIZE_BITS_MASK);
        }

        [Inline(256)]
        public static void SetAll(BitSet* set, bool state, int count)
        {
            if ((uint)count > (uint)set->_sizeBits)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Count exceeds bitset size");
            }

            int fullBuckets = count >> BUCKET_SHIFT;
            int remainingBits = count & WORD_SIZE_BITS_MASK;
            ulong fillValue = state ? ulong.MaxValue : 0UL;

            for (int i = 0; i < fullBuckets; ++i)
            {
                set->_bits[i] = fillValue;
            }

            if (remainingBits > 0)
            {
                ulong mask = (1UL << remainingBits) - 1UL;
                set->_bits[fullBuckets] = state ? mask : 0UL;
                fullBuckets++;
            }

            for (int i = fullBuckets; i < set->_sizeBuckets; ++i)
            {
                set->_bits[i] = 0UL;
            }
        }

        [Inline(256)]
        public static void Clear(BitSet* set, int bit)
        {
            if ((uint)bit >= (uint)set->_sizeBits)
            {
                throw new IndexOutOfRangeException("Index out of range");
            }

            set->_bits[bit >> BUCKET_SHIFT] &= ~(WORD_ONE << (bit & WORD_SIZE_BITS_MASK));
        }

        [Inline(256)]
        public static bool IsSet(BitSet* set, int bit)
        {
            if ((uint)bit >= (uint)set->_sizeBits)
            {
                throw new IndexOutOfRangeException("Index out of range");
            }

            return (set->_bits[bit >> BUCKET_SHIFT] & (WORD_ONE << (bit & WORD_SIZE_BITS_MASK))) != WORD_ZERO;
        }

        [Inline(256)]
        public static bool CopyFrom(BitSet* set, BitSet* other)
        {
            if (set->_sizeBits != other->_sizeBits)
            {
                throw new InvalidOperationException("BitSet sizes should be equal.");
            }

            bool anySet = false;
            for (var i = (set->_sizeBuckets - 1); i >= 0; --i)
            {
                set->_bits[i] = other->_bits[i];
                if (set->_bits[i] != WORD_ZERO)
                {
                    anySet = true;
                }
            }
            return anySet;
        }

        [Inline(256)]
        public static void CopyAnySize(BitSet* set, BitSet* other)
        {
            int minSizeBuckets = other->_sizeBuckets;
            if (minSizeBuckets > set->_sizeBuckets)
            {
                minSizeBuckets = set->_sizeBuckets;
            }

            for (var i = (minSizeBuckets - 1); i >= 0; --i)
            {
                set->_bits[i] = other->_bits[i];
            }
        }

        [Inline(256)]
        public static bool Or(BitSet* set, BitSet* other)
        {
            if (set->_sizeBits != other->_sizeBits)
            {
                throw new InvalidOperationException("BitSet sizes should be equal.");
            }

            bool anySet = false;
            for (var i = (set->_sizeBuckets - 1); i >= 0; --i)
            {
                set->_bits[i] |= other->_bits[i];
                if (set->_bits[i] != WORD_ZERO)
                {
                    anySet = true;
                }
            }
            return anySet;
        }

        [Inline(256)]
        public static bool And(BitSet* set, BitSet* other)
        {
            if (set->_sizeBits != other->_sizeBits)
            {
                throw new InvalidOperationException("BitSet sizes should be equal.");
            }

            bool anySet = false;
            for (var i = (set->_sizeBuckets - 1); i >= 0; --i)
            {
                set->_bits[i] &= other->_bits[i];
                if (set->_bits[i] != WORD_ZERO)
                {
                    anySet = true;
                }
            }
            return anySet;
        }

        [Inline(256)]
        public static int CountSet(BitSet* set)
        {
            int count = 0;

            for (int i = 0; i < set->_sizeBuckets; i++)
            {
                ulong v = set->_bits[i];

                // Hamming weight (64-bit popcount, fallback version)
                v = v - ((v >> 1) & 0x5555555555555555UL);
                v = (v & 0x3333333333333333UL) + ((v >> 2) & 0x3333333333333333UL);
                v = (v + (v >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
                v = v * 0x0101010101010101UL;
                count += (int)(v >> 56);
            }

            return count;
        }

        [Inline(256)]
        public static bool GetFirstClearBit(BitSet* set, out int bit)
        {
            bit = 0;
            for (var i = 0; i < set->_sizeBuckets; ++i)
            {
                ulong inverted = ~set->_bits[i];
                if (inverted == WORD_ZERO) continue;

                ulong lowestSet = inverted & (ulong)-(long)inverted;
                int bitOffset = 0;
                if ((inverted & 0xFFFFFFFFUL) == 0)
                {
                    bitOffset = (WORD_SIZE_BITS / 2);
                    bit += bitOffset;
                    inverted >>= bitOffset;
                }
                if ((inverted & 0xFFFFUL) == 0)
                {
                    bitOffset = (WORD_SIZE_BITS / 4);
                    bit += bitOffset;
                    inverted >>= bitOffset;
                }
                if ((inverted & 0xFFUL) == 0)
                {
                    bitOffset = (WORD_SIZE_BITS / 8);
                    bit += bitOffset;
                    inverted >>= bitOffset;
                }
                if ((inverted & 0xFUL) == 0)
                {
                    bitOffset = (WORD_SIZE_BITS / 16);
                    bit += bitOffset;
                    inverted >>= bitOffset;
                }
                if ((inverted & 0x3UL) == 0)
                {
                    bitOffset = (WORD_SIZE_BITS / 32);
                    bit += bitOffset;
                    inverted >>= bitOffset;
                }
                if ((inverted & 0x1UL) == 0)
                {
                    bit += 1;
                }
                
                bit += i * WORD_SIZE_BITS;

                if (bit >= set->_sizeBits) break;

                return true;
            }
            bit = -1;
            return false;
        }

        [Inline(256)]
        public static bool Xor(BitSet* set, BitSet* other)
        {
            if (set->_sizeBits != other->_sizeBits)
            {
                throw new InvalidOperationException("BitSet sizes should be equal.");
            }

            bool anySet = false;
            for (var i = (set->_sizeBuckets - 1); i >= 0; --i)
            {
                set->_bits[i] ^= other->_bits[i];
                if (set->_bits[i] != WORD_ZERO)
                {
                    anySet = true;
                }
            }
            return anySet;
        }

        [Inline(256)]
        public static bool AndNot(BitSet* set, BitSet* other)
        {
            if (set->_sizeBits != other->_sizeBits)
            {
                throw new InvalidOperationException("BitSet sizes should be equal.");
            }

            bool anySet = false;
            for (var i = (set->_sizeBuckets - 1); i >= 0; --i)
            {
                set->_bits[i] &= ~other->_bits[i];
                if (set->_bits[i] != WORD_ZERO)
                {
                    anySet = true;
                }
            }
            return anySet;
        }

        [Inline(256)]
        public static void Not(BitSet* set)
        {
            for (var i = (set->_sizeBuckets - 1); i >= 0; --i)
            {
                set->_bits[i] = ~set->_bits[i];
            }
        }

        [Inline(256)]
        public static bool AnySet(BitSet* set)
        {
            for (var i = (set->_sizeBuckets - 1); i >= 0; --i)
            {
                if (set->_bits[i] != WORD_ZERO)
                {
                    return true;
                }
            }

            return false;
        }

        [Inline(256)]
        public static Enumerator GetEnumerator(BitSet* set)
        {
            return new Enumerator(set);
        }

        [Inline(256)]
        public static ReverseEnumerator GetReverseEnumerator(BitSet* set)
        {
            return new ReverseEnumerator(set);
        }

        [Inline(256)]
        public static SetBitEnumerator GetSetEnumerator(BitSet* set)
        {
            return new SetBitEnumerator(set);
        }

        public static int ToArray(BitSet* set, UnsafeArray* array)
        {
            if (UnsafeArray.GetLength(array) < set->_sizeBits)
            {
                throw new InvalidOperationException("ArrayPlusOffTooSmall");
            }

            var setCount = 0;
            var bitOffset = 0;
            var arrayBuffer = UnsafeArray.GetPtr<int>(array, 0);

            for (var i = 0; i < set->_sizeBuckets; ++i)
            {
                var word64 = set->_bits[i];
                if (word64 == WORD_ZERO)
                {
                    // since we're skipping whole word, step up offset 
                    bitOffset += WORD_SIZE_BITS;
                    continue;
                }

                var word32Count = 0;

            NEXT_WORD32:
                var word32 = *((uint*)&word64 + word32Count);
                if (word32 != 0)
                {
                    var word16Count = 0;

                NEXT_WORD16:
                    var word16 = *((ushort*)&word32 + word16Count);
                    if (word16 != 0)
                    {
                        var word8Count = 0;

                    NEXT_WORD8:
                        var word8 = *((byte*)&word16 + word8Count);
                        if (word8 != 0)
                        {
                            if ((word8 & (1 << 0)) == 1 << 0) arrayBuffer[setCount++] = (bitOffset + 0);
                            if ((word8 & (1 << 1)) == 1 << 1) arrayBuffer[setCount++] = (bitOffset + 1);
                            if ((word8 & (1 << 2)) == 1 << 2) arrayBuffer[setCount++] = (bitOffset + 2);
                            if ((word8 & (1 << 3)) == 1 << 3) arrayBuffer[setCount++] = (bitOffset + 3);
                            if ((word8 & (1 << 4)) == 1 << 4) arrayBuffer[setCount++] = (bitOffset + 4);
                            if ((word8 & (1 << 5)) == 1 << 5) arrayBuffer[setCount++] = (bitOffset + 5);
                            if ((word8 & (1 << 6)) == 1 << 6) arrayBuffer[setCount++] = (bitOffset + 6);
                            if ((word8 & (1 << 7)) == 1 << 7) arrayBuffer[setCount++] = (bitOffset + 7);
                        }

                        // always step up bitoffset here
                        bitOffset += (WORD_SIZE_BITS / 8);

                        if (word8Count == 0)
                        {
                            ++word8Count;

                            // go back
                            goto NEXT_WORD8;
                        }
                    }
                    else
                    {
                        bitOffset += (WORD_SIZE_BITS / 4);
                    }

                    if (word16Count == 0)
                    {
                        ++word16Count;

                        // go back
                        goto NEXT_WORD16;
                    }
                }
                else
                {
                    bitOffset += (WORD_SIZE_BITS / 2);
                }

                if (word32Count == 0)
                {
                    ++word32Count;

                    // go back
                    goto NEXT_WORD32;
                }
            }

            return setCount;
        }

        [Inline(256)]
        public static bool AreEqual(BitSet* set, BitSet* other)
        {
            if (set->_sizeBits != other->_sizeBits)
            {
                throw new InvalidOperationException("BitSet sizes should be equal.");
            }

            for (var i = (set->_sizeBuckets - 1); i >= 0; --i)
            {
                if (set->_bits[i] != other->_bits[i])
                    return false;
            }

            return true;
        }

        [Inline(256)]
        public static ulong GetHashCode(BitSet* set)
        {
            unchecked
            {
                int primeIndex = 0;
                int primeNumbersLength = _primeTable.Length - 1;
                ulong hash = 2166136261;

                for (var i = (set->_sizeBuckets - 1); i >= 0; --i)
                {
                    ulong bucket = set->_bits[i];
                    ++primeIndex;
                    primeIndex %= primeNumbersLength;

                    uint prime = _primeTable[primeIndex];
                    hash ^= (ulong)i;
                    hash ^= bucket * prime;
                }

                return hash;
            }
        }

        private static uint[] _primeTable = new uint[]
        {
            3,
            7,
            17,
            29,
            53,
            97,
            193,
            389,
            769,
            1543,
            3079,
            6151,
            12289,
            24593,
            49157,
            98317,
            196613,
            393241,
            786433,
            1572869,
            3145739,
            6291469,
            12582917,
            25165843,
            50331653,
            100663319,
            201326611,
            402653189,
            805306457,
            1610612741
        };

        public unsafe struct Enumerator : IUnsafeEnumerator<(int bit, bool set)>
        {
            private BitSet* _set;
            private int _current;

            internal Enumerator(BitSet* set)
            {
                _set = set;
                _current = -1;
            }

            [Inline(256)]
            public bool MoveNext()
            {
                return ++_current < _set->_sizeBits;
            }

            [Inline(256)]
            public void Reset()
            {
                _current = -1;
            }

            public (int bit, bool set) Current
            {
                [Inline(256)]
                get
                {
                    BAssert.IndexInRange(_current, _set->_sizeBits);
                    bool isSet = (_set->_bits[_current >> BUCKET_SHIFT] & (WORD_ONE << (_current & WORD_SIZE_BITS_MASK))) != WORD_ZERO;
                    return (_current, isSet);
                }
            }

            object IEnumerator.Current
            {
                get { return Current; }
            }

            public void Dispose()
            {
            }

            public Enumerator GetEnumerator()
            {
                return this;
            }
            IEnumerator<(int bit, bool set)> IEnumerable<(int bit, bool set)>.GetEnumerator()
            {
                return this;
            }
            IEnumerator IEnumerable.GetEnumerator()
            {
                return this;
            }
        }

        public unsafe struct ReverseEnumerator : IUnsafeEnumerator<(int bit, bool set)>
        {
            private BitSet* _set;
            private int _current;

            internal ReverseEnumerator(BitSet* set)
            {
                _set = set;
                _current = _set->_sizeBits;
            }

            [Inline(256)]
            public bool MoveNext()
            {
                return --_current >= 0;
            }

            [Inline(256)]
            public void Reset()
            {
                _current = _set->_sizeBits;
            }

            public (int bit, bool set) Current
            {
                [Inline(256)]
                get
                {
                    BAssert.IndexInRange(_current, _set->_sizeBits);
                    bool isSet = (_set->_bits[_current >> BUCKET_SHIFT] & (WORD_ONE << (_current & WORD_SIZE_BITS_MASK))) != WORD_ZERO;
                    return (_current, isSet);
                }
            }

            object IEnumerator.Current
            {
                get { return Current; }
            }

            public void Dispose()
            {
            }

            public ReverseEnumerator GetEnumerator()
            {
                return this;
            }
            IEnumerator<(int bit, bool set)> IEnumerable<(int bit, bool set)>.GetEnumerator()
            {
                return this;
            }
            IEnumerator IEnumerable.GetEnumerator()
            {
                return this;
            }
        }

        public unsafe struct SetBitEnumerator : IUnsafeEnumerator<int>
        {
            private readonly BitSet* _set;
            private int _bucketIndex;
            private ulong _bucketBits;
            private int _currentBit;

            internal SetBitEnumerator(BitSet* set)
            {
                _set = set;
                _bucketIndex = -1;
                _bucketBits = 0;
                _currentBit = -1;
            }

            public int Current => _currentBit;
            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                while (true)
                {
                    // If bucket still has bits, continue extracting
                    if (_bucketBits != 0)
                    {
                        int bitOffset = 0;
                        while ((_bucketBits & 1UL) == 0)
                        {
                            _bucketBits >>= 1;
                            bitOffset++;
                        }
                        _bucketBits &= _bucketBits - 1; // clear lowest set bit

                        _currentBit = (_bucketIndex << BitSet.BUCKET_SHIFT) + bitOffset;

                        if (_currentBit < _set->_sizeBits)
                            return true;
                    }
                    else
                    {
                        // Move to next bucket
                        _bucketIndex++;
                        if (_bucketIndex >= _set->_sizeBuckets)
                            return false;

                        _bucketBits = _set->_bits[_bucketIndex];
                    }
                }
            }

            public void Reset()
            {
                _bucketIndex = -1;
                _bucketBits = 0;
                _currentBit = -1;
            }

            public void Dispose() { }

            public SetBitEnumerator GetEnumerator()
            {
                return this;
            }
            IEnumerator<int> IEnumerable<int>.GetEnumerator()
            {
                return this;
            }
            IEnumerator IEnumerable.GetEnumerator()
            {
                return this;
            }
        }
    }
}
