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
    public sealed class StructuralDesignConventionTests
    {
        private static readonly IReadOnlyList<SourceFile> Files = SourceWorkspace.StyleCheckedFiles;

        [Test]
        public void GivenEveryClass_WhenInspecting_ThenItDeclaresNoPublicStaticMutableField()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (FieldDeclarationSyntax field in file.Root.DescendantNodes().OfType<FieldDeclarationSyntax>())
                {
                    bool isPublic = field.Modifiers.Any(SyntaxKind.PublicKeyword);
                    bool isStatic = field.Modifiers.Any(SyntaxKind.StaticKeyword);
                    bool isReadonly = field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword) || field.Modifiers.Any(SyntaxKind.ConstKeyword);

                    if (isPublic && isStatic && !isReadonly)
                    {
                        violations.Add(SyntaxInspections.Describe(file, field, "public static mutable fields are prohibited; expose state through a property or a readonly field."));
                    }
                }
            }

            AssertNoViolations(violations, "no public static mutable fields");
        }

        [Test]
        public void GivenEveryClass_WhenInspecting_ThenNoPublicFieldIsDeclaredInsteadOfAProperty()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (FieldDeclarationSyntax field in file.Root.DescendantNodes().OfType<FieldDeclarationSyntax>())
                {
                    if (field.Modifiers.Any(SyntaxKind.PublicKeyword) || field.Modifiers.Any(SyntaxKind.ProtectedKeyword))
                    {
                        violations.Add(SyntaxInspections.Describe(file, field, "public and protected state must be exposed through a property, not a field."));
                    }
                }
            }

            AssertNoViolations(violations, "properties instead of public fields");
        }

        [Test]
        public void GivenEveryClass_WhenInspecting_ThenNoServiceLocatorOrStaticGatewayIsUsed()
        {
            List<string> violations = [];
            string[] locatorTypes = ["ServiceLocator", "Container.Resolve", "GetService(typeof", "Activator.CreateInstance("];

            foreach (SourceFile file in Files.Where(candidate => !candidate.Project.IsTestProject))
            {
                for (int index = 0; index < file.Lines.Count; index += 1)
                {
                    foreach (string locator in locatorTypes)
                    {
                        if (file.Lines[index].Contains(locator, StringComparison.Ordinal))
                        {
                            violations.Add($"{file.RelativePath}:{index + 1}: service locator or reflective resolution '{locator}' must be replaced with constructor injection.");
                        }
                    }
                }
            }

            AssertNoViolations(violations, "no service locator usage");
        }

        [Test]
        public void GivenEveryInterfaceImplementation_WhenInspecting_ThenNoInterfaceDeclaresOnlyPropertyMembers()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (InterfaceDeclarationSyntax declaration in file.TypeDeclarations.OfType<InterfaceDeclarationSyntax>())
                {
                    if (declaration.Members.Count == 0)
                    {
                        continue;
                    }

                    bool onlyProperties = declaration.Members.All(member => member is PropertyDeclarationSyntax);

                    if (onlyProperties && declaration.Members.Count > 3)
                    {
                        violations.Add(SyntaxInspections.Describe(file, declaration, $"interface '{declaration.Identifier.ValueText}' exposes only properties; consider whether it represents behaviour or a data contract."));
                    }
                }
            }

            AssertNoViolations(violations, "behavioural interfaces");
        }

        [Test]
        public void GivenEveryClass_WhenInspecting_ThenNoTypeDeclaresMoreThanOnePublicConstructor()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (ClassDeclarationSyntax declaration in file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                {
                    int publicConstructors = declaration.Members
                        .OfType<ConstructorDeclarationSyntax>()
                        .Count(constructor => constructor.Modifiers.Any(SyntaxKind.PublicKeyword));

                    if (publicConstructors > 1)
                    {
                        violations.Add(SyntaxInspections.Describe(file, declaration, $"class '{declaration.Identifier.ValueText}' exposes {publicConstructors} public constructors; prefer a single cohesive construction path or a dedicated options type."));
                    }
                }
            }

            AssertNoViolations(violations, "single public construction path");
        }

        [Test]
        public void GivenEveryClass_WhenInspecting_ThenNoDeepInheritanceChainIsDeclared()
        {
            List<string> violations = [];

            Dictionary<string, string> baseByType = [];

            foreach (SourceFile file in Files)
            {
                foreach (ClassDeclarationSyntax declaration in file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                {
                    if (declaration.BaseList is null || declaration.BaseList.Types.Count == 0)
                    {
                        continue;
                    }

                    string? baseType = declaration.BaseList.Types
                        .Select(type => type.Type.ToString())
                        .FirstOrDefault(name => !name.StartsWith("I", StringComparison.Ordinal) || name.Length == 1);

                    if (baseType is not null)
                    {
                        baseByType[declaration.Identifier.ValueText] = baseType;
                    }
                }
            }

            foreach (KeyValuePair<string, string> entry in baseByType)
            {
                int depth = 0;
                string? current = entry.Value;

                while (current is not null && baseByType.TryGetValue(current, out string? next))
                {
                    depth += 1;
                    current = next;

                    if (depth > 3)
                    {
                        break;
                    }
                }

                if (depth > 3)
                {
                    violations.Add($"type '{entry.Key}' participates in an inheritance chain deeper than three levels; prefer composition.");
                }
            }

            AssertNoViolations(violations, "shallow inheritance");
        }

        [Test]
        public void GivenEveryClass_WhenInspecting_ThenNoTypeHasTooManyMembers()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files.Where(candidate => !candidate.Project.IsTestProject))
            {
                foreach (ClassDeclarationSyntax declaration in file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                {
                    int methodCount = declaration.Members.OfType<MethodDeclarationSyntax>()
                        .Count(method => method.Modifiers.Any(SyntaxKind.PublicKeyword));

                    if (methodCount > 12)
                    {
                        violations.Add(SyntaxInspections.Describe(file, declaration, $"class '{declaration.Identifier.ValueText}' exposes {methodCount} public methods; split cohesive responsibilities."));
                    }
                }
            }

            AssertNoViolations(violations, "cohesive type surface");
        }

        [Test]
        public void GivenEveryInterface_WhenInspecting_ThenItsMembersCarryNoImplementationBodies()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (InterfaceDeclarationSyntax declaration in file.TypeDeclarations.OfType<InterfaceDeclarationSyntax>())
                {
                    foreach (MemberDeclarationSyntax member in declaration.Members)
                    {
                        bool hasBody = member switch
                        {
                            MethodDeclarationSyntax method => method.Body is not null || method.ExpressionBody is not null,
                            PropertyDeclarationSyntax property => property.AccessorList?.Accessors.Any(accessor => accessor.Body is not null || accessor.ExpressionBody is not null) ?? false,
                            _ => false
                        };

                        if (hasBody)
                        {
                            violations.Add(SyntaxInspections.Describe(file, member, "interface members must declare only contracts, never implementation bodies."));
                        }
                    }
                }
            }

            AssertNoViolations(violations, "contract-only interfaces");
        }

        [Test]
        public void GivenEveryClass_WhenInspecting_ThenNoTypeDeclaresAnEventThatIsNeverRaised()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (ClassDeclarationSyntax declaration in file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                {
                    foreach (EventFieldDeclarationSyntax eventField in declaration.Members.OfType<EventFieldDeclarationSyntax>())
                    {
                        foreach (VariableDeclaratorSyntax variable in eventField.Declaration.Variables)
                        {
                            string name = variable.Identifier.ValueText;

                            bool raised = declaration.DescendantNodes()
                                .OfType<InvocationExpressionSyntax>()
                                .Any(invocation => invocation.Expression.ToString() == name);

                            if (!raised)
                            {
                                violations.Add(SyntaxInspections.Describe(file, variable, $"event '{name}' is declared but never raised."));
                            }
                        }
                    }
                }
            }

            AssertNoViolations(violations, "no unused events");
        }

        [Test]
        public void GivenEveryClass_WhenInspecting_ThenNoMutableStaticStateIsExposed()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (FieldDeclarationSyntax field in file.Root.DescendantNodes().OfType<FieldDeclarationSyntax>())
                {
                    if (!field.Modifiers.Any(SyntaxKind.StaticKeyword))
                    {
                        continue;
                    }

                    if (field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword) || field.Modifiers.Any(SyntaxKind.ConstKeyword))
                    {
                        continue;
                    }

                    violations.Add(SyntaxInspections.Describe(file, field, "mutable static state harms testability and concurrency safety; use an instance field or an injected dependency."));
                }
            }

            AssertNoViolations(violations, "no mutable static state");
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
