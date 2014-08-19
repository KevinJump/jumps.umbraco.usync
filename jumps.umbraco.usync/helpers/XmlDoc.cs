using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO ; 
using System.Xml ;
using System.Xml.Linq;

using umbraco.BusinessLogic ; 

using Umbraco.Core.IO ;
using Umbraco.Core.Logging;

using System.Security.Cryptography;

using jumps.umbraco.usync.Extensions;

using System.Runtime.InteropServices; 

namespace jumps.umbraco.usync.helpers
{
    /// <summary>
    /// helper class, does the bits making sure our 
    /// xml is consistantly created, and put in some
    /// form of logical place. 
    /// </summary>

    public delegate void XmlDocPreModifiedEventHandler(XmlDocFileEventArgs e);

    public class XmlDoc
    {
        private static bool _versions = false;  

        [Obsolete("use Saving event")]
        public static event XmlDocPreModifiedEventHandler preSave;

        [Obsolete("Use Deleting event")]
        public static event XmlDocPreModifiedEventHandler preDelete;

        public static event XmlDocPreModifiedEventHandler Saving; 
        public static event XmlDocPreModifiedEventHandler Saved; 
        
        public static event XmlDocPreModifiedEventHandler Deleting; 
        public static event XmlDocPreModifiedEventHandler Deleted; 

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

        #region Saving XElement files

        public static void SaveElement(string type, string path, string name, XElement element)
        {
            SaveElement(GetFilePath(type, path, name), element);
        }

        public static void SaveElement(string type, string name, XElement element)
        {
            SaveElement(GetFilePath(type, name), element);
        }

        public static void SaveElement(string path, XElement element)
        {
            string targetFile = GetFullFilePath(path);
            string folder = Path.GetDirectoryName(targetFile);

            OnPreSave(new XmlDocFileEventArgs(targetFile));

            LogHelper.Info<uSync>("Saving {0}", () => targetFile);

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            else if ( File.Exists(targetFile))
            {
                if ( _versions)
                {
                    ArchiveFile(
                        Path.GetDirectoryName(path),
                        Path.GetFileNameWithoutExtension(path),
                        false);
                }

                File.Delete(targetFile);
            }

            element.Save(targetFile);

            OnSaved(new XmlDocFileEventArgs(targetFile));
        }

        #endregion 

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
            
            //
            // moved because we can attempt to delete it so we need to fire before we start
            //
            OnPreSave(new XmlDocFileEventArgs(savePath));


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

            LogHelper.Info<XmlDoc>("Saving [{0}]", ()=> savePath); 
            
            doc.Save(savePath) ;

            OnSaved(new XmlDocFileEventArgs(savePath)); 
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
                    OnPreDelete(new XmlDocFileEventArgs(currentFile));
                    File.Delete(currentFile);
                    OnDeleted(new XmlDocFileEventArgs(currentFile));

                    LogHelper.Info<XmlDoc>("Archived [{0}] to [{1}]", ()=> currentFile, ()=> archiveFile); 

                    // latest n? - only keep last n in the folder?
                    if (uSyncSettings.MaxVersions > 0)
                    {
                        ClenseArchiveFolder(Path.GetDirectoryName(archiveFile), uSyncSettings.MaxVersions);
                    }
                }
            }
            catch(Exception ex)
            {
               // archive is a non critical thing - if it fails we are not stopping
               // umbraco, but we are going to log that it didn't work. 
               // Log.Add(LogTypes.Error, 0, "Failed to archive") ; 
               // to do some dialog popup text like intergration
               LogHelper.Info<XmlDoc>("Failed to Archive {1}, {0}", ()=> type, ()=> name ); 
            }

        }
        
        private static void ClenseArchiveFolder(string folder, int versions)
        {
            LogHelper.Info<XmlDoc>("Keeping Archive versions in {0} at {1} versions", () => folder, () => versions);
            if ( Directory.Exists(folder))
            {
                DirectoryInfo dir = new DirectoryInfo(folder);
                FileInfo[] FileList = dir.GetFiles("*.*");
                var files = FileList.OrderByDescending(file => file.CreationTime);
                var i = 0;
                foreach(var file in files)
                {
                    i++;
                    if (i > versions)
                    {
                        file.Delete();
                    }
                }
            }
        }
         

        public static void DeleteuSyncFile(string type, string path, string name)
        {
            string liveRoot = IOHelper.MapPath(uSyncIO.RootFolder);
            string archiveRoot = IOHelper.MapPath(uSyncIO.ArchiveFolder);

            string currentFile = string.Format(@"{0}\{1}\{2}.config",
                liveRoot, GetTypeFolder(type), ScrubFile(name));

            if (File.Exists(currentFile))
            {
                OnPreDelete(new XmlDocFileEventArgs(currentFile));
                File.Delete(currentFile);
                OnDeleted(new XmlDocFileEventArgs(currentFile));

                LogHelper.Info<XmlDoc>("Deleted File [{0}]", ()=> currentFile); 
            }
            
            
        }

        /// <summary>
        ///  a slightly more complex one - for data types we take the preVal id fields
        ///  away - because they are internal and change per install. 
        /// </summary>
        /// <returns></returns>
        public static string CalculateMD5Hash(XElement node, Boolean removePreValIds)
        {
            if (removePreValIds)
            {
                XElement copy = new XElement(node);
                var preValueRoot = copy.Element("PreValues");
                if (preValueRoot.HasElements)
                {
                    var preValues = preValueRoot.Elements("PreValue");
                    foreach (var preValue in preValues)
                    {
                        // find pre-vals - blank ids...
                        preValue.SetAttributeValue("Id", "");
                    }
                }
                return CalculateMD5Hash(copy);
            }
            else
            {
                return CalculateMD5Hash(node);
            }

        }


        //
        // Compute the MD5 of an xml file
        //
        public static string CalculateMD5Hash(XElement node)
        {
            string md5Hash = "";
            MemoryStream stream = new MemoryStream();
            node.Save(stream);

            stream.Position = 0;

            using (var md5 = MD5.Create())
            {
                md5Hash = BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLower();
            }

            stream.Close();
            return md5Hash; 
        }

        public static string CalculateMD5Hash(XmlDocument node)
        {
            string md5Hash = "";
            MemoryStream stream = new MemoryStream();
            node.Save(stream);

            stream.Position = 0;

            using (var md5 = MD5.Create())
            {
                md5Hash = BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLower();
            }

            stream.Close();

            return md5Hash; 
        }

        public static string CalculateMD5Hash(string input)
        {
            string hash = "";
            byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
            using( var md5 = MD5.Create())
            {
                hash = BitConverter.ToString(md5.ComputeHash(inputBytes)).Replace("-", "").ToLower();
            }
            return hash; 
        }

        public static string GetPreCalculatedHash(XElement node)
        {
            XElement hashNode = node.Element("Hash");
            if ( hashNode == null )
                return "" ;

            return hashNode.Value ; 
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

        #region FilePath Helpers
        public static string GetFilePath(string type, string path, string name)
        {
            return string.Format("{0}\\{1}\\{2}.config", GetTypeFolder(type), path, ScrubFile(name));
        }

        public static string GetFilePath(string type, string name)
        {
            return string.Format("{0}\\{1}.config", GetTypeFolder(type), ScrubFile(name));
        }

        public static string GetFullFilePath(string path)
        {
            return string.Format("{0}\\{1}", IOHelper.MapPath(uSyncIO.RootFolder), path);
        }
        #endregion


        public static void OnPreSave(XmlDocFileEventArgs e)
        {
            SyncFileWatcher.Pause();

            /* going to phase this out - naming is saving/saved) */
            if (preSave != null)
            {
                preSave(e);
            }

            if (Saving != null)
            {
                Saving(e);
            }
        }

        public static void OnSaved(XmlDocFileEventArgs e)
        {
            if (Saved != null)
            {
                Saved(e);
            }
            SyncFileWatcher.Start();

        }

        public static void OnPreDelete(XmlDocFileEventArgs e)
        {
            SyncFileWatcher.Pause();
            if (preDelete != null)
            {
                preDelete(e);
            }

            if (Deleting != null)
            {
                Deleting(e);
            }
        }

        public static void OnDeleted(XmlDocFileEventArgs e)
        {
            if (Deleted != null)
            {
                Deleted(e);
            }
            SyncFileWatcher.Start();

        }

        #region XElement value getters 


        public static bool GetValueOrDefault(XElement element, bool defaultValue)
        {
            if (element != null && !string.IsNullOrEmpty(element.Value))
            {
                return bool.Parse(element.Value);
            }
            return defaultValue;
        }


        public static int GetValueOrDefault(XElement element, int defaultValue)
        {
            if (element != null && !string.IsNullOrEmpty(element.Value))
            {
                return int.Parse(element.Value);
            }
            return defaultValue;
        }


        public static string GetValueOrDefault(XElement element, string defaultValue)
        {
            if (element != null && !string.IsNullOrEmpty(element.Value))
            {
                return element.Value;
            }
            return defaultValue;
        }


        #endregion
    }
}
