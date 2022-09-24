using ConjureNextToCaster;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments.DI;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace ConjureNextToCasterTests
{
    public class ProgramTests
    {

        [Theory, MyTestData]
        public void MustHaveScriptFindsFileTest(MockFileSystem fs, IDataDirectoryProvider dataDirectory)
        {
            fs.AddFile(fs.Path.Combine(dataDirectory.Path, "Scripts", "ASnazzyScript.pex"), new("A Snazzy Script"));

            Program._fileSystem = fs;

            Program.MustHaveScript("ASnazzyScript", GameRelease.SkyrimSE, dataDirectory.Path);
        }

        [Theory, MyTestData]
        public void MustHaveScriptFailsToFindFileTest(IDataDirectoryProvider dataDirectory)
        {
            // FIXME find out how to feed a MockFileSystem to Archive
#if WE_CAN_FEED_A_MOCKFILESYSTEM_TO_ARCHIVE
            Assert.Throws<FileNotFoundException>(() => Program.MustHaveScript("ASnazzyScript", GameRelease.SkyrimSE, dataDirectory.Path));
#else
            Assert.ThrowsAny<IOException>(() => Program.MustHaveScript("ASnazzyScript", GameRelease.SkyrimSE, dataDirectory.Path));
#endif
        }
    }
}