using IndustrialProcessingSystem.Models;

namespace IndustrialProcessingSystem.Infrastructure;

public class PriorityJobQueue
{
    private readonly PriorityQueue<Job, int> _queue = new();
    private readonly object _lock = new();
    private readonly SemaphoreSlim _signal = new(0);

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _queue.Count;
            }
        }
    }

    public bool Enqueue(Job job, int maxSize)
    {
        lock (_lock)
        {
            if (_queue.Count >= maxSize)
                return false;

            _queue.Enqueue(job, job.Priority);
            _signal.Release();
            return true;
        }
    }

    public async Task<Job> DequeueAsync(CancellationToken token)
    {
        await _signal.WaitAsync(token);

        lock (_lock)
        {
            return _queue.Dequeue();
        }
    }

    public List<Job> GetTopJobs(int n)
    {
        lock (_lock)
        {
            return _queue.UnorderedItems
                .OrderBy(x => x.Priority)
                .ThenBy(x => x.Element.Id)
                .Take(n)
                .Select(x => x.Element)
                .ToList();
        }
    }
}