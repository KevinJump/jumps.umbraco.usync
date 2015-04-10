using jumps.umbraco.usync.helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using umbraco.cms.businesslogic.media;

using Umbraco.Core;
using Umbraco.Core.Logging;

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
            node = uDocType.GetTabSortOrder(item, node);

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
            LogHelper.Info<SyncMediaTypes>("Sync Import Fit and Fix");
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

        private static MediaType FindMediaTypeByPath(string path)
        {
            var pathBits = path.Split('/');
            try
            {
                var doc = MediaType.GetByAlias(pathBits[pathBits.Length - 1]);

                if (doc != null)
                    return doc;
            }
            catch(Exception ex)
            {
                LogHelper.Warn<SyncMediaTypes>("Failed to find media type (and caught umbraco exception)");
            }

            return null;
        }

        internal static ChangeItem Delete(string path, bool reportOnly = false)
        {
            var name = System.IO.Path.GetFileName(path);

            var change = ChangeItem.DeleteStub(name, ItemType.MediaItem);
            var media = ApplicationContext.Current.Services.ContentTypeService.GetMediaType(name);
            if (media == null)
                return change;

            var item = new MediaType(media.Id);
            if (item != null)
            {
                if (!reportOnly)
                {
                    item.delete();
                    item.Save();
                    change.changeType = ChangeType.Delete;
                }
                else
                {
                    change.changeType = ChangeType.WillChange;
                }
            }

            return change;
        }

        internal static ChangeItem Rename(string oldPath, string newPath, bool reportOnly = false)
        {
            var oldName = System.IO.Path.GetFileName(oldPath);
            var newName = System.IO.Path.GetFileName(newPath);

            var change = ChangeItem.RenameStub(oldName, newName, ItemType.MediaItem);

            var media = ApplicationContext.Current.Services.ContentTypeService.GetMediaType(oldName);
            if (media == null)
                return change;

            var item = new MediaType(media.Id);
            if (item != null)
            {
                if (!reportOnly)
                {
                    LogHelper.Info<SyncDocType>("Renaming : {0} to {1}", () => oldName, () => newName);

                    change.changeType = ChangeType.Success;
                    item.Alias = newName;
                    item.Save();
                }
                else
                {
                    change.changeType = ChangeType.WillChange;
                }
            }

            return change;
        }

    }
}
