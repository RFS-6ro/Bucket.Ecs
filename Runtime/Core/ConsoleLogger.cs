using System;

namespace Bucket.Ecs.v3
{
    public class ConsoleLogger : IBucketLogger
    {
        [System.Diagnostics.DebuggerStepThrough]
        public void Debug(string message)
        {
            Console.WriteLine(message);
        }

        [System.Diagnostics.DebuggerStepThrough]
        public void Info(string message)
        {
            Console.WriteLine(message);
        }

        [System.Diagnostics.DebuggerStepThrough]
        public void Warning(string message)
        {
            Console.WriteLine(message);
        }

        [System.Diagnostics.DebuggerStepThrough]
        public void Error(string message)
        {
            Console.WriteLine(message);
        }
    }
    public class EmptyLogger : IBucketLogger
    {
        [System.Diagnostics.DebuggerStepThrough]
        public void Debug(string message)
        {
        }

        [System.Diagnostics.DebuggerStepThrough]
        public void Info(string message)
        {
        }

        [System.Diagnostics.DebuggerStepThrough]
        public void Warning(string message)
        {
        }

        [System.Diagnostics.DebuggerStepThrough]
        public void Error(string message)
        {
        }
    }
}
