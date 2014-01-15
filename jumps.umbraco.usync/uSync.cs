//
// uSync 1.3.4

// For Umbraco 4.11.x/6.0.6+
//
// uses precompile conditions to build a v4 and v6 version
// of usync. 
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO; // so we can write to disk..
using System.Xml; // so we can serialize stuff

using Umbraco.Core.IO;
using Umbraco.Core;
using Umbraco.Core.Logging;

using umbraco.businesslogic;
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
            LogHelper.Debug<uSync>("Get Settings");
                       
            _read = uSyncSettings.Read;
            LogHelper.Debug<uSync>("Settings : Read = {0}", () => _read); 

            _write = uSyncSettings.Write;
            LogHelper.Debug<uSync>("Settings : Write = {0}", ()=> _write); 

            _attach = uSyncSettings.Attach;
            LogHelper.Debug<uSync>("Settings : Attach = {0}", () => _attach);

            // Remove version check
            // we don't work on pre v6.0.1 anymore anyway 
            _docTypeSaveWorks = true ; 
        }

        /// <summary>
        /// save everything in the DB to disk. 
        /// </summary>
        public void SaveAllToDisk()
        {
            LogHelper.Debug<uSync>("Saving to disk - start");

            if ( uSyncSettings.Elements.DocumentTypes ) 
                SyncDocType.SaveAllToDisk();

            if ( uSyncSettings.Elements.Macros ) 
                SyncMacro.SaveAllToDisk();

            if ( uSyncSettings.Elements.MediaTypes ) 
                SyncMediaTypes.SaveAllToDisk();

            if ( uSyncSettings.Elements.Templates ) 
                SyncTemplate.SaveAllToDisk();

            if ( uSyncSettings.Elements.Stylesheets ) 
                SyncStylesheet.SaveAllToDisk();

            if ( uSyncSettings.Elements.DataTypes ) 
                SyncDataType.SaveAllToDisk();

            if (uSyncSettings.Elements.Dictionary)
            {
                SyncLanguage.SaveAllToDisk();
                SyncDictionary.SaveAllToDisk();
            }

            LogHelper.Debug<uSync>("Saving to Disk - End"); 
        }

        /// <summary>
        /// read all settings from disk and sync to the database
        /// </summary>
        public void ReadAllFromDisk()
        {
            LogHelper.Debug<uSync>("Reading from Disk - starting"); 

            if ( uSyncSettings.Elements.Templates ) 
                SyncTemplate.ReadAllFromDisk();

            if ( uSyncSettings.Elements.Stylesheets ) 
                SyncStylesheet.ReadAllFromDisk();

            if ( uSyncSettings.Elements.DataTypes ) 
                SyncDataType.ReadAllFromDisk();

            if ( uSyncSettings.Elements.DocumentTypes ) 
                SyncDocType.ReadAllFromDisk();

            if ( uSyncSettings.Elements.Macros ) 
                SyncMacro.ReadAllFromDisk();

            if ( uSyncSettings.Elements.MediaTypes ) 
                SyncMediaTypes.ReadAllFromDisk();

            if (uSyncSettings.Elements.Dictionary)
            {
                SyncLanguage.ReadAllFromDisk(); 
                SyncDictionary.ReadAllFromDisk();
            }

            LogHelper.Debug<uSync>("Reading from Disk - End"); 
        }

        /// <summary>
        /// attach to the onSave and onDelete event for all types
        /// </summary>
        public void AttachToAll()
        {
            LogHelper.Debug<uSync>("Attaching to Events - Start"); 
            
            if ( uSyncSettings.Elements.DataTypes ) 
                SyncDataType.AttachEvents();

            if ( uSyncSettings.Elements.DocumentTypes )
                SyncDocType.AttachEvents();

            if ( uSyncSettings.Elements.MediaTypes ) 
                SyncMediaTypes.AttachEvents();

            if ( uSyncSettings.Elements.Macros ) 
                SyncMacro.AttachEvents();

            if ( uSyncSettings.Elements.Templates ) 
                SyncTemplate.AttachEvents();

            if ( uSyncSettings.Elements.Stylesheets ) 
                SyncStylesheet.AttachEvents();

            if (uSyncSettings.Elements.Dictionary)
            {
                SyncLanguage.AttachEvents(); 
                SyncDictionary.AttachEvents();
            }

            LogHelper.Debug<uSync>("Attaching to Events - End");
        }

        public void WatchFolder()
        {
            if (uSyncSettings.WatchFolder)
            {
                LogHelper.Info<uSync>("Watching uSync Folder for Changes"); 
                SyncFileWatcher.Init(IOHelper.MapPath(helpers.uSyncIO.RootFolder));
                SyncFileWatcher.Start();
            }
        }

        /// <summary>
        ///  run through the first sync (called at startup)
        /// </summary>
        private void RunSync()
        {
            LogHelper.Info<uSync>("uSync Starting - for detailed debug info. set priority to 'Debug' in log4net.config file"); 

            // Save Everything to disk.
            // only done first time or when write = true           
            if (!Directory.Exists(IOHelper.MapPath(helpers.uSyncIO.RootFolder)) || _write )
            {
                SaveAllToDisk();
            }

            //
            // we take the disk and sync it to the DB, this is how 
            // you can then distribute using uSync.
            //

            if (_read)
            {
                if (!File.Exists(Path.Combine(IOHelper.MapPath(helpers.uSyncIO.RootFolder), "usync.stop")))
                {

                    ReadAllFromDisk(); 

                    if (File.Exists(Path.Combine(IOHelper.MapPath(helpers.uSyncIO.RootFolder), "usync.once")))
                    {
                        LogHelper.Debug<uSync>("Renaming once file"); 
                        
                        File.Move(Path.Combine(IOHelper.MapPath(helpers.uSyncIO.RootFolder), "usync.once"),
                            Path.Combine(IOHelper.MapPath(helpers.uSyncIO.RootFolder), "usync.stop"));
                        LogHelper.Debug<uSync>("Once renamed to stop"); 
                    }
                }
                else
                {
                    LogHelper.Info<uSync>("Read stopped by usync.stop"); 
                }

            }

            if (_attach)
            {
                // everytime. register our events to all the saves..
                // that way we capture things as they are done.
                AttachToAll(); 
            }

            WatchFolder();

            LogHelper.Info<uSync>("uSync Initilized"); 
        }

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

    }
}
