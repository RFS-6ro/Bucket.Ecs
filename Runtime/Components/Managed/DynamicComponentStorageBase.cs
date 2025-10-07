using System;

namespace Bucket.Ecs.v3
{
    public abstract class DynamicComponentStorageBase
    {
        public abstract DynamicComponentStorageBase CreateNew();

        public virtual void CopyFrom(short oldEntityIndex, short newEntityIndex, DynamicComponentStorageBase other, int length = 1) { }
    }
}