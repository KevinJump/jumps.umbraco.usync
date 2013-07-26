using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using umbraco.cms.businesslogic.language;

using System.Xml;
using System.IO;
using Umbraco.Core.IO;

namespace jumps.umbraco.usync
{
    public class SyncLanguage
    {
        public static void SaveToDisk(Language item)
        {
            if (item != null)
            {
                XmlDocument xmlDoc = helpers.XmlDoc.CreateDoc(); 
                xmlDoc.AppendChild(item.ToXml(xmlDoc));
                helpers.XmlDoc.SaveXmlDoc(item.GetType().ToString(), item.CultureAlias, xmlDoc) ; 
            }
        }

        public static void SaveAllToDisk()
        {
            helpers.uSyncLog.DebugLog(">>>> Language save all to disk");
            foreach (Language item in Language.GetAllAsList())
            {
                helpers.uSyncLog.DebugLog(">>>> {0} <<<<<", item.CultureAlias);
                SaveToDisk(item);
            }
        }

        public static void ReadAllFromDisk()
        {
            string path = IOHelper.MapPath(String.Format("{0}{1}",
                helpers.uSyncIO.RootFolder,
                "Language"));

            ReadFromDisk(path);
        }

        public static void ReadFromDisk(string path)
        {
            helpers.uSyncLog.DebugLog("Reading from disk {0}", path); 
            if (Directory.Exists(path))
            {
                foreach (string file in Directory.GetFiles(path, "*.config"))
                {
                    helpers.uSyncLog.DebugLog("Reading file {0} from disk", file); 

                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(file);

                    helpers.uSyncLog.DebugLog("XML Loaded"); 

                    XmlNode node = xmlDoc.SelectSingleNode("//Language");

                    helpers.uSyncLog.DebugLog("Node Found"); 

                    if (node != null)
                    {
                        helpers.uSyncLog.DebugLog("About to Load Language {0}", node.OuterXml); 
                        Language l = Language.Import(node);

                        if (l != null)
                        {
                            l.Save();
                        }
                    }
                    helpers.uSyncLog.DebugLog("Language done"); 
                }
            }
        }

        public static void AttachEvents()
        {
            Language.New += Language_New;
            Language.AfterSave += Language_AfterSave;
            Language.AfterDelete += Language_AfterDelete;
        }

        static void Language_New(Language sender, global::umbraco.cms.businesslogic.NewEventArgs e)
        {
            SaveToDisk(sender); 
        }

        static void Language_AfterDelete(Language sender, global::umbraco.cms.businesslogic.DeleteEventArgs e)
        {
            helpers.XmlDoc.ArchiveFile(sender.GetType().ToString(), sender.CultureAlias);

        }

        static void Language_AfterSave(Language sender, global::umbraco.cms.businesslogic.SaveEventArgs e)
        {
            SaveToDisk(sender); 
        }
    }
}
