using Microsoft.CodeAnalysis.CSharp.Syntax;

using PersonalPresentationWebsite.ArchitectureTests.Infrastructure;

namespace PersonalPresentationWebsite.ArchitectureTests
{
    internal sealed class ConfigurationClass
    {
        internal SourceFile File { get; }

        internal ClassDeclarationSyntax Declaration { get; }

        internal ConfigurationClass(SourceFile file, ClassDeclarationSyntax declaration)
        {
            File = file;
            Declaration = declaration;
        }
    }
}
