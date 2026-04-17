using System.Collections.Concurrent;
using IndustrialProcessingSystem.Infrastructure;
using IndustrialProcessingSystem.Models;

namespace IndustrialProcessingSystem.Services;

public class ProcessingSystem
{
    private readonly PriorityJobQueue _queue = new();
    private readonly ConcurrentDictionary<Guid, Job> _jobs = new();
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<int>> _jobResults = new();
    private readonly ConcurrentDictionary<Guid, byte> _completedJobs = new();

    private readonly List<Task> _workers = new();
    private readonly CancellationTokenSource _cts = new();

    private readonly int _maxQueueSize;
    private readonly AsyncFileLogger _logger;

    public event Func<Job, int, Task>? JobCompleted;
    public event Func<Job, string, Task>? JobFailed;

    public ProcessingSystem(int workerCount, int maxQueueSize, string logFilePath)
    {
        _maxQueueSize = maxQueueSize;
        _logger = new AsyncFileLogger(logFilePath);

        JobCompleted += async (job, result) =>
        {
            await _logger.LogAsync(
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [COMPLETED] {job.Id}, {result}");
        };

        JobFailed += async (job, error) =>
        {
            await _logger.LogAsync(
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [FAILED] {job.Id}, {error}");
        };

        for (int i = 0; i < workerCount; i++)
        {
            _workers.Add(Task.Run(() => WorkerLoopAsync(_cts.Token)));
        }
    }

    public JobHandle Submit(Job job)
    {
        if (job == null)
            throw new ArgumentNullException(nameof(job));

        if (job.Id == Guid.Empty)
            job.Id = Guid.NewGuid();

        if (_jobs.TryAdd(job.Id, job))
        {
            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            _jobResults[job.Id] = tcs;

            bool enqueued = _queue.Enqueue(job, _maxQueueSize);
            if (!enqueued)
            {
                tcs.TrySetException(new InvalidOperationException("Queue is full."));
            }

            return new JobHandle
            {
                Id = job.Id,
                Result = tcs.Task
            };
        }

        return new JobHandle
        {
            Id = job.Id,
            Result = _jobResults[job.Id].Task
        };
    }

    public IEnumerable<Job> GetTopJobs(int n)
    {
        return _queue.GetTopJobs(n);
    }

    public Job GetJob(Guid id)
    {
        if (_jobs.TryGetValue(id, out var job))
            return job;

        throw new KeyNotFoundException($"Job with id {id} not found.");
    }

    public async Task StopAsync()
    {
        _cts.Cancel();
        await Task.WhenAll(_workers);
    }

    private async Task WorkerLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var job = await _queue.DequeueAsync(token);
                await ProcessJobWithRetryAsync(job);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Worker error: {ex.Message}");
            }
        }
    }

    private async Task ProcessJobWithRetryAsync(Job job)
    {
        if (_completedJobs.ContainsKey(job.Id))
            return;

        const int maxAttempts = 3;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var executionTask = ExecuteJobAsync(job);
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(2));

                var completedTask = await Task.WhenAny(executionTask, timeoutTask);

                if (completedTask != executionTask)
                    throw new TimeoutException("Execution took longer than 2 seconds.");

                int result = await executionTask;

                if (_completedJobs.TryAdd(job.Id, 0))
                {
                    _jobResults[job.Id].TrySetResult(result);

                    if (JobCompleted != null)
                        await JobCompleted.Invoke(job, result);
                }

                return;
            }
            catch (Exception ex)
            {
                if (JobFailed != null)
                    await JobFailed.Invoke(job, $"Attempt {attempt}: {ex.Message}");

                if (attempt == maxAttempts)
                {
                    await _logger.LogAsync(
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ABORT] {job.Id}, ignored result");

                    _jobResults[job.Id].TrySetException(
                        new Exception($"Job failed after 3 attempts. Last error: {ex.Message}"));
                }
            }
        }
    }

    private Task<int> ExecuteJobAsync(Job job)
    {
        return job.Type switch
        {
            JobType.Prime => ExecutePrimeJobAsync(job.Payload),
            JobType.IO => ExecuteIoJobAsync(job.Payload),
            _ => throw new InvalidOperationException("Unsupported job type.")
        };
    }

    private Task<int> ExecuteIoJobAsync(string payload)
    {
        return Task.Run(() =>
        {
            var parts = payload.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2 || !parts[0].Equals("delay", StringComparison.OrdinalIgnoreCase))
                throw new FormatException("Invalid IO payload format. Expected: delay:1000");

            string delayText = parts[1].Replace("_", "");
            int delayMs = int.Parse(delayText);

            Thread.Sleep(delayMs);
            return Random.Shared.Next(0, 101);
        });
    }

    private Task<int> ExecutePrimeJobAsync(string payload)
    {
        return Task.Run(() =>
        {
            var parts = payload.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
                throw new FormatException("Invalid Prime payload format. Expected: numbers:10000,threads:3");

            string numbersPart = parts[0];
            string threadsPart = parts[1];

            var numbersSplit = numbersPart.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var threadsSplit = threadsPart.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (numbersSplit.Length != 2 || !numbersSplit[0].Equals("numbers", StringComparison.OrdinalIgnoreCase))
                throw new FormatException("Invalid Prime payload numbers part.");

            if (threadsSplit.Length != 2 || !threadsSplit[0].Equals("threads", StringComparison.OrdinalIgnoreCase))
                throw new FormatException("Invalid Prime payload threads part.");

            string maxText = numbersSplit[1].Replace("_", "");
            string threadText = threadsSplit[1].Replace("_", "");

            int max = int.Parse(maxText);
            int threadCount = int.Parse(threadText);

            threadCount = Math.Clamp(threadCount, 1, 8);

            int total = 0;
            object lockObj = new();

            Parallel.For(
                2,
                max + 1,
                new ParallelOptions { MaxDegreeOfParallelism = threadCount },
                () => 0,
                (number, state, localCount) =>
                {
                    if (IsPrime(number))
                        localCount++;

                    return localCount;
                },
                localCount =>
                {
                    lock (lockObj)
                    {
                        total += localCount;
                    }
                });

            return total;
        });
    }

    private bool IsPrime(int number)
    {
        if (number < 2) return false;
        if (number == 2) return true;
        if (number % 2 == 0) return false;

        int limit = (int)Math.Sqrt(number);

        for (int i = 3; i <= limit; i += 2)
        {
            if (number % i == 0)
                return false;
        }

        return true;
    }
}