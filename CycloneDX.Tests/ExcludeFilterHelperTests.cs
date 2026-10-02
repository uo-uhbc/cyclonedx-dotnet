using System.Collections.Generic;
using System.Linq;
using CycloneDX.Models;
using Xunit;

namespace CycloneDX.Tests
{
    public class ExcludeFilterHelperTests
    {
        private static DotnetDependency Package(string name, string version, bool direct = false,
            params (string Name, string Version)[] dependencies)
        {
            return new DotnetDependency
            {
                Name = name,
                Version = version,
                IsDirectReference = direct,
                DependencyType = DependencyType.Package,
                Dependencies = dependencies.ToDictionary(d => d.Name, d => d.Version)
            };
        }

        /// <summary>
        /// Root -> Middle -> Leaf, plus Stray which nothing references and which is not a direct reference.
        /// </summary>
        private static HashSet<DotnetDependency> Graph()
        {
            return new HashSet<DotnetDependency>
            {
                Package("Root", "1.0.0", direct: true, dependencies: ("Middle", "1.0.0")),
                Package("Middle", "1.0.0", dependencies: ("Leaf", "1.0.0")),
                Package("Leaf", "1.0.0"),
                Package("Stray", "1.0.0")
            };
        }

        [Fact]
        public void FindUnreachableDependencies_ReturnsOnlyPackagesWithNoPathFromADirectReference()
        {
            var unreachable = ExcludeFilterHelper.FindUnreachableDependencies(Graph());

            Assert.Single(unreachable);
            Assert.Equal("Stray", unreachable.Single().Name);
        }

        [Fact]
        public void RemoveOrphanedPackages_RemovesPackagesTheFilterOrphaned()
        {
            var packages = Graph();
            var preExistingOrphans = ExcludeFilterHelper.FindUnreachableDependencies(packages);

            ExcludeFilterHelper.ExcludePackages(packages, "Middle");
            ExcludeFilterHelper.RemoveOrphanedPackages(packages, preExistingOrphans);

            Assert.DoesNotContain(packages, p => p.Name == "Middle");
            Assert.DoesNotContain(packages, p => p.Name == "Leaf");
            Assert.Contains(packages, p => p.Name == "Root");
        }

        [Fact]
        public void RemoveOrphanedPackages_KeepsPackagesThatWereAlreadyUnreachable()
        {
            var packages = Graph();
            var preExistingOrphans = ExcludeFilterHelper.FindUnreachableDependencies(packages);

            ExcludeFilterHelper.ExcludePackages(packages, "Middle");
            ExcludeFilterHelper.RemoveOrphanedPackages(packages, preExistingOrphans);

            Assert.Contains(packages, p => p.Name == "Stray");
        }

        /// <summary>
        /// An already unreachable package named by the filter is still removed.
        /// </summary>
        [Fact]
        public void RemoveOrphanedPackages_StillRemovesAnExplicitlyExcludedOrphan()
        {
            var packages = Graph();
            var preExistingOrphans = ExcludeFilterHelper.FindUnreachableDependencies(packages);

            ExcludeFilterHelper.ExcludePackages(packages, "Stray");
            ExcludeFilterHelper.RemoveOrphanedPackages(packages, preExistingOrphans);

            Assert.DoesNotContain(packages, p => p.Name == "Stray");
            Assert.Equal(3, packages.Count);
        }

        /// <summary>
        /// Without a seed of direct references the whole graph is unreachable. Nothing may be removed,
        /// rather than everything.
        /// </summary>
        [Fact]
        public void RemoveOrphanedPackages_WithoutAnyDirectReference_RemovesNothing()
        {
            var packages = new HashSet<DotnetDependency>
            {
                Package("Root", "1.0.0", dependencies: ("Middle", "1.0.0")),
                Package("Middle", "1.0.0")
            };
            var preExistingOrphans = ExcludeFilterHelper.FindUnreachableDependencies(packages);

            ExcludeFilterHelper.RemoveOrphanedPackages(packages, preExistingOrphans);

            Assert.Equal(2, packages.Count);
        }

        /// <summary>
        /// The parameterless behaviour is unchanged: every unreachable package is removed.
        /// </summary>
        [Fact]
        public void RemoveOrphanedPackages_WithoutPreExistingOrphans_RemovesEveryUnreachablePackage()
        {
            var packages = Graph();

            ExcludeFilterHelper.RemoveOrphanedPackages(packages);

            Assert.DoesNotContain(packages, p => p.Name == "Stray");
            Assert.Equal(3, packages.Count);
        }
    }
}
