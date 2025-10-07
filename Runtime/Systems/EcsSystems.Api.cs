using System;
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

    public class UpdateGroup : EcsSystemsGroupDescriptor { }
    public class FixedUpdateGroup : EcsSystemsGroupDescriptor { }
    public class LateUpdateGroup : EcsSystemsGroupDescriptor { }

    public sealed partial class EcsSystems
    {
        private Dictionary<Type, EcsSystemsGroupDescriptor> _groups;

        private List<EcsEngine.IStartCallback> _startCallbacks;
        private List<EcsEngine.IPauseCallback> _pauseCallbacks;
        private List<EcsEngine.IStopCallback> _stopCallbacks;

        private UpdateGroup _updateSystemsGroup;
        private FixedUpdateGroup _fixedUpdateSystemsGroup;
        private LateUpdateGroup _lateUpdateSystemsGroup;

        [Inline(256)]
        private void InitSystemGroups()
        {    
            _groups = new Dictionary<Type, EcsSystemsGroupDescriptor>();

            AddGroup<UpdateGroup>(); _updateSystemsGroup = Group<UpdateGroup>();
            AddGroup<FixedUpdateGroup>(); _fixedUpdateSystemsGroup = Group<FixedUpdateGroup>();
            AddGroup<LateUpdateGroup>(); _lateUpdateSystemsGroup = Group<LateUpdateGroup>();
            
            _startCallbacks = new();
            _pauseCallbacks = new();
            _stopCallbacks = new();
        }

        [Inline(256)]
        public EcsSystems AddFeature<T>(T feature) where T : IEcsFeature
        {
            feature.Register(this);
            return this;
        }

        [Inline(256)]
        public EcsSystems AddGroup<T>() where T : EcsSystemsGroupDescriptor, new()
        {
            var group = new T()
            {
                Group = new EcsSystemsGroup(_systemScheduler, this, _world),
                Systems = this,
                EcsWorld = _world
            };
            BAssert.False(_groups.ContainsKey(typeof(T)), $"Group of type {typeof(T)} is already registered");
            _groups.Add(typeof(T), group);
            return this;
        }

        [Inline(256)]
        public T Group<T>() where T : EcsSystemsGroupDescriptor
        {
            BAssert.True(_groups.TryGetValue(typeof(T), out var group), $"Group of type {typeof(T)} is not registered");
            return (T)group;
        }

        [Inline(256)]
        private void InitMainThreadSystems()
        {
            foreach (var groupDescriptor in _groups.Values)
            {
                groupDescriptor.Group.Init();
                groupDescriptor.Group.AddSyncPoint();
            }
        }

        [Inline(256)]
        public EcsSystems RegisterEngineCallbacks<TEngineCallback>(TEngineCallback callback)
            where TEngineCallback : class, EcsEngine.IEngineCallback
        {
            BAssert.False(_isInitialized, "EcsSystems has already been initialized.");

            if (callback is EcsEngine.IStartCallback startCallback)
            {
                _startCallbacks.Add(startCallback);
            }
            
            if (callback is EcsEngine.IPauseCallback pauseCallback)
            {
                _pauseCallbacks.Add(pauseCallback);
            }
            
            if (callback is EcsEngine.IStopCallback stopCallback)
            {
                _stopCallbacks.Add(stopCallback);
            }
            
            return this;
        }

        [Inline(256)]
        public void Start()
        {
            BAssert.True(_isInitialized, "EcsSystems was not yet initialized.");

            // Run Start In Systems
            for (int i = 0; i < _startCallbacks.Count; i++)
            {
                _startCallbacks[i].OnStart();
            }
        }

        [Inline(256)]
        public void Update(in double deltaTime)
        {
            // Run Update Systems
            _updateSystemsGroup.Run(in deltaTime);
            _world.OnUpdate();
        }

        [Inline(256)]
        public void FixedUpdate(in double fixedDeltaTime)
        {
            // Run Fixed Update Systems
            _fixedUpdateSystemsGroup.Run(in fixedDeltaTime);
        }

        [Inline(256)]
        public void LateUpdate(in double deltaTime)
        {
            // Run Late Update Systems
            _lateUpdateSystemsGroup.Run(in deltaTime);
        }

        [Inline(256)]
        public void OnPauseChanged(bool onPause)
        {
            BAssert.True(_isInitialized, "EcsSystems was not yet initialized.");

            for (int i = 0; i < _pauseCallbacks.Count; i++)
            {
                _pauseCallbacks[i].OnPause(onPause);
            }
        }

        [Inline(256)]
        public void OnStop()
        {
            BAssert.True(_isInitialized, "EcsSystems was not yet initialized.");

            for (int i = 0; i < _stopCallbacks.Count; i++)
            {
                _stopCallbacks[i].OnStop();
            }
        }

        [Inline(256)]
        private void DisposeSystemGroups()
        {
            foreach (var groupDescriptor in _groups.Values)
            {
                groupDescriptor.Group.Dispose();
            }
        }
    }
}
