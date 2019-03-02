using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FrameMachine
{
    public enum ThreadState
    {
        Initializing,
        Stopped,
        Starting,
        Running,
        Stopping,
        Error = -1,
    }

    class Pool
    {
        public const int PoolSize = 0x100;
    }

    class Pool<I, R, O> : Pool // input, rank, output types
        where R : struct, IComparable
    { 
        ConcurrentDictionary<Machine, R> Machines =
            new ConcurrentDictionary<Machine, R>();

        readonly Converter<I, int> InputConverter;
        readonly Converter<int, O> OutputConverter;
        readonly Func<I, R, O, R> FitnessFunc;
        readonly Func<
            IReadOnlyDictionary<Machine, R>, 
            IEnumerable<Machine>> SelectionFunc;
        readonly int SelectionFreq;

        readonly IEnumerable<I> Input;
        
        Thread Thread;
        ThreadState ThreadState = ThreadState.Initializing;

        public Pool(
            Converter<I, int> inputconverter,
            Converter<int, O> outputconverter,
            Func<I, R, O, R> fitnessfunc, 
            Func<
                IReadOnlyDictionary<Machine, R>,
                IEnumerable<Machine>> selectionfunc,
            int selectionfreq, 
            IEnumerable<I> input)
        {
            InputConverter = inputconverter
                ?? throw new ArgumentNullException(
                    "inputconverter");
            OutputConverter = outputconverter
                ?? throw new ArgumentNullException(
                    "outputconverter");
            FitnessFunc = fitnessfunc
                ?? throw new ArgumentNullException(
                    "fitnessfunc");
            SelectionFunc = selectionfunc
                ?? throw new ArgumentNullException(
                    "selectionfunc");
            SelectionFreq = selectionfreq;
            Input = input
                ?? throw new ArgumentNullException(
                    "input");
            for (int i = 0; i < PoolSize; i++)
                Machines.AddOrUpdate(
                    Machine.Generate(), 
                    m => default(R), 
                    (m, r) => default(R));
            ThreadState = ThreadState.Stopped;
        }

        // there are two steps to ga pool
        // 1. evaluate input
        // 2. assign score based on fitness
        //
        // fitness function: M, R, O -> R
        // which R gets updated in the dict
        //
        // machines don't keep a score, so
        // a fitness function must update
        // it in a dictionary

        void Run()
        {
            ThreadState = ThreadState.Running;

            int selectcountdown = SelectionFreq;
            foreach (var x in Input)
            {
                if (ThreadState != ThreadState.Running)
                    return;

                Parallel.ForEach(Machines.Keys, m =>
                {
                    m.Input = InputConverter(x);
                    m.Cycle();
                    var r = 
                        FitnessFunc(
                            x,
                            Machines[m], 
                            OutputConverter(m.Output));
                    Machines.AddOrUpdate(m, w => r, (w, s) => r);
                });

                if (--selectcountdown < 0)
                {
                    lock (Machines)
                    {
                        Machines = new ConcurrentDictionary<Machine, R>(
                            SelectionFunc(Machines as IReadOnlyDictionary<Machine, R>)
                            .Select(m => new KeyValuePair<Machine, R>(m, default(R))));
                    }
                    selectcountdown = SelectionFreq;
                }
            }


            Stop();
        }

        public void Start()
        {
            if (ThreadState != ThreadState.Stopped)
                throw new InvalidOperationException(
                    "Can only start from the stopped state.");
            ThreadState = ThreadState.Starting;
            Thread = new Thread(Run);
            Thread.Start();
        }

        public void Stop()
        {
            if (ThreadState != ThreadState.Running)
                throw new InvalidOperationException(
                    "Can only stop from the running state.");
            ThreadState = ThreadState.Stopping;
            Thread.Join(1000);
            Thread = null;
            ThreadState = ThreadState.Stopped;
        }

        public void Restart()
        {
            Stop();
            Start();
        }

        #region IDisposable Support
        private bool disposed = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    if (ThreadState == ThreadState.Running)
                        Stop();
                }
                disposed = true;
            }
        }
        public void Dispose()
            => Dispose(true);
        #endregion
    }
}
