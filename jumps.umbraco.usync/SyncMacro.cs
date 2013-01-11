using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml ;
using umbraco.cms.businesslogic; 
using umbraco.cms.businesslogic.macro ;

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

        public static void AttachEvents()
        {
            Macro.AfterSave += Macro_AfterSave;
            Macro.AfterDelete += Macro_AfterDelete;
        }

        static void Macro_AfterDelete(Macro sender, DeleteEventArgs e)
        {
            throw new NotImplementedException();
        }

        static void Macro_AfterSave(Macro sender, SaveEventArgs e)
        {
            SaveToDisk(sender); 
        }


    }
}
