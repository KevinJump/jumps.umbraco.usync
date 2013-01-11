using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml ;
using umbraco.cms.businesslogic;
using umbraco.cms.businesslogic.template; 

namespace jumps.umbraco.usync
{
    public class SyncTemplate
    {
        public static void SaveToDisk(Template item)
        {
            XmlDocument xmlDoc = helpers.XmlDoc.CreateDoc();
            xmlDoc.AppendChild(item.ToXml(xmlDoc));
            helpers.XmlDoc.SaveXmlDoc(
                item.GetType().ToString() + GetDocPath(item), 
                item.Text, 
                xmlDoc);
        }

        public static void SaveAllToDisk()
        {
            foreach (Template item in Template.GetAllAsList().ToArray())
            {
                SaveToDisk(item);                
            }
        }

        private static string GetDocPath(Template item)
        {
            string path = "";
            if (item.MasterTemplate != 0)
            {
                path = GetDocPath(new Template(item.MasterTemplate));
            }

            path = string.Format("{0}//{1}", path, helpers.XmlDoc.ScrubFile(item.Text));

            return path; 


        }

        public static void AttachEvents()
        {
            Template.AfterSave += Template_AfterSave;
            Template.AfterDelete += Template_AfterDelete;

        }

        static void Template_AfterDelete(Template sender, DeleteEventArgs e)
        {
            
        }

        static void Template_AfterSave(Template sender, SaveEventArgs e)
        {
            // save
            SaveToDisk(sender); 
        }
        
    }
}
