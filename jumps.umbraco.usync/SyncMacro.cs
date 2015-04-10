using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml ;
using System.IO; 

using umbraco.cms.businesslogic; 
using umbraco.cms.businesslogic.macro ;
using umbraco.cms.businesslogic.packager ; 
using umbraco.BusinessLogic;

using Umbraco.Core.IO;
using Umbraco.Core.Logging;
using Umbraco.Core;

using jumps.umbraco.usync.helpers;
using jumps.umbraco.usync.Models;
using System.Xml.Linq;
using System.Timers;

namespace jumps.umbraco.usync
{
    /// <summary>
    /// Sycronizes all the macros to/from the usync folder
    /// 
    /// the macros definitions are stored compleatly in the 
    /// database although they often point to files on the 
    /// disk (scrips, user controls). 
    /// 
    /// SyncMacro uses the package API to read write the xml
    /// files for macros. no structure in macros.
    /// 
    /// </summary>
    /// 
    ///
    public class SyncMacro : SyncItemBase<Macro>
    {
        public SyncMacro() :
            base() { }

        public SyncMacro(ImportSettings settings) :
            base (settings) {}

        public override void ExportAll()
        {
            foreach(Macro item in Macro.GetAll())
            {
                ExportToDisk(item);
            }
        }

        public override void ExportToDisk(Macro item, string folder = null)
        {
            if (item == null)
                throw new ArgumentNullException("item");

            if (string.IsNullOrEmpty(folder))
                folder = _settings.Folder;

            try
            {
                XElement node = item.SyncExport();
                XmlDoc.SaveNode(folder, item.Alias, node, Constants.ObjectTypes.Macro);
            }
            catch (Exception ex)
            {
                LogHelper.Info<SyncMacro>("uSync: Error Saving Macro {0} - {1}", () => item.Name, () => ex.ToString());
            }
        }

        public override void ImportAll()
        {
            var renames = uSyncNameManager.GetRenames(Constants.ObjectTypes.Macro, _settings.Folder);
            foreach (var rename in renames)
            {
                AddChange(uMacro.Rename(rename.Key, rename.Value, _settings.ReportOnly));
            }

            var deletes = uSyncNameManager.GetDeletes(Constants.ObjectTypes.Macro, _settings.Folder);
            foreach (var delete in deletes)
            {
                AddChange(uMacro.Delete(delete.Value, _settings.ReportOnly));
            }

            string root = IOHelper.MapPath(string.Format("{0}\\{1}", _settings.Folder, Constants.ObjectTypes.Macro));
            base.ImportFolder(root);
        }

        public override void Import(string filePath)
        {
            if (!File.Exists(filePath))
                throw new ArgumentNullException("filePath");

            XElement node = XElement.Load(filePath);

            if (node.Name.LocalName != "macro")
                throw new ArgumentException("Not a macro file", filePath);

            if (_settings.ForceImport || tracker.MacroChanged(node))
            {
                if (!_settings.ReportOnly)
                {
                    var backup = Backup(node);

                    ChangeItem change = uMacro.SyncImport(node, !_settings.Restore);

                    if (uSyncSettings.ItemRestore && change.changeType == ChangeType.Mismatch)
                    {
                        Restore(backup);
                        change.changeType = ChangeType.RolledBack;
                    }

                    uSyncReporter.WriteToLog("Imported Macro [{0}] {1}", change.name, change.changeType.ToString());
                    AddChange(change);
                }
                else
                {
                    AddChange(new ChangeItem
                    {
                        changeType = ChangeType.WillChange,
                        itemType = ItemType.Macro,
                        name = node.Element("name").Value,
                        message = "Reporting: will update"
                    });
                }
            }
            else
                AddNoChange(ItemType.Macro, filePath);
        }

        protected override string Backup(XElement node, string filePath = null)
        {
            try
            {
                if (_settings.Restore)
                    return null;

                if (uSyncSettings.ItemRestore || uSyncSettings.FullRestore || uSyncSettings.BackupOnImport)
                {

                    var alias = node.Element("alias").Value;
                    var macro = Macro.GetByAlias(alias);

                    if (macro != null)
                    {
                        ExportToDisk(macro, _settings.BackupPath);
                        return XmlDoc.GetSavePath(_settings.BackupPath, macro.Alias, Constants.ObjectTypes.Macro);
                    }
                }
            }
            catch( Exception ex )
            {
                LogHelper.Warn<SyncMacro>("Failed to create backup pre-changes", () => ex.ToString());
            }
            return "";
        }

        protected override void Restore(string backup)
        {
            XElement backupNode = XmlDoc.GetBackupNode(backup);

            if (backupNode != null)
                uMacro.SyncImport(backupNode, false);
        }

        private static Timer _saveTimer;
        private static Queue<int> _saveQueue = new Queue<int>();
        private static object _saveLock = new object();
        private static string _eventFolder = "";

        public static void AttachEvents(string folder)
        {
            InitNameCache();

            _eventFolder = folder;
            Macro.AfterSave += Macro_AfterSave;
            Macro.AfterDelete += Macro_AfterDelete;
            Macro.New += Macro_New;

            _saveTimer = new Timer(2048);
            _saveTimer.Elapsed += _saveTimer_Elapsed;
                
        }

        static void _saveTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            lock (_saveLock)
            {
                while (_saveQueue.Count > 0 )
                {
                    SaveMacro(_saveQueue.Dequeue()); 
                }
            }
        }


        static void Macro_AfterDelete(Macro sender, DeleteEventArgs e)
        {
            if (!uSync.EventPaused)
            {
                uSyncNameManager.SaveDelete(Constants.ObjectTypes.Macro, sender.Name, uSyncSettings.Folder, null);
                XmlDoc.ArchiveFile(XmlDoc.GetSavePath(_eventFolder, sender.Alias, Constants.ObjectTypes.Macro), true);
                e.Cancel = false;
            }
        }

        static void Macro_AfterSave(Macro sender, SaveEventArgs e)
        {
            if (!uSync.EventPaused)
            {
                lock( _saveLock )
                {
                    _saveTimer.Stop();

                    _saveQueue.Enqueue(sender.Id);

                    _saveTimer.Start();
                }
            }
        }

        static void Macro_New(Macro sender, NewEventArgs e)
        {
            LogHelper.Info<SyncMacro>("New Fired");
            if (!uSync.EventPaused)
            {
                SaveMacro(sender);
            }
        }

        static void SaveMacro(int id)
        {
            var m = Macro.GetById(id);
            if (m != null)
                SaveMacro(m);
        }

        static void SaveMacro(Macro sender)
        {
            if (uSyncNameCache.IsRenamed(sender))
            {
                uSyncNameManager.SaveRename(Constants.ObjectTypes.Macro,
                    uSyncNameCache.Macros[sender.Id], sender.Alias, uSyncSettings.Folder);

                // delete old one
                XmlDoc.ArchiveFile(XmlDoc.GetSavePath(_eventFolder, uSyncNameCache.Macros[sender.Id], Constants.ObjectTypes.Macro), true);
            }

            uSyncNameCache.UpdateCache(sender, uSyncSettings.Folder);

            SyncMacro m = new SyncMacro();
            m.ExportToDisk(sender, _eventFolder);
        }

        static void InitNameCache()
        {
            if (uSyncNameCache.Macros == null)
            {
                uSyncNameCache.Macros = new Dictionary<int, string>();
                foreach (Macro item in Macro.GetAll())
                {
                    uSyncNameCache.Macros.Add(item.Id, item.Alias);
                }
            }
        }
    }
}
