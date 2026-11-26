using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using DOL.Logging;

namespace DOL.GS
{
    public sealed class GameLoopTickPacer
    {
        private static readonly Logger log = LoggerManager.Create(MethodBase.GetCurrentMethod().DeclaringType);

        private const bool DYNAMIC_BUSY_WAIT_THRESHOLD = true;

        private double _tickDuration;
        private bool _running;
        private Thread _busyWaitThresholdThread;
        private int _busyWaitThreshold;

        private double _gameLoopTime;
        private double _totalElapsedTime;
        private Stopwatch _stopwatch;

        public GameLoopTickPacerStats Stats { get; private set; }

        public GameLoopTickPacer(double tickDuration)
        {
            if (tickDuration <= 0)
                throw new ArgumentOutOfRangeException(nameof(tickDuration), "Tick duration must be a positive value.");

            _tickDuration = tickDuration;
        }

        public void Start()
        {
            if (_running)
                return;

            _running = true;

            if (DYNAMIC_BUSY_WAIT_THRESHOLD && Environment.ProcessorCount > 1)
            {
                _busyWaitThresholdThread = new(new ThreadStart(UpdateBusyWaitThreshold))
                {
                    Name = $"{GameLoop.THREAD_NAME}_BusyWaitThreshold",
                    Priority = ThreadPriority.AboveNormal,
                    IsBackground = true
                };
                _busyWaitThresholdThread.Start();
            }

            Stats = new([60000, 30000, 10000], 1000.0 / _tickDuration);
            _stopwatch = Stopwatch.StartNew();
        }

        public void Stop()
        {
            if (_busyWaitThresholdThread == null)
                return;

            _running = false;

            if (_busyWaitThresholdThread != Thread.CurrentThread && _busyWaitThresholdThread.IsAlive)
                _busyWaitThresholdThread.Interrupt(); // This thread sleeps for a long time, let's not wait for it to finish.

            _busyWaitThresholdThread = null;
        }

        public long WaitForNextTick()
        {
            int sleepFor = (int) (_tickDuration - _stopwatch.Elapsed.TotalMilliseconds);
            int busyWaitThreshold = _busyWaitThreshold;

            if (sleepFor >= busyWaitThreshold)
                Thread.Sleep(sleepFor - busyWaitThreshold);

            if (_tickDuration > _stopwatch.Elapsed.TotalMilliseconds)
            {
                // Any small number will do here. Technically, this could be 0.
                // If the game loop appears to overshoot the tick duration for no reason, this can be reduced even further.
                while (_tickDuration > _stopwatch.Elapsed.TotalMilliseconds)
                    Thread.SpinWait(10);
            }

            double elapsedTime = _stopwatch.Elapsed.TotalMilliseconds;
            _totalElapsedTime += elapsedTime;
            _stopwatch.Restart();

            _gameLoopTime += Advance(elapsedTime, _tickDuration, ref _owed);
            Stats.RecordTick(_totalElapsedTime);
            return (long) _gameLoopTime;
        }

        /// <summary>The most time the game's clock may be behind after a tick that ran long; what is beyond it is let go.</summary>
        public const double MostOwedMilliseconds = 250;
        private double _owed;

        /// <summary>
        /// By how much the game's clock goes on after a tick that took <paramref name="elapsed"/>
        /// milliseconds. The wait for the next tick ends when a tick's time is up or later, never
        /// before, and the clock went on by one tick's time whatever the tick had taken: every
        /// tick that ran long (the turn of an 8v8 evening once a second, a collection of memory)
        /// put the game's clock behind the wall's for good, and with it whatever is timed by it,
        /// the walk of every bot and monster among it. A player's character is moved by its own
        /// client, by the wall's clock. Seen 2026-11-26: bots with the speed song ran 383 units a
        /// second over the ground where their speed says 390, and the owner, on /stick behind the
        /// Bard that drove its group and as fast as it by the numbers, ran up to it and was
        /// braked about every fifth second. Now a tick that ran long is made up for in the ticks
        /// after it: the clock goes on by what the tick took and what is still owed, by two
        /// ticks' time at the most in one tick, and it owes <see cref="MostOwedMilliseconds"/> at
        /// the most (a server that stood for seconds does not then run at twice its speed for as
        /// long: that time is let go, as all of it was before).
        /// </summary>
        public static double Advance(double elapsed, double tickDuration, ref double owed)
        {
            double due = elapsed + owed;
            double step = Math.Min(due, 2 * tickDuration);
            owed = Math.Min(due - step, MostOwedMilliseconds);
            return step;
        }

        private void UpdateBusyWaitThreshold()
        {
            int maxIteration = 10;
            int sleepFor = 1;
            int pauseFor = 10000;
            Stopwatch stopwatch = new();
            stopwatch.Start();

            try
            {
                while (Volatile.Read(ref _running))
                {
                    double start;
                    double overSleptFor;
                    double highest = 0;

                    for (int i = 0; i < maxIteration; i++)
                    {
                        start = stopwatch.Elapsed.TotalMilliseconds;
                        Thread.Sleep(sleepFor);
                        overSleptFor = stopwatch.Elapsed.TotalMilliseconds - start - sleepFor;

                        if (highest < overSleptFor)
                            highest = overSleptFor;
                    }

                    _busyWaitThreshold = (int) Math.Max(0, Math.Ceiling(highest));
                    Thread.Sleep(pauseFor);
                }
            }
            catch (ThreadInterruptedException)
            {
                if (log.IsWarnEnabled)
                    log.Warn($"Thread \"{Thread.CurrentThread.Name}\" was interrupted");
            }
            catch (Exception e)
            {
                if (log.IsErrorEnabled)
                    log.Error($"Critical error encountered in {nameof(GameLoopTickPacer)}: {e}");
            }

            if (log.IsInfoEnabled)
                log.Info($"Thread \"{Thread.CurrentThread.Name}\" is stopping");
        }
    }
}
