using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using jumps.umbraco.usync.helpers;

using Umbraco.Core.Logging;
using Umbraco.Core.IO;

using System.IO;
using System.Net.Mail;

namespace jumps.umbraco.usync
{
    /// <summary>
    ///  handles the reporting of usync stuff.
    /// </summary>
    public class uSyncReporter
    {
        public uSyncReporter()
        {
        }

        public static void WriteToLog(string message, params object[] args)
        {
            WriteToLog(string.Format(message, args));
        }

        public static void WriteToLog(string message)
        {
            string logFile = IOHelper.MapPath("~/app_data/logs/changes.log");
            if (!File.Exists(logFile))
            {
                if (!Directory.Exists(Path.GetDirectoryName(logFile)))
                    Directory.CreateDirectory(Path.GetDirectoryName(logFile));

                using ( StreamWriter sw = File.CreateText(logFile))
                {
                   sw.WriteLine("Starting Log: {0}", DateTime.Now);
                }                
            }


            using (StreamWriter sw = File.AppendText(logFile))
            {
                sw.WriteLine("[{0}] {1}", DateTime.Now, message );
            }

        }

        public List<string> GetRecentLog()
        {
            string logFile = IOHelper.MapPath("~/app_data/logs/changes.log");
            if (!File.Exists(logFile))
                return null;

            return File.ReadLines(logFile).Reverse().Take(50).ToList();
        }

        // will email. - for now just logs.
        public void ReportChanges(List<ChangeItem> changes)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("<h3>uSync Change Report: {0}</h3>", DateTime.Now);

            StringBuilder changeMsg = new StringBuilder();

            if (uSyncSettings.Reporter.ReportChanges)
            {
                int changeCount = 0;
                int rollbackCount = 0; 
                int errorCount = 0;
                changeMsg.Append("<table style=\"width:400px\"><tr><th>Type</th><th>Name</th><th>Status</th><th>Message</th></tr>\n");
                foreach (var change in changes)
                {
                    if (change.changeType >= ChangeType.Fail)
                    {
                        errorCount++;
                    }

                    if (change.changeType != ChangeType.NoChange && change.changeType != ChangeType.WillChange)
                    {
                        changeCount++;
                    }

                    if (change.changeType == ChangeType.RolledBack)
                    {
                        rollbackCount++;
                    }

                    if (change.changeType != ChangeType.NoChange || uSyncSettings.Reporter.ReportNoChange)
                    {
                        changeMsg.AppendFormat("<tr><td>{0}</td><td>{1}</td><td>{2}</td><td>{3}</td></tr>\n",
                            change.itemType, change.name, change.changeType, change.message);
                    }

                }
                changeMsg.Append("</table>");


                if (changeCount > 0)
                {
                    sb.AppendFormat("<p>A Total of <strong>{0} changes</strong> where made\n", changeCount);
                    sb.AppendFormat("<p>There where {0} errors</p>", errorCount);

                    if ( rollbackCount > 0 )
                        sb.AppendFormat("<p>{0} itesm where rolled back</p>", errorCount);
                    sb.Append(changeMsg.ToString());
                    sb.Append("uSync - Report Complete");
                    LogHelper.Info<uSyncReporter>("Emailing Changes to {0}", () => uSyncSettings.Reporter.Email);
                    var subject = new StringBuilder();
                    subject.AppendFormat("uSync Report {0}: {1} Changes", DateTime.Now, changeCount);
                    
                    if (errorCount > 0)
                        subject.AppendFormat("- {0} Errors", errorCount);
                    
                    if (rollbackCount > 0)
                        subject.AppendFormat("- {0} Rollbacks", rollbackCount);

                    LogHelper.Debug<uSyncReporter>("Email: Subject: {0}", ()=> subject.ToString());
                    LogHelper.Debug<uSyncReporter>("Email:\n{0}", () => sb.ToString());
    
                    SendMailMessage(
                        subject.ToString(),
                        sb.ToString());
                }
                else
                {
                    LogHelper.Info<uSyncReporter>("Nothing changed that time...");
                }

                var logline = string.Format("uSync {0} Items processed, {1} Changes made {2} Errors {3} Rollbacks", changes.Count(), changeCount, errorCount, rollbackCount);
                WriteToLog(logline);
                LogHelper.Info<uSyncReporter>(logline);
            }
        }


        private void SendMailMessage(string subject, string message)
        {
            try
            {
                SmtpClient client = new SmtpClient();
                MailMessage msg = new MailMessage("usync@jumoo.co.uk", uSyncSettings.Reporter.Email);
                msg.Body = message;
                msg.IsBodyHtml = true;
                msg.Subject = subject;
                client.Send(msg);
            }
            catch (Exception ex)
            {
                LogHelper.Info<uSyncReporter>("Cannot send email: {0}", ()=> ex.Message);
            }

        }
    }
}
