using System.Collections.Generic;

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

    public sealed class EcsSystemsGroup
    {
        private readonly ISystemScheduler _scheduler;
        private readonly EcsSystems _systems;
        private readonly EcsWorld _world;

        private ulong _currentFrame;
        private double _currentTime;
        
        private readonly List<ExecutionStep> _steps;
        
        public EcsSystemsGroup(ISystemScheduler scheduler, EcsSystems systems, EcsWorld world)
        {
            _scheduler = scheduler;
            _systems = systems;
            _world = world;
            
            _steps = new List<ExecutionStep>();

            _currentFrame = 0;
            _currentTime = double.MinValue;
        }

        [Inline(256)]
        public void RegisterSystem<TSystem>(TSystem system)
            where TSystem : SystemBase
        {   
            if (system is EcsEngine.IEngineCallback engineCallback)
            {
                _systems.RegisterEngineCallbacks(engineCallback);
            }

            if (_steps.Count > 0 && _steps[_steps.Count - 1].Systems != null)
            {
                _steps[_steps.Count - 1].Systems.Add(new MainThreadSystemInfo(system));
            }
            else
            {
                _steps.Add(new ExecutionStep()
                {
                    Systems = new() { new MainThreadSystemInfo(system) },
                    Graph = null,
                    SubGroup = null
                });
            }
        }

        [Inline(256)]
        public void RegisterSystem<TSystem>(short contextTypeIndex)
            where TSystem : unmanaged, IChunkSystem
        {
            if (_scheduler == null)
            {
                BExceptionThrower.SchedulerIsNotAssigned();
            }
            
            Scope.ScopeType currentScope = _systems.GetCurrentScope();
            
            bool currentScopeIsMainThread = currentScope == Scope.ScopeType.MainThread;
            if (currentScopeIsMainThread)
            {
                BLogger.Warning("MultiThreadSystem registered on main thread scope.");
            }

            GetPreviousStep(out var previousStep);
            if (previousStep.Graph != null && previousStep.Graph.ScopeType == currentScope)
            {
                previousStep.Graph.RegisterSystem<TSystem>(contextTypeIndex);
            }
            else
            {
                var graph = new MultiThreadSystemsGraph(_scheduler, currentScope, _world);
                graph.RegisterSystem<TSystem>(contextTypeIndex);
                _steps.Add(new ExecutionStep()
                {
                    Systems = null,
                    Graph = graph,
                    SubGroup = null
                });
            }
        }

        [Inline(256)]
        public void RegisterGroup<T>(T groupDescriptor) where T : EcsSystemsGroupDescriptor
        {
            _steps.Add(new ExecutionStep()
            {
                Systems = null,
                Graph = null,
                SubGroup = groupDescriptor.Group
            });
        }

        [Inline(256)]
        private void GetPreviousStep(out ExecutionStep step)
        {
            step = default;

            if (_steps.Count - 1 < 0)
            {
                return;
            }

            step = _steps[_steps.Count - 1];
        }

        [Inline(256)]
        public void AddSyncPoint()
        {
            if (_steps.Count - 1 >= 0)
            {
                if (_steps[_steps.Count - 1].Graph == null 
                    && _steps[_steps.Count - 1].Systems == null)
                {
                    // We already have one sync point, no need to add another
                    return;
                }
            }
            
            // if both strategies are null, then we won't execute anything, but sync point
            _steps.Add(new ExecutionStep()
            {
                Systems = null,
                Graph = null,
                SubGroup = null
            });
        }

        [Inline(256)]
        public void Init()
        {
            foreach (var step in _steps)
            {
                if (step.Graph != null)
                {
                    // Build graph
                    step.Graph.Build();
                }
                else if (step.Systems != null)
                {
                    // Sort main thread systems from previous step
                    step.Systems.Sort((a, b) => a.System.Priority.CompareTo(b.System.Priority));
                    for (var index = 0; index < step.Systems.Count; index++)
                    {
                        step.Systems[index].System.World = _world;
                        step.Systems[index].System.Init();
                    }
                }
            }
        }

        public void Run(in double deltaTime)
        {
            _currentFrame++;
            _currentTime += deltaTime;

            foreach (var step in _steps)
            {
                if (step.Systems != null)
                {
                    for (var index = 0; index < step.Systems.Count; index++)
                    {
                        if (step.Systems[index].ShouldRun(_currentFrame, _currentTime) == false) continue;
                        
                        step.Systems[index].System.Run(in deltaTime);

                        _world.RunSyncPoint();
                    }
                }
                else if (step.Graph != null)
                {
                    step.Graph.Run(_currentFrame, _currentTime, in deltaTime);
                    _world.DispatchSchedulledCommands();
                    _world.RunSyncPoint();
                }
                else if (step.SubGroup != null)
                {
                    step.SubGroup.Run(in deltaTime);
                }
                else
                {
                    _world.RunSyncPoint();
                }
            }
        }

        [Inline(256)]
        public void Dispose()
        {
            foreach (var step in _steps)
            {
                if (step.Graph != null)
                {
                    step.Graph.Dispose();
                }
                else if (step.Systems != null)
                {
                    foreach (var system in step.Systems)
                    {
                        system.System.Dispose();
                    }
                }
            }
        }

        internal struct ExecutionStep
        {
            public List<MainThreadSystemInfo> Systems;
            public MultiThreadSystemsGraph Graph;
            public EcsSystemsGroup SubGroup;
        }
    }
}
