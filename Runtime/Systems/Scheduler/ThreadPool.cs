namespace Bucket.Ecs.v3
{
#if UNITY
    using AllowUnsafePtr = Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestrictionAttribute;
    using WriteAccess = Unity.Collections.LowLevel.Unsafe.WriteAccessRequiredAttribute;
#else
    using AllowUnsafePtr = UnityAttribute;
    using WriteAccess = UnityAttribute;
#endif
    using Inline = System.Runtime.CompilerServices.MethodImplAttribute;

    using System;
    using System.Threading;
    using UnsafeCollections.Collections.Unsafe;

    // Reason to have a separate threadpool and allowing user to set it:
    // The best scenario is to use the same thread pool as in the engine itself. That way you'll get the best performance.
    // If we'll set the amount of threads in a variable - we'll always assume there are N threads available.
    // But that's not always the case. You may reserve one thread for custom logic, the engine may reserve one or more for assets loading. 
    // With predefined amount of threads we'll either request for too many of them, or leave few unused (because in this frame assets are not loading, for example)
    public unsafe interface IThreadPool
    {
        void Queue(MultiThreadSystemExecutionHelper.ScheduledSystemState state);
        void WaitForAll(); 
    }
    public unsafe class ThreadPool : IThreadPool
    {
        private readonly object _lock = new();

        private readonly UnsafeList* _scheduledTasks;
        private readonly UnsafeList* _tasksToRun;
        private readonly Action<MultiThreadSystemExecutionHelper.ScheduledSystemState> _action;

        // If a WaitForAll is in progress, we track its CTS so Cancel() can cancel it.
        // Protected by the same _lock.
        // private CancellationTokenSource _currentWaitCts;

        public ThreadPool(Action<MultiThreadSystemExecutionHelper.ScheduledSystemState> action = null)
        {
            _scheduledTasks = UnsafeList.Allocate<MultiThreadSystemExecutionHelper.ScheduledSystemState>(128); // TODO: size from config
            _tasksToRun = UnsafeList.Allocate<MultiThreadSystemExecutionHelper.ScheduledSystemState>(128); // TODO: size from config
            _action = action;
        }

        [Inline(256)]
        public void Queue(MultiThreadSystemExecutionHelper.ScheduledSystemState state)
        {
            lock (_lock)
            {
                UnsafeList.Add(_scheduledTasks, state);
            }
        }

        public void WaitForAll() // CancellationToken? token
        {
            int count;
            lock (_lock)
            {
                count = UnsafeList.GetCount(_scheduledTasks);
                if (count == 0)
                {
                    BLogger.Warning("Trying to wait for unscheduled systems.");
                    return;
                }

                UnsafeList.Clear(_tasksToRun);
                for (int i = 0; i < count; i++)
                {
                    UnsafeList.Add(_tasksToRun, UnsafeList.Get<MultiThreadSystemExecutionHelper.ScheduledSystemState>(_scheduledTasks, i));
                }
                UnsafeList.Clear(_scheduledTasks);

                // if (token.HasValue)
                // {
                //     _currentWaitCts = CancellationTokenSource(token);
                // }
            }

            using var countdown = new CountdownEvent(count);
            using var startSignal = new ManualResetEventSlim(false);

            for (int i = 0; i < count; i++)
            {
                var task = UnsafeList.Get<MultiThreadSystemExecutionHelper.ScheduledSystemState>(_tasksToRun, i);
                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    // startSignal.Wait(); // Block until start signal is set

                    try
                    {
                        if (_action != null) // TODO: temp code for test
                        {
                            _action.Invoke(task);
                        }
                        else
                        {
                            MultiThreadSystemExecutionHelper.Execute(
                                task.RunMethodPtr,
                                in task.DeltaTime,
                                in task.Data,
                                in task.CommandsScheduler);
                        }
                    }
                    catch (Exception e)
                    {
                        BLogger.Error(e.Message);
                    }
                    finally
                    {
                        countdown.Signal();
                    }
                });
            }

            startSignal.Set();  // Release all tasks to start together
            countdown.Wait();   // Block until all complete

            lock (_lock)
            {
                count = UnsafeList.GetCount(_scheduledTasks);
            }
            
            if (count > 0)
            {
                BLogger.Warning("Trying to schedule systems before previous frame finished execution.");
            }
        }
    }
}
