using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PersonalPresentationWebsite.ArchitectureTests.Infrastructure
{
    internal sealed class SourceFile
    {
        internal string FilePath { get; }

        internal ProjectSources Project { get; }

        internal SyntaxTree Tree { get; }

        internal SyntaxNode Root { get; }

        internal IReadOnlyList<string> Lines { get; }

        internal string RelativePath => Path.GetRelativePath(SourceRepository.Root, FilePath).Replace('\\', '/');

        internal string FileNameStem => Path.GetFileNameWithoutExtension(FilePath);

        internal IReadOnlyList<BaseTypeDeclarationSyntax> TypeDeclarations
            => Root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().ToList();

        internal IReadOnlyList<DelegateDeclarationSyntax> DelegateDeclarations
            => Root.DescendantNodes().OfType<DelegateDeclarationSyntax>().ToList();

        internal IReadOnlyList<BaseTypeDeclarationSyntax> TopLevelTypeDeclarations
            => Root.DescendantNodes()
                .OfType<BaseTypeDeclarationSyntax>()
                .Where(declaration => declaration.Parent is BaseNamespaceDeclarationSyntax)
                .ToList();

        internal SourceFile(string filePath, ProjectSources project, SyntaxTree tree)
        {
            FilePath = filePath;
            Project = project;
            Tree = tree;
            Root = tree.GetRoot();
            Lines = File.ReadAllLines(filePath);
        }
    }
}
