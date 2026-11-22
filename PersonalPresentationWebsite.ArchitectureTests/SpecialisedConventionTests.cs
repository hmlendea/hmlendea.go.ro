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
    public sealed class SpecialisedConventionTests
    {
        private static readonly IReadOnlyList<SourceFile> Files = SourceWorkspace.StyleCheckedFiles;

        private static readonly string[] DateTimeParseMethods =
        [
            "Parse", "ParseExact", "TryParse", "TryParseExact", "ToString", "ToStringExact"
        ];

        [Test]
        public void GivenEveryMappingClass_WhenInspecting_ThenItIsAnInternalStaticClassInAMappingNamespace()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (ClassDeclarationSyntax declaration in file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                {
                    string name = declaration.Identifier.ValueText;

                    if (!name.EndsWith("MappingExtensions", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!declaration.Modifiers.Any(SyntaxKind.InternalKeyword) ||
                        !declaration.Modifiers.Any(SyntaxKind.StaticKeyword))
                    {
                        violations.Add(SyntaxInspections.Describe(file, declaration, $"mapping class '{name}' must be internal static."));
                    }

                    string namespaceName = declaration.Ancestors()
                        .OfType<BaseNamespaceDeclarationSyntax>()
                        .FirstOrDefault()?.Name.ToString() ?? string.Empty;

                    if (!namespaceName.EndsWith(".Mapping", StringComparison.Ordinal))
                    {
                        violations.Add(SyntaxInspections.Describe(file, declaration, $"mapping class '{name}' must reside in a '.Mapping' namespace."));
                    }
                }
            }

            AssertNoViolations(violations, "mapping class shape");
        }

        [Test]
        public void GivenEveryMappingMethod_WhenInspecting_ThenItUsesThePrescribedNameAndIsExtensionBodied()
        {
            List<string> violations = [];
            string[] mappingNames = ["ToServiceModel", "ToServiceModels", "ToDataObject", "ToDataObjects"];

            foreach (SourceFile file in Files)
            {
                foreach (ClassDeclarationSyntax declaration in file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                {
                    if (!declaration.Identifier.ValueText.EndsWith("MappingExtensions", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    foreach (MethodDeclarationSyntax method in declaration.Members.OfType<MethodDeclarationSyntax>())
                    {
                        if (!mappingNames.Contains(method.Identifier.ValueText))
                        {
                            continue;
                        }

                        if (!method.Modifiers.Any(SyntaxKind.StaticKeyword))
                        {
                            violations.Add(SyntaxInspections.Describe(file, method, $"mapping method '{method.Identifier.ValueText}' must be static."));
                        }

                        if (!method.Modifiers.Any(SyntaxKind.InternalKeyword))
                        {
                            violations.Add(SyntaxInspections.Describe(file, method, $"mapping method '{method.Identifier.ValueText}' must be internal."));
                        }

                        if (method.ParameterList.Parameters.Count > 0 && !method.ParameterList.Parameters[0].Modifiers.Any(SyntaxKind.ThisKeyword))
                        {
                            violations.Add(SyntaxInspections.Describe(file, method, $"mapping method '{method.Identifier.ValueText}' must declare an extension receiver."));
                        }

                        if (method.ExpressionBody is null)
                        {
                            violations.Add(SyntaxInspections.Describe(file, method, $"mapping method '{method.Identifier.ValueText}' must be expression-bodied."));
                        }
                    }
                }
            }

            AssertNoViolations(violations, "mapping method contract");
        }

        [Test]
        public void GivenEveryImplementationClass_WhenInspecting_ThenItDeclaresNoMappingMethod()
        {
            List<string> violations = [];
            string[] mappingNames = ["ToServiceModel", "ToServiceModels", "ToDataObject", "ToDataObjects"];

            foreach (SourceFile file in Files)
            {
                foreach (ClassDeclarationSyntax declaration in file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                {
                    if (declaration.Identifier.ValueText.EndsWith("MappingExtensions", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    foreach (MethodDeclarationSyntax method in declaration.Members.OfType<MethodDeclarationSyntax>())
                    {
                        if (mappingNames.Contains(method.Identifier.ValueText))
                        {
                            violations.Add(SyntaxInspections.Describe(file, method, $"mapping method '{method.Identifier.ValueText}' must live in a dedicated mapping extensions class."));
                        }
                    }
                }
            }

            AssertNoViolations(violations, "mapping method colocation");
        }

        [Test]
        public void GivenEveryControllerClass_WhenInspecting_ThenItHasAPluralisedNounNameAndReturnsActionResult()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (ClassDeclarationSyntax declaration in file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                {
                    if (!declaration.Identifier.ValueText.EndsWith("Controller", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    foreach (MethodDeclarationSyntax method in declaration.Members.OfType<MethodDeclarationSyntax>())
                    {
                        if (!method.Modifiers.Any(SyntaxKind.PublicKeyword))
                        {
                            continue;
                        }

                        string returnType = method.ReturnType.ToString();

                        if (returnType.StartsWith("ActionResult<", StringComparison.Ordinal))
                        {
                            violations.Add(SyntaxInspections.Describe(file, method, $"action '{method.Identifier.ValueText}' must return non-generic ActionResult."));
                        }
                    }
                }
            }

            AssertNoViolations(violations, "controller action return types");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoLogCallOmitsAnOperation()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (InvocationExpressionSyntax invocation in file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (invocation.Expression is not MemberAccessExpressionSyntax access)
                    {
                        continue;
                    }

                    string memberName = access.Name.Identifier.ValueText;

                    if (!memberName.StartsWith("Log", StringComparison.Ordinal) ||
                        memberName.StartsWith("LogInfoKey", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    bool hasOperation = invocation.ArgumentList.Arguments
                        .Any(argument => argument.Expression is IdentifierNameSyntax identifier &&
                            identifier.Identifier.ValueText.Contains("operation", StringComparison.OrdinalIgnoreCase));

                    if (!hasOperation)
                    {
                        violations.Add(SyntaxInspections.Describe(file, invocation, $"log call '{memberName}' does not pass an Operation."));
                    }
                }
            }

            AssertNoViolations(violations, "logging operation presence");
        }

        [Test]
        public void GivenEveryLogInfoKeyDerivedKey_WhenInspecting_ThenSensitiveKeysCallTheSensitiveBaseConstructor()
        {
            List<string> violations = [];
            string[] sensitivityFragments = ["password", "secret", "token", "credential", "apikey", "privatekey"];

            foreach (SourceFile file in Files)
            {
                foreach (ClassDeclarationSyntax declaration in file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                {
                    if (declaration.BaseList is null ||
                        !declaration.BaseList.Types.Any(type => type.Type.ToString().Contains("LogInfoKey", StringComparison.Ordinal)))
                    {
                        continue;
                    }

                    bool isSensitive = sensitivityFragments.Any(fragment =>
                        declaration.Identifier.ValueText.Contains(fragment, StringComparison.OrdinalIgnoreCase));

                    if (!isSensitive)
                    {
                        continue;
                    }

                    bool callsSensitiveBase = declaration.Members
                        .OfType<ConstructorDeclarationSyntax>()
                        .Any(constructor => constructor.Initializer is not null &&
                            constructor.Initializer.ArgumentList.Arguments
                                .Any(argument => argument.Expression.IsKind(SyntaxKind.TrueLiteralExpression)));

                    if (!callsSensitiveBase)
                    {
                        violations.Add(SyntaxInspections.Describe(file, declaration, $"sensitive log key '{declaration.Identifier.ValueText}' must pass true to the base LogInfoKey constructor."));
                    }
                }
            }

            AssertNoViolations(violations, "sensitive log key masking");
        }

        [Test]
        public void GivenEveryLoggingType_WhenInspecting_ThenItResidesInTheProjectLoggingNamespace()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files.Where(candidate => !candidate.Project.IsTestProject))
            {
                foreach (ClassDeclarationSyntax declaration in file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                {
                    bool isLoggingType = declaration.BaseList is not null &&
                        declaration.BaseList.Types.Any(type =>
                            type.Type.ToString().StartsWith("LogInfoKey", StringComparison.Ordinal) ||
                            type.Type.ToString().StartsWith("Operation", StringComparison.Ordinal));

                    if (!isLoggingType)
                    {
                        continue;
                    }

                    string namespaceName = declaration.Ancestors()
                        .OfType<BaseNamespaceDeclarationSyntax>()
                        .FirstOrDefault()?.Name.ToString() ?? string.Empty;

                    if (!namespaceName.EndsWith(".Logging", StringComparison.Ordinal))
                    {
                        violations.Add(SyntaxInspections.Describe(file, declaration, $"logging type '{declaration.Identifier.ValueText}' must reside in the project '.Logging' namespace."));
                    }
                }
            }

            AssertNoViolations(violations, "logging namespace placement");
        }

        [Test]
        public void GivenEveryExceptionThrowingLogStatement_WhenInspecting_ThenNoExceptionMessageIsEmbeddedInTheMessageText()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                for (int index = 0; index < file.Lines.Count; index += 1)
                {
                    string line = file.Lines[index];

                    if (!line.Contains(".Log", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (line.Contains(".Message", StringComparison.Ordinal) ||
                        line.Contains("StackTrace", StringComparison.Ordinal))
                    {
                        violations.Add($"{file.RelativePath}:{index + 1}: exception messages and stack traces must be passed via the dedicated exception parameter.");
                    }
                }
            }

            AssertNoViolations(violations, "structured exception logging");
        }

        [Test]
        public void GivenEveryDateTimeParseOrFormatCall_WhenInspecting_ThenAFormatProviderIsSupplied()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (InvocationExpressionSyntax invocation in file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (invocation.Expression is not MemberAccessExpressionSyntax access)
                    {
                        continue;
                    }

                    if (!DateTimeParseMethods.Contains(access.Name.Identifier.ValueText))
                    {
                        continue;
                    }

                    string receiver = access.Expression.ToString();

                    if (!receiver.Contains("DateTime", StringComparison.Ordinal) &&
                        !receiver.Contains("TimeSpan", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    bool hasFormatProvider = invocation.ArgumentList.Arguments
                        .Any(argument => argument.Expression.ToString().Contains("CultureInfo", StringComparison.Ordinal) ||
                            argument.Expression.ToString().Contains("InvariantCulture", StringComparison.Ordinal));

                    bool hasFormatString = invocation.ArgumentList.Arguments
                        .Any(argument => argument.Expression is LiteralExpressionSyntax literal &&
                            literal.IsKind(SyntaxKind.StringLiteralExpression) &&
                            literal.Token.ValueText.Contains('y', StringComparison.Ordinal));

                    if (!hasFormatProvider && !hasFormatString)
                    {
                        violations.Add(SyntaxInspections.Describe(file, invocation, $"date call '{access.Name.Identifier.ValueText}' must supply a format provider or format string."));
                    }
                }
            }

            AssertNoViolations(violations, "culture-independent date handling");
        }

        [Test]
        public void GivenEveryApiDto_WhenInspecting_ThenIdentifierSegmentsSerialiseAsId()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files.Where(candidate => !candidate.Project.IsTestProject))
            {
                foreach (ClassDeclarationSyntax declaration in file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                {
                    foreach (PropertyDeclarationSyntax property in declaration.Members.OfType<PropertyDeclarationSyntax>())
                    {
                        string name = property.Identifier.ValueText;

                        if (!name.EndsWith("Identifier", StringComparison.Ordinal) &&
                            !name.Contains("Identifier", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        AttributeSyntax? attribute = property.AttributeLists
                            .SelectMany(list => list.Attributes)
                            .FirstOrDefault(candidate => candidate.Name.ToString().Contains("JsonPropertyName", StringComparison.Ordinal));

                        if (attribute is null)
                        {
                            violations.Add(SyntaxInspections.Describe(file, property, $"property '{name}' requires a JsonPropertyName mapping that replaces the Identifier segment with 'id'."));
                            continue;
                        }

                        AttributeArgumentSyntax? argument = attribute.ArgumentList?.Arguments.FirstOrDefault();

                        if (argument?.Expression is LiteralExpressionSyntax literal &&
                            !literal.Token.ValueText.Contains("id", StringComparison.Ordinal))
                        {
                            violations.Add(SyntaxInspections.Describe(file, property, $"property '{name}' maps to '{literal.Token.ValueText}', which does not use 'id'."));
                        }
                    }
                }
            }

            AssertNoViolations(violations, "DTO identifier serialisation");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoJsonSerializerOptionsIsAllocatedInsideALoop()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (ObjectCreationExpressionSyntax creation in file.Root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
                {
                    if (!string.Equals(creation.Type.ToString(), "JsonSerializerOptions", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    bool insideLoop = creation.Ancestors().Any(ancestor => ancestor is ForStatementSyntax ||
                        ancestor is CommonForEachStatementSyntax ||
                        ancestor is WhileStatementSyntax ||
                        ancestor is DoStatementSyntax);

                    if (insideLoop)
                    {
                        violations.Add(SyntaxInspections.Describe(file, creation, "JsonSerializerOptions must not be allocated inside a loop."));
                    }
                }
            }

            AssertNoViolations(violations, "serializer options reuse");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoSensitiveValueIsPassedIntoAMessageOrOperationText()
        {
            List<string> violations = [];
            string[] sensitiveFragments = ["Password", "Secret", "Token", "ApiKey", "Credential", "PrivateKey"];

            foreach (SourceFile file in Files)
            {
                for (int index = 0; index < file.Lines.Count; index += 1)
                {
                    string line = file.Lines[index];

                    if (!line.Contains("Message", StringComparison.Ordinal) &&
                        !line.Contains("Operation.", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    foreach (string fragment in sensitiveFragments)
                    {
                        if (line.Contains(fragment + "}", StringComparison.Ordinal) ||
                            line.Contains(fragment + "\"", StringComparison.Ordinal))
                        {
                            violations.Add($"{file.RelativePath}:{index + 1}: sensitive value '{fragment}' must not be interpolated into message or operation text.");
                        }
                    }
                }
            }

            AssertNoViolations(violations, "sensitive value masking");
        }

        [Test]
        public void GivenEveryProjectDeclaringHttpUsage_WhenInspecting_ThenItReferencesNuciWebHttp()
        {
            List<string> violations = [];

            foreach (ProjectSources project in SourceRepository.ProductionProjects)
            {
                bool usesHttpClient = SourceWorkspace.FilesIn(project)
                    .Any(file => file.Root.DescendantNodes()
                        .OfType<IdentifierNameSyntax>()
                        .Any(identifier => string.Equals(identifier.Identifier.ValueText, "HttpClient", StringComparison.Ordinal)));

                if (!usesHttpClient)
                {
                    continue;
                }

                string projectFile = System.IO.Path.Combine(project.Directory, project.Name + ".csproj");
                string content = System.IO.File.ReadAllText(projectFile);

                if (!content.Contains("NuciWeb.HTTP", StringComparison.Ordinal))
                {
                    violations.Add($"{project.Name}: uses HttpClient but does not reference NuciWeb.HTTP.");
                }
            }

            AssertNoViolations(violations, "NuciWeb.HTTP dependency");
        }

        [Test]
        public void GivenEveryHttpClientCreation_WhenInspecting_ThenItUsesTheHttpClientCreator()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files.Where(candidate => !candidate.Project.IsTestProject))
            {
                foreach (ObjectCreationExpressionSyntax creation in file.Root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
                {
                    if (!string.Equals(creation.Type.ToString(), "HttpClient", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    bool usesCreator = creation.Ancestors()
                        .OfType<MethodDeclarationSyntax>()
                        .Any(method => method.Body is not null &&
                            method.Body.ToString().Contains("HttpClientCreator.Create(", StringComparison.Ordinal));

                    if (!usesCreator)
                    {
                        violations.Add(SyntaxInspections.Describe(file, creation, "HttpClient instances must be created through HttpClientCreator.Create()."));
                    }
                }
            }

            AssertNoViolations(violations, "HttpClient construction path");
        }

        [Test]
        public void GivenEveryListenerBinding_WhenInspecting_ThenIpv6AnyIsUsedForIpv6AcceptingListeners()
        {
            List<string> violations = [];
            Regex ipv4Any = new(@"IPAddress\.Any\b");

            foreach (SourceFile file in Files.Where(candidate => !candidate.Project.IsTestProject))
            {
                for (int index = 0; index < file.Lines.Count; index += 1)
                {
                    if (ipv4Any.IsMatch(file.Lines[index]))
                    {
                        violations.Add($"{file.RelativePath}:{index + 1}: listeners that accept IPv6 must bind IPAddress.IPv6Any.");
                    }
                }
            }

            AssertNoViolations(violations, "IPv6 listener binding");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoSynchronousBlockingCallIsMadeOnAnAsyncOperation()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (InvocationExpressionSyntax invocation in file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (invocation.Expression is not MemberAccessExpressionSyntax access)
                    {
                        continue;
                    }

                    string name = access.Name.Identifier.ValueText;

                    if (name is not ("Result" or "Wait" or "GetAwaiter"))
                    {
                        continue;
                    }

                    violations.Add(SyntaxInspections.Describe(file, invocation, $"synchronous '{name}' call on an asynchronous operation is prohibited."));
                }
            }

            AssertNoViolations(violations, "asynchronous awaiting");
        }

        [Test]
        public void GivenEveryAsyncReturningMethod_WhenInspecting_ThenItIsNotMarkedAsyncWithoutAwait()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (MethodDeclarationSyntax method in file.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    if (!method.Modifiers.Any(SyntaxKind.AsyncKeyword))
                    {
                        continue;
                    }

                    if (method.DescendantNodes().OfType<AwaitExpressionSyntax>().Any())
                    {
                        continue;
                    }

                    violations.Add(SyntaxInspections.Describe(file, method, $"async method '{method.Identifier.ValueText}' awaits nothing; remove 'async' or add the awaited operation."));
                }
            }

            AssertNoViolations(violations, "async keyword necessity");
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