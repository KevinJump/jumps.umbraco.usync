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
using Umbraco.Core.IO ; 

//  Check list
// ====================
//  SaveOne         X
//  SaveAll         X
//  OnSave          X
//  OnDelete        X
//  ReadFromDisk    X

namespace jumps.umbraco.usync
{
    public class SyncMacro
    {
        public static void SaveToDisk(Macro item)
        {
            XmlDocument xmlDoc = helpers.XmlDoc.CreateDoc();
            xmlDoc.AppendChild(item.ToXml(xmlDoc));
            helpers.XmlDoc.SaveXmlDoc(item.GetType().ToString(), item.Name, xmlDoc);
        }

        public static void SaveAllToDisk()
        {
            foreach (Macro item in Macro.GetAll())
            {
                SaveToDisk(item);
            }
        }

        public static void ReadAllFromDisk()
        {
            string path = IOHelper.MapPath(string.Format("{0}{1}",
                helpers.uSyncIO.RootFolder,
                "umbraco.cms.businesslogic.macro.Macro"));

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
            helpers.XmlDoc.ArchiveFile(sender.GetType().ToString(), sender.Name);

            e.Cancel = false;
        }

        static void Macro_AfterSave(Macro sender, SaveEventArgs e)
        {
            SaveToDisk(sender); 
        }


    }
}
