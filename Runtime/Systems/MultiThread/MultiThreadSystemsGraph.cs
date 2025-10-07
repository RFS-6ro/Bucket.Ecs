using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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

    public unsafe class MultiThreadSystemsGraph : IDisposable
    {
        private readonly ISystemScheduler _scheduler;
        private readonly Scope.ScopeType _scope;
        private readonly EcsWorld _world;
        
        // List will contain system ids [155][344][75][42][23] etc
        private UnsafeList* _systemsInfo; // MultiThreadSystemInfo
        private UnsafeArray* _allSystemsInfo; // MultiThreadSystemInfo
        private MultiThreadSystemInfo* _allSystemsInfoRaw;

        // List will contain system indexes from _systemsInfo in order of execution
        private UnsafeList* _systemsExecutionOrder;

        internal UnsafeList* SystemsExecutionOrder => _systemsExecutionOrder; // TODO: ONLY FOR TESTS
        
        private bool RebuildEachFrame => Config.RebuildSystemsDependencyGraphEachFrame;

        public Scope.ScopeType ScopeType => _scope;

        private int _count;
        public int Count => _count;
        
        public MultiThreadSystemsGraph(ISystemScheduler scheduler, Scope.ScopeType scope, EcsWorld world)
        {
            _scheduler = scheduler;
            _scope = scope;
            _world = world;
            _count = 0;
            _systemsInfo = UnsafeList.Allocate<MultiThreadSystemInfo>(10);
            _systemsExecutionOrder = UnsafeList.Allocate<int>(10);
        }

        [Inline(256)]
        public void RegisterSystem<TSystem>(short contextIndex)
            where TSystem : unmanaged, IChunkSystem
        {
            short systemId = MultiThreadSystemDescriptor<TSystem>.TypeIndex;
            
            TSystem system = default(TSystem);
            SystemConditionsInfo systemConditionsInfo = default;
            if (system is ISpreadFramesSystem spreadFramesSystem)
            {
                systemConditionsInfo = SystemConditionsInfo.DelayByFrame(spreadFramesSystem.DelayFrames);
            } else if (system is ISpreadTimestampSystem spreadTimestampSystem)
            {
                systemConditionsInfo = SystemConditionsInfo.DelayByTime(spreadTimestampSystem.DelayTime);
            }

            // Save system, filter and conditions to info storage
            EcsUnmanagedFilter filter = _world.GetOrCreateUnmanagedFilter(systemId);

            // MultiThread System has only one filter and should be defined.
            new TSystem().GetFilterMask(filter.GetMask());

            _world.InitializeUnmanagedFilterWithAliveArchetypes(systemId);

            UnsafeList.Add
            (
                _systemsInfo, 
                new MultiThreadSystemInfo 
                (
                    filter,
                    systemId, 
                    systemConditionsInfo, 
                    MultiThreadSystemExecutionHelper.GetSystemRunMethodPtr<TSystem>(),
                    contextIndex
                )
            );
            _count++;
        }

        

        [Inline(256)]
        public void Build(in double deltaTime = -1) 
        {
            if (_allSystemsInfo == null)
            {
                _allSystemsInfo = UnsafeArray.Allocate<MultiThreadSystemInfo>(_count);
                _allSystemsInfoRaw = UnsafeArray.GetPtr<MultiThreadSystemInfo>(_allSystemsInfo, 0);
                
                UnsafeList.CopyTo<MultiThreadSystemInfo>(_systemsInfo, _allSystemsInfoRaw, 0);

                UnsafeList.Free(_systemsInfo);
                _systemsInfo = null;
            }
            if (_scope != Scope.ScopeType.UnorderedDependencyGraph) return;

            // order all system ids that presented in _systemsExecutionOrder
            // if ids are filled in sequence [1][2][3] then chunks in systems should be executed in parallel.
            // if one of the element is -1 that means we should wait for all chunks to be executed before continue with next elements

            UnsafeList.Clear(_systemsExecutionOrder);

            // Step 1: Build a blocking relationship graph
            // TODO: cache or optimize if build will happen every frame
            bool[,] blockingGraph = new bool[_count, _count];
            for (int i = 0; i < _count; i++)
            {
                for (int j = i + 1; j < _count; j++)
                {
                    ref MultiThreadSystemInfo systemInfoA = ref *(_allSystemsInfoRaw + i);
                    ref MultiThreadSystemInfo systemInfoB = ref *(_allSystemsInfoRaw + j);

                    if (AreBlocking(systemInfoA.Filter._dependenciesBits, systemInfoB.Filter._dependenciesBits))
                    {
                        blockingGraph[i, j] = true;
                        blockingGraph[j, i] = true;
                    }
                }
            }
        
            // Step 2: Greedy scheduling of execution steps
            // TODO: replace with unsafe and optimize if build will happen every frame
            HashSet<int> scheduled = new HashSet<int>();
            List<int> currentStep = new List<int>();
            while (scheduled.Count < _count)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (scheduled.Contains(i)) continue;
                    
                    bool canBeScheduled = true;
                    foreach (int s in currentStep)
                    {
                        if (blockingGraph[s, i] == false) continue;
                        
                        canBeScheduled = false;
                        break;
                    }

                    if (canBeScheduled) currentStep.Add(i);
                }

                foreach (int index in currentStep)
                {
                    scheduled.Add(index);
                    UnsafeList.Add(_systemsExecutionOrder, index);
                }

                if (scheduled.Count < _count)
                {
                    UnsafeList.Add(_systemsExecutionOrder, -1);
                }
                
                currentStep.Clear();
            }
        }

        private static unsafe bool AreBlocking(BitSet* systemA, BitSet* systemB)
        {
            int size = RUNTIME_REFERENCES.UnmanagedComponentsCount * Config.FilterBitsPerDependency;
            for (int i = 0; i < size; i += Config.FilterBitsPerDependency)
            {
                bool aRO = BitSet.IsSet(systemA, i);
                bool aRW = BitSet.IsSet(systemA, i + 1);

                if (aRO == false && aRW == false) continue;

                bool bRO = BitSet.IsSet(systemB, i);
                bool bRW = BitSet.IsSet(systemB, i + 1);

                if (bRO == false && bRW == false) continue;
                if (aRO == true && aRW == false && bRO == true && bRW == false) continue;

                return true;
            }
            return false;
        }

        [Inline(256)]
        public void Run(ulong currentFrame, double currentTime, in double deltaTime)
        {
            switch (_scope)
            {
                case Scope.ScopeType.MainThread:
                    UpdateSystemInMainThread(currentFrame, currentTime, in deltaTime);
                    break;
                case Scope.ScopeType.Parallel:
                    UpdateSystemAsSequenceParallel(currentFrame, currentTime, in deltaTime);
                    break;
                case Scope.ScopeType.UnorderedDependencyGraph:
                    UpdateSystemAsUnorderedDependencyGraph(currentFrame, currentTime, in deltaTime);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        [Inline(256)]
        private void UpdateSystemInMainThread(ulong currentFrame, double currentTime, in double deltaTime)
        {
            CommandsScheduler commandsScheduler = new CommandsScheduler(_world.SchedulledCommandsQueue);
            for (int index = 0; index < _count; index++)
            {
                ref MultiThreadSystemInfo systemInfo = ref *(_allSystemsInfoRaw + index);
                int systemId = systemInfo.SystemId;

                if (ShouldRun(ref systemInfo.ConditionsInfo, currentFrame, currentTime) == false) continue;

                byte* contextDataRaw = null;
                if (systemInfo.ContextTypeIndex != -1)
                {
                    contextDataRaw = _world.GetSystemContext(systemInfo.ContextTypeIndex);
                }

                // Get affected archetypes from filter
                foreach (ArchetypeId archetypeId in systemInfo.Filter)
                {
                    ref var archetype = ref _world.GetArchetype(archetypeId);
                    // For Each archetype Get chunks
                    foreach (var chunk in archetype)
                    {
                        UnmanagedChunkData data = chunk.GetUnmanagedChunkData(contextDataRaw, systemInfo.ContextTypeIndex);
                        data._dependenciesBits = systemInfo.Filter._dependenciesBits;
                        data._filterIncludeBits = systemInfo.Filter._filterIncludeBits;
                        MultiThreadSystemExecutionHelper.Execute(systemInfo.RunMethodPtr, in deltaTime, in data, in commandsScheduler);
                    }
                }
            }
        }

        [Inline(256)]
        private void UpdateSystemAsSequenceParallel(ulong currentFrame, double currentTime, in double deltaTime)
        {
            // TODO: IN FACT - RIGHT NOW PARALLEL WOULD ALLOW ACCESS TO SAME CHUNKS EVEN IF SYSTEMS ARE EXCLUDING EACH OTHER
            // TODO: Either create graph, using dependencies or implement a more difficult chunk-lock mechanism
            // NOTE: For now I fixed that with asserting the scheduler works with only one system
            // No need to use _systemsExecutionOrder - just run all systems in sequence
            for (int i = 0; i < _count; i++)
            {
                ref MultiThreadSystemInfo systemInfo = ref *(_allSystemsInfoRaw + i);
                int systemId = systemInfo.SystemId;

                if (ShouldRun(ref systemInfo.ConditionsInfo, currentFrame, currentTime) == false) continue;
                
                _scheduler.Schedule(in systemInfo, in deltaTime);
                _scheduler.Complete();
            }
        }

        [Inline(256)]
        private void UpdateSystemAsUnorderedDependencyGraph(ulong currentFrame, double currentTime, in double deltaTime)
        {
            if (RebuildEachFrame)
            {
                Build(in deltaTime);
            }

            // Run all systems according to _systemsExecutionOrder list
            for (int i = 0; i < UnsafeList.GetCount(_systemsExecutionOrder); i++)
            {
                // If ids are filled in sequence [1][2][3] then chunks in systems should be executed in parallel.
                // If one of the element is -1 that means we should wait for all chunks to be executed before continue with next systems
                int systemIndex = UnsafeList.Get<int>(_systemsExecutionOrder, i);

                if (systemIndex == -1)
                {
                    // Wait for all systems to execute
                    _scheduler.Complete();
                    continue;
                }

                ref var systemInfo = ref *(_allSystemsInfoRaw + systemIndex);

                // No need to check condition twice if we built the graph every frame with conditions taken into account 
                bool ignoreSystemConditions = RebuildEachFrame;
                if (ignoreSystemConditions == false)
                {
                    if (ShouldRun(ref systemInfo.ConditionsInfo, currentFrame, currentTime) == false) continue;
                }

                _scheduler.Schedule(in systemInfo, in deltaTime);
            }
            _scheduler.Complete();
        }

        [Inline(256)]
        private bool ShouldRun(ref SystemConditionsInfo conditionsInfo, ulong currentFrame, double currentTime)
        {
            if (conditionsInfo.IsDelayedByFrame)
            {
                if (currentFrame - conditionsInfo.LastUpdatedFrame < conditionsInfo.DelayFrameDelta)
                {
                    return false;
                }

                conditionsInfo.LastUpdatedFrame = currentFrame;
                return true;
            }

            if (conditionsInfo.IsDelayedByTime)
            {
                if (currentTime - conditionsInfo.LastUpdatedTime < conditionsInfo.DelayTimeDelta)
                {
                    return false;
                }
                
                conditionsInfo.LastUpdatedTime = currentTime;
                return true;
            }

            return true;
        }

        [Inline(256)]
        public void Dispose()
        {
            if (_systemsInfo != null)
            {
                UnsafeList.Free(_systemsInfo);
                _systemsInfo = null;
            }

            if (_allSystemsInfo != null)
            {
                UnsafeArray.Free(_allSystemsInfo);
                _allSystemsInfo = null;
                _allSystemsInfoRaw = null;
            }

            if (_systemsExecutionOrder != null)
            {
                UnsafeList.Free(_systemsExecutionOrder);
                _systemsExecutionOrder = null;
            }
        }
    }
}
