using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO ; 
using System.Xml ;

using umbraco.BusinessLogic ; 
using Umbraco.Core.IO ;
using System.Runtime.InteropServices; 

namespace jumps.umbraco.usync.helpers
{
    /// <summary>
    /// helper class, does the bits making sure our 
    /// xml is consistantly created, and put in some
    /// form of logical place. 
    /// </summary>
    public class XmlDoc
    {
        private static bool _versions = false;  

        static XmlDoc()
        {
            _versions = uSyncSettings.Versions;
        }

        public static XmlDocument CreateDoc()
        {
            XmlDocument doc = new XmlDocument();
            XmlDeclaration dec = doc.CreateXmlDeclaration("1.0", "UTF-8", "no");
            doc.AppendChild(dec);

            return doc;
        }

        public static void SaveXmlDoc(string type, string path, string name, XmlDocument doc)
        {
            string savePath = string.Format("{0}/{1}/{2}.config", GetTypeFolder(type), path, name) ;
            SaveXmlDoc(savePath, doc); 
        }

        public static void SaveXmlDoc(string type, string name, XmlDocument doc)
        {
            string savePath = string.Format("{0}/{1}.config", GetTypeFolder(type), ScrubFile(name)) ;
            SaveXmlDoc(savePath, doc) ;
        }
              

        public static void SaveXmlDoc(string path, XmlDocument doc)
        {
            string savePath = string.Format("{0}/{1}", IOHelper.MapPath(uSyncIO.RootFolder), path);

            if ( !Directory.Exists(Path.GetDirectoryName(savePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(savePath));
            }
            else {
                if ( File.Exists(savePath) ) 
                {
                    // TODO: Archive here..? 
                    if ( _versions ) {
                        ArchiveFile(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path), false);  
                    }
            
                    File.Delete(savePath);
                }
            }

            uSyncLog.InfoLog("Saving [{0}]", savePath); 
            doc.Save(savePath) ; 
        }

        /// <summary>
        /// Archive a file (and delete the orgininal) called when a file is deleted
        /// </summary>
        public static void ArchiveFile(string path, string name)
        {
            ArchiveFile(path, name, true);
        }

        public static void ArchiveFile(string type, string path, string name)
        {
            string savePath = string.Format(@"{0}\{1}\", GetTypeFolder(type), path);
            ArchiveFile(savePath, name, true);
        }

        /// <summary>
        /// archive a file, and optionally delete the orgiinal, allows us to use archive 
        /// as a versioning tool :) 
        /// </summary>
        public static void ArchiveFile(string type, string name, bool delete)
        {
            string liveRoot = IOHelper.MapPath(uSyncIO.RootFolder);
            string archiveRoot = IOHelper.MapPath(uSyncIO.ArchiveFolder);

            string currentFile = string.Format(@"{0}\{1}\{2}.config",
                liveRoot, GetTypeFolder(type),ScrubFile(name));


            string archiveFile = string.Format(@"{0}\{1}\{2}_{3}.config",
                archiveRoot, GetTypeFolder(type), ScrubFile(name), DateTime.Now.ToString("ddMMyy_HHmmss"));


            try
            {

                // we need to confirm the archive directory exists 
                if (!Directory.Exists(Path.GetDirectoryName(archiveFile)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(archiveFile));
                }

                if (File.Exists(currentFile))
                {
                    // it shouldn't happen as we are going for a unique name
                    // but it might be called twice v'quickly

                    if (File.Exists(archiveFile))
                    {
                        File.Delete(archiveFile);
                    }

                    // 
                    File.Copy(currentFile, archiveFile);
                    File.Delete(currentFile);

                    uSyncLog.DebugLog("Archived [{0}] to [{1}]", currentFile, archiveFile); 
                }
            }
            catch(Exception ex)
            {
               // archive is a non critical thing - if it fails we are not stopping
               // umbraco, but we are going to log that it didn't work. 
               // Log.Add(LogTypes.Error, 0, "Failed to archive") ; 
               // to do some dialog popup text like intergration
               uSyncLog.ErrorLog(ex, "Failed to Archive {1}, {0}", type, name ); 
            }

        }

       
        /// <summary>
        /// we need to clean the name up to make it a valid file name..
        /// </summary>
        /// <param name="filename"></param>
        public static string ScrubFile(string filename)
        {
            // TODO: a better scrub

            StringBuilder sb = new StringBuilder(filename);
            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char item in invalid)
            {
                sb.Replace(item.ToString(), "");
            }

            return sb.ToString() ;
        }

        public static string GetNodeValue(XmlNode val)
        {
            string value = val.Value;

            if (String.IsNullOrEmpty(value))
                return "";
            else
                return value;
        }

        public static string GetTypeFolder(string type)
        {
            return type.Substring(type.LastIndexOf('.') + 1);
        }
        
    }
}
