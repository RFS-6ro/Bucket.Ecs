using System;

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

    public abstract class EcsSystemsGroupDescriptor
    {
        internal EcsSystemsGroup Group { get; set; }
        internal EcsSystems Systems { get; set; }
        internal EcsWorld EcsWorld { get; set; }

        [Inline(256)]
        public virtual EcsSystemsGroupDescriptor AddGroup<T>() where T : EcsSystemsGroupDescriptor
        {
            T group = Systems.Group<T>();
            Group.RegisterGroup(group);
            return this;
        }

        [Inline(256)]
        public virtual EcsSystemsGroupDescriptor Add<T>() where T : SystemBase, new()
        {
            Group.RegisterSystem(new T());
            return this;
        }

        [Inline(256)]
        public EcsSystemsGroupDescriptor Add(SystemBase system)
        {
            Group.RegisterSystem(system);
            return this;
        }

        [Inline(256)]
        public void Run(in double deltaTime)
        {
            Group.Run(in deltaTime);
        }

        [Inline(256)]
        public EcsSystemsGroupDescriptor ThreadPoolScope(Action<ThreadPoolScopeBuilder> scope)
        {
            scope?.Invoke(new ThreadPoolScopeBuilder(Group, Systems));
            Systems.PopScope();
            return this;
        }

        [Inline(256)]
        public EcsSystemsGroupDescriptor AddSyncPoint()
        {
            Group.AddSyncPoint();
            return this;
        }
        
        public sealed class ThreadPoolScopeBuilder
        {
            private readonly EcsSystemsGroup _group;
            private readonly EcsSystems _systems;

            public ThreadPoolScopeBuilder(EcsSystemsGroup group, EcsSystems systems)
            {
                _group = group;
                _systems = systems;
                _systems.PushScope(Scope.ScopeType.UnorderedDependencyGraph);
            }

            [Inline(256)]
            public ThreadPoolScopeBuilder AddForEachSystem<T>() where T : unmanaged, IForEachSystem
            {
                _group.RegisterSystem<MultiThreadForEachSystem<T>>(-1);
                return this;
            }

            [Inline(256)]
            public ThreadPoolScopeBuilder AddChunkSystem<T>()
                where T : unmanaged, IChunkSystem
            {
                _group.RegisterSystem<T>(-1);
                return this;
            }

            [Inline(256)]
            public ThreadPoolScopeBuilder AddChunkSystem<T, TContext>()
                where T : unmanaged, IChunkSystem
                where TContext : unmanaged, IMultiThreadSystemContext
            {
                short contextTypeIndex = EcsMultiThreadSystemContextDescriptor<TContext>.TypeIndex;
                _group.RegisterSystem<T>(contextTypeIndex);
                return this;
            }

            [Inline(256)]
            public ThreadPoolScopeBuilder AddSystem<T>() where T : unmanaged, ISystem
            {
                // TODO: Register system
                return this;
            }

            [Inline(256)]
            public ThreadPoolScopeBuilder AddSyncPoint()
            {
                _group.AddSyncPoint();
                return this;
            }

            [Inline(256)]
            public ThreadPoolScopeBuilder Barrier()
            {
                // TODO: should assert schedulled systems are finished before any next system is registered, but no sync point is created
                _group.AddSyncPoint();
                return this;
            }

            [Inline(256)]
            public ThreadPoolScopeBuilder RespectOrder()
            {
                _systems.PopScope();
                _systems.PushScope(Scope.ScopeType.Parallel);
                return this;
            }
        }
    }
}
