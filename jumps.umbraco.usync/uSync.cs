//
// uSync 1.6.1 for Umbraco 6.1.x
// 
// fairly major re-write to include: 
//      * Mapping :
//          will map Content, Media, Stylesheet and Tab Ids
//          inside DataTypes during the import
//
//      * Rollback :
//          at the flick of a config, will attempt to work out
//          if an import has worked (by comparing before and afters)
//          and if they don't match it will then either
//              a) rollback the item you just imported
//              b) rollback the whole import
//
//          rollbacks can help as part of a callorie controleld diet
//          but you need to make sure you're watching or you could
//          just end up in forever rollback.
//
//      * Dashboard support
//          A nice dashboard in umbraco, to manually control imports
//          and exports, as well as a nice - what might change report
//          
using System;
using System.Collections.Generic;
using System.IO; 
using System.Diagnostics;

using Umbraco.Core.IO;
using Umbraco.Core;
using Umbraco.Core.Logging;

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

        // our own events - fired when we start and stop
        public static event uSyncBulkEventHander Starting;
        public static event uSyncBulkEventHander Initialized;

        public static bool EventPaused = false; 

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
        }

        public void ClearFolder()
        {
            var root = IOHelper.MapPath(helpers.uSyncIO.RootFolder);
            if ( Directory.Exists(root))
            {
                foreach(var subDir in Directory.GetDirectories(root))
                {
                    Directory.Delete(subDir, true);
                }
            }
        }

        /// <summary>
        /// save everything in the DB to disk. 
        /// </summary>
        public void SaveAllToDisk(string folder = null)
        {
            if (String.IsNullOrEmpty(folder))
                folder = helpers.uSyncIO.RootFolder;

            ImportSettings settings = new ImportSettings(folder);

            LogHelper.Info<uSync>("Saving to disk - start {0}", ()=> folder);

            if (uSyncSettings.Elements.DocumentTypes)
            {
                var docSync = new SyncDocType(settings);
                docSync.ExportAll();
            }

            if (uSyncSettings.Elements.Macros)
            {
                var macroSync = new SyncMacro(settings);
                macroSync.ExportAll();
            }

            if ( uSyncSettings.Elements.MediaTypes )
            {
                var mediaSync = new SyncMediaTypes(settings);
                mediaSync.ExportAll();
            }


            if (uSyncSettings.Elements.Templates)
            {
                var tSync = new SyncTemplate(settings);
                tSync.ExportAll();
            }

            if (uSyncSettings.Elements.Stylesheets)
            {
                var styleSync = new SyncStylesheet(settings);
                styleSync.ExportAll();
            }

            if (uSyncSettings.Elements.DataTypes)
            {
                var dataTypeSync = new SyncDataType(settings);
                dataTypeSync.ExportAll();
            }

            if (uSyncSettings.Elements.Dictionary)
            {
                var langSync = new SyncLanguage(settings);
                langSync.ExportAll();

                var dicSync = new SyncDictionary(settings);
                dicSync.ExportAll();
            }

            LogHelper.Info<uSync>("Saving to Disk - End"); 
        }

        /// <summary>
        /// read all settings from disk and sync to the database
        /// </summary>
        public List<ChangeItem> ReadAllFromDisk(ImportSettings importSettings = null)
        {
            if (importSettings == null)
                importSettings = new ImportSettings();

            if (!importSettings.ReportOnly)
                EventPaused = true;

            if (!File.Exists(Path.Combine(IOHelper.MapPath(importSettings.Folder), "usync.stop")))
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();
                var last = 0.0;
                LogHelper.Debug<uSync>("Reading from Disk - starting");

                // if backup first...
                var changes = new List<ChangeItem>();

                if (uSyncSettings.Elements.Templates)
                {
                    LogHelper.Info<uSync>("Importing Templates");
                    var tSync = new SyncTemplate(importSettings);
                    tSync.ImportAll();
                    changes.AddRange(tSync.ChangeList);
                    LogHelper.Info<uSync>("Imported Templates {0} - changes ({1} ms)", ()=> tSync.ChangeCount, () => sw.ElapsedMilliseconds - last);
                    last = sw.ElapsedMilliseconds;
                }

                if (uSyncSettings.Elements.Stylesheets)
                {
                    LogHelper.Info<uSync>("Importing Stylesheets");
                    var styleSync = new SyncStylesheet(importSettings);
                    styleSync.ImportAll();
                    changes.AddRange(styleSync.ChangeList);

                    LogHelper.Info<uSync>("Imported Stylesheets {0} - changes ({1} ms)", ()=> styleSync.ChangeCount, () => sw.ElapsedMilliseconds - last);
                    last = sw.ElapsedMilliseconds;
                }

                if (uSyncSettings.Elements.DataTypes)
                {
                    LogHelper.Info<uSync>("Importing DataTypes");
                    var dataTypeSync = new SyncDataType(importSettings);
                    dataTypeSync.ImportAll();
                    changes.AddRange(dataTypeSync.ChangeList);

                    LogHelper.Info<uSync>("Imported DataTypes {0} changes - ({1} ms)", ()=> dataTypeSync.ChangeCount, () => sw.ElapsedMilliseconds - last);
                    last = sw.ElapsedMilliseconds;
                }

                if (uSyncSettings.Elements.DocumentTypes)
                {
                    LogHelper.Info<uSync>("Importing Document Types");
                    var docSync = new SyncDocType(importSettings);
                    docSync.ImportAll();
                    changes.AddRange(docSync.ChangeList);

                    LogHelper.Info<uSync>("Imported Document Types {0} changes ({1}ms)", () => docSync.ChangeCount, () => sw.ElapsedMilliseconds - last);
                    last = sw.ElapsedMilliseconds;
                }

                if (uSyncSettings.Elements.Macros)
                {
                    var macroSync = new SyncMacro(importSettings);
                    macroSync.ImportAll();
                    changes.AddRange(macroSync.ChangeList);

                    LogHelper.Info<uSync>("Imported Macros {0} changes ({1}ms)", ()=> macroSync.ChangeCount, () => sw.ElapsedMilliseconds - last);
                    last = sw.ElapsedMilliseconds;
                }

                if (uSyncSettings.Elements.MediaTypes)
                {
                    LogHelper.Info<uSync>("Importing Media Types");
                    var mediaSync = new SyncMediaTypes(importSettings);
                    mediaSync.ImportAll();
                    changes.AddRange(mediaSync.ChangeList);

                    LogHelper.Info<uSync>("Imported MediaTypes {0} changes ({1} ms)", ()=> mediaSync.ChangeCount, () => sw.ElapsedMilliseconds - last);
                    last = sw.ElapsedMilliseconds;
                }

                if (uSyncSettings.Elements.Dictionary) 
                {
                    LogHelper.Info<uSync>("Importing Languages");
                    var langSync = new SyncLanguage(importSettings);
                    langSync.ImportAll();
                    changes.AddRange(langSync.ChangeList);

                    LogHelper.Info<uSync>("Imported Languages {0} changes ({1}ms)", () => langSync.ChangeCount, ()=> sw.ElapsedMilliseconds - last);
                    last = sw.ElapsedMilliseconds;
                    LogHelper.Info<uSync>("Importing Dictionary Items");

                    var dicSync = new SyncDictionary(importSettings);
                    dicSync.ImportAll();
                    changes.AddRange(dicSync.ChangeList);

                    LogHelper.Info<uSync>("Imported Dictionary Items {0} changes ({1} ms)", ()=>  dicSync.ChangeCount, () => sw.ElapsedMilliseconds - last);
                    last = sw.ElapsedMilliseconds;
                }

                // double datatype pass - because when mapping it becomes dependent on doctypes
                if (uSyncSettings.Elements.DataTypes)
                {
                    LogHelper.Info<uSync>("Importing Datatypes (Second Pass)");
                    var dataTypeSync = new SyncDataType(importSettings);
                    dataTypeSync.ImportAll();
                    LogHelper.Info<uSync>("Imported DataTypes (again) {0} changes ({1} ms)", () => dataTypeSync.ChangeCount, () => sw.ElapsedMilliseconds - last);
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

                var report = new uSyncReporter();
                report.ReportChanges(changes);

                EventPaused = false; 

                if ( !importSettings.ReportOnly && !importSettings.ForceImport && uSyncSettings.FullRestore)
                {
                    // if we're not on a reporting run, or a force run and the global settings is for a full restore
                    // then we need to go through our import. 

                    // if there are any changes that didn't work - we do a full restore of the whole backup.
                    var errors = false;
                    foreach(var change in changes)
                    {
                        if (change.changeType >= ChangeType.Fail)
                        {
                            errors = true;
                            break;
                        }
                    }

                    if ( errors )
                    {
                        LogHelper.Info<uSync>("Import contained errors - Full Rollback to {0}", () => importSettings.BackupPath);

                        var restoreSettings = new ImportSettings(importSettings.BackupPath);
                        restoreSettings.ForceImport = true;

                        var rollbackChanges = ReadAllFromDisk(restoreSettings);

                        changes.AddRange(rollbackChanges);
                    }
                }

                return changes; 
            }
            else
            {
                LogHelper.Info<uSync>("Read stopped by usync.stop");
                return new List<ChangeItem>();
            }
        }

        /// <summary>
        /// attach to the onSave and onDelete event for all types
        /// </summary>
        public void AttachToAll(string folder = null)
        {
            if (String.IsNullOrEmpty(folder))
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
            // only done first time (no directory and when attach = true) or when write = true 
            if ((!Directory.Exists(IOHelper.MapPath(helpers.uSyncIO.RootFolder)) && _attach) || _write )
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

    public class ImportSettings
    {
        public bool ReportOnly;
        public bool ForceImport;
        public string Folder;
        public string BackupPath;

        public ImportSettings()
        {
            ReportOnly = false;
            ForceImport = false;
            Folder = helpers.uSyncIO.RootFolder;
            var set = DateTime.Now.ToString("yyyy_MM_dd_HHmmss");
            BackupPath = string.Format("{0}\\{1}", uSyncSettings.BackupFolder.Trim('\\'), set);
        }

        public ImportSettings(string folder)
        {
            ReportOnly = false;
            ForceImport = false;
            Folder = folder;
            var set = DateTime.Now.ToString("yyyy_MM_dd_HHmmss");
            BackupPath = string.Format("{0}\\{1}", uSyncSettings.BackupFolder.Trim('\\'), set);
        }
    }
}