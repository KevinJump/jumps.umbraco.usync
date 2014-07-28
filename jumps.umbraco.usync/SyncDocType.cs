using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Xml;
using System.Xml.Linq;

using System.Diagnostics; 

using Umbraco.Core ;
using Umbraco.Core.IO;
using Umbraco.Core.Services;
using Umbraco.Core.Models;
using Umbraco.Core.Logging;

using jumps.umbraco.usync.Extensions;
using jumps.umbraco.usync.helpers;

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
        static Dictionary<string, XElement> updated;

        private static IPackagingService _packageService;
        private static IContentTypeService _contentTypeService;
        private static int _readCount; 

        static SyncDocType()
        {
            _packageService = ApplicationContext.Current.Services.PackagingService;
            _contentTypeService = ApplicationContext.Current.Services.ContentTypeService;
        }


        /// <summary>
        /// save a document type to the disk, the document type will be 
        /// saved as an xml file, in a folder structure that mimics 
        /// that of the document type structure in umbraco.
        ///  
        /// this makes it easier to read them back in
        /// </summary>
        /// <param name="item">DocumentType to save</param>
        public static void SaveToDisk(IContentType item)
        {
            if (item != null)
            {
                try
                {
                    XElement element = item.ExportToXml();
                    element.AddMD5Hash();
                    XmlDoc.SaveElement("DocumentType", item.GetSyncPath(), "def", element);
                }
                catch (Exception e) 
                {
                    LogHelper.Info<SyncDocType>("uSync: Error Saving DocumentType {0} - {1}", 
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
                LogHelper.Info<SyncDocType>("uSync: Error Writing doctypes to disk {0}", ()=> ex.ToString());
            }
        }
        
              
        /// <summary>
        /// Gets all teh documentTypes from the disk, and puts them into
        /// umbraco 
        /// </summary>
        public static void ReadAllFromDisk()
        {
            // start the enumberation, get the root
            Stopwatch sw = new Stopwatch();
            sw.Start();
            _readCount = 0;

            // TODO: nicer way of getting the type string 
            //       (without creating a dummy doctype?)
            string path = IOHelper.MapPath(string.Format("{0}{1}",
                helpers.uSyncIO.RootFolder,
                "DocumentType"));

            // rest the alias names
            updated = new Dictionary<string, XElement>();

            // import stuff
            ReadFromDisk(path);
            LogHelper.Debug<uSync>("Processing DocTypes Part 1 - complete ({0}ms)", () => sw.ElapsedMilliseconds);

            //
            // Fit and Fix : 
            // because we have a List of updated nodes and files, this should be quicker
            // than traversing the tree again. Also we just do the update of the bit we
            // need to update
            // 
            SecondPassFitAndFix();
            sw.Stop();

            LogHelper.Info<uSync>("Processed Doctypes [{0}]({1}ms)", ()=> _readCount, ()=> sw.ElapsedMilliseconds);


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
                        // LogHelper.Info<SyncDocType>("Reading file {0}", () => node.Element("Info").Element("Alias").Value);
                        _readCount++; 

                        if (Tracker.ContentTypeChanged(node))
                        {
                            LogHelper.Info<SyncDocType>("updating DocType {0}", ()=> node.Element("Info").Element("Alias").Value);
                            node.ImportContentType();

                            if (!updated.ContainsKey(node.Element("Info").Element("Alias").Value))
                            {
                                updated.Add(node.Element("Info").Element("Alias").Value, node);
                            }
                            else
                            {
                                LogHelper.Info<SyncDocType>("WARNING: Multiple DocTypes detected - check your uSync folder");
                            }
                        }
                        else
                        {
                            LogHelper.Debug<SyncDocType>("Skipping update (Matching Hash Values)");
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
            foreach (KeyValuePair<string, XElement> update in updated)
            {
                XElement node = update.Value;
                LogHelper.Debug<uSync>("Second pass {0}", () => update.Value);

                if (node != null)
                {
                    // load the doctype
                    IContentType docType = ApplicationContext.Current.Services.ContentTypeService.GetContentType(update.Key);

                    if (docType != null)
                    {
                        LogHelper.Debug<uSync>("Container Type", () => update.Value);

                        // is it a container (in v6 api but only really a v7 thing)
                        docType.ImportContainerType(node);

                        LogHelper.Debug<uSync>("Structure", () => update.Value);
                        // import structure
                        docType.ImportStructure(node);


                        LogHelper.Debug<uSync>("Missing Property Removal", () => update.Value);
                        // delete things that are not in our source xml?
                        docType.ImportRemoveMissingProps(node);

                        LogHelper.Debug<uSync>("Sort order", () => update.Value);
                        // fix tab order 
                        docType.ImportTabSortOrder(node);

                        LogHelper.Debug<uSync>("Save", () => update.Value);
                        _contentTypeService.Save(docType);
                    }
                }
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
            if (!uSync.EventsPaused)
            {
                LogHelper.Debug<SyncDocType>("SaveContent Type Fired for {0} types",
                    () => e.SavedEntities.Count());
                foreach (var docType in e.SavedEntities)
                {
                    SaveToDisk(docType);
                }
            }
        }

        static void ContentTypeService_DeletingContentType(IContentTypeService sender, Umbraco.Core.Events.DeleteEventArgs<IContentType> e)
        {
            if (!uSync.EventsPaused)
            {
                LogHelper.Debug<SyncDocType>("Deleting Type Fired for {0} types", () => e.DeletedEntities.Count());
                // delete things (there can sometimes be more than one??)
                foreach (var docType in e.DeletedEntities)
                {
                    helpers.XmlDoc.ArchiveFile("DocumentType", docType.GetSyncPath(), "def");
                }
            }
        }
    }
}
