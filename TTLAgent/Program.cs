using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualBasic.FileIO;
using SearchOption = System.IO.SearchOption;

namespace TTLAgent
{
    class Program
    {
        private static bool _verbose;
        private static bool _quiet;
        private static bool _local;//direct database connection, no web service
        private static bool _testMode;
        private static bool _noquest;
        private static bool _rmdir;
        private static bool _subs;//Delete files in subdirectories
        private static bool _noEmpty;//Delete empty directories
        private static KillMethod _km;
        private static int _days;
        private static int _freeSpace;
        private static string[] _masks;
        private static bool _sdwarned;//Used to not show the sdelete warning twice
        private static bool _noEventLog;
        private static bool _ede;
        private static int _fileCount;
        private static int _dirCount;
        private static byte[] _bytez;//Used to overwrite files with bfg
        private static int _keepalive = 0;//How many files to keep alive

        //private const int _maxFiles = 1000;//Max files to delete at once
        //private const int _maxDirs = 1000;//Max directories to delete at once
        private const int _maxPath = 260;//Max path length

        [STAThread()]
        static void Main(string[] args)
        {
            var thisVersion = Assembly.GetExecutingAssembly().GetName().Version;
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.White;
            const string clientName = "Time To Leave";
            Console.WriteLine(clientName);
            Console.WriteLine(new string('-', clientName.Length));
            Console.WriteLine(Strings.Copyright);
            Console.ForegroundColor = oldColor;
            //Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("version {0}.{1}.{2}", thisVersion.Major, thisVersion.Minor, thisVersion.Revision);
            Console.WriteLine();
            //Prepare event logger
            try
            {
                if (!EventLogger.PrepareEventLog("TTL"))
                {
                    PrintConsole("Could not prepare windows event log.", MessageType.Error);
                    _noEventLog = true;
                    //Environment.Exit(2);
                }
            }
            catch (Exception ex)
            {
                PrintConsole(ex.Message, MessageType.Error);
                Environment.Exit(2);
            }
            if (args.Length == 0) ShowHelpAndExit();

            if (args.Contains("/?") || args.Contains("--help") || args.Contains("-?")) ShowHelpAndExit();
            //Load command line arguments
            _verbose = args.Contains("/v");
            _testMode = args.Contains("/test");
            if (_testMode)
            {
                PrintConsole("Test mode: ON, no files or directories will be removed.", MessageType.Warning, true);
            }
            _quiet = args.Contains("/q");
            if (_verbose && _quiet)
            {
                _quiet = false;
                PrintConsole(Strings.SilentVerboseConflict);
            }
            if (args.Contains("/log"))
            {
                if (EventLogger.PrepareEventLog()) Console.WriteLine(Strings.Event_Log_prepared);
                else PrintConsole(Strings.EventLogPrepFailed, MessageType.Error, true);
                return;
            }
            if (args.Contains("/y")) _noquest = true;
            if (args.Contains("/rmdir")) _rmdir = true;
            if (args.Contains("/subs")) _subs = true;
            if (args.Contains("/noempty")) _noEmpty = true;
            _ede = (args.Contains("/ede"));
            _km = KillMethod.Simple;
            if (args.Contains("/recycle")) _km = KillMethod.Recycle;
            if (args.Contains("/secure")) _km = KillMethod.Secure;
            if (args.Contains("/bfg"))
            {
                _km = KillMethod.BFG;
                _bytez = Encoding.ASCII.GetBytes(new string((char)0, 32768));
            }

            
            bool daysSet = false;
            foreach (var arg in args)
            {
                if (!arg.StartsWith("/") || !arg.Contains(":") || arg.Length < 6) continue;
                switch (arg.Substring(1, 3).ToLower())
                {
                    case "day"://days
                        _days = ParseInt32(GetValue(arg));
                        daysSet = true;
                        continue;
                    case "fre"://free space
                        _freeSpace = ParseInt32(GetValue(arg));
                        continue;
                    case "mas"://file mask
                        _masks = GetValue(arg).Split(',');
                        continue;
                    case "kee"://keep - keep this many files alive
                        _keepalive = ParseInt32(GetValue(arg));
                        Program.PrintConsole($"Will only delete {_keepalive} old files per directory.");
                        continue;
                }
                
            }
            if (daysSet == false)
            {
                PrintConsole("You did not set the number of days with /days parameter. Assuming the number is 30.");
                _days = 30;
            }
            if (_masks == null)
            {
                PrintConsole("No file masks were given, assuming all files.");
                _masks = new string[1];
                _masks[0] = "*";
            }

            if (args.Contains("/emptyrecyclebin"))
            {
                PrintConsole("Emptying old items from Recycle Bin ...");
                RecycleBinHelper.DeleteOldFiles(DateTime.Now.AddDays(_days * -1));
                return;
            }
            //Process the file or directory
            string targetName = args[0];
            var isDir = IsDirectory(targetName);
            if (!isDir.HasValue)
            {
                PrintConsole($"The path {targetName} is invalid!", MessageType.Error, true);
                return;
            }
            if (args.Contains("/compress"))
            {
                _km = KillMethod.Compress;
                //Check if target is NTFS.
                if (!Common.IsNTFS(targetName.Substring(0, 1)))
                {
                    PrintConsole("You can't compress non-NTFS or network drives.", MessageType.Warning, true);
                    return;
                }
            }
            if (isDir.Value)
            {
                PrintConsole($"Processing directory {targetName} ...", MessageType.Info, true);
                ProcessDirectory(targetName);
            }
            else ProcessFile(targetName);
            PrintConsole($"{_dirCount} directories and {_fileCount} files processed.", MessageType.Success, true);
        }

        private static void ProcessFile(string path)
        {
            if (path.Length > _maxPath) return;
            bool result;
            var fi = new FileInfo(path);
            if (_days > 0)
            {//Check if this file is too old
                var fileDate = fi.CreationTime;
                if (fi.LastWriteTime > fileDate) fileDate = fi.LastWriteTime;
                if ((DateTime.Now - fileDate).TotalDays > _days)
                {
                    result = DeleteFile(path);
                    if (!result &! _quiet) PrintConsole("Could not process file " + path, MessageType.Warning);
                }
                return;
            }
            //If days is zero, then just cleanup this file
            result = DeleteFile(path);
            if (!result & !_quiet) PrintConsole("Could not process file " + path, MessageType.Warning);
        }

        private static bool DeleteFile(string path)
        {
            if (_testMode)
            {
                PrintConsole("TEST MODE: Would delete file " + path);
                _fileCount++;
                return true;
            }
            try
            {
                FixFilePermissions(path);
                switch (_km)
                {
                    case KillMethod.Simple:
                        File.Delete(path);
                        break;
                    case KillMethod.Recycle:
                        FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                        break;
                    case KillMethod.Secure:

                        string appPath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
                        if (string.IsNullOrEmpty(appPath))
                        {
                            PrintConsole("Could not find the base directory of this application. Please try again or contact support.", MessageType.Error);
                            return false;
                        }
                        var sdelete = Path.Combine(appPath, "sdelete.exe");
                        if (_sdwarned || !File.Exists(sdelete))
                        {
                            if (!_sdwarned)
                            {
                                PrintConsole(
                                    "To delete files securely, you will need sdelete.exe installed in the same directory as this application. You can download it at https://docs.microsoft.com/en-us/sysinternals/downloads/sdelete",
                                    MessageType.Warning);
                                _sdwarned = true;
                                //Delete file normally
                                File.Delete(path);
                                break;
                            }
                        }
                        //Execute sdelete with parameter
                        ProcessWindowStyle ws = ProcessWindowStyle.Minimized;
                        if (_quiet) ws = ProcessWindowStyle.Hidden;
                        ExecuteProcess(sdelete, RightFileName(path), true, ws);
                        break;
                    case KillMethod.BFG:
                        //1. Rename
                        string newFile = Guid.NewGuid().ToString("N");
                        string newPath = Path.Combine(Path.GetDirectoryName(path), newFile);
                        File.Move(path, newPath);
                        //2. Overwrite
                        File.WriteAllBytes(newPath, _bytez);
                        //3. Delete
                        File.Delete(newPath);
                        break;
                    case KillMethod.Compress:
                        if (Common.IsCompressed(path)) break;
                        Command cmd = new Command
                        {
                            Line = "compact.exe",
                            Wait = false
                        };
                        var fName = path;
                        if (path.Contains(" ")) fName = "\"" + path + "\"";
                        cmd.Arguments = "/c " + fName;
                        cmd.Execute();
                        Thread.Sleep(100);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                _fileCount++;
                return true;
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Access") && ex.Message.Contains("denied"))
                {
                    PrintConsole($"Access to {path} denied.", MessageType.Error, true);
                }
                return false;
            }
        }

        private static void FixFilePermissions(string path)
        {
            FileAttributes fa = File.GetAttributes(path);
            if ((fa & FileAttributes.ReadOnly) != 0)//ReadOnly file, fix it
            {
                fa = RemoveAttribute(fa, FileAttributes.ReadOnly);
                File.SetAttributes(path, fa);
            }
        }

        private static FileAttributes RemoveAttribute(FileAttributes attributes, FileAttributes attributesToRemove)
        {
            return attributes & ~attributesToRemove;
        }

        private static void ProcessDirectory(string path)
        {
            //1. Process all files in this directory
            var files = new List<string>();
            SearchOption so = SearchOption.TopDirectoryOnly;
            if (_subs) so = SearchOption.AllDirectories;
            foreach (var mask in _masks)
            {
                try
                {
                    if (_keepalive == 0)
                    {
                        files.AddRange(Directory.EnumerateFiles(path, mask, so));
                        continue;
                    }
                    var di = new DirectoryInfo(path);
                    var nfiles = di.GetFiles(mask, so).OrderBy(p => p.LastWriteTime).ToList();
                    //We have the keepalive specified, so let's get a fraction of files to add
                    if (nfiles.Count() <= _keepalive) 
                    {
                        PrintConsole($"There are more files to keep in {path} ({_keepalive}) than to delete ({nfiles.Count()}). Skipping this directory.");
                        continue;//We have to keep alive more files than we have. Nothing to delete.
                    }
                    int ftk = nfiles.Count() - _keepalive;
                    if (ftk < 1) continue;//unlikely, but ...
                    PrintConsole($"Checking {ftk} oldest files out of {nfiles.Count()}.");
                    for (int i = 0; i < ftk; i++)
                    {
                        files.Add(nfiles.ElementAt(i).FullName);
                    }
                }
                catch (Exception ex)
                {
                    
                }
            }
            //Process files
            foreach (var file in files)
            {
                ProcessFile(file);
            }
            //Shall we kill the empty directories?
            if (_noEmpty)
            {
                PrintConsole("Searching for empty sub-directories in " + path);
                var dirs = Directory.EnumerateDirectories(path).ToList();
                if (dirs.Any())
                {
                    foreach (var dir in dirs)
                    {
                        try
                        {
                            if (Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Any()) continue;
                            //the directory is empty, kill it!
                            PrintConsole("Deleting empty directory " + dir);
                            if (_testMode)
                            {
                                PrintConsole("TEST MODE: Would delete directory " + dir);
                                _dirCount++;
                                continue;
                            }
                            Directory.Delete(dir, true);
                            _dirCount++;
                        }
                        catch (Exception ex)
                        {
                            //do nothing
                        }
                    }
                }
            }
        }

        private static bool? IsDirectory(string path)
        {
            if (Directory.Exists(path)) return true;
            if (File.Exists(path)) return false;
            if (_ede)
            {
                Directory.CreateDirectory(path);
                if (Directory.Exists(path))
                {
                    PrintConsole($"The directory {path} did not exist and was created.");
                    return true;
                }
                PrintConsole($"The directory {path} did not exist, and could not be created.", MessageType.Error);
            }
            return null;
        }

        private static bool GetUserConsent(string prompt)
        {
            begin:
            PrintConsole(prompt + " [Y/N]", MessageType.Info, true);
            var key = Console.ReadKey();
            if (key.KeyChar == 'y' || key.KeyChar == 'Y') return true;
            if (key.KeyChar == 'n' || key.KeyChar == 'N') return false;
            goto begin;
        }

        private static string GetUserInput(string prompt)
        {
            Console.Write(prompt + ": ");
            var result = Console.ReadLine();
            return result == null ? "" : result.Trim();
        }

        internal static Int32 ParseInt32(string p)
        {
            Int32 result;
            return !Int32.TryParse(p, out result) ? 0 : result;
        }

        private static string GetValue(string arg)
        {
            //Get argument part from /X:Y arg
            if (!arg.Contains(":"))
            {
                return string.Empty;
            }
            int pos = arg.IndexOf(":");
            if (pos == arg.Length)
            {
                return string.Empty;
            }
            string result = arg.Substring(pos + 1, arg.Length - pos - 1);
            return result;
        }

        //public static string GetSetting(string settingName, string defaultValue)
        //{
        //    var reg = new RegHelper();
        //    var value = reg.GetSettingString(settingName, RegistryRootType.HKEY_LOCAL_MACHINE);
        //    return string.IsNullOrEmpty(value) ? defaultValue : value;
        //}

        //public static bool GetSettingBool(string settingName)
        //{
        //    var reg = new RegHelper();
        //    var value = reg.GetSettingInt(settingName, RegistryRootType.HKEY_LOCAL_MACHINE);
        //    return (value != 0);
        //}
        public static bool ShellExecAndWait(string commandLine)
        {//executes one or more commands from single batch file
            string batchName = Path.GetTempPath() + GenRandomString(8) + ".bat";
            if (File.Exists(batchName))
                File.Delete(batchName);
            commandLine = "@echo off\r\n" + commandLine;
            commandLine += "\r\ndel " + RightFileName(batchName);//this will erase .bat as the last command
            File.WriteAllText(batchName, commandLine);
            return ExecuteProcess(batchName, string.Empty, true, ProcessWindowStyle.Minimized);
        }
        public static string RightFileName(string fileName)
        {
            return !fileName.Contains(" ") ? fileName : "\"" + fileName + "\"";
        }

        public static bool ExecuteProcess(string executableFile, string arguments, bool wait, ProcessWindowStyle windowStyle)
        {
            Process prc = new Process
            {
                StartInfo =
                {
                    FileName = executableFile,
                    WindowStyle = windowStyle,
                    Arguments = arguments + "-c-",
                    WorkingDirectory = Path.GetDirectoryName(executableFile)
                }
            };
            //prc.PriorityClass = ProcessPriorityClass.BelowNormal;
            //prc.ProcessorAffinity = new IntPtr(1);
            if (!prc.Start()) return false;
            if (wait)
            {
                prc.WaitForExit();
                bool success = prc.ExitCode < 2;
                prc.Dispose();
                return success;
            }
            return true;
        }

        public static bool ExecuteProcess(string executableFile, bool Wait, ProcessWindowStyle windowStyle)
        {
            return ExecuteProcess(executableFile, string.Empty, Wait, windowStyle);
        }

        private static void ShowHelpAndExit()
        {
            Console.WriteLine(Strings.ParamList);
            Environment.Exit(0);
        }

        public static void PrintConsole(string message, MessageType msgType = MessageType.Info, bool enforce = false, bool overwrite = false)
        {
            var oldColor = Console.ForegroundColor;
            //Print time
            if (_quiet)
            {
                LogEvent(message, msgType, enforce);
                return;
            }
            if (!_verbose & !enforce & msgType == MessageType.Info) return;

            //Simple line break?
            if (msgType == MessageType.LineBreak)
            {
                Console.Write("\n");
                return;
            }

            //if (!overwrite)
            //{
            //    Console.ForegroundColor = ConsoleColor.White;
            //    Console.Write("[{0} {1}]: ", DateTime.Now.ToShortDateString(),
            //                                DateTime.Now.ToShortTimeString());
            //}
            //Print the rest
            switch (msgType)
            {
                case MessageType.Info:
                    if (!_verbose & !enforce) return;
                    //Console.ForegroundColor = ConsoleColor.White;
                    break;
                case MessageType.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case MessageType.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case MessageType.Success:
                    Console.ForegroundColor = ConsoleColor.Green;
                    break;
            }
            if (!overwrite) Console.WriteLine(message);
            else
            {
                Console.Write(new string('\b', message.Length));
                Console.Write(message);
            }
            Console.ForegroundColor = oldColor;
        }

        private static void LogEvent(string message, MessageType msgType = MessageType.Info, bool enforce = false)
        {
            if (!_verbose & !enforce & msgType == MessageType.Info) return;
            if (_noEventLog) return;
            switch (msgType)
            {
                case MessageType.Info:
                    if (!_verbose & !enforce) return;
                    EventLogger.WriteEventLog(message, EventLogEntryType.Information, 0);
                    break;
                case MessageType.Warning:
                    EventLogger.WriteEventLog(message, EventLogEntryType.Warning, 1);
                    break;
                case MessageType.Error:
                    EventLogger.WriteEventLog(message, EventLogEntryType.Error, 5);
                    break;
            }
        }

        public static string GenRandomString(Int32 length)
        {
            var rnd = new Random();
            var alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();
            var result = "";
            for (int i = 0; i < length; i++)
            {
                result += alphabet[rnd.Next(alphabet.Length)];
            }
            return result;
        }
    }

    public enum MessageType
    {
        Info,
        Warning,
        Error,
        LineBreak,
        Success
    }

    public enum KillMethod
    {
        Simple,
        Recycle,
        Secure,
        BFG,
        Compress
    }
}
