using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using NUnit.Framework;

using PersonalPresentationWebsite.ArchitectureTests.Infrastructure;

namespace PersonalPresentationWebsite.ArchitectureTests
{
    [TestFixture]
    public sealed class DocumentationTests
    {
        private static readonly IReadOnlyList<SourceFile> Files = SourceWorkspace.StyleCheckedFiles;

        [Test]
        public void GivenEveryPublicMemberOfANuGetType_WhenInspecting_ThenItHasCurrentXmlDocumentation()
        {
            if (!IsPackedAsNuGet())
            {
                Assert.Ignore("No project in the solution is published as a NuGet package, so the NuGet documentation contract is inapplicable.");
            }

            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (ClassDeclarationSyntax declaration in file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                {
                    if (!declaration.Modifiers.Any(SyntaxKind.PublicKeyword))
                    {
                        continue;
                    }

                    RecordMemberViolations(file, declaration, violations);
                }

                foreach (InterfaceDeclarationSyntax declaration in file.TypeDeclarations.OfType<InterfaceDeclarationSyntax>())
                {
                    if (!declaration.Modifiers.Any(SyntaxKind.PublicKeyword))
                    {
                        continue;
                    }

                    RecordMemberViolations(file, declaration, violations);
                }

                foreach (EnumDeclarationSyntax declaration in file.TypeDeclarations.OfType<EnumDeclarationSyntax>())
                {
                    if (!declaration.Modifiers.Any(SyntaxKind.PublicKeyword))
                    {
                        continue;
                    }

                    RecordEnumViolations(file, declaration, violations);
                }
            }

            AssertNoViolations(violations, "XML documentation on public NuGet surface");
        }

        [Test]
        public void GivenEveryPublicClass_WhenInspecting_ThenNoPublicMemberIsRemovedWithoutExplicitUserRequest()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (ClassDeclarationSyntax declaration in file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                {
                    if (!declaration.Modifiers.Any(SyntaxKind.PublicKeyword))
                    {
                        continue;
                    }

                    foreach (MemberDeclarationSyntax member in declaration.Members)
                    {
                        string? name = member switch
                        {
                            MethodDeclarationSyntax method => method.Identifier.ValueText,
                            PropertyDeclarationSyntax property => property.Identifier.ValueText,
                            EventDeclarationSyntax eventMember => eventMember.Identifier.ValueText,
                            _ => null
                        };

                        if (name is not null && name.StartsWith("Remove", StringComparison.Ordinal) && !name.Contains("Exception", StringComparison.Ordinal))
                        {
                            violations.Add(SyntaxInspections.Describe(file, member, $"method '{name}' may be removing a public member; removal requires an explicit user request."));
                        }
                    }
                }
            }

            AssertNoViolations(violations, "public member retention");
        }

        private static bool IsPackedAsNuGet()
            => SourceRepository.Projects
                .Any(project => project.IsPackable);

        private static void RecordMemberViolations(
            SourceFile file,
            TypeDeclarationSyntax container,
            List<string> violations)
        {
            foreach (MethodDeclarationSyntax method in container.Members.OfType<MethodDeclarationSyntax>())
            {
                if (method.Modifiers.Any(SyntaxKind.PublicKeyword) is false)
                {
                    continue;
                }

                bool hasDocumentation = method.GetLeadingTrivia()
                    .Any(trivia => trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));

                if (!hasDocumentation)
                {
                    violations.Add(SyntaxInspections.Describe(file, method, $"public method '{method.Identifier.ValueText}' requires XML documentation."));
                }
            }

            foreach (PropertyDeclarationSyntax property in container.Members.OfType<PropertyDeclarationSyntax>())
            {
                if (property.Modifiers.Any(SyntaxKind.PublicKeyword) is false)
                {
                    continue;
                }

                bool hasDocumentation = property.GetLeadingTrivia()
                    .Any(trivia => trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));

                if (!hasDocumentation)
                {
                    violations.Add(SyntaxInspections.Describe(file, property, $"public property '{property.Identifier.ValueText}' requires XML documentation."));
                }
            }

            foreach (EventDeclarationSyntax eventMember in container.Members.OfType<EventDeclarationSyntax>())
            {
                if (eventMember.Modifiers.Any(SyntaxKind.PublicKeyword) is false)
                {
                    continue;
                }

                violations.Add(SyntaxInspections.Describe(file, eventMember, $"public event '{eventMember.Identifier.ValueText}' requires XML documentation."));
            }
        }

        private static void RecordEnumViolations(SourceFile file, EnumDeclarationSyntax declaration, List<string> violations)
        {
            foreach (EnumMemberDeclarationSyntax member in declaration.Members)
            {
                bool hasDocumentation = member.GetLeadingTrivia()
                    .Any(trivia => trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));

                if (!hasDocumentation)
                {
                    violations.Add(SyntaxInspections.Describe(file, member, $"public enum member '{member.Identifier.ValueText}' requires XML documentation."));
                }
            }
        }

        private static void AssertNoViolations(IReadOnlyList<string> violations, string convention)
        {
            if (violations.Count == 0)
            {
                return;
            }

            Assert.Fail(
                $"Violated {convention} with {violations.Count} finding(s):{Environment.NewLine}" +
                string.Join(Environment.NewLine, violations.Take(50)));
        }
    }
}
