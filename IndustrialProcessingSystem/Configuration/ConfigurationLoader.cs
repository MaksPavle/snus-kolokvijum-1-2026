using System.Xml.Linq;
using IndustrialProcessingSystem.Models;

namespace IndustrialProcessingSystem.Configuration;

public static class ConfigurationLoader
{
    public static SystemConfig Load(string path)
    {
        var doc = XDocument.Load(path);
        var root = doc.Root ?? throw new InvalidOperationException("XML root not found.");

        var config = new SystemConfig
        {
            WorkerCount = int.Parse(root.Element("WorkerCount")?.Value ?? "1"),
            MaxQueueSize = int.Parse(root.Element("MaxQueueSize")?.Value ?? "10")
        };

        var jobsElement = root.Element("Jobs");

        if (jobsElement != null)
        {
            foreach (var jobElement in jobsElement.Elements("Job"))
            {
                config.InitialJobs.Add(new Job
                {
                    Id = Guid.NewGuid(),
                    Type = Enum.Parse<JobType>(jobElement.Attribute("Type")?.Value ?? "IO", true),
                    Payload = jobElement.Attribute("Payload")?.Value ?? string.Empty,
                    Priority = int.Parse(jobElement.Attribute("Priority")?.Value ?? "5")
                });
            }
        }

        return config;
    }
}