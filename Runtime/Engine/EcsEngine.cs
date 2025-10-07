using System;
using System.Threading;

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

    public sealed partial class EcsEngine : IDisposable
    {
        private EcsSystems _systems;
        private CancellationTokenSource _cts;

        private bool _onPause;
        private double _previousTimestamp = -1D;
        private double _fixedUpdateInterval;
        private double _fixedUpdateAccumulation;

        private uint _fixedFramesRate = 0;
        public uint FixedFramesRate
        {
            [Inline(256)]
            get => _fixedFramesRate;
            [Inline(256)]
            set
            {
                // TODO: Add Maximum Allowed Timestep, as one of the ways to prevent infinite loop
                _fixedFramesRate = value;
                
                if (value == 0) _fixedUpdateInterval = -1;

                _fixedUpdateInterval = 1000D / value;

                _fixedUpdateAccumulation = 0D;
            }
        }
        public bool IsRunning { [Inline(256)] get => _onPause == false && _cts != null; }

        public EcsEngine(EcsSystems systems, uint fixedFramesRate = 50)
        {
            _systems = systems;
            FixedFramesRate = fixedFramesRate;
            BLogger.Info($"Fixed Frames Rate = {_fixedFramesRate}, Fixed Update Interval = {_fixedUpdateInterval}");
        }

        public void Start(CancellationToken token = default)
        {
            if (_cts != null) return;
            _cts = new CancellationTokenSource();
            
            CancellationTokenSource source = token == default ? _cts : CancellationTokenSource.CreateLinkedTokenSource(token, _cts.Token);

            _systems.Start();
            SetPause(false);
            
            while (!source.Token.IsCancellationRequested)
            {
                if (_onPause) continue; 
                
                RunFrame();
            }
            
            source.Dispose();
            Stop();
        }

        [Inline(256)]
        private void RunFrame()
        {
            //We are calculating delta time in general here. that's ok. But in spread systems we should calculate using their info fields
            double currentTimestamp = DateTime.Now.Millisecond;
            double deltaTime = currentTimestamp - _previousTimestamp;

            // TODO: Test fixed update with Thread.sleep inside a system and check how can I prevent infinite loop (if fixed update execution takes more ms, than _fixedUpdateInterval) 
            if (_fixedUpdateInterval > 0D)
            {
                _fixedUpdateAccumulation += deltaTime;
                
                while (_fixedUpdateAccumulation >= _fixedUpdateInterval)
                {
                    _systems.FixedUpdate(in _fixedUpdateInterval);
                    _fixedUpdateAccumulation -= _fixedUpdateInterval;
                }
            }

            // TODO: Add frame limit support
            _systems.Update(in deltaTime);
            _systems.LateUpdate(in deltaTime);

            _previousTimestamp = currentTimestamp;
        }

        [Inline(256)]
        public void SetPause(bool isPaused)
        {
            if (_onPause == isPaused) return;

            _onPause = isPaused;
            _systems.OnPauseChanged(_onPause);
        }

        [Inline(256)]
        public void Stop()
        {
            if (_cts == null) return;

            _systems.OnStop();
            
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        [Inline(256)]
        public void Dispose()
        {
            Stop();
        }
    }
}
