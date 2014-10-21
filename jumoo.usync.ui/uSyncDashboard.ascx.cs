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

        }

        protected void btnImport_Click(object sender, EventArgs e)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            uSync sync = new uSync();

            ImportSettings importSettings = new ImportSettings();
            importSettings.ReportOnly = chkImportReport.Checked;
            importSettings.ForceImport = chkForceImport.Checked;

            List<ChangeItem> changes = sync.ReadAllFromDisk(importSettings);

            resultsRpt.DataSource = changes;
            resultsRpt.DataBind();


            sw.Stop();

            status.Text = string.Format("Import complete, processed {0} items in {1}ms", changes.Count(), sw.ElapsedMilliseconds);


        }

        protected void btnExport_Click(object sender, EventArgs e)
        {

        }

        protected void btnSave_Click(object sender, EventArgs e)
        {

        }
    }
}