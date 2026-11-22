using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace PersonalPresentationWebsite.ArchitectureTests.Infrastructure
{
    internal static class SourceWorkspace
    {
        private static readonly Lazy<IReadOnlyList<SourceFile>> FilesLazy = new(Load);

        internal static IReadOnlyList<SourceFile> Files => FilesLazy.Value;

        internal static IReadOnlyList<SourceFile> ProductionFiles
            => Files.Where(file => !file.Project.IsTestProject).ToList();

        internal static IReadOnlyList<SourceFile> TestFiles
            => Files.Where(file => file.Project.IsTestProject).ToList();

        internal static IReadOnlyList<SourceFile> StyleCheckedFiles
            => Files.Where(file => !string.Equals(file.Project.Name, ArchitectureProjectName, StringComparison.Ordinal)).ToList();

        internal static IReadOnlyList<SourceFile> FilesIn(ProjectSources project)
            => Files.Where(file => ReferenceEquals(file.Project, project)).ToList();

        private static string ArchitectureProjectName => "PersonalPresentationWebsite.ArchitectureTests";

        private static IReadOnlyList<SourceFile> Load()
        {
            List<SourceFile> files = [];

            foreach (ProjectSources project in SourceRepository.Projects)
            {
                foreach (string filePath in project.Files)
                {
                    CSharpParseOptions options = new(LanguageVersion.Latest);
                    SyntaxTree tree = CSharpSyntaxTree.ParseText(File.ReadAllText(filePath), options, filePath);

                    files.Add(new SourceFile(filePath, project, tree));
                }
            }

            return files;
        }
    }
}
