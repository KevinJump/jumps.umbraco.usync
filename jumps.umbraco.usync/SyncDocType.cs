using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Xml;
using System.Xml.Linq; 

// using umbraco.cms.businesslogic;
using umbraco.cms.businesslogic.web;
using umbraco.cms.businesslogic.packager;
using umbraco.BusinessLogic;
using Umbraco.Core.IO;
using umbraco;

using Umbraco.Core ; 
using Umbraco.Core.Services;
using Umbraco.Core.Models;
using Umbraco.Core.Logging;

using jumps.umbraco.usync.helpers;
using jumps.umbraco.usync.Models;
using System.Timers;

namespace jumps.umbraco.usync
{
    /// <summary>
    /// Syncs the Document Types in an umbraco install to the disk, 
    /// and from the disk. 
    /// 
    /// attached to the events, it should just work, and keep everything
    /// in sync.  
    /// </summary>
    public class SyncDocType : SyncItemBase<DocumentType>
    {   
        public SyncDocType() :
            base() { }

        public SyncDocType(ImportSettings settings) :
            base(settings) { }

        public override void ExportAll()
        {
            foreach (DocumentType item in DocumentType.GetAllAsList().ToArray())
            {
                if (item != null)
                {
                    ExportToDisk(item);
                }
            }
        }

        public override void ExportToDisk(DocumentType item, string folder = null)
        {
            if (item == null)
                throw new ArgumentNullException("item");

            if (string.IsNullOrEmpty(folder))
                folder = _settings.Folder;

            try
            {
                LogHelper.Debug<SyncDocType>("Exporting DocType to disk: {0} {1}", () => item.Alias, () => Path.GetFileName(folder));
                XElement node = item.SyncExport();
                XmlDoc.SaveNode(folder, GetDocPath(item), "def", node, Constants.ObjectTypes.DocType);
            }
            catch (Exception e)
            {
                LogHelper.Debug<SyncDocType>("uSync: Error Saving DocumentType {0} - {1}",
                    () => item.Alias, () => e.ToString());
            }
        }

        Dictionary<string, Tuple<string, string>> updates;

        public override void ImportAll()
        {
            foreach(var rename in uSyncNameManager.GetRenames(Constants.ObjectTypes.DocType))
            {
                // rename (isn't going to be simple)
                AddChange(
                    uDocType.Rename(rename.Key, rename.Value, _settings.ReportOnly)
                );
            }

            foreach(var delete in uSyncNameManager.GetDeletes(Constants.ObjectTypes.DocType))
            {
                AddChange(
                    uDocType.Delete(delete.Value, _settings.ReportOnly)
                );
            }

            string root = IOHelper.MapPath(string.Format("{0}\\{1}", _settings.Folder, Constants.ObjectTypes.DocType));

            updates = new Dictionary<string,Tuple<string,string>>();
            base.ImportFolder(root);
            SecondPassFitAndFix();
        }

        public override void Import(string filePath)
        {
            LogHelper.Debug<SyncDocType>("Importing: {0}", () => filePath);

            if (!System.IO.File.Exists(filePath))
                throw new ArgumentNullException("filePath");

            XElement node = XElement.Load(filePath);

            if (node.Name.LocalName != "DocumentType")
                throw new ArgumentException("Not a DocumentType file", filePath);

            if (_settings.ForceImport || tracker.DocTypeChanged(node))
            {

                if (!_settings.ReportOnly)
                {
                    var backup = Backup(node);

                    ChangeItem change = uDocType.SyncImport(node);

                    LogHelper.Debug<SyncDocType>("Imported Part 1: {0} {1}", () => node.Name.LocalName, () => change.changeType);

                    if (change.changeType == ChangeType.Success)
                    {
                        var alias = node.Element("Info").Element("Alias").Value;

                        if (!updates.ContainsKey(alias))
                        {
                            updates.Add(alias, new Tuple<string, string>(filePath, backup));
                        }
                        else
                        {
                            // duplicate
                            change.changeType = ChangeType.ImportFail;
                            change.message = "Duplicated doctype found";
                            AddChange(change);
                        }
                    }
                }
                else
                {
                    AddChange(new ChangeItem
                    {
                        changeType = ChangeType.WillChange,
                        itemType = ItemType.DocumentType,
                        name = node.Element("Info").Element("Alias").Value,
                        message = "will change/update"
                    });
                }
            }
            else
                AddNoChange(ItemType.DocumentType, filePath);
        }

        private void SecondPassFitAndFix()
        {
            foreach(var update in updates)
            {
                var item = ApplicationContext.Current.Services.ContentTypeService.GetContentType(update.Key);
                if (item != null)
                {
                    if (System.IO.File.Exists(update.Value.Item1))
                    {
                        var node = XElement.Load(update.Value.Item1);

                        if (node != null)
                        {
                            var change = uDocType.SyncImportFitAndFix(item, node);

                            if (uSyncSettings.ItemRestore && change.changeType == ChangeType.Mismatch)
                            {
                                Restore(update.Value.Item2);
                                change.changeType = ChangeType.RolledBack;
                            }

                            uSyncReporter.WriteToLog("Imported Doctype [{0}] {1}", change.name, change.changeType.ToString());

                            AddChange(change);
                        }
                    }
                }
            }
        }

        protected override string Backup(XElement node)
        {
            if (uSyncSettings.ItemRestore || uSyncSettings.FullRestore || uSyncSettings.BackupOnImport)
            {

                var alias = node.Element("Info").Element("Alias").Value;
                var docType = DocumentType.GetByAlias(alias);

                if (docType != null)
                {
                    ExportToDisk(docType, _settings.BackupPath);
                    return XmlDoc.GetSavePath(_settings.BackupPath, GetDocPath(docType), "def", Constants.ObjectTypes.DocType);
                }
            }
            return "";
        }

        protected override void Restore(string backup)
        {
            LogHelper.Info<SyncDocType>("Restoring Previous {0}", () => backup);
            XElement backupNode = XmlDoc.GetBackupNode(backup);
            if (backupNode != null)
            {
                uDocType.SyncImport(backupNode, false);

                var alias = backupNode.Element("Info").Element("Alias").Value;

                var contentType = ApplicationContext.Current.Services.ContentTypeService.GetContentType(alias);
                if (contentType != null)
                    uDocType.SyncImportFitAndFix(contentType, backupNode, false);
            }
        }

      
        /// <summary>
        /// works out what the folder stucture for a doctype should be.
        /// 
        /// recurses up the parent path of the doctype, adding a folder
        /// for each one, this then gives us a structure we can create
        /// on the disk, that mimics that of the umbraco internal one
        /// </summary>
        /// <param name="item">DocType path to find</param>
        /// <returns>folderstucture (relative to uSync folder)</returns>
        internal string GetDocPath(DocumentType item)
        {
            string path = "";

            if (item != null)
            {
                // does this documentType have a parent 
                if (item.MasterContentType != 0)
                {
                    // recurse in to the parent to build the path
                    path = GetDocPath(new DocumentType(item.MasterContentType));
                }

                // buld the final path (as path is "" to start with we always get
                // a preceeding '/' on the path, which is nice
                path = string.Format(@"{0}\{1}", path, helpers.XmlDoc.ScrubFile(item.Alias));
            }

            return path;
        }

        private static Timer _saveTimer;
        private static Queue<int> _saveQueue = new Queue<int>();
        private static object _saveLock = new object();
        private static string _eventFolder = "";
        
        /// <summary>
        /// attach events, adds the event handlers for this class 
        /// </summary>
        public static void AttachEvents(string folder)
        {
            InitNameCache(); 
            _eventFolder = folder; 
            ContentTypeService.DeletingContentType += ContentTypeService_DeletingContentType;
            ContentTypeService.SavedContentType += ContentTypeService_SavedContentType;

            _saveTimer = new Timer(2048);
            _saveTimer.Elapsed += _saveTimer_Elapsed;
        }

        static void _saveTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            lock (_saveLock)
            {
                var docSync = new SyncDocType();
                while (_saveQueue.Count > 0)
                {
                    int docTypeId = _saveQueue.Dequeue();
                    var dt = new DocumentType(docTypeId);
                    
                    LogHelper.Info<SyncDocType>("Saving: {0}", () => dt.Alias);

                    if (uSyncNameCache.IsRenamed(dt))
                    {
                        var newSavePath = docSync.GetDocPath(dt);

                        uSyncNameManager.SaveRename(Constants.ObjectTypes.DocType,
                            uSyncNameCache.DocumentTypes[docTypeId], newSavePath);

                        XmlDoc.ArchiveFile(XmlDoc.GetSavePath(_eventFolder, uSyncNameCache.DocumentTypes[docTypeId], "def", Constants.ObjectTypes.DocType), true);

                        XmlDoc.MoveChildren(
                            XmlDoc.GetSavePath(_eventFolder, uSyncNameCache.DocumentTypes[docTypeId], "def", Constants.ObjectTypes.DocType),
                            XmlDoc.GetSavePath(_eventFolder, newSavePath, "def", Constants.ObjectTypes.DocType)
                            );

                        // we need to save all children - so we get a new export
                        if (dt.HasChildren)
                        {
                            foreach (var child in dt.GetChildTypes())
                            {
                                var childType = new DocumentType(child.Id);
                                docSync.ExportToDisk(childType, _eventFolder);
                            }
                        }

                    }
                    uSyncNameCache.UpdateCache(dt);

                    docSync.ExportToDisk(dt, _eventFolder);
                }
            }
        }

        private static void InitNameCache()
        {
            if (uSyncNameCache.DocumentTypes == null)
            {
                uSyncNameCache.DocumentTypes = new Dictionary<int, string>();
                var docSync = new SyncDocType();

                foreach (DocumentType item in DocumentType.GetAllAsList().ToArray())
                {
                    if (item != null)
                    {
                        var savePath = docSync.GetDocPath(new DocumentType(item.Id));
                        uSyncNameCache.DocumentTypes.Add(item.Id, savePath);
                    }
                }
            }
        }


        static void ContentTypeService_SavedContentType(IContentTypeService sender, Umbraco.Core.Events.SaveEventArgs<IContentType> e)
        {
            if (!uSync.EventPaused)
            {
                lock( _saveLock )
                {
                    if (e.SavedEntities.Count() > 0)
                    {
                        _saveTimer.Stop();
                        foreach(var docType in e.SavedEntities)
                        {
                            _saveQueue.Enqueue(docType.Id);
                            LogHelper.Info<SyncDocType>("Added to Queue: {0}", () => docType.Alias);
                        }
                        _saveTimer.Start();
                    }

                    
                }
            }
        }

        static void ContentTypeService_DeletingContentType(IContentTypeService sender, Umbraco.Core.Events.DeleteEventArgs<IContentType> e)
        {
            if (!uSync.EventPaused)
            {
                LogHelper.Debug<SyncDocType>("Deleting Type Fired for {0} types", () => e.DeletedEntities.Count());
                // delete things (there can sometimes be more than one??)
                if (e.DeletedEntities.Count() > 0)
                {
                    var docSync = new SyncDocType();

                    foreach (var docType in e.DeletedEntities)
                    {
                        var savePath = docSync.GetDocPath(new DocumentType(docType.Id));
                        
                        uSyncNameManager.SaveDelete(Constants.ObjectTypes.DocType, savePath);
                        XmlDoc.ArchiveFile(XmlDoc.GetSavePath(_eventFolder, savePath, "def", Constants.ObjectTypes.DocType), true);
                    }
                }
            }
        }
    }
}
