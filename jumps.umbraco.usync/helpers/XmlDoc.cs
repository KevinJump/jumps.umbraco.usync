using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

using Umbraco.Core.IO;
using Umbraco.Core.Logging;

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

        #region New Save Events 

        public static void SaveNode(string folder, string path, string name, XElement node, string type)
        {
            var fullpath = GetSavePath(folder, path, name, type);
            SaveNode(fullpath, node);
        }

        public static void SaveNode(string folder, string name, XElement node, string type)
        {
            string filePath = GetSavePath(folder, name, type);
            SaveNode(filePath, node);
        }

        public static void SaveNode(string filePath,XElement node)
        {
            LogHelper.Debug<XmlDoc>("Saving Node to Disk {0}", ()=> filePath);

            OnPreSave(new XmlDocFileEventArgs(filePath));

            if (File.Exists(filePath))
            {
                if ( _versions )
                    ArchiveFile(filePath);

                File.Delete(filePath);
            }
            
            if (!Directory.Exists(Path.GetDirectoryName(filePath)))
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));

            node.Save(filePath);
            LogHelper.Debug<XmlDoc>("Saved Node to Disk");

            OnSaved(new XmlDocFileEventArgs(filePath));
        }

        public static string GetSavePath(string folder, string path, string name, string type)
        {
            var safeName = ScrubFile(name); 
            string fullpath = string.Format("{0}\\{1}\\{2}\\{3}.config", 
                folder.TrimEnd('\\'), 
                type, 
                path.Trim('\\'),
                safeName);
            return IOHelper.MapPath(fullpath);
        }

        public static string GetSavePath(string folder, string name, string type)
        {
            var safeName = ScrubFile(name);
            string fullpath = String.Format("{0}\\{1}\\{2}.config", folder, type, safeName);
            return IOHelper.MapPath(fullpath);
        }

        public static XElement GetBackupNode(string backup, string name, string type)
        {
            string backupPath = GetSavePath(String.Format("{0}\\{1}", uSyncSettings.BackupFolder.Trim('\\'), backup), name, type);
            return GetBackupNode(backupPath);
        }

        public static XElement GetBackupNode(string backupPath)
        {
            if (!String.IsNullOrEmpty(backupPath) && File.Exists(backupPath))
            {
                return XElement.Load(backupPath);
            }

            return null;

        }
        #endregion 

        public static void ArchiveFile(string filePath, bool delete = false)
        {
            // we don't archive when we are in the backup folder...
            if (filePath.ToLower().Contains("backup"))
                return;

            // we need to remove the site folder .. and the add then add the archive one
            // arching only works on the core site files (not backups etc.)
            if (!File.Exists(filePath))
                return; 

            if (_versions)
            {
                string liveRoot = IOHelper.MapPath(uSyncIO.RootFolder).TrimEnd('\\');
                string archiveRoot = IOHelper.MapPath(uSyncIO.ArchiveFolder).TrimEnd('\\');

                var fileFolder = Path.GetDirectoryName(filePath);
                var archiveFolder = fileFolder.Replace(liveRoot, archiveRoot);
                var archiveFile = string.Format("{0}_{1}.config", Path.GetFileNameWithoutExtension(filePath), DateTime.Now.ToString("ddMMyy_HHmmss"));

                var archivePath = string.Format("{0}\\{1}", archiveFolder, archiveFile);

                if (!Directory.Exists(archiveFolder))
                    Directory.CreateDirectory(archiveFolder);

                if (File.Exists(archivePath))
                    File.Delete(archivePath);

                File.Copy(filePath, archivePath);
            }

            if (delete)
            {
                OnPreDelete(new XmlDocFileEventArgs(filePath));
                File.Delete(filePath);

                // we delete the directory if it's empty.
                var dir = Path.GetDirectoryName(filePath);
                if (IsDirectoryEmpty(dir))
                    Directory.Delete(dir);

                OnDeleted(new XmlDocFileEventArgs(filePath));
            }
        }

        private static bool IsDirectoryEmpty(string path)
        {
            if (Directory.Exists(path))
            {
                var directory = new DirectoryInfo(path);

                FileInfo[] files = directory.GetFiles();
                DirectoryInfo[] subdirs = directory.GetDirectories();

                return (files.Length == 0 && subdirs.Length == 0);
            }
            return false; 
        }

        public static void MoveChildren(string source, string dest)
        {
            var sourceDir = Path.GetDirectoryName(source);
            var destDir = Path.GetDirectoryName(dest);

            if ( Directory.Exists(sourceDir))
            {
                /*
                if (!Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);
                */

                Directory.Move(sourceDir, destDir);
            }

            if (IsDirectoryEmpty(sourceDir))
                Directory.Delete(sourceDir);
        }

        public static XmlDocument CreateDoc()
        {
            XmlDocument doc = new XmlDocument();
            XmlDeclaration dec = doc.CreateXmlDeclaration("1.0", "UTF-8", "no");
            doc.AppendChild(dec);

            return doc;
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

        public static void OnPreSave(XmlDocFileEventArgs e)
        {
            SyncFileWatcher.Pause();

            /* going to phase this out - naming is saving/saved) */
            if (preSave != null)
            {
                LogHelper.Debug<XmlDoc>("Firing on preSave EventHander : {0}", () => e.Path);
                preSave(e);
            }

            if (Saving != null)
            {
                LogHelper.Debug<XmlDoc>("Firing on Saveing EventHander : {0}", () => e.Path);
                Saving(e);
            }
        }

        public static void OnSaved(XmlDocFileEventArgs e)
        {
            if (Saved != null)
            {
                LogHelper.Debug<XmlDoc>("Firing on Saved EventHander : {0}", () => e.Path);
                Saved(e);
            }
            SyncFileWatcher.Start();

        }

        public static void OnPreDelete(XmlDocFileEventArgs e)
        {
            SyncFileWatcher.Pause();
            if (preDelete != null)
            {
                LogHelper.Debug<XmlDoc>("Firing on preDelete EventHander : {0}", () => e.Path);
                preDelete(e);
            }

            if (Deleting != null)
            {
                LogHelper.Debug<XmlDoc>("Firing on Deleting EventHander : {0}", () => e.Path);
                Deleting(e);
            }
        }

        public static void OnDeleted(XmlDocFileEventArgs e)
        {
            if (Deleted != null)
            {
                LogHelper.Debug<XmlDoc>("Firing on Deleted EventHander : {0}", () => e.Path);
                Deleted(e);
            }
            SyncFileWatcher.Start();

        }


        #region Hash values


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
                if (preValueRoot != null && preValueRoot.HasElements)
                {
                    // for pre-values, we use to remove the ids, 

                    // but to ensure the order - we create a new list of prevalues,
                    // and sort it - then replace the prevalues with that 
                    // then our hash will be in order...
                    var preValues = preValueRoot.Elements("PreValue");
                    var newPreVals = new XElement("hash_prevals");
                    List<string> vals = new List<string>();

                    foreach (var preValue in preValues)
                    {
                        var genericValue = preValue.Attribute("Value").Value;
                        vals.Add(genericValue);
                    }
                 
                    vals.Sort();
                    foreach(var v in vals)
                    {
                        newPreVals.Add(new XElement("preval", v));
                    }
                    preValueRoot.RemoveAll();
                    preValueRoot.Add(newPreVals);
                }

                var nodes = copy.Element("Nodes");
                if (nodes != null)
                    nodes.Remove();

                // tab ids 
                var tabs = copy.Element("Tabs");
                if (tabs != null && tabs.HasElements)
                {
                    foreach(var t in tabs.Elements("Tab"))
                    {
                        if ( t.Element("Id") != null )
                            t.Element("Id").Remove();
                    }
                }

                return CalculateMD5Hash(copy);
            }
            else
            {
                return CalculateMD5Hash(node);
            }
        }


        public static string CalculateDictionaryHash(XElement node)
        {
            XElement copy = new XElement(node);

            StripDictionaryIds(copy);
            return CalculateMD5Hash(copy);
        }

        private static void StripDictionaryIds(XElement node)
        {
            foreach (var val in node.Elements("Value"))
            {
                val.SetAttributeValue("LanguageId", "");
            }

            if (node.Element("DictionaryItem") != null)
            {
                StripDictionaryIds(node.Element("DictionaryItem"));
            }
        }

        //
        // Compute the MD5 of an xml file
        //
        public static string CalculateMD5Hash(XElement node)
        {
            string md5Hash = "";
            MemoryStream stream = new MemoryStream();
            node.Save(stream, SaveOptions.DisableFormatting);

            stream.Position = 0;

            using (var md5 = MD5.Create())
            {
                md5Hash = BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLower();
            }

            stream.Close();
            return md5Hash;
        }

        public static string CalculateMD5Hash(XmlDocument node, Boolean removePreValIds = false)
        {
            XElement elementNode = XElement.Load(new XmlNodeReader(node));
            return CalculateMD5Hash(elementNode, removePreValIds);
        }

        public static string CalculateMD5Hash(string input)
        {
            string hash = "";
            byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
            using (var md5 = MD5.Create())
            {
                hash = BitConverter.ToString(md5.ComputeHash(inputBytes)).Replace("-", "").ToLower();
            }
            return hash;
        }
         
        public static string ReCalculateHash(XElement node, bool removePreVals = false)
        {
            XElement copy = new XElement(node);
            if ( copy.Element("Hash") != null)
                copy.Element("Hash").Remove();

            return CalculateMD5Hash(copy, removePreVals);
        }

        public static string ReCalculateHash(XElement node, string[] values)
        {
            var hashstring = "" ;

            foreach(var val in values)
            {
                var i = node.Element(val);
                if (i != null)
                    hashstring += i.ToString(SaveOptions.DisableFormatting);
            }
            return CalculateMD5Hash(hashstring);
        }
        #endregion
    }
}
