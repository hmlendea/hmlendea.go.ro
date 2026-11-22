using System;
using System.Collections.Generic;

namespace PersonalPresentationWebsite.ArchitectureTests.Infrastructure
{
    internal sealed class ProjectSources
    {
        private static readonly string TestProjectSuffix = "Tests";

        internal string Name { get; }

        internal string Directory { get; }

        internal IReadOnlyList<string> Files { get; }

        internal string RootNamespace => Name;

        internal bool IsTestProject => Name.EndsWith(TestProjectSuffix, StringComparison.Ordinal);

        internal bool IsPackable { get; }

        internal ProjectSources(string name, string directory, IReadOnlyList<string> files, bool isPackable)
        {
            Name = name;
            Directory = directory;
            Files = files;
            IsPackable = isPackable;
        }
    }
}
