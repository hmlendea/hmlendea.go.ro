using System;

using ArchUnitNET.Domain;
using ArchUnitNET.Loader;

using PersonalPresentationWebsite;

namespace PersonalPresentationWebsite.ArchitectureTests.Infrastructure
{
    internal static class ProductionArchitecture
    {
        private static readonly Lazy<Architecture> ArchitectureLazy = new(Load);

        internal static Architecture Instance => ArchitectureLazy.Value;

        private static Architecture Load()
            => new ArchLoader().LoadAssemblies(typeof(Program).Assembly).Build();
    }
}
