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
    public sealed class MemberOrganisationConventionTests
    {
        private static readonly IReadOnlyList<SourceFile> Files = SourceWorkspace.StyleCheckedFiles;

        [Test]
        public void GivenEveryTypeWithMembers_WhenInspecting_ThenKindOrderIsFieldsPropertiesEventsConstructorsMethods()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (TypeDeclarationSyntax declaration in file.Root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                {
                    int previousRank = -1;

                    foreach (MemberDeclarationSyntax member in declaration.Members)
                    {
                        int rank = KindRank(member);

                        if (rank < previousRank)
                        {
                            violations.Add(SyntaxInspections.Describe(file, member, $"member '{SyntaxInspections.MemberName(member)}' violates the kind order fields, properties, events, constructors, methods."));
                        }

                        previousRank = rank;
                    }
                }
            }

            AssertNoViolations(violations, "member kind ordering");
        }

        [Test]
        public void GivenEveryMemberGroup_WhenInspecting_ThenAccessibilityOrderIsPublicProtectedPrivate()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (TypeDeclarationSyntax declaration in file.Root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                {
                    RecordAccessibilityOrder(file, declaration, violations);
                }
            }

            AssertNoViolations(violations, "accessibility ordering within member groups");
        }

        [Test]
        public void GivenEveryOverloadSet_WhenInspecting_ThenOverloadsAreAdjacentAndSimplestFirst()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (TypeDeclarationSyntax declaration in file.Root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                {
                    RecordOverloadOrder(file, declaration, violations);
                }
            }

            AssertNoViolations(violations, "overload grouping and ordering");
        }

        [Test]
        public void GivenEveryInterfaceAndItsImplementation_WhenInspecting_ThenParameterNamesMatch()
        {
            List<string> violations = [];

            Dictionary<string, List<ParameterSyntax>> interfaceParameters = [];

            foreach (SourceFile file in Files)
            {
                foreach (InterfaceDeclarationSyntax declaration in file.TypeDeclarations.OfType<InterfaceDeclarationSyntax>())
                {
                    foreach (MethodDeclarationSyntax method in declaration.Members.OfType<MethodDeclarationSyntax>())
                    {
                        interfaceParameters[method.Identifier.ValueText] =
                            [.. method.ParameterList.Parameters];
                    }
                }
            }

            foreach (SourceFile file in Files)
            {
                foreach (ClassDeclarationSyntax declaration in file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                {
                    foreach (MethodDeclarationSyntax method in declaration.Members.OfType<MethodDeclarationSyntax>())
                    {
                        if (!method.Modifiers.Any(SyntaxKind.PublicKeyword) || method.Modifiers.Any(SyntaxKind.StaticKeyword))
                        {
                            continue;
                        }

                        if (!interfaceParameters.TryGetValue(method.Identifier.ValueText, out List<ParameterSyntax>? expected))
                        {
                            continue;
                        }

                        if (expected.Count != method.ParameterList.Parameters.Count)
                        {
                            continue;
                        }

                        for (int index = 0; index < expected.Count; index += 1)
                        {
                            string interfaceName = expected[index].Identifier.ValueText;
                            string implementationName = method.ParameterList.Parameters[index].Identifier.ValueText;

                            if (!string.Equals(interfaceName, implementationName, StringComparison.Ordinal))
                            {
                                violations.Add(SyntaxInspections.Describe(file, method.ParameterList.Parameters[index], $"parameter name '{implementationName}' differs from the interface parameter '{interfaceName}'."));
                            }
                        }
                    }
                }
            }

            AssertNoViolations(violations, "interface and implementation parameter names");
        }

        [Test]
        public void GivenEveryMethod_WhenInspecting_ThenItDoesNotExceedTheAllowedCognitiveComplexity()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (MethodDeclarationSyntax method in file.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    SyntaxNode? body = method.Body;

                    if (body is null && method.ExpressionBody is not null)
                    {
                        body = method.ExpressionBody;
                    }

                    if (body is null)
                    {
                        continue;
                    }

                    int complexity = CognitiveComplexity.Measure(body);

                    if (complexity > 15)
                    {
                        violations.Add(SyntaxInspections.Describe(file, method, $"method '{method.Identifier.ValueText}' has cognitive complexity {complexity}, exceeding 15."));
                    }
                }
            }

            AssertNoViolations(violations, "cognitive complexity budget");
        }

        [Test]
        public void GivenEveryMethod_WhenInspecting_ThenNoConditionalExpressionIsNested()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (ConditionalExpressionSyntax expression in file.Root.DescendantNodes().OfType<ConditionalExpressionSyntax>())
                {
                    bool isNested = expression.Ancestors().OfType<ConditionalExpressionSyntax>().Any();

                    if (isNested)
                    {
                        violations.Add(SyntaxInspections.Describe(file, expression, "nested conditional expressions are prohibited."));
                    }
                }
            }

            AssertNoViolations(violations, "no nested conditional expressions");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoFileSystemSeparatorIsEmbeddedInAUrl()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                for (int index = 0; index < file.Lines.Count; index += 1)
                {
                    string line = file.Lines[index];

                    if (!line.Contains("Path.DirectorySeparatorChar", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (line.Contains("http", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("api/", StringComparison.Ordinal) ||
                        line.Contains("/repos", StringComparison.Ordinal))
                    {
                        violations.Add($"{file.RelativePath}:{index + 1}: the file-system separator is used to build a URL; use a URL-specific separator.");
                    }
                }
            }

            AssertNoViolations(violations, "URI path composition");
        }

        private static int KindRank(MemberDeclarationSyntax member)
        {
            if (member is FieldDeclarationSyntax)
            {
                return 0;
            }

            if (member is PropertyDeclarationSyntax)
            {
                return 1;
            }

            if (member is EventDeclarationSyntax or EventFieldDeclarationSyntax)
            {
                return 2;
            }

            if (member is ConstructorDeclarationSyntax or DestructorDeclarationSyntax)
            {
                return 3;
            }

            if (member is MethodDeclarationSyntax)
            {
                return 4;
            }

            if (member is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax)
            {
                return 5;
            }

            return 6;
        }

        private static int? AccessibilityRank(MemberDeclarationSyntax member)
        {
            if (member.Modifiers.Any(SyntaxKind.PublicKeyword))
            {
                return 0;
            }

            if (member.Modifiers.Any(SyntaxKind.ProtectedKeyword))
            {
                return 1;
            }

            if (member.Modifiers.Any(SyntaxKind.PrivateKeyword))
            {
                return 2;
            }

            return null;
        }

        private static void RecordAccessibilityOrder(SourceFile file, TypeDeclarationSyntax declaration, List<string> violations)
        {
            List<MemberDeclarationSyntax> members = [.. declaration.Members];

            for (int index = 1; index < members.Count; index += 1)
            {
                if (KindRank(members[index - 1]) != KindRank(members[index]))
                {
                    continue;
                }

                int? previous = AccessibilityRank(members[index - 1]);
                int? current = AccessibilityRank(members[index]);

                if (previous is null || current is null)
                {
                    continue;
                }

                if (current.Value < previous.Value)
                {
                    violations.Add(SyntaxInspections.Describe(file, members[index], $"member '{SyntaxInspections.MemberName(members[index])}' is less accessible than a preceding member of the same kind."));
                }
            }
        }

        private static void RecordOverloadOrder(SourceFile file, TypeDeclarationSyntax declaration, List<string> violations)
        {
            List<MethodDeclarationSyntax> methods = [.. declaration.Members.OfType<MethodDeclarationSyntax>()];
            Dictionary<string, List<int>> positions = [];

            for (int index = 0; index < methods.Count; index += 1)
            {
                string name = methods[index].Identifier.ValueText;

                if (!positions.TryGetValue(name, out List<int>? indices))
                {
                    indices = [];
                    positions.Add(name, indices);
                }

                indices.Add(index);
            }

            foreach (KeyValuePair<string, List<int>> entry in positions)
            {
                if (entry.Value.Count < 2)
                {
                    continue;
                }

                bool adjacent = true;

                for (int index = 1; index < entry.Value.Count; index += 1)
                {
                    if (entry.Value[index] != entry.Value[index - 1] + 1)
                    {
                        adjacent = false;
                        break;
                    }
                }

                if (!adjacent)
                {
                    violations.Add($"{file.RelativePath}: overloads of '{entry.Key}' are not adjacent.");
                }

                for (int index = 1; index < entry.Value.Count; index += 1)
                {
                    int previousCount = methods[entry.Value[index - 1]].ParameterList.Parameters.Count;
                    int currentCount = methods[entry.Value[index]].ParameterList.Parameters.Count;

                    if (currentCount < previousCount)
                    {
                        violations.Add($"{file.RelativePath}: overloads of '{entry.Key}' are not ordered from fewest to most parameters.");
                        break;
                    }
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
