/* Filename:    HL7FileResolver.cs
 *
 * Notes:       PowerShell-independent resolution of file paths (directories, wildcards) used by the HL7 Tools GUI application.
 */

namespace HL7Tools
{
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// Expands file paths, directories and wildcards into a list of files.
    /// </summary>
    public static class HL7FileResolver
    {
        /// <summary>
        /// Resolve the paths to a list of files. A directory expands to the files it contains. When expandWildcards
        /// is true, * and ? in the file name are expanded; otherwise the path is treated literally.
        /// </summary>
        public static List<string> ResolveFiles(IEnumerable<string> paths, bool expandWildcards)
        {
            List<string> files = new List<string>();
            foreach (string path in paths)
            {
                if (Directory.Exists(path))
                {
                    files.AddRange(Directory.GetFiles(path));
                }
                else if (expandWildcards && path.IndexOfAny(new char[] { '*', '?' }) >= 0)
                {
                    string directory = Path.GetDirectoryName(path);
                    if (string.IsNullOrEmpty(directory))
                    {
                        directory = ".";
                    }
                    if (Directory.Exists(directory))
                    {
                        files.AddRange(Directory.GetFiles(directory, Path.GetFileName(path)));
                    }
                }
                else
                {
                    files.Add(path);
                }
            }
            return files;
        }
    }
}
