namespace PeptideDataHomogenizer.Data.State
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;

    public static class ControlledTimer
    {
        private static readonly ConcurrentDictionary<string, Stopwatch> _activeTimers = new();
        private static readonly ConcurrentDictionary<string, TimeSpan> _completedTimers = new();
        private static readonly object _lock = new();

        public static void StartOperationTimer(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("Description cannot be null or empty", nameof(description));

            lock (_lock)
            {
                if (_activeTimers.ContainsKey(description))
                    throw new InvalidOperationException($"Timer with description '{description}' is already running");

                var timer = new Stopwatch();
                _activeTimers[description] = timer;
                timer.Start();
            }
        }

        public static TimeSpan StopOperationTimer(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("Description cannot be null or empty", nameof(description));

            lock (_lock)
            {
                if (!_activeTimers.TryRemove(description, out var timer)){
                    Console.WriteLine($"No active timer found for description '{description}'");
                    return new TimeSpan(0,0,0);
                }

                timer.Stop();
                var duration = timer.Elapsed;

                // Store the log with description+duration as the key
                var logKey = $"{description} - {duration}";
                _completedTimers[logKey] = duration;

                return duration;
            }
        }

        public static ConcurrentDictionary<string, TimeSpan> GetAllLogs()
        {
            lock (_lock)
            {
                return new ConcurrentDictionary<string, TimeSpan>(_completedTimers);
            }
        }

        public static void ClearAllLogs()
        {
            lock (_lock)
            {
                _completedTimers.Clear();
            }
        }

        public static void PrintAllLogs()
        {
            lock (_lock)
            {
                Console.WriteLine("=== Operation Timing Logs ===");
                foreach (var log in _completedTimers)
                {
                    Console.WriteLine($"{log.Key}");
                }
                Console.WriteLine("============================");
            }
        }
    }
}
