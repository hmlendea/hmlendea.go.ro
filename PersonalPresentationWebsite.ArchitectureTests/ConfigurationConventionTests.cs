using System;
using System.Collections.Generic;
using System.IO;
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
    public sealed class ConfigurationConventionTests
    {
        private static readonly IReadOnlyList<SourceFile> Files = SourceWorkspace.StyleCheckedFiles;

        [Test]
        public void GivenEverySettingsClass_WhenInspecting_ThenItIsSealedAndSuffixedWithSettings()
        {
            List<string> violations = [];

            foreach (ConfigurationClass entry in ConfigurationClasses())
            {
                if (!entry.Declaration.Identifier.ValueText.EndsWith("Settings", StringComparison.Ordinal))
                {
                    violations.Add(SyntaxInspections.Describe(entry.File, entry.Declaration, $"configuration class '{entry.Declaration.Identifier.ValueText}' must be suffixed with 'Settings'."));
                }

                if (!entry.Declaration.Modifiers.Any(SyntaxKind.SealedKeyword))
                {
                    violations.Add(SyntaxInspections.Describe(entry.File, entry.Declaration, $"configuration class '{entry.Declaration.Identifier.ValueText}' must be public sealed."));
                }
            }

            AssertNoViolations(violations, "settings class shape");
        }

        [Test]
        public void GivenEverySettingsClassInTheConfigurationNamespace_WhenInspecting_ThenNoCustomKeyMappingAttributeIsRequired()
        {
            List<string> violations = [];

            foreach (ConfigurationClass entry in ConfigurationClasses())
            {
                foreach (PropertyDeclarationSyntax property in entry.Declaration.Members.OfType<PropertyDeclarationSyntax>())
                {
                    AttributeSyntax? mapping = property.AttributeLists
                        .SelectMany(list => list.Attributes)
                        .FirstOrDefault(attribute => attribute.Name.ToString().Contains("ConfigurationKeyName", StringComparison.Ordinal));

                    if (mapping is null)
                    {
                        continue;
                    }

                    string? keyName = ExtractFirstStringArgument(mapping);

                    if (keyName is null)
                    {
                        continue;
                    }

                    if (string.Equals(keyName, property.Identifier.ValueText, StringComparison.OrdinalIgnoreCase))
                    {
                        violations.Add(SyntaxInspections.Describe(entry.File, mapping, $"[ConfigurationKeyName(\"{keyName}\")] differs from the property name '{property.Identifier.ValueText}' only by case."));
                    }
                }
            }

            AssertNoViolations(violations, "unnecessary configuration key mapping");
        }

        [Test]
        public void GivenEverySettingsClass_WhenInspecting_ThenEveryStringKeyDeclaresAnEmptyDefault()
        {
            List<string> violations = [];

            foreach (ConfigurationClass entry in ConfigurationClasses())
            {
                foreach (PropertyDeclarationSyntax property in entry.Declaration.Members.OfType<PropertyDeclarationSyntax>())
                {
                    if (property.Modifiers.Any(SyntaxKind.StaticKeyword))
                    {
                        continue;
                    }

                    if (!string.Equals(property.Type.ToString(), "string", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    bool hasEmptyDefault = property.Initializer is not null &&
                        IsEmptyStringDefault(property.Initializer.Value);

                    if (!hasEmptyDefault)
                    {
                        violations.Add(SyntaxInspections.Describe(entry.File, property, $"settings property '{property.Identifier.ValueText}' must default to string.Empty."));
                    }
                }
            }

            AssertNoViolations(violations, "empty string defaults");
        }

        [Test]
        public void GivenEverySettingsClass_WhenInspecting_ThenItsSectionNameIsACamelCaseStaticExpression()
        {
            List<string> violations = [];
            Regex camelCase = new(@"^[a-z][A-Za-z0-9]*$");

            foreach (ConfigurationClass entry in ConfigurationClasses())
            {
                PropertyDeclarationSyntax? sectionProperty = entry.Declaration.Members
                    .OfType<PropertyDeclarationSyntax>()
                    .FirstOrDefault(property => string.Equals(property.Identifier.ValueText, "SectionName", StringComparison.Ordinal));

                if (sectionProperty is null)
                {
                    continue;
                }

                if (!sectionProperty.Modifiers.Any(SyntaxKind.StaticKeyword))
                {
                    violations.Add(SyntaxInspections.Describe(entry.File, sectionProperty, "SectionName must be static."));
                }

                if (sectionProperty.ExpressionBody is null)
                {
                    violations.Add(SyntaxInspections.Describe(entry.File, sectionProperty, "SectionName must be expression-bodied."));
                }

                string? value = ExtractStringLiteral(sectionProperty);

                if (value is not null && !camelCase.IsMatch(value))
                {
                    violations.Add(SyntaxInspections.Describe(entry.File, sectionProperty, $"SectionName value '{value}' must be camelCase."));
                }
            }

            AssertNoViolations(violations, "section name convention");
        }

        [Test]
        public void GivenEveryJsonConfigurationFile_WhenInspecting_ThenEverySectionRootAndKeyIsCamelCase()
        {
            List<string> violations = [];
            Regex camelCase = new(@"^[a-z][A-Za-z0-9]*$");

            foreach (string file in JsonConfigurationFiles())
            {
                string content = File.ReadAllText(file);

                foreach (Match match in Regex.Matches(content, "\"([A-Za-z][A-Za-z0-9]*)\"\\s*:"))
                {
                    string key = match.Groups[1].Value;

                    if (!camelCase.IsMatch(key))
                    {
                        violations.Add($"{Path.GetFileName(file)}: configuration key '{key}' is not camelCase.");
                    }
                }
            }

            AssertNoViolations(violations, "camelCase configuration keys");
        }

        [Test]
        public void GivenEverySettingsClass_WhenInspecting_ThenEveryDeclaredKeyBindsWithoutAnExplicitMapping()
        {
            List<string> violations = [];

            foreach (ConfigurationClass entry in ConfigurationClasses())
            {
                foreach (PropertyDeclarationSyntax property in entry.Declaration.Members.OfType<PropertyDeclarationSyntax>())
                {
                    if (property.Modifiers.Any(SyntaxKind.StaticKeyword))
                    {
                        continue;
                    }

                    AttributeSyntax? mapping = property.AttributeLists
                        .SelectMany(list => list.Attributes)
                        .FirstOrDefault(attribute => attribute.Name.ToString().Contains("ConfigurationKeyName", StringComparison.Ordinal));

                    if (mapping is null)
                    {
                        continue;
                    }

                    bool usesJsonPropertyName = property.AttributeLists
                        .SelectMany(list => list.Attributes)
                        .Any(attribute => attribute.Name.ToString().Contains("JsonPropertyName", StringComparison.Ordinal));

                    if (!usesJsonPropertyName && !property.Identifier.ValueText.Contains("Identifier", StringComparison.Ordinal))
                    {
                        violations.Add(SyntaxInspections.Describe(entry.File, mapping, $"property '{property.Identifier.ValueText}' uses a key mapping it does not require."));
                    }
                }
            }

            AssertNoViolations(violations, "configuration key mapping necessity");
        }

        private static IEnumerable<ConfigurationClass> ConfigurationClasses()
        {
            foreach (SourceFile file in Files.Where(candidate => !candidate.Project.IsTestProject))
            {
                foreach (ClassDeclarationSyntax declaration in file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                {
                    if (declaration.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StaticKeyword))
                    {
                        continue;
                    }

                    bool inConfigurationNamespace = declaration.Ancestors()
                        .OfType<BaseNamespaceDeclarationSyntax>()
                        .Any(ns => string.Equals(ns.Name.ToString(), ProductionNamespaces.Configuration, StringComparison.Ordinal));

                    if (inConfigurationNamespace)
                    {
                        yield return new ConfigurationClass(file, declaration);
                    }
                }
            }
        }

        private static IEnumerable<string> JsonConfigurationFiles()
        {
            foreach (ProjectSources project in SourceRepository.ProductionProjects)
            {
                foreach (string file in Directory.EnumerateFiles(project.Directory, "appsettings*.json", SearchOption.TopDirectoryOnly))
                {
                    yield return file;
                }
            }
        }

        private static bool IsEmptyStringDefault(ExpressionSyntax expression)
        {
            if (expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                return literal.Token.ValueText.Length == 0;
            }

            return string.Equals(expression.ToString(), "string.Empty", StringComparison.Ordinal);
        }

        private static string? ExtractFirstStringArgument(AttributeSyntax attribute)
        {
            AttributeArgumentSyntax? argument = attribute.ArgumentList?.Arguments.FirstOrDefault();

            if (argument?.Expression is LiteralExpressionSyntax literal)
            {
                return literal.Token.ValueText;
            }

            return null;
        }

        private static string? ExtractStringLiteral(PropertyDeclarationSyntax property)
        {
            ExpressionSyntax? expression = property.ExpressionBody?.Expression;

            if (expression is LiteralExpressionSyntax literal)
            {
                return literal.Token.ValueText;
            }

            return null;
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
