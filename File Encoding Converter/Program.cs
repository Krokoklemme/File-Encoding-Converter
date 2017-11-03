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
using System.Diagnostics;

namespace FileEncodingConverter
{
    static class Program
    {
        /// <summary>
        /// Enumerates all files in the specified directory and its subdirectories recursively
        /// </summary>
        /// <param name="path">Path of the root directory</param>
        /// <returns><see cref="IEnumerable{T}"/> of all files</returns>
        public static IEnumerable<string> GetFiles(string path)
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
        public static Encoding GetFileEncoding(string filename)
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
            return Encoding.UTF8;
        }

        /// <summary>
        /// Reencode all files in the current working directory to UTF-8
        /// </summary>
        /// <param name="verbose">Specifes whether or not this function shall give a verbose ouput</param>
        public static void ConvertToUtf8(bool verbose = false)
        {
            var totalConverted = 0;

            // enumarate all files in all subdirectories
            foreach (var filepath in GetFiles(CWD))
            {
                // If the file has no extension and extensionless files aren't whitelisted, there's no point in continuing
                if (!Path.HasExtension(filepath) && !Properties.Settings.Default.WhitelistExtensionless)
                    continue;

                var ignoreFile = false;

                // determine if the program should ignore the file, based on the file extension
                foreach (var ext in ExcludedExtensions)
                {
                    if (Path.GetExtension(filepath).ToLowerInvariant() == ext.ToLowerInvariant())
                    {
                        ignoreFile = true;
                        break;
                    }
                }

                // if the file ís not being ignored, read all bytes,
                // convert them to UTF-8 and write them to the file again
                if (!ignoreFile)
                {
                    try
                    {
						byte[] t = File.ReadAllBytes(filepath);
						byte[] byteArray = Encoding.Convert(GetFileEncoding(filepath), Encoding.UTF8, t);

                        // TODO: Figure out why this doesn't work
						if (!Properties.Settings.Default.AddBom)
						{
							byte[] bfr = new byte[byteArray.Length - 3];

							for (int i = 3; i < byteArray.Length; i++)
							{
								bfr[i - 3] = byteArray[i];
							}
						}

                        File.WriteAllBytes(
                            filepath,
							byteArray
                        );

                        if (verbose)
                        {
                            Console.WriteLine("Successfully converted " + filepath);
                            totalConverted++;
                        }
                    }
					catch (UnauthorizedAccessException ex)
					{
						Console.Error.WriteLine(ex.Message);
					}
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("Failed to convert " + filepath + ":\n" + ex.Message);
                    }
                }
            }

            if (verbose)
            {
                Console.WriteLine("\nSuccessfully converted a total of " + totalConverted + " files");
            }
        }

        private static void ResetToDefaults()
        {
            // Those are a few extensions that are to be
            // ignored by default. I choose the ones I
            // could think of at that moment.
            ExcludedExtensions = new List<string>
            {
                ".exe",
                ".dll",
                ".com",
                ".db",
                ".sys",
                ".png",
                ".jpg",
                ".jpeg",
                ".gif",
                ".pdb",
                ".mp3",
                ".mp4",
                ".mov",
                ".ogg",
                ".wav",
                ".webp",
                ".obj",
                ".bmp",
                ".fbx",
                ".rar",
                ".zip",
                ".7z",
                ".jar"
            };

            Properties.Settings.Default.WhitelistExtensionless = false;
            Properties.Settings.Default.Save();

            File.WriteAllLines(IgnoreFile, ExcludedExtensions);
        }

        // convenience variables
        internal static List<string> ExcludedExtensions { get; set; }
        internal static string IgnoreFile => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ignore");
        internal static string CWD => Directory.GetCurrentDirectory();


        public static void PrintFormatted(this Exception exception)
        {
            Console.Error.WriteLine($"Exception thrown in {exception.Source} (in {exception.TargetSite.Name}): {exception.Message}\n{exception.StackTrace}\n\nHRESULT is {exception.HResult}\nHelp-link: {exception.HelpLink}");
        }

        static void Main(string[] args)
        {
            if (!File.Exists(IgnoreFile))
            {
                ResetToDefaults();
            }
            
            ExcludedExtensions = new List<string>();
            // Load all currently disabled file-extensions
            try
            {
                ExcludedExtensions.AddRange(File.ReadAllLines(IgnoreFile));
            }
            catch (Exception ex)
            {
                ex.PrintFormatted();
            }

            // If we have cmd-line args, that indidcates that we want to do something with the extension "database"
			// EDIT: a couple of weeks later, I realized that I should've used a library for this...
            if (args.Length != 0)
            {
				if (args[0] == "--help" || args[0] == "-h")
                {
                    Console.WriteLine("\tFile Encoding Converter\n\nTurns the encoding of all files into UTF-8 and\nexcludes files with a globally specified extensions\n" +
						"\n\t--help\t\t-h\t\tShows this help" +
						"\n\t--add\t\t-a\t\tAdds an extension to the global list (without the dot)" +
						"\n\t--remove\t-r\t\tRemoves an extension from the global list" +
						"\n\t--list\t\t-l\t\tLists all currently disabled extensions" +
						"\n\t--show\t\t-s   [all]\tShows all unrecognized fileformats. If it's called with 'all', it'll list all fileformats found" +
						"\n\t--verbose\t-v\t\tRuns Encoder in verbose mode (prints messages while working)" +
						"\n\t--whitelist-extensionless\tRemoves extensionless files from the blacklist" +
						"\n\t--blacklist-extensionless\tAdds extensionless files to the blacklist" +
						"\n\t--bom\t\t\t\tAdds the UTF-8 BOM to the file (default)" +
						"\n\t--no-bom\t\t\tSuppresses addition of BOM to the file" +
						"\n\t--info\t-i   [extension...]\tLooks up information on the specified filetypes" +
						"\n\t--default\t\t\tResets the configuration to a default value" +
						"\n\nCopyright (c) 2017 Henning Hoppe");
                }

                else if (args[0] == "--add" || args[0] == "-a")
                {
                    var extensionsToAdd = new List<string>();

                    // no foreach here, since we don't want to add the command to the ignore-list
                    for (var i = 1; i < args.Length; i++)
                    {
                        var alreadyListed = false;

                        // checks if the requested extension is already being ignored (eliminating redundancy)
                        foreach (var ext in ExcludedExtensions)
                        {
                            if (args[i].ToLowerInvariant() == ext.ToLowerInvariant())
                            {
                                alreadyListed = true;
                                break;
                            }
                        }

                        // if it's not listed, add it
                        if (!alreadyListed)
                        {
                            extensionsToAdd.Add("." + args[i].ToLowerInvariant());
                        }
                    }

                    // actually update the list
                    ExcludedExtensions.AddRange(extensionsToAdd);
                    File.WriteAllLines(IgnoreFile, ExcludedExtensions, Encoding.UTF8);
                }

                else if (args[0] == "--remove" || args[0] == "-r")
                {
                    // remove all listed extensions. It'll ignore non-existent values
                    for (var i = 1; i < args.Length; i++)
                    {
                        ExcludedExtensions.Remove("." + args[i].ToLowerInvariant());
                    }

                    // update the list
                    File.WriteAllLines(IgnoreFile, ExcludedExtensions, Encoding.UTF8);
                }

                else if (args[0] == "--list" || args[0] == "-l")
                {
                    // I just made this to conform to MS complete ruleset for managed code. Kinda overkill though tbh
                    Console.WriteLine("Globally ignored file-extensions");

                    foreach (var ext in ExcludedExtensions)
                    {
                        Console.WriteLine(ext);
                    }
                }

                else if (args[0] == "--show" || args[0] == "-s")
                {
                    var shownExtensions = new List<string>();
                    var showAll = false;

                    foreach (var ext in GetFiles(CWD))
                    {
                        var extension = Path.GetExtension(ext);

                        // Removing any previous occurances to avoid duplicates
                        shownExtensions.RemoveAll((i) => i.ToLowerInvariant() == extension.ToLowerInvariant());
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
                        foreach (var ext in ExcludedExtensions)
                        {
                            shownExtensions.RemoveAll((i) => i.ToLowerInvariant() == ext.ToLowerInvariant());
                        }
                    }

                    Console.WriteLine("Found the following " + (showAll ? "(potentially blacklisted)" : "") + " fileformats: ");
                    foreach (var ext in shownExtensions)
                    {
                        Console.WriteLine(ext);
                    }
                }

                else if (args[0] == "--verbose" || args[0] == "-v")
                {
                    ConvertToUtf8(true);
                }

                else if (args[0] == "--whitelist-extensionless")
                {
                    Properties.Settings.Default.WhitelistExtensionless = true;
                    Properties.Settings.Default.Save();
                }
				
                else if (args[0] == "--blacklist-extensionless")
                {
                    Properties.Settings.Default.WhitelistExtensionless = false;
                    Properties.Settings.Default.Save();
                }

				else if (args[0] == "--bom")
				{
					Properties.Settings.Default.AddBom = true;
					Properties.Settings.Default.Save();
				}

				else if (args[0] == "--no-bom")
				{
					Properties.Settings.Default.AddBom = false;
					Properties.Settings.Default.Save();
				}
				
                else if (args[0] == "--info" || args[0] == "-i")
                {
                    if (args.Length >= 2)
                    {
                        for (var i = 1; i < args.Length; i++)
                        {
							Process.Start(new ProcessStartInfo
							{
								FileName = @"https://fileinfo.com/extension/" + args[i],
								CreateNoWindow = true,
								UseShellExecute = true
							}).Dispose();
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine("You must specify a file to look up");
                    }
                }

                else if (args[0] == "--default")
                {
                    Console.WriteLine("Are you sure you want to reset to defaults? This operation is not reversable. Type 'Yes' and press Enter to confirm");
                    if (Console.ReadLine().ToLowerInvariant() == "Yes".ToLowerInvariant())
                    {
                        ResetToDefaults();
                        Console.WriteLine("Extension-Database and settings have been reset to default");
                    }
                    else
                    {
                        Console.WriteLine("Unrecognized input. Encoder will terminate without making any changes");
                    }
                }

                else
                {
                    Console.Error.WriteLine("Unrecognized option '" + args[0] + "'\nTry --help or -h for usage instructions");
                }
            }

            // If we're not modifying the extension database (don't provide cmd args), that means that we simply want to run the program
            else
            {
                ConvertToUtf8();
            }
        }
    }
}
