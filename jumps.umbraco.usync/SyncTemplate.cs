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
        public SyncTemplate() :
            base(uSyncSettings.Folder) { }

        public SyncTemplate(string folder) :
            base(folder) { }

        public SyncTemplate(string folder, string set) :
            base(folder, set) { }


        public override void ExportAll(string folder)
        {
            try
            {
                foreach (Template item in Template.GetAllAsList().ToArray())
                {
                    ExportToDisk(item, folder);
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
                folder = _savePath;

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
        private string GetDocPath(Template item)
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

        public override void ImportAll(string folder)
        {
            string root = IOHelper.MapPath(string.Format("{0}\\{1}", folder, Constants.ObjectTypes.Template));
            base.ImportFolder(root);
        }

        public override void Import(string filePath)
        {
            if (!File.Exists(filePath))
                throw new ArgumentNullException("filePath");

            XElement node = XElement.Load(filePath);

            if (node.Name.LocalName != "Template")
                throw new ArgumentException("Not a template file", filePath);


            if (tracker.TemplateChanged(node))
            {
                var backup = Backup(node);

                ChangeItem change = uTemplate.SyncImport(node);

                if (change.changeType == ChangeType.Mismatch)
                    Restore(backup);

                AddChange(change);
            }
            else
                AddNoChange(ItemType.Template, filePath);
        }

        protected override string Backup(XElement node)
        {
            var alias = node.Element("Alias").Value;
            var template = Template.GetByAlias(alias);

            if (template != null)
            {
                ExportToDisk(template, _backupPath);
                return XmlDoc.GetSavePath(_backupPath, GetDocPath(template), "def", Constants.ObjectTypes.Template);
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
            _eventFolder = folder;
            Template.AfterSave += Template_AfterSave;
            Template.AfterDelete += Template_AfterDelete;

        }

        static void Template_AfterDelete(Template sender, DeleteEventArgs e)
        {
            // helpers.XmlDoc.ArchiveFile( helpers.XmlDoc.GetTypeFolder(sender.GetType().ToString()) + GetDocPath(sender), "def");
            var tSync = new SyncTemplate(_eventFolder);

            XmlDoc.ArchiveFile(XmlDoc.GetSavePath(_eventFolder, tSync.GetDocPath(sender), "def", Constants.ObjectTypes.Template), true);

            e.Cancel = false; 
        }

        static void Template_AfterSave(Template sender, SaveEventArgs e)
        {
            // save
            var tSync = new SyncTemplate();
            tSync.ExportToDisk(sender, _eventFolder);
        }
        
    }
}
