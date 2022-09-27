using System;
using System.Diagnostics;
using System.Security;

namespace TTLAgent
{
    public static class EventLogger
    {
        public static void ExitWithMessage(string message, EventLogEntryType messageType, int exitCode)
        {
            WriteEventLog(message, messageType, exitCode);
            Environment.Exit(exitCode);
        }

        public static void WriteEventLog(string message, EventLogEntryType messageType, int eventID)
        {
            string sSource = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
            try
            {
                PrepareEventLog(sSource);
                EventLog.WriteEntry(sSource, message, messageType, eventID);
            }
            catch (Exception)
            {
            }
        }

        //[PrincipalPermission(SecurityAction.Demand, Role = @"BUILTIN\Administrators")]
        public static bool PrepareEventLog(string sSource = "", string sLog = "Application")
        {
            if (string.IsNullOrEmpty(sSource))
                sSource = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
            try
            {
                if (!EventLog.SourceExists(sSource))
                    EventLog.CreateEventSource(sSource, sLog);
                return true;
            }
            catch (SecurityException)
            {
                return false;
            }
        }
    }
}
