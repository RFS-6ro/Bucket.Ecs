// ----------------------------------------------------------------------------
// The Proprietary or MIT License
// Copyright (c) 20224-2024 RFS_6ro <rfs6ro@gmail.com>
// ----------------------------------------------------------------------------

using System.Threading;

namespace BucketEcs
{
    public static class EcsComponentDescriptor<T> where T : struct, IEcsComponent
    {
        static EcsComponentDescriptor() 
        {
            TypeIndex = (ComponentId)Interlocked.Increment(ref EcsWorld.LastRegisteredComponentTypeId);
        }

        public static readonly ComponentId TypeIndex;
    }
}
