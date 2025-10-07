using System.Threading;

namespace Bucket.Ecs.v3
{
    public interface IEntityIdFactory
    {
        ulong GetNewId();
        void Recycle(ulong id);
    }
    public class EntityIdFactory : IEntityIdFactory
    {
        private long _counter = 0;
        
        public ulong GetNewId()
        {
            return (ulong)Interlocked.Increment(ref _counter);
        }

        public void Recycle(ulong id)
        {

        }
    }
}
