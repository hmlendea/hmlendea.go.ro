using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace PersonalPresentationWebsite.ArchitectureTests.Infrastructure
{
    internal static class SourceRepository
    {
        private static readonly string[] ExcludedDirectoryNames =
        [
            "bin", "obj", "generated"
        ];

        private static readonly Lazy<string> RootLazy = new(LocateRoot);

        private static readonly Lazy<IReadOnlyList<ProjectSources>> ProjectsLazy = new(LoadProjects);

        internal static string Root => RootLazy.Value;

        internal static IReadOnlyList<ProjectSources> Projects => ProjectsLazy.Value;

        internal static IReadOnlyList<ProjectSources> ProductionProjects
            => Projects.Where(project => !project.IsTestProject).ToList();

        internal static IReadOnlyList<ProjectSources> TestProjects
            => Projects.Where(project => project.IsTestProject).ToList();

        internal static string SolutionFilePath
            => System.IO.Directory.EnumerateFiles(Root, "*.slnx").Single();

        private static string LocateRoot()
        {
            DirectoryInfo directory = new(AppContext.BaseDirectory);

            while (directory is not null)
            {
                if (directory.EnumerateFiles("*.slnx").Any())
                {
                    return directory.FullName;
                }

                directory = directory.Parent!;
            }

            throw new InvalidOperationException(
                "Unable to locate the repository root from the architecture test assembly location.");
        }

        private static IReadOnlyList<ProjectSources> LoadProjects()
        {
            XDocument document = XDocument.Load(SolutionFilePath);
            List<ProjectSources> projects = [];

            foreach (XElement element in document.Descendants("Project"))
            {
                XAttribute? pathAttribute = element.Attribute("Path");

                if (pathAttribute is null)
                {
                    continue;
                }

                string projectFile = Path.GetFullPath(Path.Combine(Root, pathAttribute.Value));
                string projectDirectory = Path.GetDirectoryName(projectFile)!;
                string name = Path.GetFileNameWithoutExtension(projectFile);

                projects.Add(new ProjectSources(
                    name,
                    projectDirectory,
                    EnumerateSources(projectDirectory),
                    ReadIsPackable(projectFile)));
            }

            return projects;
        }

        private static IReadOnlyList<string> EnumerateSources(string projectDirectory)        {
            List<string> files = [];

            foreach (string file in Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories))
            {
                if (IsExcluded(projectDirectory, file))
                {
                    continue;
                }

                files.Add(file);
            }

            return files.OrderBy(file => file, StringComparer.Ordinal).ToList();
        }

        private static bool ReadIsPackable(string projectFile)
        {
            if (!File.Exists(projectFile))
            {
                return false;
            }

            XDocument document = XDocument.Load(projectFile);
            string? value = document.Descendants("IsPackable").FirstOrDefault()?.Value;

            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsExcluded(string projectDirectory, string file)
        {
            string relativePath = Path.GetRelativePath(projectDirectory, file);
            string[] segments = relativePath.Split(Path.DirectorySeparatorChar);

            if (segments.Length < 2)
            {
                return false;
            }

            for (int index = 0; index < segments.Length - 1; index += 1)
            {
                if (ExcludedDirectoryNames.Contains(segments[index]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
