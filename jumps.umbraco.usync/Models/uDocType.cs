using jumps.umbraco.usync.helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using umbraco.cms.businesslogic.web;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;

namespace jumps.umbraco.usync.Models
{
    public static class uDocType
    {
        public static XElement SyncExport(this DocumentType item)
        {
            XmlDocument xmlDoc = helpers.XmlDoc.CreateDoc();
            xmlDoc.AppendChild(item.ToXml(xmlDoc));

            return XElement.Load(new XmlNodeReader(xmlDoc));
        }

        public static ChangeItem SyncImport(XElement node, bool postCheck = true)
        {
            var change = new ChangeItem
            {
                itemType = ItemType.DocumentType,
                changeType = ChangeType.Success,
                name = node.Element("Info").Element("Name").Value
            };

            ApplicationContext.Current.Services.PackagingService.ImportContentTypes(node, false);

            return change;
        }

        public static ChangeItem SyncImportFitAndFix(IContentType item, XElement node, bool postCheck = true)
        {
            var change = new ChangeItem
            {
                itemType = ItemType.DocumentType,
                changeType = ChangeType.Success,
            };

            if ( item != null )
            {
                change.id = item.Id;
                change.name = item.Name;

                ImportStructure(item, node);

                RemoveMissingProperties(item, node);

                // tab sort order
                // TabSortOrder(item, node);

                UpdateExistingProperties(item, node);

                ApplicationContext.Current.Services.ContentTypeService.Save(item);

                if ( postCheck && tracker.DocTypeChanged(node) )
                {
                    change.changeType = ChangeType.Mismatch;
                }

            }

            return change; 
        }


        private static void ImportStructure(IContentType docType, XElement node)
        {
            XElement structure = node.Element("Structure");

            List<ContentTypeSort> allowed = new List<ContentTypeSort>();
            int sortOrder = 0;

            foreach (var doc in structure.Elements("DocumentType"))
            {
                string alias = doc.Value;
                IContentType aliasDoc = ApplicationContext.Current.Services.ContentTypeService.GetContentType(alias);

                if (aliasDoc != null)
                {
                    allowed.Add(new ContentTypeSort(new Lazy<int>(() => aliasDoc.Id), sortOrder, aliasDoc.Name));
                    sortOrder++;
                }
            }

            docType.AllowedContentTypes = allowed;
        }

        private static void TabSortOrder(IContentType docType, XElement node)
        {
            XElement tabs = node.Element("tabs");

            foreach (var tab in tabs.Elements("tab"))
            {
                var caption = tab.Element("Caption").Value;

                if (tab.Element("SortOrder") != null)
                {
                    var sortOrder = tab.Element("SortOrder").Value;
                    docType.PropertyGroups[caption].SortOrder = int.Parse(sortOrder);
                }
            }
        }

        private static void RemoveMissingProperties(IContentType docType, XElement node)
        {
            if (!uSyncSettings.docTypeSettings.DeletePropertyValues)
            {
                LogHelper.Debug<SyncDocType>("DeletePropertyValue = false - exiting");
                return;
            }

            List<string> propertiesToRemove = new List<string>();

            foreach (var property in docType.PropertyTypes)
            {
                // is this property in our xml ?
                XElement propertyNode = node.Element("GenericProperties")
                                            .Elements("GenericProperty")
                                            .Where(x => x.Element("Alias").Value == property.Alias)
                                            .SingleOrDefault();

                if (propertyNode == null)
                {
                    // delete it from the doctype ? 
                    propertiesToRemove.Add(property.Alias);
                    LogHelper.Debug<SyncDocType>("Removing property {0} from {1}",
                        () => property.Alias, () => docType.Name);

                }
            }

            foreach (string alias in propertiesToRemove)
            {
                docType.RemovePropertyType(alias);
            }
        }

        private static void UpdateExistingProperties(IContentType docType, XElement node)
        {
            Dictionary<string, string> tabMoves = new Dictionary<string, string>();

            foreach (var property in docType.PropertyTypes)
            {
                XElement propNode = node.Element("GenericProperties")
                                        .Elements("GenericProperty")
                                        .Where(x => x.Element("Alias").Value == property.Alias)
                                        .SingleOrDefault();
                if (propNode != null)
                {
                    property.Name = propNode.Element("Name").Value;
                    property.Alias = propNode.Element("Alias").Value;
                    property.Mandatory = bool.Parse(propNode.Element("Mandatory").Value);
                    property.ValidationRegExp = propNode.Element("Validation").Value;
                    property.Description = propNode.Element("Description").Value;

                    // change of type ? 
                    var defId = Guid.Parse(propNode.Element("Definition").Value);
                    var dtd = ApplicationContext.Current.Services.DataTypeService.GetDataTypeDefinitionById(defId);
                    if (dtd != null && property.DataTypeDefinitionId != dtd.Id)
                    {
                        property.DataTypeDefinitionId = dtd.Id;
                    }

                    var tabName = propNode.Element("Tab").Value;
                    if (!string.IsNullOrEmpty(tabName))
                    {
                        if (docType.PropertyGroups.Contains(tabName))
                        {
                            var propGroup = docType.PropertyGroups.First(x => x.Name == tabName);
                            if (!propGroup.PropertyTypes.Contains(property.Alias))
                            {
                                LogHelper.Info<SyncDocType>("Moving between tabs..");
                                tabMoves.Add(property.Alias, tabName);
                            }
                        }
                    }
                }
            }

            // you have to move tabs outside the loop as you are 
            // chaning the collection. 
            foreach (var move in tabMoves)
            {
                docType.MovePropertyType(move.Key, move.Value);
            }

        }

    }
}
