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

using jumps.umbraco.usync.helpers;


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

        public void SaveToDisk(Template item, string path = null)
        {
            if (item != null)
            {
                try
                {
                    if (string.IsNullOrEmpty(path))
                        path = _savePath;

                    XmlDocument xmlDoc = helpers.XmlDoc.CreateDoc();
                    xmlDoc.AppendChild(item.ToXml(xmlDoc));
                    xmlDoc.AddMD5Hash(item.Alias + item.Text);
                    helpers.XmlDoc.SaveXmlDoc(
                        item.GetType().ToString(), GetDocPath(item) , "def", xmlDoc, path);
                }
                catch (Exception ex)
                {
                    LogHelper.Info<SyncTemplate>("uSync: Error Saving Template {0} - {1}", 
                        ()=>item.Text, ()=>ex.ToString());
                }
            }
        }

        public void SaveAllToDisk()
        {
            try
            {
                foreach (Template item in Template.GetAllAsList().ToArray())
                {
                    SaveToDisk(item);
                }
            }
            catch( Exception ex )
            {
                LogHelper.Info<SyncTemplate>("uSync: Error saving all templates {0}", ()=> ex.ToString()); 
            }
        }

        private string GetDocPath(Template item)
        {
            string path = "";
            if (item != null)
            {
                if (item.MasterTemplate != 0)
                {
                    path = GetDocPath(new Template(item.MasterTemplate));
                }

                path = string.Format("{0}//{1}", path, helpers.XmlDoc.ScrubFile(item.Alias));
            }
            return path;
        }

        public void ReadAllFromDisk()
        {

            string path = IOHelper.MapPath(string.Format("{0}{1}",
                helpers.uSyncIO.RootFolder,
                "Template"));

            ReadFromDisk(path);
        }

        public void ReadFromDisk(string path)
        {
            if (Directory.Exists(path))
            {
                User user = new User(0); 

                foreach (string file in Directory.GetFiles(path, "*.config"))
                {
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(file);

                    XmlNode node = xmlDoc.SelectSingleNode("//Template");

                    if (node != null)
                    {

                        if (tracker.TemplateChanged(xmlDoc))
                        {
                            var change = new ChangeItem
                            {
                                file = file,
                                itemType = ItemType.Template,
                                changeType = ChangeType.Success
                            };

                            PreChangeBackup(node);

                            LogHelper.Debug<SyncTemplate>("Importing template {0} {1}",
                                () => path, () => node.InnerXml);


                            Template t = Template.Import(node, user);

                            if (t != null)
                            {
                                change.id = t.Id;
                                change.name = t.Text;
                                
                                string master = XmlHelper.GetNodeValue(node.SelectSingleNode("Master"));

                                if (master.Trim() != "")
                                {
                                    Template masterTemplate = Template.GetByAlias(master);
                                    if (masterTemplate != null)
                                    {
                                        t.MasterTemplate = masterTemplate.Id;
                                    }
                                    t.Save();
                                }
                            }
                            else
                            {
                                // need to check if we get null on success.
                                LogHelper.Info<SyncTemplate>("Null templated returned? this might be ok");
                            }

                            AddChange(change);
                        }
                        else
                        {
                            AddNoChange(ItemType.Template, file);
                        }
                    }
                }

                foreach (string folder in Directory.GetDirectories(path))
                {
                    ReadFromDisk(folder);
                }
            }
        }

        private void PreChangeBackup(XmlNode node)
        {
            string alias = xmlHelper.GetNodeValue(node.SelectSingleNode("Alias"));
            if (string.IsNullOrEmpty(alias))
                return;

            var template = Template.GetByAlias(alias);
            if (template == null)
                return;

            SaveToDisk(template, _backupPath);
                

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
            helpers.XmlDoc.ArchiveFile(sender.GetType().ToString(), tSync.GetDocPath(sender), "def"); 


            e.Cancel = false; 
        }

        static void Template_AfterSave(Template sender, SaveEventArgs e)
        {
            // save
            var tSync = new SyncTemplate(_eventFolder);
            tSync.SaveToDisk(sender);
        }
        
    }
}
