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
                    helpers.XmlDoc.SaveXmlDoc(item.GetType().ToString() + GetDocPath(item), item.Text, xmlDoc);
                }
                catch (Exception e)
                {
                    throw new Exception(string.Format("Failed Saving Doctype:{0} to disk\n{1}", item.Text, e.ToString()));
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
            string last ="start" ;
            try
            {
                foreach (DocumentType item in DocumentType.GetAllAsList().ToArray())
                {
                    if (item != null)
                    {
                        last = item.Text;
                        SaveToDisk(item);
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("uSync Failure when Saving DocTypes: last DocType {0}\n, {1}", last, e.ToString())); 
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
                path = string.Format(@"{0}\{1}", path, helpers.XmlDoc.ScrubFile(item.Text));
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
                "umbraco.cms.businesslogic.web.DocumentType"));

            // recurse in
            ReadFromDisk(path); 
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
                    // load the xml
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(file);

                    // get the first node
                    XmlNode node = xmlDoc.SelectSingleNode("//DocumentType");

                    if (node != null)
                    {
                        // use the umbraco package installer to import
                        Installer.ImportDocumentType(node, User.GetUser(0), true);
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
        /// attach events, adds the event handlers for this class 
        /// </summary>
        public static void AttachEvents()
        {
            DocumentType.AfterSave += DocumentType_AfterSave;
            DocumentType.BeforeDelete += DocumentType_BeforeDelete;
        }

        /// <summary>
        ///  called when a document type is about to be deleted. 
        ///  
        /// we archive the file (rename it from .xml to .archive) 
        /// this makes it not be read in on next application start.
        /// </summary>
        static void DocumentType_BeforeDelete(DocumentType sender, DeleteEventArgs e)
        {
            helpers.XmlDoc.ArchiveFile(sender.GetType().ToString() + GetDocPath(sender), sender.Text);
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
    }
}
