using System;

namespace Bucket.Ecs.v3
{
    public unsafe struct MultiThreadSystemInfo
    {
        public int SystemId;
        public EcsUnmanagedFilter Filter;
        public SystemConditionsInfo ConditionsInfo;
        public IntPtr RunMethodPtr;
        public short ContextTypeIndex;

        public MultiThreadSystemInfo(EcsUnmanagedFilter filter, int systemId, SystemConditionsInfo conditionsInfo, IntPtr runMethodPtr, short contextTypeIndex)
        {
            Filter = filter;
            SystemId = systemId;
            ConditionsInfo = conditionsInfo;
            RunMethodPtr = runMethodPtr;
            ContextTypeIndex = contextTypeIndex;
        }
    }
}
