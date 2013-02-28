//#define UMBRACO4

// IApplicationEventHanlder moved from Umbraco.Web to Umbraco.Core
// between v4 and v6 
//
// Arguments also changed. the UMBRACO4 defines the functions.
//
// when you compile for 4 or 6 you will also need to change
// the dll refrences as Umbraco.Web no longer contains the
// names of the functions in 6.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO; // so we can write to disk..
using System.Xml; // so we can serialize stuff

using umbraco.businesslogic;
using Umbraco.Core.IO;
using Umbraco.Core;

using umbraco.BusinessLogic;
using Umbraco.Web;

namespace jumps.umbraco.usync
{

    /// <summary>
    /// usync. the umbraco database to disk and back again helper.
    /// 
    /// first thing, lets register ourselfs with the umbraco install
    /// </summary>
    // public class uSync : IApplicationEventHandler
    public class uSync : IApplicationEventHandler
    {
        // mutex stuff, so we only do this once.
        private static object _syncObj = new object(); 
        private static bool _synced = false ; 
        
        // TODO: Config this
        private bool _read;
        private bool _write;
        private bool _attach;

        private bool _docTypeSaveWorks = false; 

        public uSync()
        {
            // GetSettings(); 
        }

        private void GetSettings() 
        {
            Log.Add(LogTypes.Custom, 0, "Usync Starting (Contstructor)"); 

            _read = uSyncSettings.Read;
            Log.Add(LogTypes.Debug, 0, string.Format("uSync: Setting: Read = {0}", _read));
            _write = uSyncSettings.Write;
            Log.Add(LogTypes.Debug, 0, string.Format("uSync: Setting: Write = {0}", _write));
            _attach = uSyncSettings.Attach;
            Log.Add(LogTypes.Debug, 0, string.Format("uSync: Setting: Attach = {0}", _attach)); 

            // better than 4.11.4 (upto 4.99.99)
            if ((global::umbraco.GlobalSettings.VersionMajor == 4)
                  && (global::umbraco.GlobalSettings.VersionMinor >= 11)
                  && (global::umbraco.GlobalSettings.VersionPatch > 4))
            {
                _docTypeSaveWorks = true;
                Log.Add(LogTypes.Debug, 0, string.Format("uSync: Setting: Post 4.11.5" )); 

            }
            // better than 6.0.0 -> forever...
            if ((global::umbraco.GlobalSettings.VersionMajor >= 6)
                  && (global::umbraco.GlobalSettings.VersionPatch > 0))
            {
                Log.Add(LogTypes.Debug, 0, string.Format("uSync: Setting: Post = 6.0.0"));
                _docTypeSaveWorks = true;
            }
        }

        private void RunSync()
        {
            Log.Add(LogTypes.Custom, 0, "uSync Starting");
            Log.Add(LogTypes.Debug, 0, "========== uSync Starting"); 
            

            // in theory when it is all working, 
            // this would only be done first time

            //
            
            if (!Directory.Exists(IOHelper.MapPath(helpers.uSyncIO.RootFolder)) || _write )
            {
                Log.Add(LogTypes.Custom, 0, "uSync Saving All to Disk");
                SyncDocType.SaveAllToDisk();
                SyncMacro.SaveAllToDisk();
                SyncMediaTypes.SaveAllToDisk(); 
                SyncTemplate.SaveAllToDisk();
                SyncStylesheet.SaveAllToDisk();
                SyncDataType.SaveAllToDisk();
            }

            // bugs in the DataType EventHandling, mean it isn't fired 
            // onSave - so we just write it out to disk everyload.
            // this will make it hard 
            // to actually delete anything via the sync
            // we only do this < 4.11.5 and < 6.0.1 
            //
            // this mimics attach.. so if you turn _attach off, this doesn't
            // happen
            //
            if (!_docTypeSaveWorks && _attach)
            {
                Log.Add(LogTypes.Custom, 0, "uSync Saving DataTypes to Disk (Bug work)");
                SyncDataType.SaveAllToDisk();
            }

            //
            // we take the disk and sync it to the DB, this is how 
            // you can then distribute using uSync.
            //
            
            if (_read)
            {
                Log.Add(LogTypes.Custom, 0, "uSync Syncing from to Disk");
                SyncTemplate.ReadAllFromDisk();
                SyncStylesheet.ReadAllFromDisk();
                SyncDataType.ReadAllFromDisk();
                SyncDocType.ReadAllFromDisk();
                SyncMacro.ReadAllFromDisk();
                SyncMediaTypes.ReadAllFromDisk(); 
            }

            if (_attach)
            {
                // everytime. register our events to all the saves..
                // that way we capture things as they are done.
                Log.Add(LogTypes.Custom, 0, "uSync Attaching to Events"); 

                SyncDataType.AttachEvents();
                SyncDocType.AttachEvents();
                SyncMediaTypes.AttachEvents(); 
                SyncMacro.AttachEvents();
                SyncTemplate.AttachEvents();
                SyncStylesheet.AttachEvents();
            }
        }

        private void DoOnStart()
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
                        GetSettings(); 

                        RunSync();
                        
                        _synced = true;
                    }
                }
            }
        }
#if UMBRACO4
        public void OnApplicationStarted(UmbracoApplication httpApplication, Umbraco.Core.ApplicationContext applicationContext)
        {
            Log.Add(LogTypes.Debug, 0, "On Application Started"); 
            DoOnStart();
        }

        public void OnApplicationStarting(UmbracoApplication httpApplication, Umbraco.Core.ApplicationContext applicationContext)
        {
            // don't think i do it here.
        }

        public void OnApplicationInitialized(UmbracoApplication httpApplication, Umbraco.Core.ApplicationContext applicationContext)
        {
            // don't think i do it here.
        }
#else
        public void OnApplicationStarted(UmbracoApplicationBase httpApplication, Umbraco.Core.ApplicationContext applicationContext)
        {            
            DoOnStart();
        }
        
        public void OnApplicationStarting(UmbracoApplicationBase httpApplication, Umbraco.Core.ApplicationContext applicationContext)
        {
            // don't think i do it here.
        }

        public void OnApplicationInitialized(UmbracoApplicationBase httpApplication, Umbraco.Core.ApplicationContext applicationContext)
        {
            // don't think i do it here.
        }
#endif
    }
}
