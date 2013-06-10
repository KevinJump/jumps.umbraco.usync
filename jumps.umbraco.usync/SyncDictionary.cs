using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using umbraco.cms.businesslogic;
using System.Xml;
using System.IO;
using Umbraco.Core.IO; 
using umbraco.BusinessLogic;



namespace jumps.umbraco.usync
{
    public class SyncDictionary
    {
        public static void SaveToDisk(Dictionary.DictionaryItem item)
        {
            if (item != null)
            {
                XmlDocument xmlDoc = helpers.XmlDoc.CreateDoc();
                xmlDoc.AppendChild(item.ToXml(xmlDoc));
                helpers.XmlDoc.SaveXmlDoc("Dictionary", item.key, xmlDoc);
            }
        }

        public static void SaveAllToDisk()
        {
            helpers.uSyncLog.DebugLog("Saving Dictionary Types");

            foreach (Dictionary.DictionaryItem item in Dictionary.getTopMostItems)
            {
                helpers.uSyncLog.DebugLog("Dictionary Item {0}", item.key);
                SaveToDisk(item);
            }
        }

        public static void ReadAllFromDisk()
        {
            string path = IOHelper.MapPath(string.Format("{0}{1}",
                helpers.uSyncIO.RootFolder,
                "Dictionary"));

            ReadFromDisk(path);

        }

        public static void ReadFromDisk(string path)
        {
            if (Directory.Exists(path))
            {
                foreach (string file in Directory.GetFiles(path, "*.config"))
                {
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(file);

                    XmlNode node = xmlDoc.SelectSingleNode("//DictionaryItem");

                    if (node != null)
                    {
                        Dictionary.DictionaryItem item = Dictionary.DictionaryItem.Import(node);
                        // item.(); 
                    }
                }
            }
        }

        public static void AttachEvents()
        {
            Dictionary.DictionaryItem.Saving += DictionaryItem_Saving;
            Dictionary.DictionaryItem.Deleting += DictionaryItem_Deleting;
        }

        static void DictionaryItem_Deleting(Dictionary.DictionaryItem sender, EventArgs e)
        {
            if (!sender.IsTopMostItem())
            {
                // if it's not top most, we save it's parent (that will delete)
                SaveToDisk(GetTop(sender));
            }
            else
            {
                // it's top we need to delete
                helpers.XmlDoc.ArchiveFile("Dictionary", sender.key);

            }
        }


        static void DictionaryItem_Saving(Dictionary.DictionaryItem sender, EventArgs e)
        {
            SaveToDisk(GetTop(sender));
        }

        private static Dictionary.DictionaryItem GetTop(Dictionary.DictionaryItem item)
        {
            if (!item.IsTopMostItem())
                return GetTop(item.Parent);
            else
                return item; 
        }
    }
}
