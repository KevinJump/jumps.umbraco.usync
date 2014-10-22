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

        public void WriteToLog(string message)
        {
            string logFile = IOHelper.MapPath(string.Format("{0}{1}", uSyncSettings.Folder, "changes.log"));
            if (!File.Exists(logFile))
            {
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
            string logFile = IOHelper.MapPath(string.Format("{0}{1}", uSyncSettings.Folder, "changes.log"));
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
                int errorCount = 0;
                changeMsg.Append("<table style=\"width:400px\"><tr><th>Type</th><th>Name</th><th>Status</th><th>Message</th></tr>");
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

                    if (change.changeType != ChangeType.NoChange || uSyncSettings.Reporter.ReportNoChange)
                    {
                        changeMsg.AppendFormat("<tr><td>{0}</td><td>{1}</td><td>{2}</td><td>{3}</td></tr>",
                            change.itemType, change.name, change.changeType, change.message);
                    }

                }
                changeMsg.Append("</table>");


                if (changeCount > 0)
                {
                    sb.AppendFormat("<p>A Total of <strong>{0} changes</strong> where made\n", changeCount);
                    sb.AppendFormat("<p>There where {0} errors</p>", errorCount);
                    sb.Append(changeMsg.ToString());
                    sb.Append("uSync - Report Complete");
                    LogHelper.Info<uSyncReporter>("Emailing Changes to {0}", () => uSyncSettings.Reporter.Email);
                    // LogHelper.Debug<uSyncReporter>("Email:\n{0}", () => sb.ToString());
                    SmtpClient client = new SmtpClient();
                    MailMessage msg = new MailMessage("usync@jumoo.co.uk", uSyncSettings.Reporter.Email);
                    msg.Body = sb.ToString();
                    msg.IsBodyHtml = true;
                    msg.Subject = string.Format("uSync Report {0} : {1} changes {2} errors", DateTime.Now, changeCount, errorCount);
                    client.Send(msg);
                }
                else
                {
                    LogHelper.Info<uSyncReporter>("Nothing changed that time...");
                }

                WriteToLog(string.Format("uSync {0} Items processed, {1} Changes made {2} Errors", changes.Count(), changeCount, errorCount));
            }
        }

    }
}
