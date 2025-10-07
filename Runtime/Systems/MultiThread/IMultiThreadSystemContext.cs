using System.Collections.Generic;
using UnsafeCollections.Collections.Unsafe;

namespace Bucket.Ecs.v3
{
    public interface IMultiThreadSystemContext { }

    public interface IMultiThreadSystemPerChunkContext<TContext> : IMultiThreadSystemContext
        where TContext : unmanaged, IMultiThreadSystemChunkContext
    {
        TContext GetContextForChunk(ChunkIndex index);
        void GetAll(List<(TContext, ChunkIndex)> contexts);
    }

    public interface IMultiThreadSystemChunkContext { }

    public unsafe struct SystemPerChunkContext : IMultiThreadSystemPerChunkContext<PerChunkContext>
    {
        private object _lock;
        private UnsafeList* _contexts;
        private UnsafeList* _indexes;

        public static SystemPerChunkContext Get() =>
        new SystemPerChunkContext()
        {
            _lock = new object(),
            _contexts = UnsafeList.Allocate<PerChunkContext>(10),
            _indexes = UnsafeList.Allocate<ChunkIndex>(10)
        };

        public PerChunkContext GetContextForChunk(ChunkIndex index)
        {
            lock (_lock)
            {
                var context =  new PerChunkContext();
                UnsafeList.Add<PerChunkContext>(_contexts, context);
                UnsafeList.Add<ChunkIndex>(_contexts, index);
                return context;
            }
        }

        public void GetAll(List<(PerChunkContext, ChunkIndex)> contexts)
        {
            contexts.Clear();
            for (int i = 0; i < UnsafeList.GetCount(_contexts); i++)
            {
                contexts.Add
                (
                    (UnsafeList.Get<PerChunkContext>(_contexts, i), UnsafeList.Get<ChunkIndex>(_contexts, i))
                );
            }
        }
    }

    public struct PerChunkContext : IMultiThreadSystemChunkContext
    {

    }
}
