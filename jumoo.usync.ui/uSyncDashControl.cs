using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using jumps.umbraco.usync;

namespace jumoo.usync.ui
{
    /// <summary>
    ///  Handles talking to usync via the dashboard.
    /// </summary>
    public class uSyncDashControl
    {
        uSync _uSync; 

        public uSyncDashControl()
        {
            _uSync = new uSync();
        }

        /// <summary>
        ///  run a standard import 
        /// </summary>
        /// <returns></returns>
        public int RunImport()
        {
            List<ChangeItem> changes = _uSync.ReadAllFromDisk();

            

            return changes.Count();
            
        }
    }
}