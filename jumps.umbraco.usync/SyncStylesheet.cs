using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml;
using umbraco.cms.businesslogic;
using umbraco.cms.businesslogic.web;

using umbraco.BusinessLogic; 

using System.IO ;
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
    public class SyncStylesheet
    {
        public static void SaveToDisk(StyleSheet item)
        {
            if (item != null)
            {
                try
                {
                    XmlDocument xmlDoc = helpers.XmlDoc.CreateDoc();
                    xmlDoc.AppendChild(item.ToXml(xmlDoc));
                    helpers.XmlDoc.SaveXmlDoc(item.GetType().ToString(), item.Text, xmlDoc);
                }
                catch (Exception ex)
                {
                    Log.Add(LogTypes.Error, 0, String.Format("uSync: Error Reading Stylesheet {0} - {1}", item.Text, ex.ToString()));
                    throw new SystemException(string.Format("error saving stylesheet {0}", item.Text), ex); 
                }
            }
        }

        public static void SaveAllToDisk()
        {
            foreach (StyleSheet item in StyleSheet.GetAll())
            {
                SaveToDisk(item);
            }
        }

        public static void ReadAllFromDisk()
        {

            string path = IOHelper.MapPath(string.Format("{0}{1}",
                helpers.uSyncIO.RootFolder,
                "StyleSheet" )) ;

            ReadFromDisk(path); 
        }

        public static void ReadFromDisk(string path)
        {
            if (Directory.Exists(path))
            {
                User user = new User(0); 

                foreach (string file in Directory.GetFiles(path, "*.config"))
                {
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(file);

                    XmlNode node = xmlDoc.SelectSingleNode("//Stylesheet");

                    if (node != null)
                    {
                        StyleSheet s = StyleSheet.Import(node, user );
                        s.Save();
                    }
                }
            }


        }

        public static void AttachEvents()
        {
            StyleSheet.AfterSave += StyleSheet_AfterSave;
            StyleSheet.BeforeDelete += StyleSheet_BeforeDelete;

        }

        static void StyleSheet_BeforeDelete(StyleSheet sender, DeleteEventArgs e)
        {
            helpers.XmlDoc.ArchiveFile(sender.GetType().ToString(), sender.Text);

            e.Cancel = false;
        }
        

        static void StyleSheet_AfterSave(StyleSheet sender, SaveEventArgs e)
        {
            SaveToDisk(sender); 
        }
    }
}