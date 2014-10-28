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

            var node = XElement.Load(new XmlNodeReader(xmlDoc));
            node = FixProperies(item, node);
            node = GetTabSortOrder(item, node);

            return node;
        }

        internal static XElement FixProperies(global::umbraco.cms.businesslogic.ContentType item, XElement node)
        {
            var props = node.Element("GenericProperties");
            if (props == null)
                return node;
            
            props.RemoveAll();

            foreach (var property in item.PropertyTypes.OrderBy(x => x.Name))
            {
                XElement prop = new XElement("GenericProperty");

                if ( property.Name != null )
                    prop.Add(new XElement("Name", property.Name));


                if ( property.Alias != null )
                    prop.Add(new XElement("Alias", property.Alias));

                if (property.DataTypeDefinition != null)
                {
                    prop.Add(new XElement("Type", property.DataTypeDefinition.DataType.Id.ToString()));
                    prop.Add(new XElement("Definition", property.DataTypeDefinition.UniqueId.ToString()));
                }

                var tab = item.PropertyTypeGroups.Where(x => x.Id == property.PropertyTypeGroup ).FirstOrDefault();
                if (tab != null)
                    prop.Add(new XElement("Tab",tab.Name));

                prop.Add(new XElement("Mandatory", property.Mandatory));

                if ( property.ValidationRegExp != null)
                    prop.Add(new XElement("Validation", property.ValidationRegExp));

                if ( property.Description != null)
                    prop.Add(new XElement("Description", new XCData(property.Description)));

                prop.Add(new XElement("SortOrder", property.SortOrder));

                props.Add(prop);
            }
            return node;
        }

        
        internal static XElement GetTabSortOrder(global::umbraco.cms.businesslogic.ContentType item, XElement node)
        {
            var tabNode = node.Element("Tabs");

            if ( tabNode != null )
            {
                tabNode.RemoveAll();
            }
            foreach(var tab in item.PropertyTypeGroups.OrderBy(x => x.SortOrder))
            {
                var t = new XElement("Tab");
                t.Add(new XElement("Id", tab.Id));
                t.Add(new XElement("Caption", tab.Name));
                t.Add(new XElement("SortOrder", tab.SortOrder));

                tabNode.Add(t);
            }

            return node;
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

                // basic stuff (like name)
                item.Description = node.Element("Info").Element("Description").Value;
                item.Thumbnail = node.Element("Info").Element("Thumbnail").Value;

                ImportStructure(item, node);

                ImportTemplates(item, node);

                RemoveMissingProperties(item, node);

                // tab sort order
                TabSortOrder(item, node);

                UpdateExistingProperties(item, node);

                ApplicationContext.Current.Services.ContentTypeService.Save(item);

                if ( postCheck && tracker.DocTypeChanged(node) )
                {
                    change.changeType = ChangeType.Mismatch;
                }

            }
            return change; 
        }

        internal static void ImportStructure(IContentTypeBase item, XElement node, string structureType = "DocumentType")
        {
            XElement structure = node.Element("Structure");

            List<ContentTypeSort> allowed = new List<ContentTypeSort>();
            int sortOrder = 0;

            foreach (var doc in structure.Elements(structureType))
            {
                string alias = doc.Value;
                IContentTypeBase aliasDoc = null;
                if (structureType == "DocumentType")
                {
                    aliasDoc = ApplicationContext.Current.Services.ContentTypeService.GetContentType(alias);
                }
                else
                {
                    aliasDoc = ApplicationContext.Current.Services.ContentTypeService.GetMediaType(alias);
                }

                if (aliasDoc != null)
                {
                    allowed.Add(new ContentTypeSort(new Lazy<int>(() => aliasDoc.Id), sortOrder, aliasDoc.Name));
                    sortOrder++;
                }
            }
            item.AllowedContentTypes = allowed;
        }

        internal static void ImportTemplates(IContentType item, XElement node)
        {
            XElement templates = node.Element("Info").Element("AllowedTemplates");

            if (templates == null)
                return ;

            List<ITemplate> allowedTemplates = new List<ITemplate>();

            foreach(var template in templates.Elements("Template"))
            {
                string name = template.Value;
                if (!string.IsNullOrEmpty(name))
                {
                    var itemplate = ApplicationContext.Current.Services.FileService.GetTemplate(template.Value);
                    if (itemplate != null)
                        allowedTemplates.Add(itemplate);
                }
            }

            item.AllowedTemplates = allowedTemplates;
        }

        internal static void TabSortOrder(IContentTypeBase docType, XElement node)
        {
            XElement tabs = node.Element("Tabs");

            if (tabs == null)
                return;

            foreach (var tab in tabs.Elements("Tab"))
            {
                var caption = tab.Element("Caption").Value;

                if (tab.Element("SortOrder") != null)
                {
                    var sortOrder = tab.Element("SortOrder").Value;
                    docType.PropertyGroups[caption].SortOrder = int.Parse(sortOrder);
                }
            }

            // ToDo: Delete tabs that have gone? 
            List<string> tabsToRemove = new List<string>();
            foreach(var tab in docType.PropertyGroups)
            {
                var tabE = tabs.Elements("Tab").Where(x => x.Element("Caption").Value == tab.Name).FirstOrDefault();
                if (tabE == null)
                {
                    // delete the tab? 
                    tabsToRemove.Add(tab.Name);
                }
            }

            foreach(var tab in tabsToRemove)
            {
                docType.RemovePropertyGroup(tab);
                LogHelper.Info<SyncDocType>("Will we remove this tab? {0}", () => tab);
            }
        }

        internal static void RemoveMissingProperties(IContentTypeBase docType, XElement node)
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

        internal static void UpdateExistingProperties(IContentTypeBase docType, XElement node)
        {
            LogHelper.Info<SyncMediaTypes>("Update Properties: 1");
            Dictionary<string, string> tabMoves = new Dictionary<string, string>();

            foreach (var property in docType.PropertyTypes)
            {
                XElement propNode = node.Element("GenericProperties")
                                        .Elements("GenericProperty")
                                        .Where(x => x.Element("Alias").Value == property.Alias)
                                        .SingleOrDefault();
                if (propNode != null)
                {
                    if (propNode.Element("Name") != null)
                        property.Name = propNode.Element("Name").Value;

                    if (propNode.Element("Alias") != null)
                        property.Alias = propNode.Element("Alias").Value;

                    if (propNode.Element("Mandatory") != null)
                        property.Mandatory = bool.Parse(propNode.Element("Mandatory").Value);

                    if (propNode.Element("Validation") != null)
                        property.ValidationRegExp = propNode.Element("Validation").Value;

                    if (propNode.Element("Description") != null)
                        property.Description = propNode.Element("Description").Value;

                    // media can't do sort ? 
                    if (propNode.Element("SortOrder") != null)
                        property.SortOrder = int.Parse(propNode.Element("SortOrder").Value);

                    // change of type ? 
                    var defId = Guid.Parse(propNode.Element("Definition").Value);
                    var dtd = ApplicationContext.Current.Services.DataTypeService.GetDataTypeDefinitionById(defId);
                    if (dtd != null && property.DataTypeDefinitionId != dtd.Id)
                    {
                        property.DataTypeDefinitionId = dtd.Id;
                    }
                    LogHelper.Info<SyncMediaTypes>("Update Properties: 4");

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
                    LogHelper.Info<SyncMediaTypes>("Update Properties: 5");

                }
            }
            LogHelper.Info<SyncMediaTypes>("Update Properties: 6");

            // you have to move tabs outside the loop as you are 
            // chaning the collection. 
            foreach (var move in tabMoves)
            {
                LogHelper.Info<SyncMediaTypes>("Update Properties: 7");

                docType.MovePropertyType(move.Key, move.Value);
                LogHelper.Info<SyncMediaTypes>("Update Properties: 7.1");

            }

            LogHelper.Info<SyncMediaTypes>("Update Properties: Finish");

        }

        internal static ChangeItem Delete(string path, bool reportOnly = false)
        {
            var name = System.IO.Path.GetFileName(path);

            var change = ChangeItem.DeleteStub(name, ItemType.DocumentType);
            var item = DocumentType.GetByAlias(name);
            if ( item != null )
            {
                if ( !reportOnly)
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

            var change = ChangeItem.RenameStub(oldName, newName, ItemType.DocumentType);

            var item = DocumentType.GetByAlias(oldName);
            if ( item != null )
            {
                if ( !reportOnly)
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
