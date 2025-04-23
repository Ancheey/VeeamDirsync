using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using VeeamDirsync.Logging;
using VeeamDirsync.Syncing.DataManipulation;

namespace VeeamDirsync.Syncing
{
    internal class DataSyncer
    {

        private readonly string src;
        private readonly string dst;
        private readonly ILogger[] loggers;
        private DataSyncer(string src, string dst, params ILogger[] loggers)
        {
            this.src = src;
            this.dst = dst;
            this.loggers = loggers;
        }

        public async Task<bool> Sync()
        {
            var filesInSrc = GetRelativeFiles(src);
            var filesInDst = GetRelativeFiles(dst);

            List<FileManipulationAction> Actions = [];

            Actions.AddRange(GetDirectoryCreationActions());
            Actions.AddRange(GetFileMoveActions(filesInSrc, filesInDst));
            Actions.AddRange(GetFileDeletionActions(filesInSrc, filesInDst));
            Actions.AddRange(GetFileCopyActions(filesInSrc, filesInDst));
            Actions.AddRange(GetFileUpdateActions(filesInSrc, filesInDst));
            Actions.AddRange(GetDirectoryDeletionActions());

            await ProcessActions(Actions);
            return true;
        }
        /// <summary>
        /// Retrieves a list of actions that will build directories to match the source
        /// </summary>
        private FileManipulationAction[] GetDirectoryCreationActions()
        {
            var SrcDirList = GetRelativeDirectiories(src);
            var DstDirList = GetRelativeDirectiories(dst);
            var DirDiscrepency = SrcDirList.Except(DstDirList).ToList();

            return [.. DirDiscrepency.Select(dir => new FileManipulationAction.CreateDirectory(dst, dir))];
        }
        /// <summary>
        /// Retrieves a list of actions that will delete directories that do not exist in the source
        /// </summary>
        private FileManipulationAction[] GetDirectoryDeletionActions()
        {
            var SrcDirList = GetRelativeDirectiories(src);
            var DstDirList = GetRelativeDirectiories(dst);
            var DeletionDirs = DstDirList.Except(SrcDirList).ToList();

            return [.. DeletionDirs.Select(dir => new FileManipulationAction.DeleteDirectory(dst, dir))];
        }
        /// <summary>
        /// Retrieves a list of actions that will move files within the destination if they have a counterpart in the source, but under a different path
        /// </summary>
        private FileManipulationAction[] GetFileMoveActions(
            List<string> filesInSrc,
            List<string> filesInDst)
        {
            List<FileManipulationAction> Actions = [];

            //Get files missing from destination
            var FilesNotFoundInDst = filesInSrc.Where(x => !filesInDst.Contains(x)).ToList();
            //Get files in destination that do not exist in the source
            var FilesMarkedForDeletion = filesInDst.Except(filesInSrc).ToList();

            var potentialMatches =
                from dstFile in FilesMarkedForDeletion
                from srcFile in FilesNotFoundInDst
                where ShallowCommpareFiles(
                    Path.Combine(dst, dstFile),
                    Path.Combine(src, srcFile)
                )
                select (dstFile, srcFile);

            if (!potentialMatches.Any())
                return [];

            foreach (var (dstFile, srcFile) in potentialMatches)
            {
                if (DeepCompareFiles(
                    Path.Combine(dst, dstFile),
                    Path.Combine(src, srcFile)
                ))
                {
                    Actions.Add(new FileManipulationAction.MoveFile(dst, srcFile, dstFile));
                }
            }
            return [.. Actions];
        }
        /// <summary>
        /// Retrieves a list of actions that will delete files that do not have their counterpart in the source.
        /// </summary>
        private FileManipulationAction[] GetFileDeletionActions(
            List<string> filesInSrc,
            List<string> filesInDst)
        {
            var FilesMarkedForDeletion = filesInDst.Except(filesInSrc).ToList();
            return [.. FilesMarkedForDeletion.Select(file => new FileManipulationAction.DeleteFile(dst, file))];
        }
        /// <summary>
        /// Retrieves a list of actions that will copy files from the source to the destination.
        /// </summary>
        private FileManipulationAction[] GetFileCopyActions(
            List<string> filesInSrc,
            List<string> filesInDst)
        {
            var FilesNotFoundInDst = filesInSrc.Except(filesInDst).ToList();
            return [.. FilesNotFoundInDst.Select(file => new FileManipulationAction.CopyFile(dst, file, src))];
        }
        /// <summary>
        /// Retrieves a list of actions that will update files that are outdated.
        /// </summary>
        private FileManipulationAction[] GetFileUpdateActions(
            List<string> filesInSrc,
            List<string> filesInDst)
        {
            List<FileManipulationAction> Actions = [];
            var FilesToCheckForUpdates = filesInSrc.Where(filesInDst.Contains);
            foreach (var updFile in FilesToCheckForUpdates)
            {
                var srcPath = Path.Combine(src, updFile);
                var dstPath = Path.Combine(dst, updFile);
                if (AreFilesDifferent(srcPath, dstPath))
                {
                    Actions.Add(new FileManipulationAction.CopyFile(dst, updFile, src));
                }
            }
            return [.. Actions];
        }
        private async Task<bool> ProcessActions(IEnumerable<FileManipulationAction> actions)
        {
            await Task.WhenAll(actions.Select(k => k.Execute(loggers)));
            return true;
        }
        private static bool AreFilesDifferent(string srcFile, string dstFile) =>
            !ShallowCommpareFiles(dstFile, srcFile) || !DeepCompareFiles(dstFile, srcFile);
        /// <summary>
        /// Returns only dirs relative to the provided path.
        /// </summary>
        private static List<string> GetRelativeDirectiories(string path)
        {
            if (!Directory.Exists(path))
                throw new DirectoryNotFoundException($"Directory {path} does not exist");
            List<string> dirs = [.. Directory.GetDirectories(path, "*", SearchOption.AllDirectories)];
            return [.. dirs.Select(dir => Path.GetRelativePath(path, dir))];
        }

        /// <summary>
        /// Returns only relative paths to all files in provided directory path
        /// </summary>
        private static List<string> GetRelativeFiles(string path)
        {
            if (!Directory.Exists(path))
                throw new DirectoryNotFoundException($"Directory {path} does not exist");
            List<string> dirs = [.. Directory.GetFiles(path, "*", SearchOption.AllDirectories)];
            return [.. dirs.Select(dir => Path.GetRelativePath(path, dir))];
        }
        /// <summary>
        /// Compares just the outside data to see if a file has the same edit time and size. First step to identifying similar files.
        /// </summary>
        private static bool ShallowCommpareFiles(string fileAPath, string fileBPath)
        {
            if(File.Exists(fileAPath) && File.Exists(fileBPath))
            {
                FileInfo fileInfoA = new(fileAPath);
                FileInfo fileInfoB = new(fileBPath);
                return fileInfoA.Length == fileInfoB.Length && fileInfoA.LastWriteTimeUtc == fileInfoB.LastWriteTimeUtc;
            }
            return false;
        }
        /// <summary>
        /// Compares file hashes. Takes time but it is the best option for checking copies
        /// </summary>
        private static bool DeepCompareFiles(string fileAPath, string fileBPath)
        {
            return GetFileHash(fileAPath) == GetFileHash(fileBPath);
        }
        /// <summary>
        /// Attempts creation of a file hash used for file comparisons comparisons
        /// </summary>
        /// <param name="FilePath"></param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        private static string GetFileHash(string FilePath)
        {
            if (!File.Exists(FilePath))
                throw new FileNotFoundException(@$"File {FilePath} not found. Cannot calculate hash");
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(FilePath);
            byte[] hash = md5.ComputeHash(stream);
            return Convert.ToHexString(hash);
        }

        /// <summary>
        /// Attempt the creation of a Data Syncer for provided source and destination folders.
        /// </summary>
        /// <returns>Whether the creation was succesfull</returns>
        public static bool TryCreate(out DataSyncer? syncer,string src, string dst, params ILogger[] loggers)
        {
            syncer = null;
            if (!Directory.Exists(src))
            {
                foreach (var item in loggers)
                {
                    item.Log($"Directory {src} does not exist.");
                }
                return false;
            }
            else if (!Directory.Exists(dst))
            {
                foreach (var item in loggers)
                {
                    item.Log($"Directory {dst} does not exist.");
                }
                return false;
            }
            syncer = new DataSyncer(src, dst, loggers);
            return true;
        }
    }
}
