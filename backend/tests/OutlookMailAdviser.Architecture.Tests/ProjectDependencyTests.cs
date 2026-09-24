using System.Xml.Linq;

namespace OutlookMailAdviser.Architecture.Tests;

public sealed class ProjectDependencyTests
{
    private static readonly IReadOnlyDictionary<string, string[]> AllowedProjectReferences =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["OutlookMailAdviser.Domain"] = [],
            ["OutlookMailAdviser.Application"] = ["OutlookMailAdviser.Domain"],
            ["OutlookMailAdviser.Adapters.Ollama"] =
                ["OutlookMailAdviser.Application", "OutlookMailAdviser.Domain"],
            ["OutlookMailAdviser.Adapters.OpenAI"] =
                ["OutlookMailAdviser.Application", "OutlookMailAdviser.Domain"],
            ["OutlookMailAdviser.Adapters.MailContent"] =
                ["OutlookMailAdviser.Application", "OutlookMailAdviser.Domain"],
            ["OutlookMailAdviser.Api"] =
            [
                "OutlookMailAdviser.Adapters.MailContent",
                "OutlookMailAdviser.Adapters.Ollama",
                "OutlookMailAdviser.Adapters.OpenAI",
                "OutlookMailAdviser.Application"
            ]
        };

    [Fact]
    public void ProductionProjectsFollowTheCleanArchitectureDependencyRule()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourceRoot = Path.Combine(repositoryRoot, "backend", "src");

        foreach (var (projectName, allowedReferences) in AllowedProjectReferences)
        {
            var projectFile = Path.Combine(sourceRoot, projectName, $"{projectName}.csproj");
            var actualReferences = ReadProjectReferences(projectFile);
            var expectedReferences = allowedReferences.Order(StringComparer.Ordinal).ToArray();

            Assert.Equal(expectedReferences, actualReferences);
        }
    }

    private static string[] ReadProjectReferences(string projectFile)
    {
        Assert.True(File.Exists(projectFile), $"Project file was not found: {projectFile}");

        var document = XDocument.Load(projectFile);

        return document
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .Select(reference => Path.GetFileNameWithoutExtension(reference!))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OutlookMailAdviser.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
