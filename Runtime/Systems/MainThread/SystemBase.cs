using System;

namespace Bucket.Ecs.v3
{
    public abstract class SystemBase : IDisposable
    {
        public EcsWorld World { get; internal set; }
        public virtual int Priority => 0;
        public virtual void Init() { }
        public virtual void Run(in double deltaTime) { }
        public virtual void Dispose() { }
    }
}
