using jumps.umbraco.usync.helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using umbraco.cms.businesslogic.media;

using Umbraco.Core;

namespace jumps.umbraco.usync.Models
{
    public static class uMediaType
    {
        public static XElement SyncExport(this MediaType item)
        {
            XmlDocument xmlDoc = helpers.XmlDoc.CreateDoc();
            xmlDoc.AppendChild(MediaTypeHelper.ToXml(xmlDoc, item));
            return XElement.Load(new XmlNodeReader(xmlDoc));
        }

        public static ChangeItem SyncImport(XElement node, bool postCheck = true)
        {
            var change = new ChangeItem
            {
                itemType = ItemType.MediaItem,
                changeType = ChangeType.Success,
                name = node.Element("Info").Element("Alias").Value
            };

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(node.ToString());

            XmlNode xmlNode = xmlDoc.SelectSingleNode("//MediaType");

            MediaTypeHelper.Import(xmlNode, false);

            return change;
        }

        public static ChangeItem SyncImportFitAndFix(XElement node, bool postCheck = true)
        {
            var change = new ChangeItem
            {
                itemType = ItemType.MediaItem,
                changeType = ChangeType.Success,
                name = node.Element("Info").Element("Alias").Value
            };

            var contentService = ApplicationContext.Current.Services.ContentTypeService;
            var alias = node.Element("Info").Element("Alias").Value;

            var item = contentService.GetMediaType(alias);
            if (item != null)
            {
                MediaTypeHelper.SyncImportFitAndFix(item, node, true);
                contentService.Save(item);
            }

            if ( postCheck && tracker.MediaTypeChanged(node))
            {
                change.changeType = ChangeType.Mismatch;
            }


            return change; 
        }
    }
}
