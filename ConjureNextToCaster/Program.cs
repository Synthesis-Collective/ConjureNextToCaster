using Mutagen.Bethesda;
using Mutagen.Bethesda.Archives;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using System.IO.Abstractions;

namespace ConjureNextToCaster
{
    public class Program
    {
        private static Lazy<Settings> Settings = null!;

        internal static IFileSystem? _fileSystem = null;

        private static IFileSystem FileSystem
        {
            get
            {
                _fileSystem ??= new FileSystem();
                return _fileSystem;
            }
        }

        private static IPath Path => FileSystem.Path;
        private static IFile File => FileSystem.File;

        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .AddRunnabilityCheck(RunnabilityCheck)
                .SetAutogeneratedSettings(
                    nickname: "Settings",
                    path: "settings.json",
                    out Settings
                )
                .SetTypicalOpen(GameRelease.SkyrimSE, "Test.esp")
                .Run(args);
        }

        internal static void RunnabilityCheck(IRunnabilityState state)
        {
            switch (state.GameRelease)
            {
                case GameRelease.SkyrimSE:
                case GameRelease.SkyrimVR:
                case GameRelease.EnderalSE: // I guess it will work here, too?
                    MustHaveScript("DankAddSecondSpell", state.GameRelease, state.DataFolderPath);
                    break;
                case GameRelease.SkyrimLE:
                case GameRelease.EnderalLE:
                    // TODO See if this exists/works for LE; if not, port it, I guess?
                    MustHaveScript("DankAddSecondSpell", state.GameRelease, state.DataFolderPath);
                    break;
                case GameRelease.Fallout4:
                case GameRelease.Oblivion:
                default:
                    throw new NotImplementedException();
            }
        }

        internal static void MustHaveScript(string scriptName, GameRelease gameRelease, string dataFolderPath)
        {
            var scriptPath = Path.Combine("Scripts", scriptName + ".pex");
            var pathToFileOnDisk = Path.Combine(dataFolderPath, scriptPath);

            if (File.Exists(pathToFileOnDisk)) return;

            foreach (var filePath in Archive.GetApplicableArchivePaths(gameRelease, dataFolderPath))
                foreach (var archiveFile in Archive.CreateReader(gameRelease, filePath).Files)
                    if (archiveFile.Path.Equals(scriptPath, StringComparison.OrdinalIgnoreCase))
                        return;

            throw new FileNotFoundException(message: null, fileName: pathToFileOnDisk);
        }

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state) => new ConjureNextToCasterSkyrim(LoadOrder: state.LoadOrder, state.PatchMod, state.LinkCache, Settings).Run();
    }
}
