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
    public class SyncMacro
    {
        public static void SaveToDisk(Macro item)
        {
            if (item != null)
            {
                try
                {
                    XmlDocument xmlDoc = helpers.XmlDoc.CreateDoc();
                    xmlDoc.AppendChild(item.ToXml(xmlDoc));
                    helpers.XmlDoc.SaveXmlDoc(item.GetType().ToString(), item.Alias, xmlDoc);
                }
                catch (Exception ex)
                {
                    LogHelper.Info<SyncMacro>("uSync: Error Saving Macro {0} - {1}", ()=> item.Name, ()=> ex.ToString());
                }
            }
        }

        public static void SaveAllToDisk()
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

        public static void ReadAllFromDisk()
        {
            string path = IOHelper.MapPath(string.Format("{0}{1}",
                helpers.uSyncIO.RootFolder,
                "Macro"));

            ReadFromDisk(path); 

        }

        public static void ReadFromDisk(string path)
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
                        Macro m = Macro.Import(node);
                        m.Save();
                    }
                }
            }
        }

        public static void AttachEvents()
        {
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
            SaveToDisk(sender); 
        }
    }
}
