﻿using jumps.umbraco.usync.helpers;
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
            LogHelper.Debug<SyncDocType>("export: {0}", ()=> item.Alias);

            XmlDocument xmlDoc = helpers.XmlDoc.CreateDoc();
            xmlDoc.AppendChild(item.ToXml(xmlDoc));

            var node = XElement.Load(new XmlNodeReader(xmlDoc));
            node = FixProperies(item, node);
            node = GetTabSortOrder(item, node);

            return node;
        }

        internal static XElement FixProperies(global::umbraco.cms.businesslogic.ContentType item, XElement node)
        {
            LogHelper.Debug<SyncDocType>("export: fixing doctypes");
            var props = node.Element("GenericProperties");
            if (props == null)
                return node;
            
            props.RemoveAll();

            foreach (var property in item.PropertyTypes.OrderBy(x => x.Name))
            {
                LogHelper.Debug<SyncDocType>("Adding property: {0}", () => property.Name);

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
                    prop.Add(new XElement("Tab", tab.Name));
                else
                    prop.Add(new XElement("Tab", ""));

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
            LogHelper.Debug<SyncDocType>("Export: Fixing the sort order of tabs");

            var tabNode = node.Element("Tabs");

            if ( tabNode != null )
            {
                tabNode.RemoveAll();
            }
            foreach(var tab in item.PropertyTypeGroups.OrderBy(x => x.SortOrder))
            {
                LogHelper.Debug<SyncDocType>("Adding tab: {0}", () => tab.Name);
                
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

            try
            {
                LogHelper.Info<SyncDocType>("Import: Calling package service to import");
                ApplicationContext.Current.Services.PackagingService.ImportContentTypes(node, false);
            }
            catch (Exception ex)
            {
                LogHelper.Warn<SyncDocType>("Import via Packaging Service Failed for {0} \n{1}", () => node.Element("Info").Element("Name").Value, ()=> ex.ToString());
                change.changeType = ChangeType.ImportFail;
            }

            return change;
        }

        public static ChangeItem SyncImportFitAndFix(IContentType item, XElement node, bool postCheck = true)
        {
            LogHelper.Info<SyncDocType>("Import: Performing fit and fix on doctype: {0}", () => item.Name);

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
                LogHelper.Debug<SyncDocType>("Saving changes so far: (After Remove Missing Properties): {0}", () => item.Name);
                ApplicationContext.Current.Services.ContentTypeService.Save(item);
                
                // tab sort order
                TabSortOrder(item, node);
                LogHelper.Debug<SyncDocType>("Saving changes so far: (After Tab Sort Order): {0}", () => item.Name);
                ApplicationContext.Current.Services.ContentTypeService.Save(item);

                UpdateExistingProperties(item, node);

                LogHelper.Debug<SyncDocType>("Saving changes so far: (After Update Existing Properties): {0}", () => item.Name);
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
            LogHelper.Debug<SyncDocType>("Importing Structure: {0}", () => item.Name);

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
                    LogHelper.Debug<SyncDocType>("Adding Allowed Type: {0}", () => aliasDoc.Name);
                    allowed.Add(new ContentTypeSort(new Lazy<int>(() => aliasDoc.Id), sortOrder, aliasDoc.Name));
                    sortOrder++;
                }
            }
            item.AllowedContentTypes = allowed;
        }

        internal static void ImportTemplates(IContentType item, XElement node)
        {
            LogHelper.Debug<SyncDocType>("Importing Allowed Templates");

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
                    {
                        LogHelper.Debug<SyncDocType>("Adding Template to allowed list: {0}", () => itemplate.Alias);
                        allowedTemplates.Add(itemplate);
                    }
                }
            }

            item.AllowedTemplates = allowedTemplates;
        }

        internal static void TabSortOrder(IContentTypeBase docType, XElement node)
        {
            LogHelper.Debug<SyncDocType>("Import: Fixing tab sort order");

            XElement tabs = node.Element("Tabs");

            if (tabs == null)
                return;

            foreach (var tab in tabs.Elements("Tab"))
            {
                var caption = tab.Element("Caption").Value;

                if (tab.Element("SortOrder") != null)
                {
                    var sortOrder = tab.Element("SortOrder").Value;
                    LogHelper.Debug<SyncDocType>("Setting sort order: {0} to {1}", () => caption, () => sortOrder);
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
                LogHelper.Info<SyncDocType>("Removing Tab {0}", () => tab);
                docType.RemovePropertyGroup(tab);
            }
        }

        internal static void RemoveMissingProperties(IContentTypeBase docType, XElement node)
        {
            LogHelper.Debug<SyncDocType>("Removing missing properties from doctype: {0}", () => docType.Alias);

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
            LogHelper.Debug<SyncDocType>("Updating existing properties in doctype: {0}", () => docType.Alias);

            Dictionary<string, string> tabMoves = new Dictionary<string, string>();

            foreach (var property in docType.PropertyTypes)
            {
                XElement propNode = node.Element("GenericProperties")
                                        .Elements("GenericProperty")
                                        .Where(x => x.Element("Alias").Value == property.Alias)
                                        .SingleOrDefault();
                if (propNode != null)
                {
                    LogHelper.Debug<SyncDocType>("Updating values in property: {0}", ()=> property.Alias);

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
                        LogHelper.Debug<SyncDocType>("Property type change {0}", () => dtd.Id);
                        property.DataTypeDefinitionId = dtd.Id;
                    }

                    var tab = propNode.Element("Tab");
                    if (tab != null)
                    {
                        var tabName = propNode.Element("Tab").Value;
                        if (!string.IsNullOrEmpty(tabName))
                        {
                            if (docType.PropertyGroups.Contains(tabName))
                            {
                                var propGroup = docType.PropertyGroups.First(x => x.Name == tabName);
                                if (!propGroup.PropertyTypes.Contains(property.Alias))
                                {
                                    tabMoves.Add(property.Alias, tabName);
                                }
                            }
                        }
                    }

                }
            }

            // you have to move tabs outside the loop as you are 
            // chaning the collection. 
            foreach (var move in tabMoves)
            {
                LogHelper.Debug<SyncDocType>("Moving Property between tabs: {0}", ()=> move.Key, () => move.Value);
                docType.MovePropertyType(move.Key, move.Value);
            }
        }

        internal static ChangeItem Delete(string path, bool reportOnly = false)
        {
            LogHelper.Debug<SyncDocType>("Deleting {0}", () => path);
            var name = System.IO.Path.GetFileName(path);

            var change = ChangeItem.DeleteStub(name, ItemType.DocumentType);
            var item = DocumentType.GetByAlias(name);
            if ( item != null )
            {
                if ( !reportOnly)
                {
                    LogHelper.Debug<SyncDocType>("Removing from Umbraco: {0}", () => item.Alias);
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
