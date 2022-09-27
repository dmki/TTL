using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TTLAgent
{
    public static class TTLWorks
    {
        private static string _dbFileName;
        private static bool _canDelete;
        public static void ProcessTasks()
        {
            //Get settings from INI file.
            var iniFile = Path.Combine(Environment.CurrentDirectory, "ttl.ini");
            if (!File.Exists(iniFile))
            {
                Program.PrintConsole("Cannot find file " + iniFile, MessageType.Error, true);
                Environment.Exit(1);
            }
            var iniParser = new IniParser.FileIniDataParser();
            var ini = iniParser.ReadFile(iniFile);
            _canDelete = GetSettingBool(ini["Cleanup"]["Enable"]);
            //Find local database
            _dbFileName = Path.Combine(Environment.CurrentDirectory, "ttl.mdb");
            if (!File.Exists(_dbFileName))
            {
                Program.PrintConsole("Cannot find file " + _dbFileName, MessageType.Error, true);
                Environment.Exit(1);
            }
            //Open database
            string connectionString = string.Format(@"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={0};User Id=admin;Password=AqdpJ1ycj;", _dbFileName);
            using (var conn = new OleDbConnection(connectionString))
            {
                conn.Open();
            }

            
            //If cleanup is not enabled - tell user about it and quit
            //A. Process directories
            //Read directory items
            //Process each directory
            //B. Process files
            //Read file items and process each file
        }

        private static bool GetSettingBool(string value)
        {
            throw new NotImplementedException();
        }

        private static void ProcessDirectory(string dirName)
        {
            
        }

        private static void ProcessFile(string fileName, Int16 days, DeleteMethod deleteMethod)
        {
            
        }

    }

    enum DeleteMethod:byte
    {
        Simple = 1,
        RecycleBin,
        Secure,
        Compress
    }
}
