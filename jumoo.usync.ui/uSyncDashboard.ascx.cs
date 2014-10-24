using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

using System.Diagnostics;

using jumps.umbraco.usync;

namespace jumoo.usync.ui
{
    public partial class uSyncDashboard : System.Web.UI.UserControl
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                chkRead.Checked = uSyncSettings.Read;
                chkAttach.Checked = uSyncSettings.Attach;
                chkWrite.Checked = uSyncSettings.Write;
                chkWatch.Checked = uSyncSettings.WatchFolder;
            }

            loadRecent();
        }

        private void loadRecent()
        {
            uSyncReporter r = new uSyncReporter();
            List<string> recent = r.GetRecentLog();

            repRecent.DataSource = recent;
            repRecent.DataBind();
        }

        protected void btnImport_Click(object sender, EventArgs e)
        {
            try
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();
                uSync sync = new uSync();

                ImportSettings importSettings = new ImportSettings();
                importSettings.ReportOnly = chkImportReport.Checked;
                importSettings.ForceImport = chkForceImport.Checked;

                List<ChangeItem> changes = sync.ReadAllFromDisk(importSettings);

                var changesMade = changes.Where(x => x.changeType != ChangeType.NoChange);
                resultsRpt.DataSource = changesMade;
                resultsRpt.DataBind();

                sw.Stop();

                resultsPanel.Visible = true;
                loadRecent();
                status.Text = string.Format("Import complete: {2} changes in {0} items in {1}ms", changes.Count(), sw.ElapsedMilliseconds, changesMade.Count());
            }
            catch (Exception ex)
            {
                status.Text = string.Format("Error Importing {0}", ex.Message);
            }
        }

        protected void btnExport_Click(object sender, EventArgs e)
        {
            try
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();
                uSync sync = new uSync();
                sync.ClearFolder();
                sync.SaveAllToDisk();

                resultsPanel.Visible = true;
                sw.Stop();
                status.Text = string.Format("New Export created {0} ms", sw.ElapsedMilliseconds);
            }
            catch ( Exception ex )
            {
                status.Text = string.Format("Error exporting: {0}", ex.Message);
            }
        }

        protected void btnSave_Click(object sender, EventArgs e)
        {
            uSyncSettings.Read = chkRead.Checked;
            uSyncSettings.Write = chkWatch.Checked;
            uSyncSettings.Attach = chkAttach.Checked;
            uSyncSettings.WatchFolder = chkWatch.Checked;

            uSyncSettings.Save();
        }
    }
}