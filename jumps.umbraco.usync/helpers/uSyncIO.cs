using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jumps.umbraco.usync.helpers
{
    public class uSyncIO
    {

        public static string RootFolder {
            get {
                uSyncSettings config =
                   (uSyncSettings)System.Configuration.ConfigurationManager.GetSection("usync");

                if (config != null)
                    return config.Folder;
                else
                    return "~/uSync/"; 
            }
        }

        public static string ArchiveFolder
        {
            get
            {
                uSyncSettings config =
                    (uSyncSettings)System.Configuration.ConfigurationManager.GetSection("usync");

                if (config != null)
                    return config.Archive;
                else
                    return "~/uSync.Archive/"; 
            }
        }

    }
}
