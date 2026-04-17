using System.Text;

namespace IndustrialProcessingSystem.Infrastructure;

public class AsyncFileLogger
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public AsyncFileLogger(string filePath)
    {
        _filePath = filePath;
    }

    public async Task LogAsync(string message)
    {
        await _semaphore.WaitAsync();

        try
        {
            await File.AppendAllTextAsync(
                _filePath,
                message + Environment.NewLine,
                Encoding.UTF8);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}