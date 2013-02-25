using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml;
using umbraco.cms.businesslogic;
using umbraco.cms.businesslogic.datatype ;

using umbraco.BusinessLogic ; 

using System.IO;
using Umbraco.Core.IO; 

//  Check list
// ====================
//  SaveOne         X
//  SaveAll         X
//  OnSave          [BUG ?]
//  OnDelete        X
//  ReadFromDisk    X - first time only ! 

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

        public static void ReadAllFromDisk()
        {
            string path = IOHelper.MapPath(string.Format("{0}{1}",
                helpers.uSyncIO.RootFolder,
                "umbraco.cms.businesslogic.datatype.DataTypeDefinition"));

            ReadFromDisk(path); 
        }

        public static void ReadFromDisk(string path)
        {
            if (Directory.Exists(path))
            {

                User u = new User(0) ; 

                foreach (string file in Directory.GetFiles(path, "*.config"))
                {
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(file);

                    XmlNode node = xmlDoc.SelectSingleNode("//DataType");

                    if (node != null)
                    {
                        DataTypeDefinition d = Import(node, u);
                        if (d != null)
                        {
                            d.Save();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// DataType Import - taken from the core 
        /// 
        /// the core doesn't pass username, so fails on loading
        /// here we just pass usere User(0) - so we can work)
        /// </summary>
        /// <param name="xmlData"></param>
        /// <returns></returns>
        public static DataTypeDefinition Import(XmlNode xmlData, User u)
        {
            string _name = xmlData.Attributes["Name"].Value;
            string _id = xmlData.Attributes["Id"].Value;
            string _def = xmlData.Attributes["Definition"].Value;


            //Make sure that the dtd is not already present
            if (!CMSNode.IsNode(new Guid(_def)) )
            {

                if (u == null)
                    u = global::umbraco.BusinessLogic.User.GetUser(0);

                global::umbraco.cms.businesslogic.datatype.controls.Factory f = new global::umbraco.cms.businesslogic.datatype.controls.Factory();


                DataTypeDefinition dtd = DataTypeDefinition.MakeNew(u, _name, new Guid(_def));
				var dataType = f.DataType(new Guid(_id));
				if (dataType == null)
					throw new NullReferenceException("Could not resolve a data type with id " + _id);

	            dtd.DataType = dataType;
                dtd.Save();

                foreach (XmlNode xmlPv in xmlData.SelectNodes("PreValues/PreValue"))
                {
                    XmlAttribute val = xmlPv.Attributes["Value"];

                    if (val != null)
                    {
                        PreValue p = new PreValue(0, 0, val.Value);
                        p.DataTypeId = dtd.Id;
                        p.Save();
                    }
                }

                return dtd;
            }

            return null;
        }


        public static void AttachEvents()
        {
            // these are not firing...
            DataTypeDefinition.Saving += new DataTypeDefinition.SaveEventHandler(DataTypeDefinition_Saving);
            //DataTypeDefinition.AfterSave += DataTypeDefinition_AfterSave;
            //DataTypeDefinition.BeforeSave += new EventHandler<SaveEventArgs>(DataTypeDefinition_AfterSave);
            
            // but this is 
            DataTypeDefinition.AfterDelete += DataTypeDefinition_AfterDelete;
        }

        public static void DataTypeDefinition_AfterSave(object sender, SaveEventArgs e)
        {
            SaveToDisk((DataTypeDefinition)sender);
        }

        public static void DataTypeDefinition_Saving(DataTypeDefinition sender, EventArgs e)
        {
            SaveToDisk((DataTypeDefinition)sender);
        }

        public static void DataTypeDefinition_AfterDelete(object sender, DeleteEventArgs e)
        {
            helpers.XmlDoc.ArchiveFile(sender.GetType().ToString(), ((DataTypeDefinition)sender).Text);

            e.Cancel = false;
       
        }
        
    }
}
