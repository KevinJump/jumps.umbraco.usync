using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml; 
using System.IO ; 

using umbraco.cms.businesslogic; 
using umbraco.cms.businesslogic.media ;

/* 
 *  Media types don't natively save to XML, you can't save them into packages.
 *  todo is write the xml thing. 
 *  
namespace jumps.umbraco.usync
{
    /// <summary>
    /// Syncs the Media Types in umbraco to the disk
    /// </summary>
    public class SyncMediaTypes
    {
        public static void SaveToDisk(MediaType item)
        {
            XmlDocument xmlDoc = helpers.XmlDoc.CreateDoc();
            xmlDoc.AppendChild(item.ToXml(xmlDoc, false));
            helpers.XmlDoc.SaveXmlDoc(item.GetType().ToString(), item.Text, xmlDoc);
        }

        public static void SaveAllToDisk()
        {
            foreach (MediaType item in MediaType.GetAllAsList())
            {
                SaveToDisk(item);
            }
        }

        public static void ReadAllFromDisk()
        {

        }

        public static void AttachEvents()
        {
            MediaType.AfterSave += MediaType_AfterSave;
        }


        static void MediaType_AfterSave(MediaType sender, SaveEventArgs e)
        {
            SaveToDisk((MediaType)sender); 
        }
    }
}
*/