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
    using System;

    public class MainThreadSystemInfo
    {
        private SystemBase _system;
        private SystemConditionsInfo _conditionsInfo;
        
        public SystemBase System { [Inline(256)] get => _system; }
                
        public MainThreadSystemInfo(SystemBase system)
        {
            _system = system;

            if (system is ISpreadFramesSystem spreadFramesSystem)
            {
                _conditionsInfo = SystemConditionsInfo.DelayByFrame(spreadFramesSystem.DelayFrames);
            } else if (system is ISpreadTimestampSystem spreadTimestampSystem)
            {
                _conditionsInfo = SystemConditionsInfo.DelayByTime(spreadTimestampSystem.DelayTime);
            }
        }

        [Inline(256)]
        public bool ShouldRun(ulong currentFrame, double currentTime)
        {
            if (_conditionsInfo.IsDelayedByFrame)
            {
                if (currentFrame - _conditionsInfo.LastUpdatedFrame < _conditionsInfo.DelayFrameDelta)
                {
                    return false;
                }

                _conditionsInfo.LastUpdatedFrame = currentFrame;
                return true;
            }

            if (_conditionsInfo.IsDelayedByTime)
            {
                if (currentTime - _conditionsInfo.LastUpdatedTime < _conditionsInfo.DelayTimeDelta)
                {
                    return false;
                }
                
                _conditionsInfo.LastUpdatedTime = currentTime;
                return true;
            }

            return true;
        }
    }
}
