using System;
using System.Linq;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Slices;
using ArchUnitNET.NUnit;

using NUnit.Framework;

using PersonalPresentationWebsite.ArchitectureTests.Infrastructure;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace PersonalPresentationWebsite.ArchitectureTests
{
    [TestFixture]
    public sealed class DependencyTests
    {
        private static readonly Architecture Production = ProductionArchitecture.Instance;

        [Test]
        public void GivenTheProductionAssembly_WhenLoaded_ThenItContributesTypes()
        {
            RuleAssertions.EnsureSelected(Production.Types.Count(), "production types");

            Assert.That(Production.Types, Is.Not.Empty);
        }

        [Test]
        public void GivenTheProductionAssembly_WhenLoaded_ThenNoTestAssemblyIsPresent()
        {
            string[] assemblies = Production.Assemblies
                .Select(assembly => assembly.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.That(
                assemblies,
                Is.EqualTo(new[] { "PersonalPresentationWebsite" }));
        }

        [Test]
        public void GivenTheProductionNamespaces_WhenEnumerated_ThenOnlyTheExpectedOnesExist()
        {
            string[] namespaces = Production.Namespaces
                .Select(namespaceObject => namespaceObject.FullName)
                .Where(name => name.StartsWith(ProductionNamespaces.Root, StringComparison.Ordinal))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.That(
                namespaces,
                Is.EqualTo(
                    new[]
                    {
                        ProductionNamespaces.Root,
                        ProductionNamespaces.Components,
                        $"{ProductionNamespaces.Components}.Layout",
                        $"{ProductionNamespaces.Components}.Pages",
                        ProductionNamespaces.Configuration,
                        ProductionNamespaces.Endpoints
                    }));
        }

        [Test]
        public void GivenTheProductionTypes_WhenInspectingNamespaces_ThenNoInterfacesNamespaceExists()
        {
            IArchRule rule = Types()
                .That()
                .ResideInNamespaceMatching($"{ProductionNamespaces.Root}.*")
                .Should()
                .NotResideInNamespaceMatching(".*\\.Interfaces$");

            rule.Check(Production);
        }

        [Test]
        public void GivenTheProductionTypes_WhenInspectingNamespaces_ThenNoGenericServiceOrUtilityNamespaceExists()
        {
            IArchRule servicesRule = Types()
                .That()
                .ResideInNamespaceMatching($"{ProductionNamespaces.Root}.*")
                .Should()
                .NotResideInNamespaceMatching(".*\\.Services$");

            IArchRule utilitiesRule = Types()
                .That()
                .ResideInNamespaceMatching($"{ProductionNamespaces.Root}.*")
                .Should()
                .NotResideInNamespaceMatching(".*\\.Utilities$");

            servicesRule.Check(Production);
            utilitiesRule.Check(Production);
        }

        [Test]
        public void GivenTheProductionTypes_WhenInspectingNamespaces_ThenNoEnumerationsNamespaceExists()
        {
            IArchRule enumerationsRule = Types()
                .That()
                .ResideInNamespaceMatching($"{ProductionNamespaces.Root}.*")
                .Should()
                .NotResideInNamespaceMatching(".*\\.Enumerations$");

            IArchRule enumsRule = Types()
                .That()
                .ResideInNamespaceMatching($"{ProductionNamespaces.Root}.*")
                .Should()
                .NotResideInNamespaceMatching(".*\\.Enums$");

            enumerationsRule.Check(Production);
            enumsRule.Check(Production);
        }

        [Test]
        public void GivenTheConfigurationTypes_WhenInspectingDependencies_ThenTheyDoNotDependOnComponents()
        {
            IArchRule rule = Types()
                .That()
                .ResideInNamespaceMatching($"{ProductionNamespaces.Configuration}.*")
                .Should()
                .NotDependOnAnyTypesThat()
                .ResideInNamespaceMatching($"{ProductionNamespaces.Components}.*");

            rule.Check(Production);
        }

        [Test]
        public void GivenTheConfigurationTypes_WhenInspectingDependencies_ThenTheyDoNotDependOnEndpoints()
        {
            IArchRule rule = Types()
                .That()
                .ResideInNamespaceMatching($"{ProductionNamespaces.Configuration}.*")
                .Should()
                .NotDependOnAnyTypesThat()
                .ResideInNamespaceMatching($"{ProductionNamespaces.Endpoints}.*");

            rule.Check(Production);
        }

        [Test]
        public void GivenTheEndpointTypes_WhenInspectingDependencies_ThenTheyDoNotDependOnComponents()
        {
            IArchRule rule = Types()
                .That()
                .ResideInNamespaceMatching($"{ProductionNamespaces.Endpoints}.*")
                .Should()
                .NotDependOnAnyTypesThat()
                .ResideInNamespaceMatching($"{ProductionNamespaces.Components}.*");

            rule.Check(Production);
        }

        [Test]
        public void GivenTheProductionTypes_WhenInspectingDependencies_ThenNoneDependsOnEntityFrameworkCore()
        {
            IArchRule rule = Types()
                .That()
                .ResideInNamespaceMatching($"{ProductionNamespaces.Root}.*")
                .Should()
                .NotDependOnAnyTypesThat()
                .ResideInNamespaceMatching("Microsoft\\.EntityFrameworkCore.*");

            rule.Check(Production);
        }

        [Test]
        public void GivenTheProductionTypes_WhenInspectingDependencies_ThenNoneDependsOnDapper()
        {
            IArchRule rule = Types()
                .That()
                .ResideInNamespaceMatching($"{ProductionNamespaces.Root}.*")
                .Should()
                .NotDependOnAnyTypesThat()
                .ResideInNamespaceMatching("Dapper.*");

            rule.Check(Production);
        }

        [Test]
        public void GivenTheProductionTypes_WhenInspectingDependencies_ThenNoneDependsOnDynamoDb()
        {
            IArchRule rule = Types()
                .That()
                .ResideInNamespaceMatching($"{ProductionNamespaces.Root}.*")
                .Should()
                .NotDependOnAnyTypesThat()
                .ResideInNamespaceMatching("Amazon\\.DynamoDB.*");

            rule.Check(Production);
        }

        [Test]
        public void GivenTheProductionTypes_WhenInspectingDependencies_ThenNoneDependsOnMediatR()
        {
            IArchRule rule = Types()
                .That()
                .ResideInNamespaceMatching($"{ProductionNamespaces.Root}.*")
                .Should()
                .NotDependOnAnyTypesThat()
                .ResideInNamespaceMatching("MediatR.*");

            rule.Check(Production);
        }

        [Test]
        public void GivenTheProductionTypes_WhenInspectingDependencies_ThenNoneDependsOnNHibernateOrMongoDb()
        {
            IArchRule nhibernateRule = Types()
                .That()
                .ResideInNamespaceMatching($"{ProductionNamespaces.Root}.*")
                .Should()
                .NotDependOnAnyTypesThat()
                .ResideInNamespaceMatching("NHibernate.*");

            IArchRule mongoRule = Types()
                .That()
                .ResideInNamespaceMatching($"{ProductionNamespaces.Root}.*")
                .Should()
                .NotDependOnAnyTypesThat()
                .ResideInNamespaceMatching("MongoDB.*");

            nhibernateRule.Check(Production);
            mongoRule.Check(Production);
        }

        [Test]
        public void GivenTheProductionTypes_WhenInspectingDependencies_ThenNoneDependsOnTestFrameworks()
        {
            IArchRule nunitRule = Types()
                .That()
                .ResideInNamespaceMatching($"{ProductionNamespaces.Root}.*")
                .Should()
                .NotDependOnAnyTypesThat()
                .ResideInNamespaceMatching("NUnit.*");

            IArchRule moqRule = Types()
                .That()
                .ResideInNamespaceMatching($"{ProductionNamespaces.Root}.*")
                .Should()
                .NotDependOnAnyTypesThat()
                .ResideInNamespaceMatching("Moq.*");

            IArchRule bunitRule = Types()
                .That()
                .ResideInNamespaceMatching($"{ProductionNamespaces.Root}.*")
                .Should()
                .NotDependOnAnyTypesThat()
                .ResideInNamespaceMatching("Bunit.*");

            nunitRule.Check(Production);
            moqRule.Check(Production);
            bunitRule.Check(Production);
        }

        [Test]
        public void GivenTheProductionTypes_WhenInspectingDependencies_ThenNoneDependsOnTestProjects()
        {
            IArchRule unitTestRule = Types()
                .That()
                .ResideInNamespaceMatching($"{ProductionNamespaces.Root}.*")
                .Should()
                .NotDependOnAnyTypesThat()
                .ResideInNamespaceMatching(".*\\.UnitTests.*");

            IArchRule integrationTestRule = Types()
                .That()
                .ResideInNamespaceMatching($"{ProductionNamespaces.Root}.*")
                .Should()
                .NotDependOnAnyTypesThat()
                .ResideInNamespaceMatching(".*\\.IntegrationTests.*");

            IArchRule architectureTestRule = Types()
                .That()
                .ResideInNamespaceMatching($"{ProductionNamespaces.Root}.*")
                .Should()
                .NotDependOnAnyTypesThat()
                .ResideInNamespaceMatching(".*\\.ArchitectureTests.*");

            unitTestRule.Check(Production);
            integrationTestRule.Check(Production);
            architectureTestRule.Check(Production);
        }

        [Test]
        public void GivenTheProductionNamespaces_WhenInspectingCycles_ThenTheyFormAnAcyclicGraph()
        {
            RuleAssertions.EnsureSelected(Production.Namespaces.Count(), "production namespaces");

            SliceRule rule = SliceRuleDefinition
                .Slices()
                .Matching($"{ProductionNamespaces.Root}.(*)")
                .Should()
                .BeFreeOfCycles();

            Assert.That(rule.HasNoViolations(Production), Is.True, rule.Description);
        }

        [Test]
        public void GivenTheEndpointNamespace_WhenInspectingTypes_ThenItDeclaresNoRepositoryOrHandler()
        {
            IArchRule repositoryRule = Types()
                .That()
                .ResideInNamespaceMatching($"{ProductionNamespaces.Endpoints}.*")
                .Should()
                .NotHaveNameEndingWith("Repository")
                .AndShould()
                .NotHaveNameEndingWith("CommandHandler")
                .AndShould()
                .NotHaveNameEndingWith("QueryHandler");

            repositoryRule.Check(Production);
        }

        [Test]
        public void GivenTheProductionTypes_WhenInspectingDependencyInversion_ThenNoPersistenceContractExists()
        {
            IArchRule rule = Interfaces()
                .That()
                .ResideInNamespaceMatching($"{ProductionNamespaces.Root}.*")
                .Should()
                .NotHaveNameEndingWith("Repository")
                .AndShould()
                .NotHaveNameEndingWith("UnitOfWork")
                .WithoutRequiringPositiveResults();

            rule.Check(Production);
        }
    }
}
