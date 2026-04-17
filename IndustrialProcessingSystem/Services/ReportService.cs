using System.Xml.Linq;

namespace IndustrialProcessingSystem.Services;

public class ReportService : IDisposable
{
    private readonly ProcessingSystem _processingSystem;
    private readonly string _reportsDirectory;
    private readonly Timer _timer;
    private int _reportIndex;

    public ReportService(ProcessingSystem processingSystem, string reportsDirectory)
    {
        _processingSystem = processingSystem;
        _reportsDirectory = reportsDirectory;

        Directory.CreateDirectory(_reportsDirectory);

        _reportIndex = GetStartingIndex();

        _timer = new Timer(
            GenerateReport,
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1));
    }

    private void GenerateReport(object? state)
    {
        var history = _processingSystem.GetHistory();

        var groupedByType = history
            .GroupBy(x => x.Type)
            .OrderBy(x => x.Key)
            .Select(group => new XElement("JobType",
                new XAttribute("Name", group.Key),
                new XElement("ExecutedCount", group.Count(x => x.Success)),
                new XElement("AverageExecutionTimeMs",
                    group.Where(x => x.Success).Any()
                        ? Math.Round(group.Where(x => x.Success).Average(x => x.Duration.TotalMilliseconds), 2)
                        : 0),
                new XElement("FailedCount", group.Count(x => !x.Success))
            ));

        var document = new XDocument(
            new XElement("ProcessingReport",
                new XElement("CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
                new XElement("Summary", groupedByType)
            ));

        string fileName = $"report_{_reportIndex}.xml";
        string fullPath = Path.Combine(_reportsDirectory, fileName);

        document.Save(fullPath);
        Console.WriteLine($"[REPORT] Generated: {fullPath}");

        _reportIndex++;

        CleanupOldReports();
    }

    private int GetStartingIndex()
    {
        var files = Directory.GetFiles(_reportsDirectory, "report_*.xml");

        if (files.Length == 0)
            return 0;

        int maxIndex = files
            .Select(file =>
            {
                string name = Path.GetFileNameWithoutExtension(file);
                string suffix = name.Substring("report_".Length);
                return int.TryParse(suffix, out int number) ? number : -1;
            })
            .DefaultIfEmpty(-1)
            .Max();

        return maxIndex + 1;
    }

    private void CleanupOldReports()
    {
        var files = Directory.GetFiles(_reportsDirectory, "report_*.xml")
            .Select(file => new
            {
                Path = file,
                Index = GetReportIndex(file)
            })
            .Where(x => x.Index >= 0)
            .OrderBy(x => x.Index)
            .ToList();

        while (files.Count > 10)
        {
            File.Delete(files[0].Path);
            files.RemoveAt(0);
        }
    }

    private int GetReportIndex(string filePath)
    {
        string name = Path.GetFileNameWithoutExtension(filePath);
        string suffix = name.Substring("report_".Length);

        return int.TryParse(suffix, out int number) ? number : -1;
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}