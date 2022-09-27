using System;
using System.Diagnostics;

namespace TTLAgent
{
    /// <summary>
    /// Class for invoking commands in Windows
    /// 2014. DMKI
    /// </summary>
    class Command
    {
        private ProcessPriorityClass _priority = ProcessPriorityClass.Normal;
        private int _expectCode = 0;
        private string _line = "";

        public string Name { get; set; }
        public string Arguments { get; set; }
        public string WorkingDir { get; set; }
        public ProcessWindowStyle UI { get; set; }
        public bool Wait { get; set; }
        public int Timeout { get; set; } //Only if Wait is True
        public string UserName { get; set; }
        public System.Security.SecureString Password { get; set; }
        public string Domain { get; set; }
        public bool Async { get; set; }//Placed for future compatibility with .NET 5. Doesn't work now.
        public bool Run {get; set; }
        public bool Shell {get; set; }
        public bool Verbose { get; set; }
        public bool RedirectStandardOutput;
        public string StandardOutput { get; private set; }//Text variable used to hold output from process

        public string Line 
        {
            get => _line;
            set =>
                //Check for system variables
                _line = Environment.ExpandEnvironmentVariables(value);
        }

        public int ExpectCode { 
            get
            {
                return _expectCode;
            }
            set
            {
                _expectCode = value;
                Wait=true;
            }
        }

        public ProcessPriorityClass Priority
        {
            get
            {
                return _priority;
            }
            set
            { 
                if (value == ProcessPriorityClass.RealTime) value = ProcessPriorityClass.High;
                _priority = value;
            }
        }
        public Command()
        {
            //Set some defaults
            UI = ProcessWindowStyle.Hidden;
            UserName = "";
            ExpectCode = -1;
        }
        public int Execute()
        {//Perform desired action, return True on success
            ProcessStartInfo startInfo = new ProcessStartInfo(_line, Arguments);
            startInfo.CreateNoWindow = (UI== ProcessWindowStyle.Hidden);//So, if hidden it is, no window will be created at the first place
            startInfo.WindowStyle = UI;
            startInfo.UseShellExecute = Shell;
            
            if (RedirectStandardOutput)
            {
                startInfo.RedirectStandardOutput = true;
                Wait = true;
                StandardOutput = String.Empty;
            }
            if (UserName.Length > 0)
            {
                startInfo.UserName = UserName;
                startInfo.Domain = Domain;
                startInfo.Password = Password;
            }
            if (WorkingDir != null) startInfo.WorkingDirectory = WorkingDir;
            Process thisProcess;
            try
            {
                //if (Verbose) Program.WriteEventLog(string.Format("Executing command {0} with parameters {1}.", Line, Arguments), EventLogEntryType.Information, 0);
                thisProcess = Process.Start(startInfo);
            }
            catch (Exception)
            {
                return -1;
            }
            //Set priority?
            if (thisProcess == null) return 0;
            if (thisProcess.Handle != null) thisProcess.PriorityClass = _priority;
            //Wait?
            if (!Wait) return thisProcess.Id;
            if (Timeout > 0)
            {
                thisProcess.WaitForExit(Timeout * 1000);
            }
            else
            {
                thisProcess.WaitForExit();
            }
            //Returned anything?
            if (RedirectStandardOutput)
            {
                StandardOutput = thisProcess.StandardOutput.ReadToEnd();
            }

            if (ExpectCode > -1)
            {
                //Do we expect any exit code?
                int returnCode = thisProcess.ExitCode;
                //if (Verbose) Program.WriteEventLog(string.Format("Executing {0} with parameters {1} returned error code {2}.", Line, Arguments, returnCode.ToString()), EventLogEntryType.Information, 0);
                //Do we have to do something on fail?
                //Program.WriteEventLog(string.Format("Process {0} exited with error code {1}", Name, returnCode.ToString()), EventLogEntryType.Information, 0);
                if (returnCode != ExpectCode)
                {
                    Console.WriteLine(string.Format("Process {0} exited with error code {1} which differs from expected code {2}", Name, returnCode, ExpectCode));
                    //Program.WriteEventLog(string.Format("Process {0} exited with error code {1} which differs from expected code {2}", Name, returnCode, ExpectCode), EventLogEntryType.Error, 2);
                    //switch (_onFail)
                    //{
                    //    case DefaultAction.Continue:
                    //        return true;
                    //    case DefaultAction.Cancel:
                    //        Environment.Exit((int) ShellExitCode.CancelInstall);
                    //        break;//unreachable code detected :D
                    //        //case DefaultAction.Enforce:
                    //        //    break;
                    //        //case DefaultAction.None:
                    //        //    break;
                    //    default:
                    //        break;
                    //}
                }
            }

            return thisProcess.Id;

        }

    }
}