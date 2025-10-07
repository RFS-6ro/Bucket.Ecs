using System;
using System.Runtime.InteropServices;

namespace Bucket.Ecs.v3
{
#if UNITY
#else
    using AllowUnsafePtr = UnityAttribute;
    using WriteAccess = UnityAttribute;
#endif
    using Inline = System.Runtime.CompilerServices.MethodImplAttribute;

    public unsafe static class MultiThreadSystemExecutionHelper
    {
        public readonly struct ScheduledSystemState
        {
            public readonly UnmanagedChunkData Data;
            public readonly double DeltaTime;
            public readonly IntPtr RunMethodPtr;
            public readonly CommandsScheduler CommandsScheduler;

            public ScheduledSystemState(in UnmanagedChunkData data, in double deltaTime, IntPtr runMethodPtr, in CommandsScheduler commandsScheduler)
            {
                Data = data;
                DeltaTime = deltaTime;
                RunMethodPtr = runMethodPtr;
                CommandsScheduler = commandsScheduler;
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void ExecuteDelegate(in double deltaTime, in UnmanagedChunkData chunkData, in CommandsScheduler commandsScheduler);
        
        [Inline(256)]
        public static void Execute(IntPtr functionPtr, in double deltaTime, in UnmanagedChunkData chunkData, in CommandsScheduler commandsScheduler) 
        {
            var functionDelegate = (ExecuteDelegate)Marshal.GetDelegateForFunctionPointer(functionPtr, typeof(ExecuteDelegate));
            functionDelegate(in deltaTime, in chunkData, in commandsScheduler);
        }

        public static IntPtr GetSystemRunMethodPtr<TSystem>()
            where TSystem : unmanaged, IChunkSystem
        {
            return Marshal.GetFunctionPointerForDelegate((ExecuteDelegate)Invoke<TSystem>);
        }

        private static void Invoke<TSystem>(in double deltaTime, in UnmanagedChunkData chunkData, in CommandsScheduler commandsScheduler)
            where TSystem : unmanaged, IChunkSystem
        {
            TSystem system = new TSystem();
            system.CommandsScheduler = commandsScheduler;
            system.Run(in deltaTime, in chunkData);
        }
    }
}
