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
    public class SyncTemplate : SyncItemBase
    {
        public SyncTemplate() :
            base(uSyncSettings.Folder) { }

        public SyncTemplate(string folder) :
            base(folder) { }

        public void SaveToDisk(Template item)
        {
            if (item != null)
            {
                try
                {
                    XmlDocument xmlDoc = helpers.XmlDoc.CreateDoc();
                    xmlDoc.AppendChild(item.ToXml(xmlDoc));
                    xmlDoc.AddMD5Hash(item.Alias + item.Text);
                    helpers.XmlDoc.SaveXmlDoc(
                        item.GetType().ToString(), GetDocPath(item) , "def", xmlDoc, _savePath);
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
                            this._changeCount++;

                            LogHelper.Debug<SyncTemplate>("Importing template {0} {1}",
                                () => path, () => node.InnerXml);


                            Template t = Template.Import(node, user);

                            string master = global::umbraco.xmlHelper.GetNodeValue(node.SelectSingleNode("Master"));

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
                    }
                }

                foreach (string folder in Directory.GetDirectories(path))
                {
                    ReadFromDisk(folder);
                }
            }
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
