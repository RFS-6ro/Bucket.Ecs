// ----------------------------------------------------------------------------
// The Proprietary or MIT License
// Copyright (c) 20224-2024 RFS_6ro <rfs6ro@gmail.com>
// ----------------------------------------------------------------------------

using System.Runtime.CompilerServices;

namespace BucketEcs
{
    public class EntityBuilder : System.IDisposable
    {
        public EntityBuilder(EcsWorld ecsWorld, int builderIndex)
        {
            _builderIndex = builderIndex;
            _ecsWorld = ecsWorld;
        }
 
        private readonly EcsWorld _ecsWorld;
        private readonly int _builderIndex;
        
        private EntityRepository _repository;
        private ComponentBitMask _mask;

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EntityBuilder SetMask(ComponentBitMask mask)
        {
            _mask = mask;
            _repository = null;
            return this;
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EntityBuilder Add<T>() where T : struct, IEcsComponent
        {
            var componentId = EcsComponentDescriptor<T>.TypeIndex;
            if (_ecsWorld.HasEcsPoolById(componentId) == false)
            {
                _ = _ecsWorld.GetEcsPool<T>();
            }

            _mask = _mask.With<T>();
            _repository = null;
            return this;
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EntityRepositoryId Create()
        {
            if (_repository == null) _repository = _ecsWorld.GetOrCreateEntityRepository(_mask);

            //In case repository was not yet created in the world
            if (_repository == null) return _ecsWorld.CreateEntity(_mask);
            
            _ecsWorld.CreateEntity(_repository);

            return _repository.Id;
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            throw new System.NotImplementedException();
            // _mask.Recycle();
            // _mask = null;
            // _ecsWorld.RecycleEntityBuilder(_builderIndex);
        }
    }
}
