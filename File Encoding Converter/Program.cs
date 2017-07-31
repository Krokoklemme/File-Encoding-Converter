// Copyright(c) 2017 Henning Hoppe
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FileEncodingConverter
{
    class Program
    {
        /// <summary>
        /// Enumerates all files in the specified directory and its subdirectories recursively
        /// </summary>
        /// <param name="path">Path of the root directory</param>
        /// <returns><see cref="IEnumerable{T}"/> of all files</returns>
        static IEnumerable<string> GetFiles(string path)
        {
            Queue<string> queue = new Queue<string>();
            queue.Enqueue(path);

            // imperatively get all subdirectories and yield-return their individual files
            while (queue.Count > 0)
            {
                // dequeue the current top-directory
                path = queue.Dequeue();

                try
                {
                    // Add all subdirectories to the Queue for later inspection
                    foreach (string subDir in Directory.GetDirectories(path))
                    {
                        queue.Enqueue(subDir);
                    }
                }
                // cause ya never know
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                }

                string[] files = null;
                try
                {
                    // Get the files
                    files = Directory.GetFiles(path);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                }

                // if the files could successfully be enumerated, we'll yield them
                if (files != null)
                {
                    foreach (var str in files)
                    {
                        yield return str;
                    }
                }
            }
        }

        /// <summary>
        /// Detects the text encoding used by the file with the specified name
        /// </summary>
        /// <param name="filename">Name of the file to inspect</param>
        /// <returns><see cref="Encoding"/> of the specified file</returns>
        public static Encoding GetEncoding(string filename)
        {
            // Allocating room for and reading the BOM
            var bom = new byte[4];
            using (var file = new FileStream(filename, FileMode.Open, FileAccess.Read))
            {
                file.Read(bom, 0, 4);
            }

            // Analyzing the BOM
            if (bom[0] == 0x2b && bom[1] == 0x2f && bom[2] == 0x76) return Encoding.UTF7;
            if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf) return Encoding.UTF8;
            if (bom[0] == 0xff && bom[1] == 0xfe) return Encoding.Unicode; //UTF-16LE
            if (bom[0] == 0xfe && bom[1] == 0xff) return Encoding.BigEndianUnicode; //UTF-16BE
            if (bom[0] == 0 && bom[1] == 0 && bom[2] == 0xfe && bom[3] == 0xff) return Encoding.UTF32;
            return Encoding.ASCII;
        }

        // convenience variables
        protected internal static string IgnoreFile => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ignore");
        protected internal static string CWD => Directory.GetCurrentDirectory();

        static void Main(string[] args)
        {
            var excludedExtensions = new List<string>();

            // Load all currently disabled file-extensions
            try
            {
                excludedExtensions.AddRange(File.ReadAllLines(IgnoreFile));
            }
            catch (FileNotFoundException)
            {
                File.Create(IgnoreFile).Dispose();
            }

            // If we have cmd-line args, that indidcates that we want to do something with the extension "database"
            if (args.Length != 0)
            {
                #region --help
                if (args[0] == "--help" || args[0] == "-h")
                {
                    Console.WriteLine("\tFile Encoding Converter\n\nTurns the encoding of all files into UTF-8 and\nexcludes files with a globally specified extensions\n\n\t--help\t\t-h\tShows this help\n\t--add\t\t-a\tAdds an extension to the global list (without the dot)\n\t--remove\t-r\tRemoves an extension from the global list\n\t--list\t\t-l\tLists all currently disabled extensions\n\t--show\t\t-s - [all]\tShows all unrecognized fileformats. If it's called with 'all', it'll list all fileformats found\n\nCopyright (c) 2017 Henning Hoppe");
                }
                #endregion

                #region --add
                else if (args[0] == "--add" || args[0] == "-a")
                {
                    var extensionsToAdd = new List<string>();

                    // no foreach here, since we don't want to add the command to the ignore-list
                    for (var i = 1; i < args.Length; i++)
                    {
                        var alreadyListed = false;

                        // checks if the requested extension is already being ignored (eliminating redundancy)
                        foreach (var ext in excludedExtensions)
                        {
                            if (args[i] == ext)
                            {
                                alreadyListed = true;
                                break;
                            }
                        }

                        // if it's not listed, add it
                        if (!alreadyListed)
                        {
                            extensionsToAdd.Add("." + args[i]);
                        }
                    }

                    // actually update the list
                    excludedExtensions.AddRange(extensionsToAdd);
                    File.WriteAllLines(IgnoreFile, excludedExtensions, Encoding.UTF8);
                }
                #endregion

                #region --remove
                else if (args[0] == "--remove" || args[0] == "-r")
                {
                    // remove all listed extensions. It'll ignore non-existent values
                    for (var i = 1; i < args.Length; i++)
                    {
                        excludedExtensions.Remove("." + args[i]);
                    }

                    // update the list
                    File.WriteAllLines(IgnoreFile, excludedExtensions, Encoding.UTF8);
                }
                #endregion

                #region --list
                else if (args[0] == "--list" || args[0] == "-l")
                {
                    // I just made this to conform to MS complete ruleset for managed code. Kinda overkill though tbh
                    Console.WriteLine("Globally ignored file-extensions");

                    foreach (var ext in excludedExtensions)
                    {
                        Console.WriteLine(ext);
                    }
                }
                #endregion

                #region --show
                else if (args[0] == "--show" || args[0] == "-s")
                {
                    var shownExtensions = new List<string>();
                    var showAll = false;

                    foreach (var ext in GetFiles(CWD))
                    {
                        var extension = Path.GetExtension(ext);

                        // Removing any previous occurances, as to avoid duplicates
                        shownExtensions.RemoveAll((i) => i == extension);
                        shownExtensions.Add(extension);
                    }

                    if (args.Length >= 2)
                    {
                        if (args[1] == "all")
                        {
                            showAll = true;
                        }
                    }

                    // If the user specified to not show all extensions, remove the blacklisted ones
                    if (!showAll)
                    {
                        foreach (var ext in excludedExtensions)
                        {
                            shownExtensions.RemoveAll((i) => i == ext);
                        }
                    }

                    Console.WriteLine("Found the following " + (showAll ? "(potentially blacklisted)" : "") + " fileformats: ");
                    foreach (var ext in shownExtensions)
                    {
                        Console.WriteLine(ext);
                    }
                }
                #endregion

                #region Unknown
                else
                {
                    Console.Error.WriteLine("Unrecognized option '" + args[0] + "'\nTry --help or -h for usage instructions");
                }
                #endregion
            }

            // If we're not modifying the extension database (don't provide cmd args), that means that we simply want to run the program
            else
            {
                // enumarate all files in all subdirectories
                foreach (var filepath in GetFiles(CWD))
                {
                    var ignoreFile = false;

                    // determine if the program should ignore the file, based on the file extension
                    foreach (var ext in excludedExtensions)
                    {
                        if (Path.GetExtension(filepath) == ext)
                        {
                            ignoreFile = true;
                            break;
                        }
                    }

                    // if the file ís not being ignored, read all bytes,
                    // convert them to UTF-8 and write them to the file again
                    if (!ignoreFile)
                    {
                        File.WriteAllBytes(
                            filepath,
                            Encoding.Convert(
                                GetEncoding(filepath),
                                Encoding.UTF8,
                                File.ReadAllBytes(filepath)
                            )
                        );
                    }
                }
            }
        }
    }
}