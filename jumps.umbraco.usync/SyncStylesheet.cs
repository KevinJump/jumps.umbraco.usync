using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml;
using umbraco.cms.businesslogic;
using umbraco.cms.businesslogic.web;

namespace jumps.umbraco.usync
{
    public class SyncStylesheet
    {
        public static void SaveToDisk(StyleSheet item)
        {
            XmlDocument xmlDoc = helpers.XmlDoc.CreateDoc();
            xmlDoc.AppendChild(item.ToXml(xmlDoc));
            helpers.XmlDoc.SaveXmlDoc(item.GetType().ToString(), item.Text, xmlDoc);
        }

        public static void SaveAllToDisk()
        {
            foreach (StyleSheet item in StyleSheet.GetAll())
            {
                SaveToDisk(item);
            }
        }

        public static void AttachEvents()
        {
            StyleSheet.AfterSave += StyleSheet_AfterSave;
            StyleSheet.BeforeDelete += StyleSheet_BeforeDelete;

        }

        static void StyleSheet_BeforeDelete(StyleSheet sender, DeleteEventArgs e)
        {
            throw new NotImplementedException();
        }

        static void StyleSheet_AfterSave(StyleSheet sender, SaveEventArgs e)
        {
            SaveToDisk(sender); 
        }
    }
}