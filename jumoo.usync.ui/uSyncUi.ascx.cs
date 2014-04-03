using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

using jumps.umbraco.usync;

namespace jumoo.usync.ui
{
    public partial class uSyncUi : System.Web.UI.UserControl
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                InitSettings();
            }
        }

        private void InitSettings()
        {
            chkAttatch.Checked = uSyncSettings.Attach;
            chkRead.Checked = uSyncSettings.Read;
            chkWrite.Checked = uSyncSettings.Write;
            chkWatch.Checked = uSyncSettings.WatchFolder;
        }

        protected void btnSave_Click(object sender, EventArgs e)
        {
            // save the settings...
            uSyncSettings.Attach = chkAttatch.Checked;
            uSyncSettings.Read = chkRead.Checked;
            uSyncSettings.Write = chkWrite.Checked;
            uSyncSettings.WatchFolder = chkWatch.Checked;
            uSyncSettings.Save();

            pnUpdate.Visible = true;
            lbStatus.Text = "Settings changed, you need to restart your umbraco instance for the changes to take effect";


            InitSettings();
        }

        protected void btnImport_Click(object sender, EventArgs e)
        {
            uSync u = new uSync();
            u.ReadAllFromDisk();

            pnUpdate.Visible = true;
            lbStatus.Text = string.Format("Imported everything from {0}", uSyncSettings.Folder);
            InitSettings();
        }

        protected void btnExport_Click(object sender, EventArgs e)
        {
            uSync u = new uSync();
            u.SaveAllToDisk();

            pnUpdate.Visible = true;
            lbStatus.Text = string.Format("Everything written out to {0}", uSyncSettings.Folder);
            InitSettings();
        }
    }
}