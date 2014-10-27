using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml ;
using umbraco.cms.businesslogic;
using umbraco.cms.businesslogic.template;

using umbraco.BusinessLogic; 

using System.IO; 
using Umbraco.Core.IO;

using Umbraco.Core;
using Umbraco.Core.Logging;

using umbraco;
using umbraco.businesslogic;

using jumps.umbraco.usync.Models;
using jumps.umbraco.usync.helpers;
using System.Xml.Linq;


namespace jumps.umbraco.usync
{
    /// <summary>
    ///  syncornizes the templates with the usync folder
    ///  
    /// templates / partial views are almost compleatly
    /// stored on disk, but the umbraco database stores
    /// and ID, alias and parent, to maintain structure
    /// 
    /// SyncTemplate uses the packaging API to import and
    /// export the templates. 
    /// </summary>
    public class SyncTemplate : SyncItemBase<Template>
    {
        public SyncTemplate() : base() { }

        public SyncTemplate(ImportSettings settings) :
            base(settings) { }

        public override void ExportAll()
        {
            try
            {
                foreach (Template item in Template.GetAllAsList().ToArray())
                {
                    ExportToDisk(item, _settings.Folder);
                }
            }
            catch (Exception ex)
            {
                LogHelper.Info<SyncTemplate>("uSync: Error saving all templates {0}", () => ex.ToString());
            }
        }

        public override void ExportToDisk(Template item, string folder = null)
        {
            if (item == null)
                throw new ArgumentNullException("item");

            if (string.IsNullOrEmpty(folder))
                folder = _settings.Folder;

            try
            {
                XElement node = item.SyncExport();
                XmlDoc.SaveNode(folder, GetDocPath(item), "def", node, Constants.ObjectTypes.Template);
            }
            catch (Exception ex)
            {
                LogHelper.Info<SyncTemplate>("uSync: Error Saving Template {0} - {1}",
                    () => item.Text, () => ex.ToString());
            }
        }

        /// <summary>
        ///  templates are stored in a tree, so we need to workout the 
        ///  tree path, we are going to save it in...
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        internal string GetDocPath(Template item)
        {
            string path = "";
            if (item != null)
            {
                if (item.MasterTemplate != 0)
                {
                    path = GetDocPath(new Template(item.MasterTemplate));
                }

                path = string.Format("{0}\\{1}", path, helpers.XmlDoc.ScrubFile(item.Alias));
            }
            return path;
        }

        public override void ImportAll()
        {
            foreach(var rename in uSyncNameManager.GetRenames(Constants.ObjectTypes.Template))
            {
                // renames are more complex for hierarchical items.
                // we get two paths, we need to workout exactly what has changed.
                
                // because it could be a new parent has been inserted.
                uTemplate.Rename(rename.Key, rename.Value, _settings.ReportOnly);
            }

            foreach(var delete in uSyncNameManager.GetDeletes(Constants.ObjectTypes.Template))
            {
                // deletes - again we get a path - so we have to delete from the top down?
                uTemplate.Delete(delete.Value, _settings.ReportOnly);
            }

            string root = IOHelper.MapPath(string.Format("{0}\\{1}", _settings.Folder, Constants.ObjectTypes.Template));
            base.ImportFolder(root);
        }

        public override void Import(string filePath)
        {
            if (!File.Exists(filePath))
                throw new ArgumentNullException("filePath");

            XElement node = XElement.Load(filePath);

            if (node.Name.LocalName != "Template")
                throw new ArgumentException("Not a template file", filePath);


            if (_settings.ForceImport || tracker.TemplateChanged(node))
            {
                if (!_settings.ReportOnly)
                {
                    var backup = Backup(node);

                    ChangeItem change = uTemplate.SyncImport(node);

                    if (uSyncSettings.ItemRestore && change.changeType == ChangeType.Mismatch)
                    {
                        Restore(backup);
                        change.changeType = ChangeType.RolledBack;
                    }
                    uSyncReporter.WriteToLog("Imported Template [{0}] {1}", change.name, change.changeType.ToString());
                    AddChange(change);
                }
                else
                {
                    AddChange(new ChangeItem
                    {
                        changeType = ChangeType.WillChange,
                        itemType = ItemType.Template,
                        name = node.Element("Name").Value,
                        message = "Reporting: will update"

                    });
                }
            }
            else
                AddNoChange(ItemType.Template, filePath);
        }

        protected override string Backup(XElement node)
        {
            if (uSyncSettings.ItemRestore || uSyncSettings.FullRestore)
            {

                var alias = node.Element("Alias").Value;
                var template = Template.GetByAlias(alias);

                if (template != null)
                {
                    ExportToDisk(template, _settings.BackupPath);
                    return XmlDoc.GetSavePath(_settings.BackupPath, GetDocPath(template), "def", Constants.ObjectTypes.Template);
                }
            }

            return "";
        }

        protected override void Restore(string backup)
        {
            XElement backupNode = XmlDoc.GetBackupNode(backup);
            if (backupNode != null)
                uTemplate.SyncImport(backupNode, false);
        }

        static string _eventFolder = "";

        public static void AttachEvents(string folder)
        {
            InitNameCache();
            _eventFolder = folder;
            Template.AfterSave += Template_AfterSave;
            Template.AfterDelete += Template_AfterDelete;
        }

        static void Template_AfterDelete(Template sender, DeleteEventArgs e)
        {
            if (!uSync.EventPaused)
            {

                // helpers.XmlDoc.ArchiveFile( helpers.XmlDoc.GetTypeFolder(sender.GetType().ToString()) + GetDocPath(sender), "def");
                var tSync = new SyncTemplate();
                var path = tSync.GetDocPath(sender);
                
                uSyncNameManager.SaveDelete(Constants.ObjectTypes.Template, path);
                uSyncNameCache.Templates.Remove(sender.Id);

                XmlDoc.ArchiveFile(XmlDoc.GetSavePath(_eventFolder, path, "def", Constants.ObjectTypes.Template), true);

                e.Cancel = false;
            }
        }

        static void Template_AfterSave(Template sender, SaveEventArgs e)
        {
            if (!uSync.EventPaused)
            {
                var tSync = new SyncTemplate();

                if (uSyncNameCache.IsRenamed(sender))
                {
                    uSyncNameManager.SaveRename(Constants.ObjectTypes.Template, uSyncNameCache.Templates[sender.Id], tSync.GetDocPath(sender));
                    XmlDoc.ArchiveFile(uSyncNameCache.Templates[sender.Id], true);
                }

                uSyncNameCache.UpdateCache(sender);

                // save
                tSync.ExportToDisk(sender, _eventFolder);
            }
        }

        static void InitNameCache()
        {
            if ( uSyncNameCache.Templates == null)
            {
                uSyncNameCache.Templates = new Dictionary<int, string>();

                var tSync = new SyncTemplate();

                foreach(var template in Template.GetAllAsList())
                {
                    uSyncNameCache.Templates.Add(template.Id,tSync.GetDocPath(template));
                }
            }
        }
    }
}
