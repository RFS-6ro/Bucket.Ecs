namespace Bucket.Ecs.v3
{
    public sealed partial class EcsEngine
    {
        // TODO: create analyzer to check that only main thread systems can implement IEngineCallback
        public interface IEngineCallback
        {
        }
        
        public interface IStartCallback : IEngineCallback
        {
            public void OnStart();
        }

        public interface IStopCallback : IEngineCallback
        {
            public void OnStop();
        }

        public interface IPauseCallback : IEngineCallback
        {
            public void OnPause(bool isPaused);
        }
    }
}
