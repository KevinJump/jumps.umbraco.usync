using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml; 
using System.IO ; 

using Umbraco.Core ; 
using umbraco.cms.businesslogic; 
using umbraco.cms.businesslogic;
using umbraco.cms.businesslogic.media ;
using umbraco.cms.businesslogic.propertytype;
using umbraco.DataLayer;


namespace jumps.umbraco.usync
{
    /*
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
     */

    public class uSyncMediaType
    {
        public static XmlElement MediaToXML(XmlDocument xd, MediaType mt)
        {
            XmlElement doc = xd.CreateElement("MediaType") ; 

            XmlElement info = xd.CreateElement("Info") ; 
            doc.AppendChild(info);
            info.AppendChild(XmlHelper.AddTextNode(xd, "Name", mt.Text)) ; 
            info.AppendChild(XmlHelper.AddTextNode(xd, "Alias", mt.Alias)) ; 
            info.AppendChild(XmlHelper.AddTextNode(xd, "Icon", mt.IconUrl));
            info.AppendChild(XmlHelper.AddTextNode(xd, "Thumbnail", mt.Thumbnail));
            info.AppendChild(XmlHelper.AddTextNode(xd, "Description", mt.Description));

            XmlElement structure = xd.CreateElement("Structure") ;

            foreach( int cc in mt.AllowedChildContentTypeIDs.ToList() )
            {
                structure.AppendChild(XmlHelper.AddTextNode(xd, "MediaType", new MediaType(cc).Alias ) ) ;
            }

           // generic properties
            XmlElement pts = xd.CreateElement("GenericProperties");
            foreach (PropertyType pt in mt.PropertyTypes)
            {
                //only add properties that aren't from master doctype
                if (pt.ContentTypeId == mt.Id)
                {
                    XmlElement ptx = xd.CreateElement("GenericProperty");
                    ptx.AppendChild(XmlHelper.AddTextNode(xd, "Name", pt.Name));
                    ptx.AppendChild(XmlHelper.AddTextNode(xd, "Alias", pt.Alias));
                    ptx.AppendChild(XmlHelper.AddTextNode(xd, "Type", pt.DataTypeDefinition.DataType.Id.ToString()));

                    //Datatype definition guid was added in v4 to enable datatype imports
                    ptx.AppendChild(XmlHelper.AddTextNode(xd, "Definition", pt.DataTypeDefinition.UniqueId.ToString()));

                    ptx.AppendChild(XmlHelper.AddTextNode(xd, "Tab", ContentType.Tab.GetCaptionById(pt.TabId)));
                    ptx.AppendChild(XmlHelper.AddTextNode(xd, "Mandatory", pt.Mandatory.ToString()));
                    ptx.AppendChild(XmlHelper.AddTextNode(xd, "Validation", pt.ValidationRegExp));
                    ptx.AppendChild(XmlHelper.AddCDataNode(xd, "Description", pt.Description));
                    pts.AppendChild(ptx);
                }
            }
            doc.AppendChild(pts);

            //tabs
            XmlElement tabs = xd.CreateElement("Tabs");
            foreach ( ContentType.TabI t in mt.getVirtualTabs.ToList())
            {
                //only add tabs that aren't from a master doctype
                if (t.ContentType == mt.Id)
                {
                    XmlElement tabx = xd.CreateElement("Tab");
                    tabx.AppendChild(XmlHelper.AddTextNode(xd, "Id", t.Id.ToString()));
                    tabx.AppendChild(XmlHelper.AddTextNode(xd, "Caption", t.Caption));
                    tabs.AppendChild(tabx);
                }
            }
            doc.AppendChild(tabs);
            return doc;


            return doc; 

        }
    }

    /*
        
             // generic properties
      ;

            // tabs
            XmlElement tabs = xd.CreateElement("Tabs");
            foreach (TabI t in getVirtualTabs.ToList())
            {
                //only add tabs that aren't from a master doctype
                if (t.ContentType == this.Id)
                {
                    XmlElement tabx = xd.CreateElement("Tab");
                    tabx.AppendChild(xmlHelper.addTextNode(xd, "Id", t.Id.ToString()));
                    tabx.AppendChild(xmlHelper.addTextNode(xd, "Caption", t.Caption));
                    tabs.AppendChild(tabx);
                }
            }
            doc.AppendChild(tabs);
            return doc;
    */
}
