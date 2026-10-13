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
        /// is true, * and ? in the path are expanded; otherwise the path is treated literally.
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
                    string root = Path.GetPathRoot(path);
                    string remainder = path.Substring(root.Length);
                    string[] segments = remainder.Split(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, System.StringSplitOptions.RemoveEmptyEntries);
                    List<string> directories = new List<string> { string.IsNullOrEmpty(root) ? "." : root };
                    for (int i = 0; i < segments.Length - 1; i++)
                    {
                        List<string> matchingDirectories = new List<string>();
                        foreach (string directory in directories)
                        {
                            if (segments[i].IndexOfAny(new char[] { '*', '?' }) >= 0)
                            {
                                if (Directory.Exists(directory))
                                {
                                    matchingDirectories.AddRange(Directory.GetDirectories(directory, segments[i]));
                                }
                            }
                            else
                            {
                                string childDirectory = Path.Combine(directory, segments[i]);
                                if (Directory.Exists(childDirectory))
                                {
                                    matchingDirectories.Add(childDirectory);
                                }
                            }
                        }
                        directories = matchingDirectories;
                    }

                    foreach (string directory in directories)
                    {
                        if (Directory.Exists(directory))
                        {
                            files.AddRange(Directory.GetFiles(directory, segments[segments.Length - 1]));
                        }
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
