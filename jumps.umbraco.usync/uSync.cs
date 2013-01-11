using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO; // so we can write to disk..
using System.Xml; // so we can serialize stuff

using umbraco.businesslogic;
using Umbraco.Core.IO;
using Umbraco.Web; 

namespace jumps.umbraco.usync
{
    /// <summary>
    /// usync. the umbraco database to disk and back again helper.
    /// 
    /// first thing, lets register ourselfs with the umbraco install
    /// </summary>
    public class uSync : IApplicationEventHandler
    {
        // mutex stuff, so we only do this once.
        private static object _syncObj = new object(); 
        private static bool _synced = false ; 
        
        // TODO: Config this
        private static string _rootFolder = "~/uSync";

        public uSync()
        {
         }

        private void RunSync()
        {

            // in theory when it is all working, 
            // this would only be done first time

            if (!Directory.Exists(IOHelper.MapPath(_rootFolder)))
            {

                SyncDataType.SaveAllToDisk();
                SyncDocType.SaveAllToDisk();
                SyncMacro.SaveAllToDisk();
                SyncTemplate.SaveAllToDisk();
                SyncStylesheet.SaveAllToDisk();
            }

            //
            // we take the disk and sync it to the DB, this is how 
            // you can then distribute using uSync.
            //
            SyncDocType.ReadAllFromDisk(); 

            // everytime. register our events to all the saves..
            // that way we capture things as they are done. 
            SyncDataType.AttachEvents();
            SyncDocType.AttachEvents();
            SyncMacro.AttachEvents();
            SyncTemplate.AttachEvents();
            SyncStylesheet.AttachEvents(); 
        }


        public void OnApplicationStarted(UmbracoApplication httpApplication, Umbraco.Core.ApplicationContext applicationContext)
        {
            // lock
            if (!_synced)
            {
                lock (_syncObj)
                {
                    if (!_synced)
                    {
                        // everything we do here is blocking
                        // on application start, so we should be 
                        // quick. 
                        RunSync(); 

                        _synced = true;
                    }
                }
            }
        }
        
        public void OnApplicationStarting(UmbracoApplication httpApplication, Umbraco.Core.ApplicationContext applicationContext)
        {
            // don't think i do it here.
        }

        public void OnApplicationInitialized(UmbracoApplication httpApplication, Umbraco.Core.ApplicationContext applicationContext)
        {
            // don't think i do it here.
        }
    }
}
