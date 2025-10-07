namespace Bucket.Ecs.v3
{
    public interface IBucketLogger
    {
        void Debug(string message);
        void Info(string message);
        void Warning(string message);
        void Error(string message);
    }

    public static class BLogger
    {
        public static IBucketLogger Logger { get; set; }

        [System.Diagnostics.Conditional("DEBUG")]
        [System.Diagnostics.DebuggerStepThrough]
        public static void Debug(string message)
        {
            Logger?.Debug(message);
        }
        
        [System.Diagnostics.DebuggerStepThrough]
        public static void Info(string message)
        {
            Logger?.Info(message);
        }
        
        [System.Diagnostics.DebuggerStepThrough]
        public static void Warning(string message)
        {
            Logger?.Warning(message);
        }
        
        [System.Diagnostics.DebuggerStepThrough]
        public static void Error(string message)
        {
            Logger?.Error(message);
        }
    }
}
