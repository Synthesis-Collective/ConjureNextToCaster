
using AutoFixture.Xunit2;
using Mutagen.Bethesda;
using AutoFixture;
using Mutagen.Bethesda.Testing.AutoData;

namespace ConjureNextToCasterTests
{
    public class MyTestDataAttribute : AutoDataAttribute
    {
        public MyTestDataAttribute(
            GameRelease Release = GameRelease.SkyrimSE,
            bool ConfigureMembers = false,
            bool UseMockFileSystem = true) : base(() =>
            {
                var fixture = new Fixture();
                fixture.Customize(new MutagenDefaultCustomization(
                    useMockFileSystem: UseMockFileSystem,
                    configureMembers: ConfigureMembers,
                    release: Release));

                return fixture;
            }) { }
    }
}