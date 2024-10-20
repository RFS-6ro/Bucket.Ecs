// ----------------------------------------------------------------------------
// The Proprietary or MIT License
// Copyright (c) 20224-2024 RFS_6ro <rfs6ro@gmail.com>
// ----------------------------------------------------------------------------

using BucketEcs.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace BucketEcs
{
    public interface IEcsSystem 
    {
        void CreateFilter(ComponentBitMask mask);
        void Init(EcsWorld world);
        void Run(float deltaTime, in IterationContext context);
    }

    public interface IEcsSystemsRunner
    {
        void Run(float deltaTime);
    }

    public interface IEcsSystems : IEcsSystemsRunner
    {
        void Init();
        IEcsSystems AddMainThread(IEcsSystem system);
    }


#region Base
    public partial class EcsSystems : IEcsSystems
    {
        public EcsSystems(EcsWorld world)
        {
            _world = world;
            RegisterSystemsConstructor();
            SingleThreadConstructor();
        }

        private readonly EcsWorld _world;
        private bool _inited;
    }
#endregion


#region Init
    public partial class EcsSystems
    {
        public void Init()
        {
            CollectSystemFilters();
            RunSystemsInit();
            _inited = true;
            _world.InitializeWorld();
        }

        private void RunSystemsInit()
        {
            for (int i = 0; i < _systemsCount; i++)
            {
                ref var systemContext = ref _allSystems[i];
            
                var system = systemContext.GetSystem();
                system.Init(_world);
            }
        }
    }
#endregion


#region ExecutionStrategies
    public enum ExecutionStrategy
    {
        None,
        SingleThreadSequential
    }

    public partial class EcsSystems
    {
        public void Run(float deltaTime)
        {
            ExecutionStrategy strategy = ExecutionStrategy.SingleThreadSequential;

            switch (strategy)
            {
                case ExecutionStrategy.SingleThreadSequential:
                    RunSingleThread(deltaTime);
                break;
                default: break;
            }
            
        }
    }
#endregion


#region RegisterSystems
    public partial class EcsSystems
    {
        private SafeGrowingArray<EcsSystemContext> _allSystems;

        private int _systemsCount;

        private void RegisterSystemsConstructor()
        {
            _systemsCount = 0;
            _allSystems = new SafeGrowingArray<EcsSystemContext>(100); //_ CONFIG
        }

        public virtual IEcsSystems AddMainThread(IEcsSystem system) 
        {
            if (_inited) 
            { 
                throw new System.Exception ("Cant add system after initialization."); 
            }
            
            _allSystems[_systemsCount] = new EcsSystemContext(system);
            _systemsCount++;

            return this;
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CollectSystemFilters()
        {
            for (int i = 0; i < _systemsCount; i++)
            {
                ref var systemContext = ref _allSystems[i];

                var system = systemContext.GetSystem();
                var componentBitMask = _world.GetNewComponentBitMask();

                system.CreateFilter(componentBitMask);

                EcsFilter filter = _world.GetOrCreateFilter(componentBitMask);
                
                systemContext.SetFilter(filter);
            }
        }

        public struct EcsSystemContext
        {
            private readonly IEcsSystem _system;
            
            private EcsFilter _filter;

            public EcsSystemContext(IEcsSystem system)
            {
                _filter = null;
                _system = system;
            }

            /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public IEcsSystem GetSystem() => _system;

            /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetFilter(EcsFilter filter) => _filter = filter;

            /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public EcsFilter GetFilter() => _filter;
        }
    }
#endregion


#region Events
    public interface IEcsSystemEnableIfEventIsRaised
    { 
        public int EnableEventId { get; }
    }

    public interface IEcsSystemDisableIfEventIsRaised
    { 
        public int DisableEventId { get; }
    }

    public partial class EcsSystems
    {
        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CheckEvents(IEcsSystem system)
        {
            //English or Spanish?
            if (system is IEcsSystemDisableIfEventIsRaised disableIfRaisedSystem 
                && _world.HasEventSingleton(disableIfRaisedSystem.DisableEventId)) return false;

            if (system is IEcsSystemEnableIfEventIsRaised enableIfRaisedSystem 
                && _world.HasEventSingleton(enableIfRaisedSystem.EnableEventId) == false) return false;

            return true;
        }
    }
#endregion


#region SingleThread
    public partial class EcsSystems
    {
        private RecycleArray<IterationContext> _iterationContexts;
        private RecycleArray<RemoveEntityContext> _entitiesToRemoveContainer;
        private List<EntityRepository> _entityRepositoriesInUseContainer;
        
        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SingleThreadConstructor()
        {
            _iterationContexts = new RecycleArray<IterationContext>(100); // _ CONFIG
            _entitiesToRemoveContainer = new RecycleArray<RemoveEntityContext>(100); // _ CONFIG
            _entityRepositoriesInUseContainer = new List<EntityRepository>(100); // _ CONFIG
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RunSingleThread(float deltaTime)
        {
            for (int i = 0; i < _systemsCount; i++)
            {
                ref EcsSystemContext systemContext = ref _allSystems[i];

                RunSystemSingleThread(deltaTime, ref systemContext);
            }
        }

        private void RunSystemSingleThread(float deltaTime, ref EcsSystemContext systemContext)
        {
            IEcsSystem system = systemContext.GetSystem();
            EcsFilter filter = systemContext.GetFilter();

            bool systemEventsMatch = CheckEvents(system);
            if (systemEventsMatch == false) return;

            CollectAllRepositoriesForIteration(filter);
            RunSystemOverContextsSingleThread(deltaTime, system);
            RebalanceAllUsedRepositoriesAfterIteration(ref systemContext);

            _entityRepositoriesInUseContainer.Clear();
            _entitiesToRemoveContainer.RecycleAll();
            _iterationContexts.RecycleAll();
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CollectAllRepositoriesForIteration(EcsFilter filter)
        {
            foreach (EntityRepositoryId entityRepositoryId in filter)
            {
                EntityRepository entityRepository = _world.GetEntityRepository(entityRepositoryId);

                _entityRepositoriesInUseContainer.Add(entityRepository);

                foreach (ref var chunk in entityRepository)
                {
                    int contextIndex = _iterationContexts.Allocate();
                    _iterationContexts[contextIndex] = new IterationContext
                    (
                        _entityRepositoriesInUseContainer,
                        _entitiesToRemoveContainer,
                        chunk.collection,
                        _world,

                        entityRepositoryId,
                        chunk.index
                    );
                }
            }
        }

        private void RunSystemOverContextsSingleThread(float deltaTime, IEcsSystem system)
        {
            for (int i = 0; i < _iterationContexts.Count; i++)
            {
                RunSystemOnASingleContext(deltaTime, system, i);
            }
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RunSystemOnASingleContext(float deltaTime, IEcsSystem system, int contextIndex)
        {
            ref readonly IterationContext context = ref _iterationContexts[contextIndex];
            system.Run(deltaTime, in context);
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RebalanceAllUsedRepositoriesAfterIteration(ref EcsSystemContext systemContext)
        {
            foreach (ref var entityToRemove in _entitiesToRemoveContainer)
            {
                EntityRepository entityRepository = _world.GetEntityRepository(entityToRemove.entityRepositoryId);
                entityRepository.RemoveEntity(entityToRemove.entityContainer, entityToRemove.entityIndex);
            }
        }
    }
#endregion
}
