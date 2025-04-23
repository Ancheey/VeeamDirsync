using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamDirsync.Logging;

namespace VeeamDirsync.Syncing.DataManipulation
{
    internal abstract class FileManipulationAction(string preRelativePath, string relativePath)
    {
        //Combining PreRelative and relative gives a full path
        public string PreRelativePath { get; init; } = preRelativePath;
        public string RelativePath { get; init; } = relativePath;

        protected string GetFullPath() => Path.Combine(PreRelativePath, RelativePath);
        protected string GetFullPath(string customSource) => Path.Combine(customSource, RelativePath);
        public abstract Task<bool> Execute(params ILogger[] loggers);

        public sealed class CreateDirectory(string preRelativePath, string relativePath) : FileManipulationAction(preRelativePath, relativePath)
        {
            public override async Task<bool> Execute(params ILogger[] loggers)
            {
                Directory.CreateDirectory(GetFullPath());
                await loggers.Log($@"Created missing directory: {GetFullPath()}");
                return true;
            }
        }
        public sealed class DeleteDirectory(string preRelativePath, string relativePath) : FileManipulationAction(preRelativePath, relativePath)
        {
            public override async Task<bool> Execute(params ILogger[] loggers)
            {
                if (!Directory.Exists(GetFullPath()))
                    return true; //Doesn't exist anymore, no action needed
                Directory.Delete(GetFullPath(), recursive: true);
                await loggers.Log($@"Removed directory and its contents: {GetFullPath()}");
                return true;
            }
        }
        public sealed class MoveFile(string targetDirectory, string filename, string oldFilePath) : FileManipulationAction(targetDirectory, filename)
        {
            private string GetSourcePath() => Path.Combine(PreRelativePath, oldFilePath);
            public override async Task<bool> Execute(params ILogger[] loggers)
            {
                if (!File.Exists(GetSourcePath()))
                {
                    await loggers.Log($@"File does not exist: {GetSourcePath()}");
                    return false;
                }
                try
                {
                    File.Move(GetSourcePath(), GetFullPath());
                    await loggers.Log($@"Moved file: {GetSourcePath()} to {GetFullPath()}");
                    return true;
                }
                catch (Exception e)
                {
                    await loggers.Log($@"Couldn't move file: {GetSourcePath()}: {e}");
                    return false;
                }
            }
        }
        public sealed class DeleteFile(string preRelativePath, string relativePath) : FileManipulationAction(preRelativePath, relativePath)
        {
            public override async Task<bool> Execute(params ILogger[] loggers)
            {
                if (!File.Exists(GetFullPath()))
                {
                    await loggers.Log($@"File does not exist: {GetFullPath()}");
                    return false;
                }
                File.Delete(GetFullPath());
                await loggers.Log($@"Deleted file: {GetFullPath()}");
                return true;
            }
        }
        public sealed class CopyFile(string preRelativePath, string relativePath, string sourcePreRelativePath) : FileManipulationAction(preRelativePath, relativePath)
        {
            public override async Task<bool> Execute(params ILogger[] loggers)
            {
                if (!File.Exists(GetFullPath(sourcePreRelativePath)))
                {
                    await loggers.Log($@"File does not exist: {GetFullPath(sourcePreRelativePath)}");
                    return false;
                }

                File.Copy(GetFullPath(sourcePreRelativePath), GetFullPath(), true);
                await loggers.Log($@"Copied file: {GetFullPath()}");
                return true;
                
            }
        }
    }
}
