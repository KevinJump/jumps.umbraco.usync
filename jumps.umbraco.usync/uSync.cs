//
// uSync 1.0 For Umbraco 4.11.x
//
// due to a number of changes in the API (noteably the MediaTypes) 
// this version of uSync is for v4.11.x of Umbraco Only
//
// uSync 1.0 for Umbraco 6.0.3+
//  
// the Interface is diffrent, 

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
    public class uSync : IApplicationEventHandler // works with 4.11.4/5 
    {
        // mutex stuff, so we only do this once.
        private static object _syncObj = new object(); 
        private static bool _synced = false ; 
        
        // TODO: Config this
        private bool _read;
        private bool _write;
        private bool _attach;

        private bool _docTypeSaveWorks = false; 

        /// <summary>
        /// do the stuff we do when we start, using locks, and flags so
        /// we only do the stuff once..
        /// </summary>
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

        private void GetSettings() 
        {
            Log.Add(LogTypes.Debug, 0, "uSync: Getting Settings"); 
           
            _read = uSyncSettings.Read;
            Log.Add(LogTypes.Debug, 0, string.Format("uSync: Setting: Read = {0}", _read));
            _write = uSyncSettings.Write;
            Log.Add(LogTypes.Debug, 0, string.Format("uSync: Setting: Write = {0}", _write));
            _attach = uSyncSettings.Attach;
            Log.Add(LogTypes.Debug, 0, string.Format("uSync: Setting: Attach = {0}", _attach));

            // version 6+ here


#if Umbraco6
            // if it's more than 6 or more than 6.0.x it should work
            if ((global::umbraco.GlobalSettings.VersionMajor > 6) ||
                (global::umbraco.GlobalSettings.VersionMinor > 0) )
            {
                // we are runining at least 7.0 or 6.0 (i.e 6.1.0)
                _docTypeSaveWorks = true ; 
            }
            else if (global::umbraco.GlobalSettings.VersionPatch > 0)
            {
                // we are better than 6.0.0 (i.e 6.0.1+)
                _docTypeSaveWorks = true;
            }
#else

            // let's assume it's always v4 (because this version crashes v6+)
            if (global::umbraco.GlobalSettings.VersionMinor > 11)
            {
                _docTypeSaveWorks = true;
            }
            else if ((global::umbraco.GlobalSettings.VersionMinor == 11)
                 && (global::umbraco.GlobalSettings.VersionPatch > 4))
            {
                _docTypeSaveWorks = true;
            }
#endif
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
                Log.Add(LogTypes.Debug, 0, "uSync: Saving to Disk - Start");
                SyncDocType.SaveAllToDisk();
                SyncMacro.SaveAllToDisk();
                SyncMediaTypes.SaveAllToDisk();
                SyncTemplate.SaveAllToDisk();
                SyncStylesheet.SaveAllToDisk();
                SyncDataType.SaveAllToDisk();
                Log.Add(LogTypes.Debug, 0, "uSync: Saving to Disk - End"); 
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
                Log.Add(LogTypes.Debug, 0, "uSync: Reading from Disk - Starting");
                SyncTemplate.ReadAllFromDisk();
                SyncStylesheet.ReadAllFromDisk();
                SyncDataType.ReadAllFromDisk();
                SyncDocType.ReadAllFromDisk();
                SyncMacro.ReadAllFromDisk();
                SyncMediaTypes.ReadAllFromDisk();

                Log.Add(LogTypes.Debug, 0, "uSync: Reading from Disk - End"); 
            }

            if (_attach)
            {
                // everytime. register our events to all the saves..
                // that way we capture things as they are done.
                Log.Add(LogTypes.Debug, 0, "uSync: Attaching to Events - Start"); 

                SyncDataType.AttachEvents();
                SyncDocType.AttachEvents();
                SyncMediaTypes.AttachEvents(); 
                SyncMacro.AttachEvents();
                SyncTemplate.AttachEvents();
                SyncStylesheet.AttachEvents();
            
                Log.Add(LogTypes.Debug, 0, "uSync: Attaching to Events - End");
            }

            Log.Add(LogTypes.Custom, 0, "uSync: Initizlized"); 
        }

#if Umbraco6
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

#else

        public void OnApplicationStarted(UmbracoApplication httpApplication, Umbraco.Core.ApplicationContext applicationContext)
        {
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
#endif
    }
}
