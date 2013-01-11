using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml;
using umbraco.cms.businesslogic.datatype ;

namespace jumps.umbraco.usync
{
    /// <summary>
    /// syncs the data types.
    /// </summary>
    public class SyncDataType
    {
        public static void SaveToDisk(DataTypeDefinition item)
        {
            XmlDocument xmlDoc = helpers.XmlDoc.CreateDoc() ; 
            xmlDoc.AppendChild(item.ToXml(xmlDoc));
            helpers.XmlDoc.SaveXmlDoc(item.GetType().ToString(), item.Text, xmlDoc); 
        }

        public static void SaveAllToDisk()
        {
            foreach (DataTypeDefinition item in DataTypeDefinition.GetAll())
            {
                SaveToDisk(item);
            }
        }

        public static void AttachEvents()
        {
            DataTypeDefinition.AfterSave += DataTypeDefinition_AfterSave;
            DataTypeDefinition.AfterDelete += DataTypeDefinition_AfterDelete;
        }

        static void DataTypeDefinition_AfterDelete(object sender, global::umbraco.cms.businesslogic.DeleteEventArgs e)
        {
            // TODO: Archive on delete  ?
        }
        
        static void DataTypeDefinition_AfterSave(object sender, global::umbraco.cms.businesslogic.SaveEventArgs e)
        {
            // System.Web.HttpContext.Current.Response.Write(sender.GetType().ToString()); 

            if ( sender.GetType() == typeof(DataTypeDefinition) )
            {
                SaveToDisk((DataTypeDefinition)sender);
            }
        }
    }
}
