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
    /// </summary>
    public class SyncMacro : SyncItemBase
    {
        public SyncMacro() :
            base(uSyncSettings.Folder) { }

        public SyncMacro(string folder) :
            base (folder) {}

        public SyncMacro(string folder, string set) :
            base(folder, set) { }

        public void SaveToDisk(Macro item, string path = null)
        {
            if (item != null)
            {
                try
                {
                    if (string.IsNullOrEmpty(path))
                        path = _savePath;

                    XmlDocument xmlDoc = helpers.XmlDoc.CreateDoc();
                    xmlDoc.AppendChild(item.ToXml(xmlDoc));
                    helpers.XmlDoc.SaveXmlDoc(item.GetType().ToString(), item.Alias, xmlDoc, path);
                }
                catch (Exception ex)
                {
                    LogHelper.Info<SyncMacro>("uSync: Error Saving Macro {0} - {1}", ()=> item.Name, ()=> ex.ToString());
                }
            }
        }

        public void SaveAllToDisk()
        {
            try
            {
                foreach (Macro item in Macro.GetAll())
                {
                    SaveToDisk(item);
                }
            }
            catch (Exception ex)
            {
                LogHelper.Info<SyncMacro>("uSync: Error Saving All Macros {0}", ()=> ex.ToString());
            }
        }

        public void ReadAllFromDisk()
        {
            string path = IOHelper.MapPath(string.Format("{0}{1}",
                this._savePath,
                "Macro"));

            ReadFromDisk(path); 

        }

        public void ReadFromDisk(string path)
        {
            if ( Directory.Exists(path) )
            {
                foreach (string file in Directory.GetFiles(path, "*.config"))
                {
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(file);

                    XmlNode node = xmlDoc.SelectSingleNode("//macro");

                    if (node != null)
                    {
                        if (tracker.MacroChanged(xmlDoc))
                        {
                            var change = new ChangeItem
                            {
                                itemType = ItemType.Macro,
                                file = file,
                                changeType = ChangeType.Success
                            };

                            PreChangeBackup(node);
                                                        
                            Macro m = Macro.Import(node);

                            if (m != null)
                            {
                                m.Save();

                                change.name = m.Name;
                                change.id = m.Id;
                                
                                if (tracker.MacroChanged(xmlDoc))
                                {
                                    // assume the save now didn't work?
                                    LogHelper.Info<SyncMacro>("Macro doesn't match - rollback?");

                                    change.changeType = ChangeType.Mismatch;
                                    change.message = "Import doesn't match final";


                                    AddChange(change);
                                    // here we would rollback ? 

                                }
                                else
                                {
                                    AddChange(change);
                                }
                            }
                            else
                            {
                                // here? if the import doesn't return and ID is that OK ? 
                            }
                        }
                        else
                        {
                            AddNoChange(ItemType.Macro, file);
                        }
                    }
                }
            }
        }

        private void PreChangeBackup(XmlNode node)
        {
            string alias = XmlHelper.GetNodeValue(node.SelectSingleNode("alias"));
            if (string.IsNullOrEmpty(alias))
                return;

            var macro = Macro.GetByAlias(alias);
            if (macro == null)
                return;

            SaveToDisk(macro, _backupPath);
        }
     
        static string _eventFolder = "";

        public static void AttachEvents(string folder)
        {
            _eventFolder = folder;
            Macro.AfterSave += Macro_AfterSave;
            Macro.AfterDelete += Macro_AfterDelete;
        }

        static void Macro_AfterDelete(Macro sender, DeleteEventArgs e)
        {
            helpers.XmlDoc.ArchiveFile(sender.GetType().ToString(), sender.Alias);

            e.Cancel = false;
        }

        static void Macro_AfterSave(Macro sender, SaveEventArgs e)
        {
            SyncMacro m = new SyncMacro();
            m.SaveToDisk(sender); 
        }
    }
}
