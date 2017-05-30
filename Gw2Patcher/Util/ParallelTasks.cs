using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Gw2Patcher.Util
{
    class ParallelTasks<TResult> : IDisposable
    {
        public abstract class TaskResult
        {
            public TResult Result
            {
                get;
                protected set;
            }

            public Exception Exception
            {
                get;
                protected set;
            }

            public bool HasResult
            {
                get;
                protected set;
            }

            public bool HasException
            {
                get
                {
                    return this.Exception != null;
                }
            }

            public abstract byte Index
            {
                get;
            }

            /// <summary>
            /// Queues more work onto the current thread
            /// </summary>
            public abstract void QueueWork(Func<TResult> action);
        }

        protected class TaskThread : TaskResult, IDisposable
        {
            protected ParallelTasks<TResult> source;
            protected Thread thread;
            public byte index;
            public DateTime timeCompleted;
            public bool isComplete, isStarted, hasWork, isSuspended;
            private ManualResetEventSlim workAdded, workComplete;
            private Func<TResult> action;

            public TaskThread(ParallelTasks<TResult> source, byte index)
            {
                this.source = source;
                this.index = index;
                this.isComplete = true;
                this.workAdded = new ManualResetEventSlim(true);
                this.workComplete = new ManualResetEventSlim(true);
            }

            public bool Wait(int millis, CancellationToken cancel)
            {
                if (isComplete)
                    return true;

                return workComplete.Wait(millis, cancel);
            }

            public void Dispose()
            {
                if (!isSuspended)
                    Suspend();
            }

            public override byte Index
            {
                get 
                {
                    return index;
                }
            }

            DateTime lastWorrkAdded;
            private void DoWork()
            {
                var cancel = source.cancel;
                while (!isSuspended && !cancel.IsCancellationRequested)
                {
                    try
                    {
                        workAdded.Wait(cancel);
                        workAdded.Reset();
                    }
                    catch
                    {
                        isSuspended = true;
                        isComplete = true;
                        return;
                    }

                    if (isSuspended)
                        return;

                    try
                    {
                        this.Result = action();
                        this.HasResult = true;
                        this.Exception = null;
                    }
                    catch (Exception e)
                    {
                        this.Result = default(TResult);
                        this.HasResult = false;
                        this.Exception = e;
                    }

                    action = null;
                    timeCompleted = DateTime.UtcNow;
                    isComplete = true;

                    workComplete.Set();
                }
            }

            public void Suspend()
            {
                if (!isSuspended)
                {
                    isSuspended = true;
                    workAdded.Set();
                }
            }

            /// <summary>
            /// Queues more work onto the current thread
            /// </summary>
            public override void QueueWork(Func<TResult> action)
            {
                if (hasWork)
                    throw new Exception("Work has already been queued");

                workComplete.Reset();
                isComplete = false;
                this.action = action;
                hasWork = true;
                source.queued++;

                lastWorrkAdded = DateTime.UtcNow;
                workAdded.Set();
                
                if (!isStarted)
                {
                    isStarted = true;
                    thread = new Thread(new ThreadStart(DoWork));
                    thread.IsBackground = true;
                    thread.Start();
                }
            }
        }

        protected TaskThread[] tasks;
        protected byte threads;
        protected CancellationToken cancel;
        protected int queued;

        public ParallelTasks(byte threads, CancellationToken cancel)
        {
            this.threads = threads;
            this.tasks = new TaskThread[threads];
            this.cancel = cancel;
        }

        public void Dispose()
        {
            foreach (var t in tasks)
            {
                if (t != null)
                    t.Dispose();
            }
        }

        /// <summary>
        /// Active number of tasks
        /// </summary>
        public int Count
        {
            get
            {
                return queued;
            }
        }

        private TaskThread lastResult;

        /// <summary>
        /// Returns the next worker. If no more work is queued, the worker will no longer be used.
        /// If no more workers are available, null will be returned and the task complete
        /// </summary>
        public async Task<TaskResult> Next()
        {
            //track the last returned next - if it has no more work, kill it.

            short k = await Task.Run<short>(
                delegate
                {
                    try
                    {
                        byte j = 0;
                        byte pass = threads;
                        byte working = 0;
                        short result = -1;
                        DateTime first = DateTime.MaxValue;

                        //first pass: new tasks (null) should be returned first
                        for (j = 0; j < threads; j++)
                        {
                            var task = tasks[j];

                            if (task == null)
                                return j;
                            
                            if (task.hasWork)
                            {
                                working++;
                                if (task.isComplete && task.timeCompleted < first)
                                {
                                    result = j;
                                    first = task.timeCompleted;
                                }
                            }
                        }

                        if (working == 0)
                            return -1;
                        if (result != -1)
                            return result;

                        j = 0;
                        while (true)
                        {
                            var task = tasks[j];
                            if (task.hasWork && task.Wait(50, cancel))
                                return j;

                            if (++j >= threads)
                                j = 0;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        return -1;
                    }
                });

            if (cancel.IsCancellationRequested)
                throw new TaskCanceledException();

            if (k == -1)
                return null;

            var t = tasks[k];

            if (t == null)
            {
                t = tasks[k] = new TaskThread(this, (byte)k);
            }
            else
            {
                queued--;
            }

            if (lastResult != null && !lastResult.hasWork)
                lastResult.Suspend();
            lastResult = t;

            t.hasWork = false;

            return t;
        }
    }
}
