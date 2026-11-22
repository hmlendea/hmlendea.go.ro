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
    public sealed class TypeDeclarationConventionTests
    {
        private static readonly IReadOnlyList<SourceFile> Files = SourceWorkspace.StyleCheckedFiles;

        [Test]
        public void GivenEveryClassDeclaration_WhenInspecting_ThenNoRecordTypeIsDeclared()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (RecordDeclarationSyntax declaration in file.TypeDeclarations.OfType<RecordDeclarationSyntax>())
                {
                    violations.Add(SyntaxInspections.Describe(file, declaration, $"record type '{declaration.Identifier.ValueText}' is not part of the accepted type vocabulary."));
                }
            }

            AssertNoViolations(violations, "class-only type declarations");
        }

        [Test]
        public void GivenEveryStructDeclaration_WhenInspecting_ThenItIsExplicitlyJustified()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (StructDeclarationSyntax declaration in file.TypeDeclarations.OfType<StructDeclarationSyntax>())
                {
                    violations.Add(SyntaxInspections.Describe(file, declaration, $"struct '{declaration.Identifier.ValueText}' requires an explicit justification."));
                }
            }

            AssertNoViolations(violations, "struct usage");
        }

        [Test]
        public void GivenEveryDomainAndEntityAndSettingsClass_WhenInspecting_ThenItIsAPublicSealedClass()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (ClassDeclarationSyntax declaration in file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                {
                    if (!IsModelOrEntityOrSettings(file, declaration))
                    {
                        continue;
                    }

                    if (!declaration.Modifiers.Any(SyntaxKind.PublicKeyword))
                    {
                        violations.Add(SyntaxInspections.Describe(file, declaration, $"type '{declaration.Identifier.ValueText}' must be public."));
                    }

                    if (!declaration.Modifiers.Any(SyntaxKind.SealedKeyword))
                    {
                        violations.Add(SyntaxInspections.Describe(file, declaration, $"type '{declaration.Identifier.ValueText}' must be sealed."));
                    }
                }
            }

            AssertNoViolations(violations, "public sealed model declarations");
        }

        [Test]
        public void GivenEveryDataObjectAndRequestAndResponseClass_WhenInspecting_ThenItIsSuffixedCorrectly()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (ClassDeclarationSyntax declaration in file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                {
                    string name = declaration.Identifier.ValueText;

                    if (name.EndsWith("DataObject", StringComparison.Ordinal) is false &&
                        name.EndsWith("Request", StringComparison.Ordinal) is false &&
                        name.EndsWith("Response", StringComparison.Ordinal) is false)
                    {
                        continue;
                    }

                    if (!name.EndsWith("DataObject", StringComparison.Ordinal) &&
                        !name.EndsWith("Request", StringComparison.Ordinal) &&
                        !name.EndsWith("Response", StringComparison.Ordinal))
                    {
                        violations.Add(SyntaxInspections.Describe(file, declaration, $"type '{name}' uses an unsupported data-object suffix."));
                    }
                }
            }

            AssertNoViolations(violations, "data-object naming");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoEnumerationNamespaceIsDeclared()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (BaseNamespaceDeclarationSyntax namespaceDeclaration in file.Root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>())
                {
                    string name = namespaceDeclaration.Name.ToString();

                    if (name.EndsWith(".Enumerations", StringComparison.Ordinal) ||
                        name.EndsWith(".Enums", StringComparison.Ordinal))
                    {
                        violations.Add(SyntaxInspections.Describe(file, namespaceDeclaration, $"enumeration namespace '{name}' is prohibited; colocate enums with their domain models."));
                    }
                }
            }

            AssertNoViolations(violations, "enumeration namespace placement");
        }

        [Test]
        public void GivenEveryEnumerationClass_WhenInspecting_ThenItFollowsTheCanonicalTemplate()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (ClassDeclarationSyntax declaration in file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                {
                    if (!IsEnumerationClass(declaration))
                    {
                        continue;
                    }

                    violations.AddRange(ValidateEnumerationClass(file, declaration));
                }
            }

            AssertNoViolations(violations, "enumeration class template");
        }

        [Test]
        public void GivenEveryServiceOrControllerOrStartupClass_WhenInspecting_ThenItUsesPrimaryConstructorParametersDirectly()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (ClassDeclarationSyntax declaration in file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                {
                    if (!IsServiceControllerOrStartup(declaration))
                    {
                        continue;
                    }

                    if (declaration.ParameterList is null)
                    {
                        continue;
                    }

                    HashSet<string> primaryParameterNames = declaration.ParameterList.Parameters
                        .Select(parameter => parameter.Identifier.ValueText)
                        .ToHashSet(StringComparer.Ordinal);

                    foreach (FieldDeclarationSyntax field in declaration.Members.OfType<FieldDeclarationSyntax>())
                    {
                        foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
                        {
                            if (primaryParameterNames.Contains(variable.Identifier.ValueText))
                            {
                                violations.Add(SyntaxInspections.Describe(file, variable, $"field '{variable.Identifier.ValueText}' duplicates a primary-constructor parameter; use the parameter directly."));
                            }
                        }
                    }
                }
            }

            AssertNoViolations(violations, "primary constructor usage");
        }

        [Test]
        public void GivenEveryAutoPropertyCandidateClass_WhenInspecting_ThenPropertiesUseAutoPropertyForm()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (ClassDeclarationSyntax declaration in file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                {
                    if (!IsModelOrEntityOrSettings(file, declaration))
                    {
                        continue;
                    }

                    foreach (PropertyDeclarationSyntax property in declaration.Members.OfType<PropertyDeclarationSyntax>())
                    {
                        if (property.Modifiers.Any(SyntaxKind.StaticKeyword) || property.ExpressionBody is not null)
                        {
                            continue;
                        }

                        if (property.AccessorList is null)
                        {
                            continue;
                        }

                        bool isAutoProperty = property.AccessorList.Accessors
                            .All(accessor => accessor.Body is null && accessor.ExpressionBody is null);

                        if (!isAutoProperty)
                        {
                            violations.Add(SyntaxInspections.Describe(file, property, $"property '{property.Identifier.ValueText}' must use auto-property form."));
                        }
                    }
                }
            }

            AssertNoViolations(violations, "auto-property declarations");
        }

        [Test]
        public void GivenEveryComputedReadonlyProperty_WhenInspecting_ThenItIsExpressionBodied()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (PropertyDeclarationSyntax property in file.Root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
                {
                    if (property.AccessorList is null)
                    {
                        continue;
                    }

                    bool isGetterOnly = property.AccessorList.Accessors.Count == 1 &&
                        property.AccessorList.Accessors[0].IsKind(SyntaxKind.GetAccessorDeclaration);

                    if (!isGetterOnly)
                    {
                        continue;
                    }

                    AccessorDeclarationSyntax getter = property.AccessorList.Accessors[0];

                    if (getter.ExpressionBody is not null)
                    {
                        continue;
                    }

                    if (getter.Body is null || getter.Body.Statements.Count != 1)
                    {
                        continue;
                    }

                    StatementSyntax onlyStatement = getter.Body.Statements[0];

                    if (onlyStatement is IfStatementSyntax ||
                        onlyStatement is ForStatementSyntax ||
                        onlyStatement is CommonForEachStatementSyntax ||
                        onlyStatement is WhileStatementSyntax ||
                        onlyStatement is SwitchStatementSyntax)
                    {
                        continue;
                    }

                    violations.Add(SyntaxInspections.Describe(file, property, $"computed property '{property.Identifier.ValueText}' must be expression-bodied."));
                }
            }

            AssertNoViolations(violations, "expression-bodied computed properties");
        }

        [Test]
        public void GivenEveryModelEqualityType_WhenInspecting_ThenItImplementsEquatableAndOverridesEquality()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (ClassDeclarationSyntax declaration in file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                {
                    if (!IsEnumerationClass(declaration))
                    {
                        continue;
                    }

                    bool implementsEquatable = declaration.BaseList is not null &&
                        declaration.BaseList.Types.Any(type => type.Type.ToString().StartsWith("IEquatable<", StringComparison.Ordinal));

                    if (!implementsEquatable)
                    {
                        violations.Add(SyntaxInspections.Describe(file, declaration, $"enumeration class '{declaration.Identifier.ValueText}' must implement IEquatable<T>."));
                    }

                    bool overridesGetHashCode = declaration.Members
                        .OfType<MethodDeclarationSyntax>()
                        .Any(method => string.Equals(method.Identifier.ValueText, "GetHashCode", StringComparison.Ordinal));

                    if (!overridesGetHashCode)
                    {
                        violations.Add(SyntaxInspections.Describe(file, declaration, $"enumeration class '{declaration.Identifier.ValueText}' must override GetHashCode."));
                    }

                    bool declaresEqualityOperators = declaration.Members
                        .OfType<OperatorDeclarationSyntax>()
                        .Count() >= 2;

                    if (!declaresEqualityOperators)
                    {
                        violations.Add(SyntaxInspections.Describe(file, declaration, $"enumeration class '{declaration.Identifier.ValueText}' must declare == and != operators."));
                    }

                    bool declaresFromString = declaration.Members
                        .OfType<MethodDeclarationSyntax>()
                        .Any(method => string.Equals(method.Identifier.ValueText, "FromString", StringComparison.Ordinal));

                    if (!declaresFromString)
                    {
                        violations.Add(SyntaxInspections.Describe(file, declaration, $"enumeration class '{declaration.Identifier.ValueText}' must declare FromString."));
                    }

                    bool declaresGetValues = declaration.Members
                        .OfType<MethodDeclarationSyntax>()
                        .Any(method => string.Equals(method.Identifier.ValueText, "GetValues", StringComparison.Ordinal));

                    if (!declaresGetValues)
                    {
                        violations.Add(SyntaxInspections.Describe(file, declaration, $"enumeration class '{declaration.Identifier.ValueText}' must declare GetValues."));
                    }
                }
            }

            AssertNoViolations(violations, "enumeration class contract");
        }

        private static bool IsEnumerationClass(ClassDeclarationSyntax declaration)
        {
            if (declaration.BaseList is null)
            {
                return false;
            }

            bool implementsEquatable = declaration.BaseList.Types.Any(type => type.Type.ToString().StartsWith("IEquatable<", StringComparison.Ordinal));

            bool declaresNameProperty = declaration.Members
                .OfType<PropertyDeclarationSyntax>()
                .Any(property => string.Equals(property.Identifier.ValueText, "Name", StringComparison.Ordinal));

            return implementsEquatable && declaresNameProperty;
        }

        private static IEnumerable<string> ValidateEnumerationClass(SourceFile file, ClassDeclarationSyntax declaration)
        {
            if (!declaration.Modifiers.Any(SyntaxKind.PublicKeyword))
            {
                yield return SyntaxInspections.Describe(file, declaration, "enumeration class must be public.");
            }

            ConstructorDeclarationSyntax? constructor = declaration.Members.OfType<ConstructorDeclarationSyntax>().FirstOrDefault();

            if (constructor is null)
            {
                yield return SyntaxInspections.Describe(file, declaration, "enumeration class must declare a private constructor.");
            }
            else if (!constructor.Modifiers.Any(SyntaxKind.PrivateKeyword))
            {
                yield return SyntaxInspections.Describe(file, constructor, "enumeration class constructor must be private.");
            }

            foreach (FieldDeclarationSyntax field in declaration.Members.OfType<FieldDeclarationSyntax>())
            {
                bool isDictionary = field.Declaration.Type.ToString().Contains("Dictionary<", StringComparison.Ordinal);
                bool isStaticReadonly = field.Modifiers.Any(SyntaxKind.StaticKeyword) &&
                    field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword);

                if (isDictionary && !isStaticReadonly)
                {
                    yield return SyntaxInspections.Describe(file, field, "enumeration backing dictionary must be static readonly.");
                }

                bool usesStringLiteralKeys = field.Declaration.Variables.Any(variable =>
                    variable.Initializer is not null &&
                    variable.Initializer.Value.DescendantNodesAndSelf()
                        .OfType<LiteralExpressionSyntax>()
                        .Any(literal => literal.IsKind(SyntaxKind.StringLiteralExpression) &&
                            string.IsNullOrEmpty(literal.Token.ValueText) is false &&
                            char.IsUpper(literal.Token.ValueText[0])));

                if (isDictionary && usesStringLiteralKeys)
                {
                    yield return SyntaxInspections.Describe(file, field, "enumeration backing dictionary should use nameof(...) rather than string literals.");
                }
            }
        }

        private static bool IsModelOrEntityOrSettings(SourceFile file, ClassDeclarationSyntax declaration)
        {
            string namespaceName = declaration.Ancestors()
                .OfType<BaseNamespaceDeclarationSyntax>()
                .FirstOrDefault()?.Name.ToString() ?? string.Empty;

            if (string.Equals(namespaceName, ProductionNamespaces.Configuration, StringComparison.Ordinal))
            {
                return true;
            }

            return declaration.Identifier.ValueText.EndsWith("Settings", StringComparison.Ordinal);
        }

        private static bool IsServiceControllerOrStartup(ClassDeclarationSyntax declaration)
        {
            string name = declaration.Identifier.ValueText;

            if (name.EndsWith("Controller", StringComparison.Ordinal) ||
                name.EndsWith("Service", StringComparison.Ordinal) ||
                string.Equals(name, "Program", StringComparison.Ordinal) ||
                string.Equals(name, "Startup", StringComparison.Ordinal))
            {
                return true;
            }

            return false;
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
