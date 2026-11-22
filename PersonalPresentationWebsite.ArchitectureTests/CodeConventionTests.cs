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
    public sealed class CodeConventionTests
    {
        private static readonly IReadOnlyList<SourceFile> Files = SourceWorkspace.StyleCheckedFiles;

        private static readonly string[] BuiltInTypeNames =
        [
            "String", "Int32", "Int64", "Int16", "UInt16", "UInt32", "UInt64",
            "Boolean", "Object", "Double", "Single", "Decimal", "Byte", "SByte", "Char"
        ];

        private static readonly string[] EmptyCollectionTypeNames =
        [
            "List", "Dictionary", "HashSet", "Queue", "Stack", "SortedDictionary", "SortedSet", "LinkedList"
        ];

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoConstMemberIsDeclared()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (MemberDeclarationSyntax member in file.Root.DescendantNodes().OfType<MemberDeclarationSyntax>())
                {
                    if (member.Modifiers.Any(SyntaxKind.ConstKeyword))
                    {
                        violations.Add(SyntaxInspections.Describe(file, member, "const members are prohibited; use a static expression-bodied constant."));
                    }
                }
            }

            AssertNoViolations(violations, "static expression-bodied constants");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoVariableIsDeclaredWithVar()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (SyntaxToken token in file.Root.DescendantTokens())
                {
                    if (token.IsKind(SyntaxKind.VarKeyword))
                    {
                        violations.Add($"{file.RelativePath}:{token.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: 'var' is prohibited; use the explicit type.");
                    }
                }
            }

            AssertNoViolations(violations, "explicit type usage");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoTernaryConditionalIsDeclared()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (ConditionalExpressionSyntax expression in file.Root.DescendantNodes().OfType<ConditionalExpressionSyntax>())
                {
                    violations.Add(SyntaxInspections.Describe(file, expression, "ternary conditional expressions are prohibited."));
                }
            }

            AssertNoViolations(violations, "absence of ternary expressions");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoNullEqualityComparisonIsUsed()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (BinaryExpressionSyntax expression in file.Root.DescendantNodes().OfType<BinaryExpressionSyntax>())
                {
                    bool isEquality = expression.IsKind(SyntaxKind.EqualsExpression) ||
                        expression.IsKind(SyntaxKind.NotEqualsExpression);

                    bool comparesNull = expression.Left.IsKind(SyntaxKind.NullLiteralExpression) ||
                        expression.Right.IsKind(SyntaxKind.NullLiteralExpression);

                    if (isEquality && comparesNull)
                    {
                        violations.Add(SyntaxInspections.Describe(file, expression, "compare against null with 'is null' or 'is not null'."));
                    }
                }
            }

            AssertNoViolations(violations, "null pattern comparisons");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoNullCoalescingOperatorIsUsed()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (BinaryExpressionSyntax expression in file.Root.DescendantNodes().OfType<BinaryExpressionSyntax>())
                {
                    if (expression.IsKind(SyntaxKind.CoalesceExpression))
                    {
                        violations.Add(SyntaxInspections.Describe(file, expression, "the null-coalescing operator is prohibited."));
                    }
                }
            }

            AssertNoViolations(violations, "absence of the null-coalescing operator");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNullableReferenceDirectivesAreAbsent()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                if (file.Lines.Any(line => line.Contains("#nullable", StringComparison.Ordinal)))
                {
                    violations.Add($"{file.RelativePath}: the '#nullable' directive is prohibited.");
                }
            }

            AssertNoViolations(violations, "absence of nullable directives");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoRegionDirectiveIsDeclared()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (SyntaxTrivia trivia in file.Root.DescendantTrivia())
                {
                    if (trivia.IsKind(SyntaxKind.RegionDirectiveTrivia) ||
                        trivia.IsKind(SyntaxKind.EndRegionDirectiveTrivia))
                    {
                        violations.Add($"{file.RelativePath}:{trivia.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: region directives are prohibited.");
                    }
                }
            }

            AssertNoViolations(violations, "absence of region directives");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoRefOrOutParameterIsDeclared()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (ParameterSyntax parameter in file.Root.DescendantNodes().OfType<ParameterSyntax>())
                {
                    if (parameter.Modifiers.Any(SyntaxKind.RefKeyword))
                    {
                        violations.Add(SyntaxInspections.Describe(file, parameter, "'ref' parameters are prohibited."));
                    }

                    if (parameter.Modifiers.Any(SyntaxKind.OutKeyword))
                    {
                        violations.Add(SyntaxInspections.Describe(file, parameter, "'out' parameters are discouraged."));
                    }
                }
            }

            AssertNoViolations(violations, "ref and out parameter usage");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoTupleTypeOrExpressionIsDeclared()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (TupleTypeSyntax tuple in file.Root.DescendantNodes().OfType<TupleTypeSyntax>())
                {
                    violations.Add(SyntaxInspections.Describe(file, tuple, "tuple types are prohibited."));
                }

                foreach (TupleExpressionSyntax tuple in file.Root.DescendantNodes().OfType<TupleExpressionSyntax>())
                {
                    violations.Add(SyntaxInspections.Describe(file, tuple, "tuple expressions are prohibited."));
                }
            }

            AssertNoViolations(violations, "absence of tuples");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoInitAccessorIsDeclared()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (AccessorDeclarationSyntax accessor in file.Root.DescendantNodes().OfType<AccessorDeclarationSyntax>())
                {
                    if (accessor.IsKind(SyntaxKind.InitAccessorDeclaration))
                    {
                        violations.Add(SyntaxInspections.Describe(file, accessor, "'init' accessors are prohibited."));
                    }
                }
            }

            AssertNoViolations(violations, "absence of init accessors");
        }

        [Test]
        public void GivenEveryParameter_WhenInspecting_ThenNoDefaultValueIsDeclared()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (ParameterSyntax parameter in file.Root.DescendantNodes().OfType<ParameterSyntax>())
                {
                    if (parameter.Default is null)
                    {
                        continue;
                    }

                    violations.Add(SyntaxInspections.Describe(file, parameter, $"parameter '{parameter.Identifier.ValueText}' declares a default value; use an overload."));
                }
            }

            AssertNoViolations(violations, "absence of optional parameters");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoExplicitSelfAssignmentIncrementIsUsed()
        {
            List<string> violations = [];
            Regex pattern = new(@"(\w+)\s*=\s*\1\s*([+\-])\s*1(?![0-9])");

            foreach (SourceFile file in Files)
            {
                for (int index = 0; index < file.Lines.Count; index += 1)
                {
                    if (pattern.IsMatch(file.Lines[index]))
                    {
                        violations.Add($"{file.RelativePath}:{index + 1}: replace the explicit self-assignment with '+=' or '-='.");
                    }
                }
            }

            AssertNoViolations(violations, "compound increment and decrement form");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoInlineFullyQualifiedTypeIsUsed()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (QualifiedNameSyntax name in file.Root.DescendantNodes().OfType<QualifiedNameSyntax>())
                {
                    if (IsPartOfUsingDirective(name) || IsPartOfNamespace(name))
                    {
                        continue;
                    }

                    violations.Add(SyntaxInspections.Describe(file, name, $"'{name}' is fully qualified; add a using directive and use the short name."));
                }
            }

            AssertNoViolations(violations, "short-name type usage");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoBclClassNameReplacesABuiltInAlias()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (IdentifierNameSyntax identifier in file.Root.DescendantNodes().OfType<IdentifierNameSyntax>())
                {
                    if (!BuiltInTypeNames.Contains(identifier.Identifier.ValueText))
                    {
                        continue;
                    }

                    if (IsMemberName(identifier) || IsPartOfUsingDirective(identifier) || IsPartOfNamespace(identifier))
                    {
                        continue;
                    }

                    violations.Add(SyntaxInspections.Describe(file, identifier, $"use the built-in alias instead of '{identifier.Identifier.ValueText}'."));
                }
            }

            AssertNoViolations(violations, "built-in type aliases");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenEveryMemberDeclaresAnExplicitAccessibility()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (MemberDeclarationSyntax member in file.Root.DescendantNodes().OfType<MemberDeclarationSyntax>())
                {
                    if (member is BaseNamespaceDeclarationSyntax or BaseTypeDeclarationSyntax or EnumMemberDeclarationSyntax)
                    {
                        continue;
                    }

                    if (member.Parent is InterfaceDeclarationSyntax)
                    {
                        continue;
                    }

                    if (!SyntaxInspections.DeclaresAccessibility(member.Modifiers))
                    {
                        violations.Add(SyntaxInspections.Describe(file, member, $"member '{SyntaxInspections.MemberName(member)}' omits an explicit accessibility."));
                    }
                }
            }

            AssertNoViolations(violations, "explicit member accessibility");
        }

        [Test]
        public void GivenEveryTopLevelType_WhenInspecting_ThenItDeclaresAnExplicitAccessibility()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (BaseTypeDeclarationSyntax declaration in file.TopLevelTypeDeclarations)
                {
                    if (!SyntaxInspections.DeclaresTypeAccessibility(declaration.Modifiers))
                    {
                        violations.Add(SyntaxInspections.Describe(file, declaration, $"type '{declaration.Identifier.ValueText}' omits an explicit accessibility."));
                    }
                }
            }

            AssertNoViolations(violations, "explicit type accessibility");
        }

        [Test]
        public void GivenEveryConcreteClass_WhenInspecting_ThenItIsSealed()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (ClassDeclarationSyntax declaration in file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                {
                    if (declaration.Modifiers.Any(SyntaxKind.StaticKeyword) ||
                        declaration.Modifiers.Any(SyntaxKind.AbstractKeyword) ||
                        declaration.Modifiers.Any(SyntaxKind.SealedKeyword))
                    {
                        continue;
                    }

                    if (IsDesignedForInheritance(declaration))
                    {
                        continue;
                    }

                    violations.Add(SyntaxInspections.Describe(file, declaration, $"class '{declaration.Identifier.ValueText}' is not sealed."));
                }
            }

            AssertNoViolations(violations, "sealed class declarations");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoPartialTypeIsDeclared()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (BaseTypeDeclarationSyntax declaration in file.TypeDeclarations)
                {
                    if (!declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
                    {
                        continue;
                    }

                    if (IsGeneratedRegexHolder(declaration))
                    {
                        continue;
                    }

                    violations.Add(SyntaxInspections.Describe(file, declaration, $"partial type '{declaration.Identifier.ValueText}' is prohibited unless it hosts a GeneratedRegex member."));
                }
            }

            AssertNoViolations(violations, "restricted partial type usage");
        }

        [Test]
        public void GivenEveryPublicClassWithMethods_WhenInspecting_ThenAColocatedInterfaceExists()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files.Where(candidate => !candidate.Project.IsTestProject))
            {
                foreach (ClassDeclarationSyntax declaration in file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                {
                    if (declaration.Modifiers.Any(SyntaxKind.StaticKeyword) ||
                        declaration.Modifiers.Any(SyntaxKind.AbstractKeyword))
                    {
                        continue;
                    }

                    if (!ExposesPublicInstanceMethod(declaration))
                    {
                        continue;
                    }

                    string expectedInterface = "I" + declaration.Identifier.ValueText;

                    bool exists = Files.Any(candidate => candidate.TypeDeclarations
                        .OfType<InterfaceDeclarationSyntax>()
                        .Any(interfaceDeclaration => string.Equals(interfaceDeclaration.Identifier.ValueText, expectedInterface, StringComparison.Ordinal)));

                    if (!exists)
                    {
                        violations.Add(SyntaxInspections.Describe(file, declaration, $"class '{declaration.Identifier.ValueText}' requires a colocated interface '{expectedInterface}'."));
                    }
                }
            }

            AssertNoViolations(violations, "interface pairing for eligible classes");
        }

        [Test]
        public void GivenEverySingleStatementMethod_WhenInspecting_ThenItIsExpressionBodied()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (MethodDeclarationSyntax method in file.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    if (method.Body is null || method.ExpressionBody is not null)
                    {
                        continue;
                    }

                    if (method.Body.Statements.Count != 1)
                    {
                        continue;
                    }

                    StatementSyntax statement = method.Body.Statements[0];

                    if (statement is not (ReturnStatementSyntax or ThrowStatementSyntax or ExpressionStatementSyntax))
                    {
                        continue;
                    }

                    violations.Add(SyntaxInspections.Describe(file, method, $"single-statement method '{method.Identifier.ValueText}' must be expression-bodied."));
                }
            }

            AssertNoViolations(violations, "expression-bodied methods");
        }

        [Test]
        public void GivenEveryMethod_WhenInspecting_ThenItsParameterCountDoesNotExceedSeven()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (BaseMethodDeclarationSyntax method in file.Root.DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
                {
                    if (method.ParameterList is null || method.ParameterList.Parameters.Count <= 7)
                    {
                        continue;
                    }

                    violations.Add(SyntaxInspections.Describe(file, method, $"declares {method.ParameterList.Parameters.Count} parameters; the limit is seven."));
                }
            }

            AssertNoViolations(violations, "seven-parameter limit");
        }

        [Test]
        public void GivenEveryLambda_WhenInspecting_ThenItsParameterCountDoesNotExceedSeven()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (AnonymousFunctionExpressionSyntax lambda in file.Root.DescendantNodes().OfType<AnonymousFunctionExpressionSyntax>())
                {
                    int count = lambda switch
                    {
                        ParenthesizedLambdaExpressionSyntax parenthesised => parenthesised.ParameterList.Parameters.Count,
                        SimpleLambdaExpressionSyntax => 1,
                        AnonymousMethodExpressionSyntax anonymous => anonymous.ParameterList?.Parameters.Count ?? 0,
                        _ => 0
                    };

                    if (count > 7)
                    {
                        violations.Add(SyntaxInspections.Describe(file, lambda, $"declares {count} lambda parameters; the limit is seven."));
                    }
                }
            }

            AssertNoViolations(violations, "seven-parameter lambda limit");
        }

        [Test]
        public void GivenEveryCancellationTokenParameter_WhenInspecting_ThenItIsDeclaredLast()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (ParameterListSyntax list in file.Root.DescendantNodes().OfType<ParameterListSyntax>())
                {
                    for (int index = 0; index < list.Parameters.Count - 1; index += 1)
                    {
                        if (string.Equals(list.Parameters[index].Type?.ToString(), "CancellationToken", StringComparison.Ordinal))
                        {
                            violations.Add(SyntaxInspections.Describe(file, list.Parameters[index], "CancellationToken must be the final parameter."));
                        }
                    }
                }
            }

            AssertNoViolations(violations, "CancellationToken parameter ordering");
        }

        [Test]
        public void GivenEveryControlFlowStatement_WhenInspecting_ThenItsBodyUsesBraces()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (IfStatementSyntax statement in file.Root.DescendantNodes().OfType<IfStatementSyntax>())
                {
                    SyntaxInspections.RecordBracelessBody(file, statement.Statement, "if", violations);

                    if (statement.Else is not null && statement.Else.Statement is not IfStatementSyntax)
                    {
                        SyntaxInspections.RecordBracelessBody(file, statement.Else.Statement, "else", violations);
                    }
                }

                foreach (ForStatementSyntax statement in file.Root.DescendantNodes().OfType<ForStatementSyntax>())
                {
                    SyntaxInspections.RecordBracelessBody(file, statement.Statement, "for", violations);
                }

                foreach (CommonForEachStatementSyntax statement in file.Root.DescendantNodes().OfType<CommonForEachStatementSyntax>())
                {
                    SyntaxInspections.RecordBracelessBody(file, statement.Statement, "foreach", violations);
                }

                foreach (WhileStatementSyntax statement in file.Root.DescendantNodes().OfType<WhileStatementSyntax>())
                {
                    SyntaxInspections.RecordBracelessBody(file, statement.Statement, "while", violations);
                }

                foreach (DoStatementSyntax statement in file.Root.DescendantNodes().OfType<DoStatementSyntax>())
                {
                    SyntaxInspections.RecordBracelessBody(file, statement.Statement, "do", violations);
                }
            }

            AssertNoViolations(violations, "braced control-flow bodies");
        }

        [Test]
        public void GivenEveryElseIfClause_WhenInspecting_ThenItOccupiesASingleLine()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (ElseClauseSyntax elseClause in file.Root.DescendantNodes().OfType<ElseClauseSyntax>())
                {
                    if (elseClause.Statement is not IfStatementSyntax nestedIf)
                    {
                        continue;
                    }

                    int elseLine = elseClause.ElseKeyword.GetLocation().GetLineSpan().StartLinePosition.Line;
                    int ifLine = nestedIf.IfKeyword.GetLocation().GetLineSpan().StartLinePosition.Line;

                    if (elseLine != ifLine)
                    {
                        violations.Add($"{file.RelativePath}:{elseLine + 1}: 'else if' must occupy a single line.");
                    }
                }
            }

            AssertNoViolations(violations, "single-line else-if layout");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoBlankLinePrecedesAClosingDelimiter()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                for (int index = 1; index < file.Lines.Count; index += 1)
                {
                    string current = file.Lines[index].TrimStart();

                    if (!current.StartsWith('}') && !current.StartsWith(']') && !current.StartsWith(')'))
                    {
                        continue;
                    }

                    if (file.Lines[index - 1].Trim().Length == 0)
                    {
                        violations.Add($"{file.RelativePath}:{index + 1}: a blank line precedes the closing delimiter '{current.Trim()}'.");
                    }
                }
            }

            AssertNoViolations(violations, "no blank line before a closing delimiter");
        }

        [Test]
        public void GivenEveryTypeDeclaration_WhenInspecting_ThenExactlyOneBlankLineSeparatesAdjacentMembers()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (TypeDeclarationSyntax declaration in file.Root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                {
                    SyntaxInspections.RecordMemberSpacing(file, declaration, violations);
                }
            }

            AssertNoViolations(violations, "one blank line between members");
        }

        [Test]
        public void GivenEveryReturnOrThrowStatement_WhenInspecting_ThenABlankLineSeparatesItFromThePreviousStatement()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (BlockSyntax block in file.Root.DescendantNodes().OfType<BlockSyntax>())
                {
                    SyntaxInspections.RecordJumpSpacing(file, block, violations);
                }
            }

            AssertNoViolations(violations, "blank line separation of return and throw statements");
        }

        [Test]
        public void GivenEveryControlFlowBlock_WhenInspecting_ThenBlankLinesSeparateItFromAdjacentStatements()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (BlockSyntax block in file.Root.DescendantNodes().OfType<BlockSyntax>())
                {
                    SyntaxInspections.RecordControlFlowSpacing(file, block, violations);
                }
            }

            AssertNoViolations(violations, "blank line separation of control-flow blocks");
        }

        [Test]
        public void GivenEveryField_WhenInspecting_ThenTheFieldOrderFollowsThePrescribedStaticAndReadonlySequence()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (TypeDeclarationSyntax declaration in file.Root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                {
                    List<FieldDeclarationSyntax> fields = [.. declaration.Members.OfType<FieldDeclarationSyntax>()];

                    for (int index = 1; index < fields.Count; index += 1)
                    {
                        if (SyntaxInspections.FieldKindRank(fields[index]) < SyntaxInspections.FieldKindRank(fields[index - 1]))
                        {
                            violations.Add(SyntaxInspections.Describe(file, fields[index], "field order must be static readonly, static mutable, instance readonly, instance mutable."));
                        }
                    }
                }
            }

            AssertNoViolations(violations, "field declaration ordering");
        }

        [Test]
        public void GivenEveryPrivateField_WhenInspecting_ThenItsNameIsCamelCaseWithoutAnUnderscorePrefix()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (FieldDeclarationSyntax field in file.Root.DescendantNodes().OfType<FieldDeclarationSyntax>())
                {
                    if (!field.Modifiers.Any(SyntaxKind.PrivateKeyword))
                    {
                        continue;
                    }

                    foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
                    {
                        string name = variable.Identifier.ValueText;

                        if (name.StartsWith('_'))
                        {
                            violations.Add(SyntaxInspections.Describe(file, variable, $"private field '{name}' must not use an underscore prefix."));
                        }

                        if (!char.IsLower(name[0]))
                        {
                            violations.Add(SyntaxInspections.Describe(file, variable, $"private field '{name}' must be camelCase."));
                        }
                    }
                }
            }

            AssertNoViolations(violations, "private field naming");
        }

        [Test]
        public void GivenEveryPublicOrProtectedMember_WhenInspecting_ThenItsNameStartsUppercase()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (MemberDeclarationSyntax member in file.Root.DescendantNodes().OfType<MemberDeclarationSyntax>())
                {
                    if (!member.Modifiers.Any(SyntaxKind.PublicKeyword) &&
                        !member.Modifiers.Any(SyntaxKind.ProtectedKeyword))
                    {
                        continue;
                    }

                    string? name = SyntaxInspections.MemberName(member);

                    if (name is null || name.Length == 0)
                    {
                        continue;
                    }

                    if (!char.IsUpper(name[0]))
                    {
                        violations.Add(SyntaxInspections.Describe(file, member, $"public or protected member '{name}' must start uppercase."));
                    }
                }
            }

            AssertNoViolations(violations, "public and protected member naming");
        }

        [Test]
        public void GivenEveryNamespace_WhenInspecting_ThenItsSegmentsArePascalCase()
        {
            List<string> violations = [];
            Regex pattern = new(@"^[A-Z][A-Za-z0-9]*$");

            foreach (SourceFile file in Files)
            {
                foreach (BaseNamespaceDeclarationSyntax declaration in file.Root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>())
                {
                    foreach (string segment in declaration.Name.ToString().Split('.'))
                    {
                        if (!pattern.IsMatch(segment))
                        {
                            violations.Add($"{file.RelativePath}: namespace segment '{segment}' is not PascalCase.");
                        }
                    }
                }
            }

            AssertNoViolations(violations, "namespace naming");
        }

        [Test]
        public void GivenEveryAsyncMethod_WhenInspecting_ThenItDeclaresAnAwaitExpression()
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

                    violations.Add(SyntaxInspections.Describe(file, method, $"async method '{method.Identifier.ValueText}' declares no await expression."));
                }
            }

            AssertNoViolations(violations, "awaited asynchronous methods");
        }

        [Test]
        public void GivenEveryRethrowStatement_WhenInspecting_ThenItDoesNotRespecifyTheCaughtException()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (ThrowStatementSyntax statement in file.Root.DescendantNodes().OfType<ThrowStatementSyntax>())
                {
                    if (statement.Expression is not IdentifierNameSyntax identifier)
                    {
                        continue;
                    }

                    CatchClauseSyntax? catchClause = statement.Ancestors().OfType<CatchClauseSyntax>().FirstOrDefault();

                    if (catchClause?.Declaration is null)
                    {
                        continue;
                    }

                    if (string.Equals(catchClause.Declaration.Identifier.ValueText, identifier.Identifier.ValueText, StringComparison.Ordinal))
                    {
                        violations.Add(SyntaxInspections.Describe(file, statement, "rethrow with a bare 'throw;' to preserve the stack trace."));
                    }
                }
            }

            AssertNoViolations(violations, "bare rethrow form");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoEmptyCatchBlockIsDeclared()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (CatchClauseSyntax catchClause in file.Root.DescendantNodes().OfType<CatchClauseSyntax>())
                {
                    if (catchClause.Block.Statements.Count == 0)
                    {
                        violations.Add(SyntaxInspections.Describe(file, catchClause, "empty catch blocks are prohibited."));
                    }
                }
            }

            AssertNoViolations(violations, "non-empty catch blocks");
        }

        [Test]
        public void GivenEveryCustomExceptionType_WhenInspecting_ThenItSuffixesExceptionAndChainsTheBaseConstructor()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (ClassDeclarationSyntax declaration in file.TypeDeclarations.OfType<ClassDeclarationSyntax>())
                {
                    if (!InheritsFromException(declaration))
                    {
                        continue;
                    }

                    if (!declaration.Identifier.ValueText.EndsWith("Exception", StringComparison.Ordinal))
                    {
                        violations.Add(SyntaxInspections.Describe(file, declaration, $"exception type '{declaration.Identifier.ValueText}' must end with 'Exception'."));
                    }

                    foreach (ConstructorDeclarationSyntax constructor in declaration.Members.OfType<ConstructorDeclarationSyntax>())
                    {
                        if (constructor.Initializer is null)
                        {
                            violations.Add(SyntaxInspections.Describe(file, constructor, "exception constructors must forward their arguments to the base Exception constructor."));
                        }
                    }
                }
            }

            AssertNoViolations(violations, "custom exception conventions");
        }

        [Test]
        public void GivenEveryCollectionReturnAndParameter_WhenInspecting_ThenInterfacesArePreferred()
        {
            List<string> violations = [];
            string[] concreteTypes = ["List<", "IList<", "IReadOnlyList<", "HashSet<", "ISet<", "Dictionary<"];

            foreach (SourceFile file in Files)
            {
                foreach (BaseMethodDeclarationSyntax method in file.Root.DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
                {
                    if (method is not MethodDeclarationSyntax declaration)
                    {
                        continue;
                    }

                    string returnType = declaration.ReturnType.ToString();
                    string[] aliases = ["List<", "IList<", "IReadOnlyList<", "HashSet<", "ISet<", "Dictionary<"];

                    if (aliases.Any(alias => returnType.Contains(alias, StringComparison.Ordinal)))
                    {
                        violations.Add(SyntaxInspections.Describe(file, declaration, $"method '{declaration.Identifier.ValueText}' returns a concrete collection type '{returnType}'."));
                    }
                }
            }

            AssertNoViolations(violations, "IEnumerable collection contracts");
        }

        [Test]
        public void GivenEverySourceFile_WhenInspecting_ThenNoEmptyConcreteCollectionIsConstructed()
        {
            List<string> violations = [];

            foreach (SourceFile file in Files)
            {
                foreach (ObjectCreationExpressionSyntax creation in file.Root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
                {
                    string typeName = creation.Type.ToString();

                    if (!EmptyCollectionTypeNames.Contains(typeName))
                    {
                        continue;
                    }

                    if (creation.ArgumentList is not null && creation.ArgumentList.Arguments.Count > 0)
                    {
                        continue;
                    }

                    violations.Add(SyntaxInspections.Describe(file, creation, $"constructs an empty '{typeName}' explicitly; use a collection expression."));
                }
            }

            AssertNoViolations(violations, "collection expression initialisation");
        }

        [Test]
        public void GivenEveryLine_WhenInspecting_ThenNoneExceedsTheArgumentWidthThresholdWithoutSplitting()
        {
            List<string> violations = [];
            Regex invocationPattern = new(@"\w+\([^()]*,[^()]*\)");

            foreach (SourceFile file in Files)
            {
                foreach (string line in file.Lines)
                {
                    if (line.Length <= 96)
                    {
                        continue;
                    }

                    if (!invocationPattern.IsMatch(line))
                    {
                        continue;
                    }

                    violations.Add($"{file.RelativePath}: line of {line.Length} characters holds a multi-argument invocation that must be split.");
                }
            }

            AssertNoViolations(violations, "argument width threshold");
        }

        private static bool IsPartOfUsingDirective(SyntaxNode node)
            => node.Ancestors().OfType<UsingDirectiveSyntax>().Any();

        private static bool IsPartOfNamespace(SyntaxNode node)
            => node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Any();

        private static bool IsMemberName(IdentifierNameSyntax identifier)
            => identifier.Parent is MemberAccessExpressionSyntax memberAccess &&
               ReferenceEquals(memberAccess.Name, identifier);

        private static bool IsDesignedForInheritance(ClassDeclarationSyntax declaration)
        {
            if (declaration.BaseList is not null && declaration.BaseList.Types.Count > 0)
            {
                return true;
            }

            return declaration.DescendantNodes()
                .OfType<AttributeSyntax>()
                .Any(attribute => attribute.Name.ToString().Contains("Inheritance", StringComparison.Ordinal));
        }

        private static bool IsGeneratedRegexHolder(BaseTypeDeclarationSyntax declaration)
            => declaration.DescendantNodes()
                .OfType<AttributeSyntax>()
                .Any(attribute => attribute.Name.ToString().Contains("GeneratedRegex", StringComparison.Ordinal));

        private static bool ExposesPublicInstanceMethod(ClassDeclarationSyntax declaration)
        {
            if (declaration.Ancestors()
                .OfType<BaseNamespaceDeclarationSyntax>()
                .Any(ns => ns.Name.ToString().EndsWith("Components", StringComparison.Ordinal) ||
                    ns.Name.ToString().EndsWith("Configuration", StringComparison.Ordinal)))
            {
                return false;
            }

            return declaration.Members
                .OfType<MethodDeclarationSyntax>()
                .Any(method => method.Modifiers.Any(SyntaxKind.PublicKeyword) &&
                    !method.Modifiers.Any(SyntaxKind.StaticKeyword));
        }

        private static bool InheritsFromException(ClassDeclarationSyntax declaration)
        {
            if (declaration.BaseList is null)
            {
                return false;
            }

            return declaration.BaseList.Types
                .Any(type => type.Type.ToString().EndsWith("Exception", StringComparison.Ordinal));
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
