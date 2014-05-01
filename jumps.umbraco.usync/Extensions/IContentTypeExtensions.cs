using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Xml;
using System.Xml.Linq;

using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Services;

using Umbraco.Core.Logging;

using jumps.umbraco.usync.helpers;

namespace jumps.umbraco.usync.Extensions
{
    /// <summary>
    /// Does the stuff we need to help up export/import content Types
    /// (DocumentTypes in this instance)
    /// </summary>
    public static class IContentTypeExtensions
    {
        static IPackagingService _packageService;
        static IContentTypeService _contentTypeService;

        static IContentTypeExtensions()
        {
            _packageService = ApplicationContext.Current.Services.PackagingService;
            _contentTypeService = ApplicationContext.Current.Services.ContentTypeService;
        }

        public static XElement ExportToXml(this IContentType item)
        {
            XElement element = _packageService.Export(item);

            // some extra stuff (we want)
            // element.Element("Info").Add(new XElement("key", item.Key));
            // element.Element("Info").Add(new XElement("Id", item.Id));
            // element.Element("Info").Add(new XElement("Updated", item.UpdateDate));
            if ( element.Element("Info").Element("Container") == null )
                element.Element("Info").Add(new XElement("Container", item.IsContainer.ToString()));

            // fix the current (v6.1/v7.0.1) api doesn't do
            // structure export proper
            var structure = element.Element("Structure");

            // empty it out. 
            structure.RemoveNodes();

            //
            // order isn't always right, and we care because we get hash values
            // 
            SortedList<int, ContentTypeSort> allowedTypes = new SortedList<int, ContentTypeSort>();
            foreach(var t in item.AllowedContentTypes)
            {
                allowedTypes.Add(t.Id.Value, t);
            }

            foreach(var allowedType in allowedTypes)
            {
                var allowedItem = _contentTypeService.GetContentType(allowedType.Value.Id.Value);
                structure.Add(new XElement("DocumentType", allowedItem.Alias));
                allowedItem.DisposeIfDisposable();
            }
            

            // put the sort order on the tabs
            var tabs = element.Element("Tabs");
            foreach(var tab in item.PropertyGroups)
            {
                XElement tabNode = tabs.Elements().First(x => x.Element("Id").Value == tab.Id.ToString());

                if ( tabNode != null)
                {
                    tabNode.Add(new XElement("SortOrder", tab.SortOrder));
                }
            }

            // put the sort order on the proerties ?
            var properties = element.Element("GenericProperties");
            foreach(var prop in item.PropertyTypes)
            {
                XElement propNode = properties.Elements().First(x => x.Element("Alias").Value == prop.Alias);

                if ( propNode != null )
                {
                    propNode.Add(new XElement("SortOrder", prop.SortOrder));
                }

            }

            return element;
        }

        public static IEnumerable<IContentType> ImportContentType(this XElement node)
        {
            XElement idElement = node.Element("Info").Element("Id");
            IEnumerable<IContentType> imported = _packageService.ImportContentTypes(node, false, raiseEvents: false );
            return imported; 

        }
    

        /*
         * Import Part 2 - these functions all do post import 2nd pass 
         * tidy up stuff.
         */

        public static void ImportStructure(this IContentType item, XElement node)
        {
            XElement structure = node.Element("Structure");

            List<ContentTypeSort> allowed = new List<ContentTypeSort>();
            int sortOrder = 0;

            foreach(var doctype in structure.Elements("DocumentType"))
            {
                string alias = doctype.Value;

                if ( !string.IsNullOrEmpty(alias))
                {
                    IContentType aliasDoc = _contentTypeService.GetContentType(alias);

                    if ( aliasDoc != null )
                    {
                        allowed.Add(new ContentTypeSort(
                            new Lazy<int>(() => aliasDoc.Id), sortOrder, aliasDoc.Name));
                        sortOrder++;
                    }
                }
            }
            item.AllowedContentTypes = allowed;
        }

        public static void ImportTabSortOrder(this IContentType item, XElement node)
        {
            XElement tabs = node.Element("Tabs");

            foreach(var tab in tabs.Elements("Tab"))
            {
                var tabId = int.Parse(tab.Element("Id").Value);
                var sortOrder = tab.Element("SortOrder");

                if ( sortOrder != null)
                {
                    if ( !String.IsNullOrEmpty(sortOrder.Value))
                    {
                        var itemTab = item.PropertyGroups.FirstOrDefault(x => x.Id == tabId);
                        if ( itemTab != null)
                        {
                            itemTab.SortOrder = int.Parse(sortOrder.Value);
                        }
                    }
                }
            }
        }

        public static void ImportRemoveMissingProps(this IContentType item, XElement node)
        {
            // don't do this if the setting is set to false
            if ( !uSyncSettings.docTypeSettings.DeletePropertyValues)
            {
                return;
            }

            List<string> propertiesToRemove = new List<string>();
            Dictionary<string, string> propertiesToMove = new Dictionary<string, string>();

            // go through the properties in the item
            foreach(var property in item.PropertyTypes)
            {
                // is this property in the xml ?
                XElement propertyNode = node.Element("GenericProperties")
                                            .Elements("GenericProperty")
                                            .Where(x => x.Element("Alias").Value == property.Alias)
                                            .SingleOrDefault();

                if (propertyNode == null)
                {
                    LogHelper.Info<uSync>("Removing {0} from {1}", () => property.Alias, () => item.Name);
                    propertiesToRemove.Add(property.Alias);
                }
                else
                {
                    // at this point we write our properties over those 
                    // in the db - because the import doesn't do this 
                    // for existing items.
                    LogHelper.Debug<uSync>("Updating prop {0} for {1}", () => property.Alias, () => item.Alias);


                    /* not sure we need to do this, we just call EditorAlias - it will find it ? */
                    /*
                    var legacyEditorId = Guid.Empty;
                    Guid.TryParse(propertyNode.Element("Type").Value, out legacyEditorId);


                    if ( legacyEditorId == Guid.Empty)
                    {
                        // new style id...?
                    }

                    var dataTypeDefinitionId = new Guid(propertyNode.Element("Definition").Value);
                    IDataTypeService _dataTypeService = ApplicationContext.Current.Services.DataTypeService;

                    var dataTypeDefinition = _dataTypeService.GetDataTypeDefinitionById(dataTypeDefinitionId);
                    */
                    var editorAlias = propertyNode.Element("Type").Value; 

                    IDataTypeService _dataTypeService = ApplicationContext.Current.Services.DataTypeService;
                    var dataTypeDefinition = _dataTypeService.GetDataTypeDefinitionByPropertyEditorAlias(editorAlias).FirstOrDefault();

                    /*
                    if ( dataTypeDefinition != null &&
                         dataTypeDefinition.Key == dataTypeDefinitionId  )
                    {
                        // all good, we are here..
                    }
                    else
                    {
                        // we need to do even more looking...
                        var dataTypeDefinitions = _dataTypeService.GetDataTypeDefinitionByControlId(legacyEditorId);

                        if ( dataTypeDefinition != null && dataTypeDefinitions.Any())
                        {
                            dataTypeDefinition = dataTypeDefinitions.First();
                        }
                    }
                    */

                    /*
                    if ( dataTypeDefinition != null)
                    {
                        // phew we have found what we are looking for.

                        // now we set it in the DB 
                        // property.DataTypeDefinitionId = dataTypeDefinition.Id;

                        // this is wrong, because you can't 
                        // actually change the DataTypeId and that prob
                        // matters when changing a type.

                        // TODO: make changes to the datatype import/export properly. 
                    }
                    */

                    property.Name = propertyNode.Element("Name").Value;
                    property.Description = propertyNode.Element("Description").Value;
                    property.Mandatory = propertyNode.Element("Mandatory").Value.ToLowerInvariant().Equals("true");
                    property.ValidationRegExp = propertyNode.Element("Validation").Value;

                    XElement sortOrder = propertyNode.Element("SortOrder");
                    if (sortOrder != null)
                        property.SortOrder = int.Parse(sortOrder.Value); 
                    
                    var tab = propertyNode.Element("Tab").Value;
                    if ( !string.IsNullOrEmpty(tab))
                    {
                        var propGroup = item.PropertyGroups.First(x => x.Name == tab);

                        if ( !propGroup.PropertyTypes.Any(x => x.Alias == property.Alias))
                        {
                            // if it's not in this prop group - we can move it it into it
                            LogHelper.Info<uSync>("Moving {0} in {1} to {2}",
                                () => property.Alias, () => item.Name, () => tab);
                            propertiesToMove.Add(property.Alias, tab);
                        }
                    }
                }
            }

            foreach (string alias in propertiesToRemove)
            {
                LogHelper.Debug<uSync>("Removing {0}", () => alias);
                item.RemovePropertyType(alias);
            }

            foreach (KeyValuePair<string, string> movePair in propertiesToMove)
            {
                item.MovePropertyType(movePair.Key, movePair.Value);
            }

            if (propertiesToRemove.Count > 0 || propertiesToMove.Count > 0)
            {
                LogHelper.Debug<uSync>("Saving {0}", () => item.Name);
                _contentTypeService.Save(item);
            }
                

        }

        public static void ImportContainerType(this IContentType item, XElement node)
        {
            XElement Info = node.Element("Info");

            if ( Info != null)
            {
                XElement container = Info.Element("Container");
                if ( container != null)
                {
                    bool isContainer = false;
                    bool.TryParse(container.Value, out isContainer);
                    item.IsContainer = isContainer;
                }
            }
        }

        public static string GetSyncPath(this IContentType item)
        {
            string path = "";

            if ( item != null)
            {
                if ( item.ParentId != 0)
                {
                    path = _contentTypeService.GetContentType(item.ParentId).GetSyncPath();
                }
                path = string.Format("{0}\\{1}", path, helpers.XmlDoc.ScrubFile(item.Alias));
            }
            return path;
        }
    }
}
