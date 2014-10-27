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

            var node = XElement.Load(new XmlNodeReader(xmlDoc));
            node = uDocType.FixProperies(item, node);
            node = uDocType.TabSortOrder(item, node);

            return node;
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

        internal static ChangeItem Delete(string path, bool reportOnly = false)
        {
            var change = ChangeItem.DeleteStub(path, ItemType.MediaItem);

            return change;
        }

        internal static ChangeItem Rename(string oldPath, string newPath, bool reportOnly = false)
        {
            var change = ChangeItem.RenameStub(oldPath, newPath, ItemType.MediaItem);

            return change;
        }
    }
}
