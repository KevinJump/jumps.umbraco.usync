using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml;
using System.IO; 

using umbraco.cms.businesslogic;
using umbraco.cms.businesslogic.web;
using umbraco.cms.businesslogic.packager;
using umbraco.BusinessLogic;
using Umbraco.Core.IO;
using umbraco;

#if UMBRACO6
using Umbraco.Core ; 
using Umbraco.Core.Services;
using Umbraco.Core.Models; 
#endif

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
                    helpers.XmlDoc.SaveXmlDoc(item.GetType().ToString(), GetDocPath(item), "def", xmlDoc);
                }
                catch (Exception e)
                {
                    helpers.uSyncLog.DebugLog( "uSync: Error Saving DocumentType {0} - {1}", item.Alias, e.ToString() ) ; 
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

            // recurse in
            ReadFromDisk(path, false);
            ReadFromDisk(path, true); // second pass, adds the childnode stuff...
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
        private static void ReadFromDisk(string path, bool structure) 
        {

            if (Directory.Exists(path))
            {
                // get all the xml files in this folder 
                // we are sort of assuming they are doctype ones.
                foreach (string file in Directory.GetFiles(path, "*.config"))
                {
                    // load the xml
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(file);

                    // get the first node
                    XmlNode node = xmlDoc.SelectSingleNode("//DocumentType");

                    if (node != null)
                    {
                        // use the umbraco package installer to import
                        helpers.uSyncLog.DebugLog("Installing {0}", file); 
                        Installer.ImportDocumentType(node, User.GetUser(0), structure);
           
                    }
                }
                // now see if there are any folders we should pop into
                foreach (string folder in Directory.GetDirectories(path))
                {
                    ReadFromDisk(folder, structure);
                }
                
            }


        }

        /// <summary>
        /// attach events, adds the event handlers for this class 
        /// </summary>
        public static void AttachEvents()
        {
#if UMBRACO6
            ContentTypeService.DeletingContentType += ContentTypeService_DeletingContentType;
            ContentTypeService.SavedContentType += ContentTypeService_SavedContentType;
#else
            DocumentType.AfterSave += DocumentType_AfterSave;
            DocumentType.BeforeDelete += DocumentType_BeforeDelete;
#endif
        }

#if UMBRACO6
        static void ContentTypeService_SavedContentType(IContentTypeService sender, Umbraco.Core.Events.SaveEventArgs<IContentType> e)
        {
            helpers.uSyncLog.DebugLog("SaveContent Type Fired for {0} types", e.SavedEntities.Count());
            foreach (var docType in e.SavedEntities)
            {
                SaveToDisk(new DocumentType(docType.Id));
            }
        }

        static void ContentTypeService_DeletingContentType(IContentTypeService sender, Umbraco.Core.Events.DeleteEventArgs<IContentType> e)
        {
            helpers.uSyncLog.DebugLog("Deleting Type Fired for {0} types", e.DeletedEntities.Count());
            // delete things (there can sometimes be more than one??)
            foreach (var docType in e.DeletedEntities)
            {
                helpers.XmlDoc.ArchiveFile("DocumentType", GetDocPath(new DocumentType(docType.Id)), "def") ; 
            }
        }
#else 
        /// <summary>
        ///  called when a document type is about to be deleted. 
        ///  
        /// we archive the file (rename it from .xml to .archive) 
        /// this makes it not be read in on next application start.
        /// </summary>
        static void DocumentType_BeforeDelete(DocumentType sender, DeleteEventArgs e)
        {
            helpers.XmlDoc.ArchiveFile(sender.GetType().ToString(), GetDocPath(sender), "def");
            e.Cancel = false; 
            
        }

        /// <summary>
        ///  called when a documenttype is saved 
        ///  
        /// we save to disk here, this means we capture any changes, or 
        /// creations of new DocumentTypes. 
        /// </summary>
        static void DocumentType_AfterSave(DocumentType sender, global::umbraco.cms.businesslogic.SaveEventArgs e)
        {
            SaveToDisk((DocumentType)sender);             
        }
#endif 
    }
}
