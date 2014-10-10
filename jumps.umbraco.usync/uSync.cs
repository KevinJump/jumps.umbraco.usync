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

using System.Diagnostics;

namespace jumps.umbraco.usync
{
    public delegate void uSyncBulkEventHander(uSyncEventArgs e);

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

        // our own events - fired when we start and stop
        public static event uSyncBulkEventHander Starting;
        public static event uSyncBulkEventHander Initialized; 

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
        public void SaveAllToDisk(string folder = null)
        {
            if (String.IsNullOrWhiteSpace(folder))
                folder = helpers.uSyncIO.RootFolder;

            LogHelper.Info<uSync>("Saving to disk - start {0}", ()=> folder);

            if (uSyncSettings.Elements.DocumentTypes)
            {
                var docSync = new SyncDocType(folder);
                docSync.SaveAllToDisk();
            }

            if (uSyncSettings.Elements.Macros)
            {
                var macroSync = new SyncMacro(folder);
                macroSync.SaveAllToDisk();
            }

            if ( uSyncSettings.Elements.MediaTypes )
            {
                var mediaSync = new SyncMediaTypes(folder);
                mediaSync.SaveAllToDisk();
            }


            if (uSyncSettings.Elements.Templates)
            {
                var tSync = new SyncTemplate(folder);
                tSync.SaveAllToDisk();
            }

            if (uSyncSettings.Elements.Stylesheets)
            {
                var styleSync = new SyncStylesheet(folder);
                styleSync.SaveAllToDisk();
            }

            if (uSyncSettings.Elements.DataTypes)
            {
                var dataTypeSync = new SyncDataType(folder);
                dataTypeSync.SaveAllToDisk();
            }

            if (uSyncSettings.Elements.Dictionary)
            {
                var langSync = new SyncLanguage(folder);
                langSync.SaveAllToDisk();

                var dicSync = new SyncDictionary(folder);
                dicSync.SaveAllToDisk();
            }

            LogHelper.Debug<uSync>("Saving to Disk - End"); 
        }

        /// <summary>
        /// read all settings from disk and sync to the database
        /// </summary>
        public void ReadAllFromDisk(string folder = null)
        {
            if (String.IsNullOrWhiteSpace(folder))
                folder = helpers.uSyncIO.RootFolder;

            if (!File.Exists(Path.Combine(IOHelper.MapPath(folder), "usync.stop")))
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();
                var last = 0.0;
                LogHelper.Debug<uSync>("Reading from Disk - starting");

                // if backup first...
                if (true)
                {
                    var backupSet = DateTime.Now.ToString("yyyy_MM_dd_HHmmss");
                    SaveAllToDisk(string.Format("~/usync.backup/{0}/", backupSet));
                    LogHelper.Info<uSync>("Backup Created ({0}ms)", ()=> sw.ElapsedMilliseconds - last);
                    last = sw.ElapsedMilliseconds;

                }


                if (uSyncSettings.Elements.Templates)
                {
                    var tSync = new SyncTemplate(folder);
                    tSync.ReadAllFromDisk();
                    LogHelper.Info<uSync>("Imported Templates {0} - changes ({1} ms)", ()=> tSync.ChangeCount, () => sw.ElapsedMilliseconds - last);
                    last = sw.ElapsedMilliseconds;
                }

                if (uSyncSettings.Elements.Stylesheets)
                {
                    var styleSync = new SyncStylesheet(folder);
                    styleSync.ReadAllFromDisk();
                    LogHelper.Info<uSync>("Imported Stylesheets {0} - changes ({1} ms)", ()=> styleSync.ChangeCount, () => sw.ElapsedMilliseconds - last);
                    last = sw.ElapsedMilliseconds;
                }

                if (uSyncSettings.Elements.DataTypes)
                {
                    var dataTypeSync = new SyncDataType(folder);
                    dataTypeSync.ReadAllFromDisk();
                    LogHelper.Info<uSync>("Imported DataTypes {0} changes - ({1} ms)", ()=> dataTypeSync.ChangeCount, () => sw.ElapsedMilliseconds - last);
                    last = sw.ElapsedMilliseconds;
                }

                if (uSyncSettings.Elements.DocumentTypes)
                {
                    var docSync = new SyncDocType(folder);
                    docSync.ReadAllFromDisk();
                    LogHelper.Info<uSync>("Document Types imported - {0} changes ({1}ms)", () => docSync.ChangeCount, () => sw.ElapsedMilliseconds - last);
                    last = sw.ElapsedMilliseconds;
                }

                if (uSyncSettings.Elements.Macros)
                {
                    var macroSync = new SyncMacro(folder);
                    macroSync.ReadAllFromDisk();
                    LogHelper.Info<uSync>("Imported Macros - {0} changes ({1}ms)", ()=> macroSync.ChangeCount, () => sw.ElapsedMilliseconds - last);
                    last = sw.ElapsedMilliseconds;
                }

                if (uSyncSettings.Elements.MediaTypes)
                {
                    var mediaSync = new SyncMediaTypes(folder);
                    mediaSync.ReadAllFromDisk();
                    LogHelper.Info<uSync>("Imported MediaTypes - {0} changes ({1} ms)", ()=> mediaSync.ChangeCount, () => sw.ElapsedMilliseconds - last);
                    last = sw.ElapsedMilliseconds;
                }

                if (uSyncSettings.Elements.Dictionary) 
                {
                    var langSync = new SyncLanguage(folder);
                    langSync.ReadAllFromDisk();
                    LogHelper.Info<uSync>("Imported Languages {0} - changes ({1}ms)", () => langSync.ChangeCount, ()=> sw.ElapsedMilliseconds - last);
                    last = sw.ElapsedMilliseconds;

                    var dicSync = new SyncDictionary(folder);
                    dicSync.ReadAllFromDisk();
                    LogHelper.Info<uSync>("Imported Dictionary Items {0} - changes ({1} ms)", ()=>  dicSync.ChangeCount, () => sw.ElapsedMilliseconds - last);
                    last = sw.ElapsedMilliseconds;
                }

                // double datatype pass - because when mapping it becomes dependent on doctypes
                if (uSyncSettings.Elements.DataTypes)
                {
                    var dataTypeSync = new SyncDataType(folder);
                    dataTypeSync.ReadAllFromDisk();
                    LogHelper.Info<uSync>("Imported DataTypes (again) {0} changes - ({1} ms)", () => dataTypeSync.ChangeCount, () => sw.ElapsedMilliseconds - last);
                }

                LogHelper.Debug<uSync>("Reading from Disk - End");

                if (File.Exists(Path.Combine(IOHelper.MapPath(helpers.uSyncIO.RootFolder), "usync.once")))
                {
                    LogHelper.Debug<uSync>("Renaming once file");

                    File.Move(Path.Combine(IOHelper.MapPath(helpers.uSyncIO.RootFolder), "usync.once"),
                        Path.Combine(IOHelper.MapPath(helpers.uSyncIO.RootFolder), "usync.stop"));
                    LogHelper.Debug<uSync>("Once renamed to stop");
                }

                sw.Stop();
                LogHelper.Info<uSync>("Imported From Disk {0}ms", () => sw.ElapsedMilliseconds);
            }
            else
            {
                LogHelper.Info<uSync>("Read stopped by usync.stop");
            }
        }

        /// <summary>
        /// attach to the onSave and onDelete event for all types
        /// </summary>
        public void AttachToAll(string folder = null)
        {
            if (String.IsNullOrWhiteSpace(folder))
                folder = helpers.uSyncIO.RootFolder;

            LogHelper.Debug<uSync>("Attaching to Events - Start"); 
            
            if ( uSyncSettings.Elements.DataTypes ) 
                SyncDataType.AttachEvents(folder);

            if (uSyncSettings.Elements.DocumentTypes)
                SyncDocType.AttachEvents(folder);                

            if ( uSyncSettings.Elements.MediaTypes ) 
                SyncMediaTypes.AttachEvents(folder);

            if ( uSyncSettings.Elements.Macros ) 
                SyncMacro.AttachEvents(folder);

            if ( uSyncSettings.Elements.Templates ) 
                SyncTemplate.AttachEvents(folder);

            if ( uSyncSettings.Elements.Stylesheets ) 
                SyncStylesheet.AttachEvents(folder);

            if (uSyncSettings.Elements.Dictionary)
            {
                SyncLanguage.AttachEvents(folder); 
                SyncDictionary.AttachEvents(folder);
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
            Stopwatch sw = new Stopwatch();
            sw.Start();

            LogHelper.Info<uSync>("uSync Starting - for detailed debug info. set priority to 'Debug' in log4net.config file");

            if (!ApplicationContext.Current.IsConfigured)
             {
                 LogHelper.Info<uSync>("umbraco not configured, usync aborting");
                 return;
             }

            OnStarting(new uSyncEventArgs(_read, _write, _attach)); 

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
                ReadAllFromDisk(); 
            }

            if (_attach)
            {
                // everytime. register our events to all the saves..
                // that way we capture things as they are done.
                AttachToAll(); 
            }

            WatchFolder();

            sw.Stop();
            LogHelper.Info<uSync>("uSync Initilized ({0} ms)", ()=> sw.ElapsedMilliseconds);
            OnComplete(new uSyncEventArgs(_read, _write, _attach));

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
        
        // our events
        public static void OnStarting(uSyncEventArgs e)
        {
            if (Starting != null)
            {
                Starting(e);
            }
        }

        public static void OnComplete(uSyncEventArgs e)
        {
            if (Initialized != null)
            {
                Initialized(e);
            }
        }

    }
    /// <summary>
    /// event firing - arguments when we fire. 
    /// </summary>
    public class uSyncEventArgs : EventArgs
    {
        private bool _import;
        private bool _export;
        private bool _attach;

        public uSyncEventArgs(bool import, bool export, bool attach)
        {
            _import = import;
            _export = export;
            _attach = attach;
        }

        public bool Import
        {
            get { return _import; }
        }

        public bool Export
        {
            get { return _export; }
        }

        public bool Attach
        {
            get { return _attach; }
        }
    }
}