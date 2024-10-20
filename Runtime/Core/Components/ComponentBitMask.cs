// ----------------------------------------------------------------------------
// The Proprietary or MIT License
// Copyright (c) 20224-2024 RFS_6ro <rfs6ro@gmail.com>
// ----------------------------------------------------------------------------

using BucketEcs.Collections;
using System.Runtime.CompilerServices;

namespace BucketEcs
{
    public class ComponentBitMask : System.IEquatable<ComponentBitMask>
    {
        public ComponentBitMask(EcsWorld ecsWorld, int index)
        {
            _index = index;
            _ecsWorld = ecsWorld;
            _bitsPerComponent = 2;
            _count = EcsWorld.LastRegisteredComponentTypeId + 1; //+1 because LastRegisteredComponentTypeId is a valid number
            _bits = new BitArray(_count * _bitsPerComponent);
            _hash = -1;
        }
 
        private readonly EcsWorld _ecsWorld;
        private readonly BitArray _bits;

        private readonly int _bitsPerComponent;
        private readonly int _index;

        private int _count;
        private int _hash;

        public BitMaskIncludeEnumerator IncludeIterator
        {
            /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new BitMaskIncludeEnumerator(this);
        }

        public BitMaskExcludeEnumerator ExcludeIterator
        {
            /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new BitMaskExcludeEnumerator(this);
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clone(ComponentBitMask origin)
        {
            _bits.Clone(origin._bits);
            _hash = origin._hash;
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ComponentBitMask With<T>() where T : struct, IEcsComponent
        {
            var componentId = GetId<T>();
            if (_ecsWorld.HasEcsPoolById(componentId) == false)
            {
                _ = _ecsWorld.GetEcsPool<T>();
            }

            return With(componentId);
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ComponentBitMask With(ComponentId componentId)
        {
            int index = ((ushort)componentId) * _bitsPerComponent;

            _bits.Set(index);
            _bits.Reset(index + 1);

            _hash = _bits.GetHashCode();

            return this;
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ComponentBitMask Without<T>() where T : struct, IEcsComponent
        {
            var componentId = GetId<T>();
            if (_ecsWorld.HasEcsPoolById(componentId) == false)
            {
                _ = _ecsWorld.GetEcsPool<T>();
            }

            return Without(componentId);
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ComponentBitMask Without(ComponentId componentId)
        {
            int index = ((ushort)componentId) * _bitsPerComponent;

            _bits.Set(index + 1);
            _bits.Reset(index);

            _hash = _bits.GetHashCode();

            return this;
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ComponentBitMask Reset<T>() where T : struct, IEcsComponent
        {
            var componentId = GetId<T>();
            if (_ecsWorld.HasEcsPoolById(componentId) == false)
            {
                _ = _ecsWorld.GetEcsPool<T>();
            }

            return Reset(componentId);
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ComponentBitMask Reset(ComponentId componentId)
        {
            int index = ((ushort)componentId) * _bitsPerComponent;

            _bits.Reset(index);
            _bits.Reset(index + 1);

            _hash = _bits.GetHashCode();

            return this;
        }

        public bool IsIncluding(ComponentBitMask repositoryAttachedComponentsMask)
        {
            _bits.EnsureCapacity(_count * _bitsPerComponent);
            for (int i = 0, j = 0; i < _count * _bitsPerComponent; i += _bitsPerComponent, j++)
            {
                bool includedInMask = _bits[i];
                bool excludedInMask = _bits[i + 1];
                bool notListedInMask = includedInMask == false && excludedInMask == false;

                if (repositoryAttachedComponentsMask._count < j) 
                {
                    //if there's no more components attached to entities in the repository, but filter requires one
                    if (includedInMask) return false; 
                    else continue;
                }

                bool includedInRepository = repositoryAttachedComponentsMask._bits[i];
                // bool excludedInRepository = repositoryAttachedComponentsMask._bits[i + 1]; // This bit should never be set anyway.
                bool notListedInRepository = includedInRepository == false;

                if (notListedInMask && notListedInRepository) continue;

                if (includedInMask && includedInRepository) continue;
                if (includedInMask && notListedInRepository) return false;
                
                if (excludedInMask && notListedInRepository) continue;
                if (excludedInMask && includedInRepository) return false;
            }

            return true;
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsComponentIncluded<T>() where T : struct, IEcsComponent
        {
            int index = ((ushort)GetId<T>()) * _bitsPerComponent;

            _bits.EnsureCapacity(index + 1);

            return _bits[index];
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsComponentExcluded<T>() where T : struct, IEcsComponent
        {
            int index = ((ushort)GetId<T>()) * _bitsPerComponent;

            _bits.EnsureCapacity(index + 1);

            return _bits[index + 1];
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAdd(ComponentId componentId, out ComponentBitMask newMask)
        {
            newMask = null;
            int index = ((ushort)componentId) * _bitsPerComponent;

            _bits.EnsureCapacity(index);

            if (_bits[index]) return false;

            newMask = _ecsWorld.GetNewComponentBitMask();
            newMask.Clone(this);

            newMask.With(componentId);

            return true;
        }
        
        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDel(ComponentId componentId, out ComponentBitMask newMask)
        {
            newMask = null;
            int index = ((ushort)componentId) * _bitsPerComponent;
            
            _bits.EnsureCapacity(index);

            if (_bits[index] == false) return false;

            newMask = _ecsWorld.GetNewComponentBitMask();
            newMask.Clone(this);

            newMask.Without(componentId);

            return true;
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ComponentId GetId<T>() where T : struct, IEcsComponent
        {
            return EcsComponentDescriptor<T>.TypeIndex;
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            _hash = -1;
            _count = EcsWorld.LastRegisteredComponentTypeId + 1; //+1 because LastRegisteredComponentTypeId is a valid number
            _bits.EnsureCapacity(_count * _bitsPerComponent);
            _bits.ResetAll();
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Recycle() 
        {
            Reset();
            _ecsWorld.RecycleBitMask(_index);
        }

        public bool Equals(ComponentBitMask other) => _bits.Equals(other);

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => _hash;

        public struct BitMaskEnumerator 
        {
            public BitMaskEnumerator(ComponentBitMask mask, bool targetBitValue) 
            {
                _count = (mask._count + 1) * mask._bitsPerComponent;
                _index = -(mask._bitsPerComponent);
                _targetBitValue = targetBitValue;
                _bits = mask._bits;
                _mask = mask;
            }
 
            private readonly ComponentBitMask _mask;
            private readonly bool _targetBitValue;
            private readonly BitArray _bits;
            private readonly int _count;
            private int _index;

            public ComponentId Current 
            {
                /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => (ComponentId)(_index / _mask._bitsPerComponent);
            }

            /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() 
            {
                do
                {
                    _index += _mask._bitsPerComponent; 

                    if (_index >= _count) return false;
                    
                    bool included = _bits[_index];
                    bool excluded = _bits[_index + 1];

                    if (_targetBitValue && included && excluded == false) return true;
                    if (_targetBitValue == false && included == false && excluded) return true;

                } while (_index < _count);

                return false;
            }
        }

        public readonly struct BitMaskIncludeEnumerator 
        {
            public BitMaskIncludeEnumerator(ComponentBitMask mask) 
            {
                _mask = mask;
            }

            private readonly ComponentBitMask _mask;

            /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public BitMaskEnumerator GetEnumerator() => new BitMaskEnumerator(_mask, true);
        }

        public readonly struct BitMaskExcludeEnumerator 
        {
            public BitMaskExcludeEnumerator(ComponentBitMask mask) 
            {
                _mask = mask;
            }
 
            private readonly ComponentBitMask _mask;

            /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public BitMaskEnumerator GetEnumerator() => new BitMaskEnumerator(_mask, false);
        }
    }
}
