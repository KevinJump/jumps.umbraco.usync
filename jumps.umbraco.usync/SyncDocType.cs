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

using jumps.umbraco.usync.SyncProviders;
using jumps.umbraco.usync.helpers;

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
    /// doesn't yet appear to be exporting "structure"
    /// 
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

        #region DocType Exporting

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
                    XElement element = item.SyncExport(); 

                    // add some stuff (namley the GUID) this makes tracking good
                    // element.Element("Info").Add(new XElement("key", helpers.KeyManager.GetMasterKey(item.Key)));
                                        
                    XmlDoc.SaveElement("DocumentType", item.GetSyncPath(), "def", element);
                    SourceInfo.Add(item.Key, item.Name, item.ParentId);
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
                SourceInfo.Save(); 
            }
            catch( Exception ex )
            {
                // error saving to disk, can happen if Umbraco has orphaned doctypes & GetAll thows an error! 
                LogHelper.Error<SyncDocType>("uSync: Error Writing doctypes to disk", ex);
            }
        }


        public static void Rename(IContentType item, string oldName)
        {
            string path = Path.GetDirectoryName(item.GetSyncPath());
            XmlDoc.RenameFile("DocumentType", path, item.Alias, oldName);

            int masterId = ImportInfo.GetMaster(item.Id); 

            SyncActionLog.AddRename(
                ImportInfo.GetMaster(item.Id), item.Alias, oldName);
        }

        public static void Move(IContentType item, int oldParentId)
        {
            // at hte moment you can't move doctypes in umbraco.
            // so we're not going to do anything about that...
            
            /*
            IContentType oldParent = _contentTypeService.GetContentType(oldParentId);

            if (oldParent != null)
            {
                XmlDoc.MoveFile(
                    oldParent.GetSyncPath(),
                    item.GetSyncPath());
            }
          */
        }

        #endregion

        #region DocType Importing
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

            ProcessDeletes();

            // import stuff
            ReadFromDisk(path);

            //
            // Fit and Fix : 
            // because we have a List of updated nodes and files, this should be quicker
            // than traversing the tree again. Also we just do the update of the bit we
            // need to update
            // 
            SecondPassFitAndFix();

            SourceInfo.Save();

        }

        /// <summary>
        ///  looks at the sync action log, and deletes stuff. 
        ///  the umbraco services don't yet exist to get DocTypes by GUID
        ///  so it's a bit slow at the moment - we do a get all and step
        ///  through until we get our guid.
        ///  
        ///  other way - don't track by GUID track by ID (seems wrong)? 
        /// </summary>
        private static void ProcessDeletes()
        {
            foreach(int delete in SyncActionLog.GetDeletes())
            {
                IContentType item = _contentTypeService.GetContentType(delete);
                if ( item != null )
                {
                    LogHelper.Info<SyncDocType>("Deleting {0}", ()=> item.Name); 
                    helpers.XmlDoc.ArchiveFile("DocumentType", item.GetSyncPath(), "def");
                    _contentTypeService.Delete(item);

                }
            }
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
                        node.SyncImport(); 

                        if (!updated.ContainsKey(node.Element("Info").Element("Alias").Value))
                        {
                            updated.Add(node.Element("Info").Element("Alias").Value, file);
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

        /// <summary>
        ///  imports a doctype from disk - it's a single process.
        /// </summary>
        /// <param name="filepath"></param>
        /// <returns></returns>
        public static IEnumerable<IContentType> ImportFromFile(string filepath)
        {
            if (!System.IO.File.Exists(filepath))
                return null;

            XElement node = XElement.Load(filepath);
            if (node != null)
            {
                IEnumerable<IContentType> items = node.SyncImport();

                foreach (IContentType item in items)
                {
                    // second pass stuff..
                    if (item != null)
                    {
                        item.SyncImportStructure(node);
                        item.SyncRemoveMissingProperties(node);
                        item.SyncTabSortOrder(node);
                        _contentTypeService.Save(item);
                    }
                }

                return items;
            }
            else
            {
                return null;
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
                        docType.SyncImportStructure(node); 

                        // delete things that are not in our source xml?
                        docType.SyncRemoveMissingProperties(node);

                        // add to our import map, 
                        // helpers.KeyManager.AddToKeyMap(docType.Key, Guid.Parse(node.Element("Info").Element("Key").Value));
                        docType.SyncTabSortOrder(node); 


                        // save
                        _contentTypeService.Save(docType);
                    }
                }
            }
        }

        #endregion

        #region DocType Events
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
            LogHelper.Debug<SyncDocType>("SaveContent Type Fired for {0} types", ()=> e.SavedEntities.Count());
            foreach (IContentType item in e.SavedEntities)
            {
                string sourceName = SourceInfo.GetName(item.Key);

                if ((sourceName != null) && (item.Name != sourceName))
                {
                    // rename ..
                    LogHelper.Info<SyncDocType>("Rename {0}", () => item.Name);
                    Rename(item, sourceName);
                }

                int? parentId = SourceInfo.GetParent(item.Key);
                if ((parentId != null) && (item.ParentId != parentId.Value))
                {
                    // move...
                    LogHelper.Info<SyncDocType>("Move {0}", ()=> item.Name);
                    Move(item, parentId.Value); 
                }

                SaveToDisk(item);
            }
            SourceInfo.Save();
        }

        static void ContentTypeService_DeletingContentType(IContentTypeService sender, Umbraco.Core.Events.DeleteEventArgs<IContentType> e)
        {
            LogHelper.Debug<SyncDocType>("Deleting Type Fired for {0} types", ()=> e.DeletedEntities.Count());
            // delete things (there can sometimes be more than one??)
            foreach (IContentType docType in e.DeletedEntities)
            {
                helpers.XmlDoc.ArchiveFile("DocumentType", docType.GetSyncPath(), "def") ;
                SyncActionLog.AddDelete(ImportInfo.GetMaster(docType.Id));
            }
        }
        #endregion 
    }
}
