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
using Umbraco.Core.Logging;

using umbraco;

namespace jumps.umbraco.usync
{
    /// <summary>
    /// Sycronizes stylesheets to the uSync folder. 
    /// 
    /// stylesheets are mainly arealy on the disk, the database
    /// contains an ID for each one - it's only ever used in
    /// rich text data type (i think).
    /// 
    /// SyncStylesheet class uses the packaging API to read and
    /// write the styles sheets to disk. 
    /// 
    /// probibly the simplest sync - no structure, and the
    /// packaging api.
    /// </summary>
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
                    helpers.uSyncLog.ErrorLog(ex, "uSync: Error Reading Stylesheet {0} - {1}", item.Text, ex.ToString());
                    throw new SystemException(string.Format("error saving stylesheet {0}", item.Text), ex); 
                }
            }
        }

        public static void SaveAllToDisk()
        {
            try
            {
                foreach (StyleSheet item in StyleSheet.GetAll())
                {
                    SaveToDisk(item);
                }
            }
            catch (Exception ex)
            {
                helpers.uSyncLog.ErrorLog(ex, "uSync: Error Saving all Stylesheets {0}", ex.ToString());
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
                        StyleSheet s = StyleSheet.Import(node, user); // <--- this is the slowest place in uSync 
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