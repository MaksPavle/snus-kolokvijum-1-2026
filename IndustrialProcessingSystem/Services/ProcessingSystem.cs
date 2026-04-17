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

    public ProcessingSystem(int workerCount, int maxQueueSize)
    {
        _maxQueueSize = maxQueueSize;

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
                await ProcessJobAsync(job);
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

    private async Task ProcessJobAsync(Job job)
    {
        if (_completedJobs.ContainsKey(job.Id))
            return;

        try
        {
            int result = await ExecuteJobAsync(job);

            if (_completedJobs.TryAdd(job.Id, 0))
            {
                _jobResults[job.Id].TrySetResult(result);
            }
        }
        catch (Exception ex)
        {
            _jobResults[job.Id].TrySetException(ex);
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
            int delayMs = int.Parse(payload);
            Thread.Sleep(delayMs);
            return Random.Shared.Next(0, 101);
        });
    }

    private Task<int> ExecutePrimeJobAsync(string payload)
    {
        return Task.Run(() =>
        {
            var parts = payload.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            int max = int.Parse(parts[0]);
            int threadCount = parts.Length > 1 ? int.Parse(parts[1]) : 1;
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