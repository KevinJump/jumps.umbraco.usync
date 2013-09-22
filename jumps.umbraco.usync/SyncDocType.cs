using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Xml;
using System.Xml.Linq; 

using Umbraco.Core ;
using Umbraco.Core.IO;
using Umbraco.Core.Services;
using Umbraco.Core.Models;
using Umbraco.Core.Logging; 

namespace jumps.umbraco.usync
{
    /// <summary>
    /// Syncs the Document Types in an umbraco install to the disk, 
    /// and from the disk. 
    /// 
    /// attached to the events, it should just work, and keep everything
    /// in sync.  
    /// 
    /// v1.6.0 - refactored to work with 6.1.x+ only -
    /// 
    /// using IContentType and XElement all the time now 
    /// 
    /// </summary>
    public class SyncDocType
    {
        static Dictionary<string, string> updated;

        private static PackagingService _packService ; 
        private static IContentTypeService _contentTypeService ; 

        static SyncDocType()
        {
            _packService = ApplicationContext.Current.Services.PackagingService;
            _contentTypeService = ApplicationContext.Current.Services.ContentTypeService;
        }

        /// <summary>
        /// save a document type to the disk, the document type will be 
        /// saved as an xml file, in a folder structure that mimics 
        /// that of the document type structure in umbraco.
        ///  
        /// this makes it easier to read them back in
        /// </summary>
        /// <param name="item">ContentType to save</param>
        public static void SaveToDisk(IContentType item)
        {
            if (item != null)
            {
                try
                {
                    XElement element = _packService.Export(item);
                    helpers.XmlDoc.SaveElement("DocumentType", GetDocPath(item), element);                              
                }
                catch (Exception ex)
                {
                    LogHelper.Error<SyncDocType>("Error Saving DocType", ex);
                }
            }
        }

        /// <summary>
        /// Saves all document types in umbraco.
        /// 
        /// enumerates through types and calls <see cref="SaveToDisk"/>
        /// </summary>
        public static void SaveAllToDisk()
        {
            try
            {
                foreach (IContentType item in _contentTypeService.GetAllContentTypes())
                {
                    if (item != null)
                    {
                        SaveToDisk(item);
                    }
                }
            }
            catch( Exception ex )
            {
                // error saving to disk, can happen if Umbraco has orphaned doctypes & GetAll thows an error! 
                helpers.uSyncLog.DebugLog("uSync: Error Writing doctypes to disk {0}", ex.ToString());
            }
        }
        
        /// <summary>
        /// works out what the folder stucture for a doctype should be.
        /// 
        /// recurses up the parent path of the doctype, adding a folder
        /// for each one, this then gives us a structure we can create
        /// on the disk, that mimics that of the umbraco internal one
        /// </summary>
        /// <param name="item">ContentType path to find</param>
        /// <returns>folderstucture (relative to uSync folder)</returns>
        private static string GetDocPath(IContentType item)
        {
            string path = "";

            if (item != null)
            {
                if (item.ParentId != 0)
                {
                    path = GetDocPath(_contentTypeService.GetContentType(item.ParentId));
                }

                path = string.Format("{0}\\{1}", path, helpers.XmlDoc.ScrubFile(item.Alias));
            }
            return path;
        }

        
        /// <summary>
        /// Gets all the ContentTypes from the disk, and puts them into
        /// umbraco 
        /// </summary>
        public static void ReadAllFromDisk()
        {
            // start the enumberation, get the root

            // TODO: nicer way of getting the type string 
            //       (without creating a dummy doctype?)
            string path = IOHelper.MapPath(string.Format("{0}{1}",
                helpers.uSyncIO.RootFolder,
                "DocumentType"));

            // rest the alias names
            updated = new Dictionary<string, string>();

            // import stuff
            ReadFromDisk(path);

            //
            // Fit and Fix : 
            // because we have a List of updated nodes and files, this should be quicker
            // than traversing the tree again. Also we just do the update of the bit we
            // need to update
            // 
            SecondPassFitAndFix(); 


        }

        /// <summary>
        ///  reads all documentTypes in a given folder and adds them to umbraco
        ///  
        /// it then recurses into any subfolders. this means we can recreate the 
        /// document types for a umbraco install, making sure we create the parent
        /// document types first, (in the top folders) that way we get no failures
        /// based on dependency, 
        /// 
        /// because we are doing this with individual xml files, it's simpler code
        /// than how the package manager does it with one massive XML file. 
        /// </summary>
        /// <param name="path"></param>
        private static void ReadFromDisk(string path) 
        {
            if (Directory.Exists(path))
            {
                // get all the xml files in this folder 
                // we are sort of assuming they are doctype ones.
                foreach (string file in Directory.GetFiles(path, "*.config"))
                {                    
                    XElement node = XElement.Load(file) ;                                                    
                    if (node != null ) 
                    {
                        LogHelper.Info<SyncDocType>("Reading file {0}", () => node.Element("Info").Element("Alias").Value); 
                        _packService.ImportContentTypes(node, false);
                        updated.Add(node.Element("Info").Element("Alias").Value, file); 
                    }
                }
            
                // now see if there are any folders we should pop into
                foreach (string folder in Directory.GetDirectories(path))
                {
                    ReadFromDisk(folder);
                }                
            }
        }

        private static void SecondPassFitAndFix()
        {
            foreach (KeyValuePair<string, string> update in updated)
            {
                XElement node = XElement.Load(update.Value);
                if (node != null)
                {
                    // load the doctype
                    IContentType docType = _contentTypeService.GetContentType(update.Key);

                    if (docType != null)
                    {
                        // import structure
                        ImportStructure(docType, node); 
                        
                        // fix tab order 
                        // TabSortOrder(docType, node); 

                        // delete things that are not in our source xml?
                        RemoveMissingProperties(docType, node); 
                        
                        // save
                        _contentTypeService.Save(docType);
                    }
                }
            }
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
            // not yet, probibly going to have to re-write quite a bit of the import/export to get this out
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
                helpers.uSyncLog.DebugLog("DeletePropertyValue = false - exiting"); 
                return;
            }

            List<string> propertiesToRemove = new List<string>();
            Dictionary<string,string> propertiesToMove = new Dictionary<string,string>(); 

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
                    helpers.uSyncLog.DebugLog("Removing property {0} from {1}", property.Alias, docType.Name);

                }
                else
                {
                    // if it is we should re-write the properties - because i default import doesn't do that

                    /* 
                     *  how we work out what datatype we are...
                     */
                    var dataTypeId = new Guid(propertyNode.Element("Type").Value);
                    var dataTypeDefinitionId = new Guid(propertyNode.Element("Definition").Value);

                    IDataTypeService _dataTypeService = ApplicationContext.Current.Services.DataTypeService;

                    var dataTypeDefintion = _dataTypeService.GetDataTypeDefinitionById(dataTypeDefinitionId);

                    if (dataTypeDefintion == null || dataTypeDefintion.ControlId != dataTypeId)
                    {
                        var dataTypeDefintions = _dataTypeService.GetDataTypeDefinitionByControlId(dataTypeId);
                        if (dataTypeDefintions != null && dataTypeDefintions.Any())
                        {
                            dataTypeDefintion = dataTypeDefintions.First();
                        }
                    }

                    if (dataTypeDefintion != null)
                    {
                        property.DataTypeDefinitionId = dataTypeDefintion.Id;
                        // as you can't set property.DataTypeId or the internal DB type i'm a bit
                        // worried this might break if the type changes inside                   
                    }                                    

                    // all the other properties.
                    property.Name = propertyNode.Element("Name").Value;
                    property.Description = propertyNode.Element("Description").Value;
                    property.Mandatory = propertyNode.Element("Mandatory").Value.ToLowerInvariant().Equals("true");
                    property.ValidationRegExp = propertyNode.Element("Validation").Value;
                    
                    var helpText = propertyNode.Element("HelpText");
                    if (helpText != null)
                    {
                        property.HelpText = helpText.Value;
                    }

                    var tab = propertyNode.Element("Tab").Value;
                    if (!string.IsNullOrEmpty(tab))
                    {
                        // node moving ? - that will be fun ?
                        
                        var pg = docType.PropertyGroups.First(x => x.Name == tab);

                        if (!pg.PropertyTypes.Any(x => x.Alias == property.Alias))
                        {
                            // if it's not in the group - we can move it into it*/
                            propertiesToMove.Add(property.Alias, tab); 
                        }
                    }

                }
            }

            foreach (string alias in propertiesToRemove)
            {
                docType.RemovePropertyType(alias);
            }

            foreach (KeyValuePair<string, string> movePair in propertiesToMove)
            {
                docType.MovePropertyType(movePair.Key, movePair.Value);
            }
        }

        /// <summary>
        /// attach events, adds the event handlers for this class 
        /// </summary>
        public static void AttachEvents()
        {
            ContentTypeService.DeletingContentType += ContentTypeService_DeletingContentType;
            ContentTypeService.SavedContentType += ContentTypeService_SavedContentType;
        }

        static void ContentTypeService_SavedContentType(IContentTypeService sender, Umbraco.Core.Events.SaveEventArgs<IContentType> e)
        {
            helpers.uSyncLog.DebugLog("SaveContent Type Fired for {0} types", e.SavedEntities.Count());
            foreach (IContentType docType in e.SavedEntities)
            {
                SaveToDisk(docType);
            }
        }

        static void ContentTypeService_DeletingContentType(IContentTypeService sender, Umbraco.Core.Events.DeleteEventArgs<IContentType> e)
        {
            helpers.uSyncLog.DebugLog("Deleting Type Fired for {0} types", e.DeletedEntities.Count());
            // delete things (there can sometimes be more than one??)
            foreach (IContentType docType in e.DeletedEntities)
            {
                helpers.XmlDoc.ArchiveFile("DocumentType", GetDocPath(docType), "def") ; 
            }
        }
    }
}
