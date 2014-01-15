using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Xml;
using System.Xml.Linq; 

using umbraco.cms.businesslogic;
using umbraco.cms.businesslogic.web;
using umbraco.cms.businesslogic.packager;
using umbraco.BusinessLogic;
using Umbraco.Core.IO;
using umbraco;

using Umbraco.Core ; 
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
    /// </summary>
    public class SyncDocType
    {
        static Dictionary<string, string> updated; 

        /// <summary>
        /// save a document type to the disk, the document type will be 
        /// saved as an xml file, in a folder structure that mimics 
        /// that of the document type structure in umbraco.
        ///  
        /// this makes it easier to read them back in
        /// </summary>
        /// <param name="item">DocumentType to save</param>
        public static void SaveToDisk(DocumentType item)
        {
            if (item != null)
            {
                try
                {
                    XmlDocument xmlDoc = helpers.XmlDoc.CreateDoc();
                    xmlDoc.AppendChild(item.ToXml(xmlDoc));
                    // add tabs..
                    helpers.XmlDoc.SaveXmlDoc(item.GetType().ToString(), GetDocPath(item), "def", xmlDoc);
                }
                catch (Exception e)
                {
                    LogHelper.Debug<SyncDocType>("uSync: Error Saving DocumentType {0} - {1}", 
                        ()=> item.Alias, ()=> e.ToString()); 
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
                foreach (DocumentType item in DocumentType.GetAllAsList().ToArray())
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
                LogHelper.Debug<SyncDocType>("uSync: Error Writing doctypes to disk {0}", ()=> ex.ToString());
            }
        }
        
        /// <summary>
        /// works out what the folder stucture for a doctype should be.
        /// 
        /// recurses up the parent path of the doctype, adding a folder
        /// for each one, this then gives us a structure we can create
        /// on the disk, that mimics that of the umbraco internal one
        /// </summary>
        /// <param name="item">DocType path to find</param>
        /// <returns>folderstucture (relative to uSync folder)</returns>
        private static string GetDocPath(DocumentType item)
        {
            string path = "";

            if (item != null)
            {
                // does this documentType have a parent 
                if (item.MasterContentType != 0)
                {
                    // recurse in to the parent to build the path
                    path = GetDocPath(new DocumentType(item.MasterContentType));
                }

                // buld the final path (as path is "" to start with we always get
                // a preceeding '/' on the path, which is nice
                path = string.Format(@"{0}\{1}", path, helpers.XmlDoc.ScrubFile(item.Alias));
            }
         
            return path; 
        }

        
        /// <summary>
        /// Gets all teh documentTypes from the disk, and puts them into
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
                        ApplicationContext.Current.Services.PackagingService.ImportContentTypes(node, false);

                        if (!updated.ContainsKey(node.Element("Info").Element("Alias").Value))
                        {
                            updated.Add(node.Element("Info").Element("Alias").Value, file);
                        }
                        else
                        {
                            LogHelper.Info<SyncDocType>("WARNING: Multiple DocTypes detected - check your uSync folder"); 
                        }
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
                    IContentType docType = ApplicationContext.Current.Services.ContentTypeService.GetContentType(update.Key);

                    if (docType != null)
                    {
                        // import structure
                        ImportStructure(docType, node); 
                        
                        // fix tab order 
                        // TabSortOrder(docType, node); 

                        // delete things that are not in our source xml?
                        RemoveMissingProperties(docType, node); 
                        
                        // save
                        ApplicationContext.Current.Services.ContentTypeService.Save(docType);
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
                        ()=> property.Alias, ()=> docType.Name);
                    
                }                
            }

            foreach (string alias in propertiesToRemove)
            {
                docType.RemovePropertyType(alias);
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
            LogHelper.Debug<SyncDocType>("SaveContent Type Fired for {0} types", 
                ()=> e.SavedEntities.Count());
            foreach (var docType in e.SavedEntities)
            {
                SaveToDisk(new DocumentType(docType.Id));
            }
        }

        static void ContentTypeService_DeletingContentType(IContentTypeService sender, Umbraco.Core.Events.DeleteEventArgs<IContentType> e)
        {
            LogHelper.Debug<SyncDocType>("Deleting Type Fired for {0} types", ()=> e.DeletedEntities.Count());
            // delete things (there can sometimes be more than one??)
            foreach (var docType in e.DeletedEntities)
            {
                helpers.XmlDoc.ArchiveFile("DocumentType", GetDocPath(new DocumentType(docType.Id)), "def") ; 
            }
        }
    }
}
