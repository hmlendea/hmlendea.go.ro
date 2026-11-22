using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using NUnit.Framework;

using PersonalPresentationWebsite.ArchitectureTests.Infrastructure;

namespace PersonalPresentationWebsite.ArchitectureTests
{
    [TestFixture]
    public sealed class CollectionAndLinqConventionTests
    {
        private static readonly IReadOnlyList<SourceFile> Files = SourceWorkspace.StyleCheckedFiles;

        private static readonly string[] ConcreteCollectionReturnTypes =
        [
            "List<", "IList<", "IReadOnlyList<", "HashSet<", "ISet<", "Dictionary<", "IDictionary<", "SortedSet<", "Queue<", "Stack<"
        ];

        [Test]
        public void GivenEveryMethod_WhenInspecting_ThenNoCollectionTypeOtherThanIEnumerableIsSigned()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (MethodDeclarationSyntax method in file.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    string returnType = method.ReturnType.ToString();

                    if (IsXmlSerialisedEntity(method))
                    {
                        continue;
                    }

                    if (ConcreteCollectionReturnTypes.Any(type => returnType.Contains(type, StringComparison.Ordinal)))
                    {
                        violations.Add(SyntaxInspections.Describe(file, method, $"method '{method.Identifier.ValueText}' returns '{returnType}'; use IEnumerable<T>."));
                    }

                    foreach (ParameterSyntax parameter in method.ParameterList.Parameters)
                    {
                        string parameterType = parameter.Type?.ToString() ?? string.Empty;

                        if (ConcreteCollectionReturnTypes.Any(type => parameterType.Contains(type, StringComparison.Ordinal)))
                        {
                            violations.Add(SyntaxInspections.Describe(file, parameter, $"parameter '{parameter.Identifier.ValueText}' uses '{parameterType}'; use IEnumerable<T>."));
                        }
                    }
                }
            }

            AssertNoViolations(violations, "IEnumerable collection contracts");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoLegacyArrayInitialiserIsUsed()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (ArrayCreationExpressionSyntax creation in file.Root.DescendantNodes().OfType<ArrayCreationExpressionSyntax>())
                {
                    violations.Add(SyntaxInspections.Describe(file, creation, "array creation expressions must use a collection expression instead."));
                }

                foreach (ImplicitArrayCreationExpressionSyntax creation in file.Root.DescendantNodes().OfType<ImplicitArrayCreationExpressionSyntax>())
                {
                    violations.Add(SyntaxInspections.Describe(file, creation, "implicit array creation must use a collection expression instead."));
                }
            }

            AssertNoViolations(violations, "collection expression initialisation");
        }

        [Test]
        public void GivenEveryCollectionProperty_WhenInspecting_ThenItUsesACollectionExpressionWhenInitialisedEmpty()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (PropertyDeclarationSyntax property in file.Root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
                {
                    if (property.Initializer?.Value is not ObjectCreationExpressionSyntax creation)
                    {
                        continue;
                    }

                    string typeName = creation.Type.ToString();

                    if (typeName is "List" or "Dictionary" or "HashSet" or "Queue" or "Stack")
                    {
                        violations.Add(SyntaxInspections.Describe(file, property, $"property '{property.Identifier.ValueText}' constructs an empty collection; use a collection expression."));
                    }
                }
            }

            AssertNoViolations(violations, "collection expression property initialisation");
        }

        [Test]
        public void GivenEveryLoopOverACollection_WhenInspecting_ThenNoManualFilteringReimplementsLinq()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (ForEachStatementSyntax statement in file.Root.DescendantNodes().OfType<ForEachStatementSyntax>())
                {
                    bool hasConditionalWithAdd = statement.Statement.DescendantNodes()
                        .OfType<IfStatementSyntax>()
                        .Any(ifStatement => ifStatement.Statement.DescendantNodes()
                            .OfType<InvocationExpressionSyntax>()
                            .Any(invocation => invocation.Expression.ToString().EndsWith(".Add", StringComparison.Ordinal) ||
                                invocation.Expression.ToString().EndsWith(".AddRange", StringComparison.Ordinal)));

                    if (hasConditionalWithAdd)
                    {
                        violations.Add(SyntaxInspections.Describe(file, statement, "a loop that filters with an if and then adds reimplements LINQ; use Where/Select."));
                    }
                }
            }

            AssertNoViolations(violations, "LINQ preference for in-memory querying");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoReusableArrayLiteralIsPassedToANonMutatingApi()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (InvocationExpressionSyntax invocation in file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    foreach (ArgumentSyntax argument in invocation.ArgumentList.Arguments)
                    {
                        if (argument.Expression is not CollectionExpressionSyntax collection)
                        {
                            continue;
                        }

                        if (collection.Elements.Count < 2)
                        {
                            continue;
                        }

                        bool allConstants = collection.Elements
                            .OfType<ExpressionElementSyntax>()
                            .All(element => element.Expression is LiteralExpressionSyntax);

                        if (allConstants && IsInsideTestMethod(invocation))
                        {
                            violations.Add(SyntaxInspections.Describe(file, argument, "a reusable multi-element literal should be a descriptively named static readonly field."));
                        }
                    }
                }
            }

            AssertNoViolations(violations, "reusable test literals as named fields");
        }

        private static bool IsXmlSerialisedEntity(MethodDeclarationSyntax method)
        {
            return method.Ancestors()
                .OfType<TypeDeclarationSyntax>()
                .Any(type => type.DescendantNodes()
                    .OfType<AttributeSyntax>()
                    .Any(attribute => attribute.Name.ToString().Contains("Xml", StringComparison.Ordinal)));
        }

        private static bool IsInsideTestMethod(SyntaxNode node)
            => node.Ancestors()
                .OfType<MethodDeclarationSyntax>()
                .Any(method => method.AttributeLists
                    .SelectMany(list => list.Attributes)
                    .Any(attribute => attribute.Name.ToString() is "Test" or "TestCase" or "TestCaseSource"));

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
