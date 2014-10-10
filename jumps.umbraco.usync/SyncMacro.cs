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

        public void SaveToDisk(Macro item)
        {
            if (item != null)
            {
                try
                {
                    XmlDocument xmlDoc = helpers.XmlDoc.CreateDoc();
                    xmlDoc.AppendChild(item.ToXml(xmlDoc));
                    xmlDoc.AddMD5Hash();
                    helpers.XmlDoc.SaveXmlDoc(item.GetType().ToString(), item.Alias, xmlDoc, this._savePath);
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
                            this._changeCount++;
                            Macro m = Macro.Import(node);
                            m.Save();
                            _changes.Add(new ChangeItem
                                {
                                    changeType = ChangeType.Success,
                                    itemType = ItemType.Macro,
                                    name = m.Name
                                });
                        }
                        else
                        {
                            _changes.Add(new ChangeItem
                                {
                                    changeType = ChangeType.NoChange,
                                    itemType = ItemType.Macro,
                                    name = Path.GetFileNameWithoutExtension(file)
                                });
                        }
                    }
                }
            }
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
