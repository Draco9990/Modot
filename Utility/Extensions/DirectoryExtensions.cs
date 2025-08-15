using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using JetBrains.Annotations;

namespace Godot.Utility.Extensions
{
    /// <summary>
    /// Contains extension methods for <see cref="DirAccess"/>.
    /// </summary>
    [PublicAPI]
    public static class DirectoryExtensions
    {
        /// <summary>
        /// Copies all files from the directory at <paramref name="from"/> to the directory at <paramref name="to"/>.
        /// </summary>
        /// <param name="directory">The <see cref="DirAccess"/> to use when copying files.</param>
        /// <param name="from">The source directory path. It can be an absolute path, or relative to <paramref name="directory"/>.</param>
        /// <param name="to">The destination directory path. It can be an absolute path, or relative to <paramref name="directory"/>.</param>
        /// <param name="recursive">Whether the contents should be copied recursively (i.e. copy files inside subdirectories and so on) or not.</param>
        /// <returns>An array of the paths of all files that were copied from <paramref name="from"/> to <paramref name="to"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string[] CopyContents(this DirAccess directory, string from, string to, bool recursive = false)
        {
            return directory.CopyContentsLazy(from, to, recursive).ToArray();
        }
        
        /// <summary>
        /// Returns the complete file paths of all files inside <paramref name="directory"/>.
        /// </summary>
        /// <param name="directory">The <see cref="DirAccess"/> to search in.</param>
        /// <param name="recursive">Whether the search should be conducted recursively (return paths of files inside <paramref name="directory"/>'s subdirectories and so on) or not.</param>
        /// <returns>An array of the paths of all files inside <paramref name="directory"/>.</returns>
        [MustUseReturnValue]
        public static string[] GetFiles(this DirAccess directory, bool recursive = false, params string[] fileExtensions)
        {
            string[] files = recursive
                ? directory.GetElementsNonRecursive(true).ToArray()
                : directory.GetElementsNonRecursive(true).ToArray();

            return fileExtensions.Any()
                ? Array.FindAll(files, file => fileExtensions.Any(file.EndsWith))
                : files;
        }

        /// <summary>
        /// Returns the complete directory paths of all directories inside <paramref name="directory"/>.
        /// </summary>
        /// <param name="directory">The <see cref="DirAccess"/> to search in.</param>
        /// <param name="recursive">Whether the search should be conducted recursively (return paths of directories inside <paramref name="directory"/>'s subdirectories and so on) or not.</param>
        /// <returns>An array of the paths of all files inside <paramref name="directory"/>.</returns>
        [MustUseReturnValue]
        public static string[] GetDirectories(this DirAccess dirAccess, bool recursive = false)
        {
            return recursive
                ? dirAccess
                    .GetElementsNonRecursive(false)
                    .SelectMany(path =>
                    {
                        DirAccess? recursiveDirAccess = DirAccess.Open(path);
                        if (recursiveDirAccess == null)
                        {
                            throw new InvalidOperationException($"Cannot open directory: {path}");
                        }

                        return recursiveDirAccess.GetDirectories(true).Prepend(path);
                    })
                    .ToArray()
                : dirAccess
                    .GetElementsNonRecursive(false)
                    .ToArray();
        }


        private static IEnumerable<string> GetElementsNonRecursive(this DirAccess directory, bool trueIfFiles)
        {
            directory.ListDirBegin();
            while (true)
            {
                string next = directory.GetNext();
                if (next is "")
                {
                    yield break;
                }
                // Continue if the current element is a file or directory depending on which one is being queried
                if (directory.CurrentIsDir() == trueIfFiles)
                {
                    continue;
                }
                string current = directory.GetCurrentDir();
                yield return current.EndsWith("/") ? $"{current}{next}" : $"{current}/{next}";
            }
        }
        
        private static IEnumerable<string> CopyContentsLazy(this DirAccess directory, string from, string to, bool recursive = false)
        {
            directory = DirAccess.Open(from);
            if (directory == null)
            {
                throw new InvalidOperationException($"Cannot open directory: {from}");
            }

            DirAccess? dirAccessTo = DirAccess.Open(to);
            dirAccessTo.MakeDirRecursive(to);

            Regex fromReplacement = new(Regex.Escape(from));

            directory.ListDirBegin();
            string fromFile;
            while ((fromFile = directory.GetNext()) != "")
            {
                if (directory.CurrentIsDir())
                {
                    continue; // Skip directories
                }

                string toFile = fromReplacement.Replace(fromFile, to, 1);
                CopyFile(fromFile, toFile); // Manual file copy
                yield return toFile;
            }
            directory.ListDirEnd();

            if (!recursive)
            {
                yield break;
            }

            // Copy files recursively
            directory.ListDirBegin();
            while ((fromFile = directory.GetNext()) != "")
            {
                if (!directory.CurrentIsDir())
                {
                    continue;
                }

                string fromSubDir = from + "/" + fromFile;
                string toSubDir = to + "/" + fromFile;
                DirAccess? subDirAccess = DirAccess.Open(fromSubDir);

                foreach (string file in subDirAccess.CopyContentsLazy(fromSubDir, toSubDir, true))
                {
                    yield return file;
                }
            }
            directory.ListDirEnd();
        }

        private static void CopyFile(string fromFile, string toFile)
        {
            using FileAccess? srcFile = FileAccess.Open(fromFile, FileAccess.ModeFlags.Read);
            using FileAccess? destFile = FileAccess.Open(toFile, FileAccess.ModeFlags.Write);

            byte[] buffer = srcFile.GetBuffer((int)srcFile.GetLength());
            destFile.StoreBuffer(buffer);
        }

    }
}