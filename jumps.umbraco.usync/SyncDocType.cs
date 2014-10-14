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
            base(uSyncSettings.Folder) { }

        public SyncDocType(string folder) :
            base(folder) { }

        public SyncDocType(string folder, string set) :
            base(folder, set) { }        

        static Dictionary<string, string> updated;

        public override void ExportAll(string folder)
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
                folder = _savePath;

            try
            {
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

        public override void ImportAll(string folder)
        {
            string root = IOHelper.MapPath(string.Format("{0}\\{1}", folder, Constants.ObjectTypes.DocType));

            updates = new Dictionary<string,Tuple<string,string>>();
            
            base.ImportFolder(root);

            SecondPassFitAndFix();
        }

        public override void Import(string filePath)
        {
            if (!System.IO.File.Exists(filePath))
                throw new ArgumentNullException("filePath");

            XElement node = XElement.Load(filePath);

            if (node.Name.LocalName != "DocumentType")
                throw new ArgumentException("Not a DocumentType file", filePath);

            if (tracker.DocTypeChanged(node))
            {
                var backup = Backup(node);

                ChangeItem change = uDocType.SyncImport(node);

                if (change.changeType == ChangeType.Success)
                {
                    var alias = node.Element("Info").Element("Alias").Value;

                    if ( !updates.ContainsKey(alias)) {
                        updates.Add(alias, new Tuple<string, string>(filePath, backup));
                    }
                    else {
                        // duplicate
                        change.changeType = ChangeType.ImportFail;
                        change.message = "Duplicated doctype found";
                        AddChange(change);
                    }                   
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

                            if (change.changeType == ChangeType.Mismatch)
                            {
                                Restore(update.Value.Item2);
                            }
                            AddChange(change);
                        }
                    }
                }
            }
        }

        protected override string Backup(XElement node)
        {
            var alias = node.Element("Info").Element("Alias").Value;
            var docType = DocumentType.GetByAlias(alias);

            if (docType != null)
            {
                ExportToDisk(docType, _backupPath);
                return XmlDoc.GetSavePath(_backupPath, GetDocPath(docType), "def", Constants.ObjectTypes.DocType);
            }

            return "";
        }

        protected override void Restore(string backup)
        {
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
        private string GetDocPath(DocumentType item)
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

        static string _eventFolder = ""; 
        
        /// <summary>
        /// attach events, adds the event handlers for this class 
        /// </summary>
        public static void AttachEvents(string folder)
        {
            _eventFolder = folder; 
            ContentTypeService.DeletingContentType += ContentTypeService_DeletingContentType;
            ContentTypeService.SavedContentType += ContentTypeService_SavedContentType;
        }


        static void ContentTypeService_SavedContentType(IContentTypeService sender, Umbraco.Core.Events.SaveEventArgs<IContentType> e)
        {
            LogHelper.Debug<SyncDocType>("SaveContent Type Fired for {0} types", 
                ()=> e.SavedEntities.Count());

            if (e.SavedEntities.Count() > 0)
            {
                var docSync = new SyncDocType(_eventFolder);

                foreach (var docType in e.SavedEntities)
                {
                    docSync.ExportToDisk(new DocumentType(docType.Id), _eventFolder);
                }
            }
        }

        static void ContentTypeService_DeletingContentType(IContentTypeService sender, Umbraco.Core.Events.DeleteEventArgs<IContentType> e)
        {
            LogHelper.Debug<SyncDocType>("Deleting Type Fired for {0} types", () => e.DeletedEntities.Count());
            // delete things (there can sometimes be more than one??)
            if (e.DeletedEntities.Count() > 0)
            {
                var docSync = new SyncDocType(_eventFolder);

                foreach (var docType in e.DeletedEntities)
                {
                    XmlDoc.ArchiveFile("DocumentType", docSync.GetDocPath(new DocumentType(docType.Id)), "def");
                }
            }
        }
    }
}
