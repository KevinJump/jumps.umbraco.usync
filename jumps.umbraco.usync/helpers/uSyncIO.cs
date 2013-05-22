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
                return uSyncSettings.Folder ; 
            }
        }

        public static string ArchiveFolder
        {
            get
            {
                return uSyncSettings.Archive;
            }
        }

        public static string CacheFile
        {
            get { return "~/usync/usyncdata.xml"; } 
        }

    }
}
