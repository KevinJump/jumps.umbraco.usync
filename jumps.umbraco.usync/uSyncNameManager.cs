using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Umbraco.Core.IO;
using System.Xml.Linq;
using Umbraco.Core.Logging;

namespace jumps.umbraco.usync
{
    /// <summary>
    ///  handles the renaming of things
    /// </summary>
    public class uSyncNameManager
    {
        static object _fileLock = new object();

        public static void SaveRename(string type, string oldName, string newName, string folder)
        {
            lock (_fileLock)
            {
                var node = LoadNameFile(folder);

                var renameNode = GetNamedNode(node, "Rename", type, oldName, true);
                if (renameNode != null)
                {
                    renameNode.Value = newName;
                }

                SaveNameFile(node, folder);
            }
        }

        public static void SaveDelete(string type, string name, string folder, string id)
        {
            LogHelper.Debug<uSyncNameManager>("Saving Delete: {0} {1} {2} {3}", () => type, () => name, () => folder, () => id);
            lock (_fileLock)
            {
                var node = LoadNameFile(folder);
                var delNode = GetNamedNode(node, "Delete", type, name, true);
                if (delNode != null)
                {
                    if (id == null)
                        id = name;
                    delNode.Value = id;
                }

                SaveNameFile(node, folder);
            }
        }

        public static void CleanFileOps(string type, string name, string folder)
        {
            lock (_fileLock)
            {
                bool change = false;
                var node = LoadNameFile(folder);
                var delNode = GetNamedNode(node, "Delete", type, name, false);

                if (delNode != null)
                {
                    // clean it up and remove it.
                    delNode.Remove();
                    change = true;
                }

                var renameNode = GetNamedNode(node, "Rename", type, name, false);
                if (renameNode != null)
                {
                    renameNode.Remove();
                    change = true;
                }

                if (change)
                    SaveNameFile(node, folder);

            }
        }

        public static Dictionary<string, string> GetRenames(string type, string folder)
        {
            return GetFileOperation(type, "Rename", folder);
        }

        public static Dictionary<string, string> GetDeletes(string type, string folder)
        {
            return GetFileOperation(type, "Delete", folder);
        }

        private static Dictionary<string, string> GetFileOperation(string type, string operation, string folder)
        {
            Dictionary<string, string> ops = new Dictionary<string, string>();
            var node = LoadNameFile(folder);

            var rNode = node.Element(operation);
            if (rNode != null)
            {
                var typeNode = rNode.Element(type);
                if (typeNode != null)
                {
                    foreach (var rename in typeNode.Elements(operation))
                    {
                        ops.Add(rename.Attribute("Name").Value, rename.Value);
                    }
                }
            }

            return ops;
        }

        private static XElement LoadNameFile(string folder/* = null */)
        {
            if (folder == null)
                folder = uSyncSettings.Folder;

            string nameFile = IOHelper.MapPath( string.Format("{0}\\fileops.config", folder));
            XElement node = new XElement("uSync");
            if ( File.Exists(nameFile) )
                node = XElement.Load(nameFile);

            return node;
        }

        private static XElement GetNamedNode(XElement node, string operation, string type, string name, bool CreateIfMissing = false)
        {
            var opNode = node.Element(operation);
            if (opNode == null)
            {
                if (CreateIfMissing)
                {
                    opNode = new XElement(operation);
                    node.Add(opNode);
                }
                else
                    return null;
            }

            var typeNode = opNode.Element(type);
            if (typeNode == null)
            {
                if (CreateIfMissing)
                {
                    typeNode = new XElement(type);
                    opNode.Add(typeNode);
                }
                else
                    return null;
            }

            var nameNode = typeNode.Elements(operation).Where(x => x.Attribute("Name").Value == name).FirstOrDefault();
            if ( nameNode == null)
            {
                if (CreateIfMissing)
                {
                    nameNode = new XElement(operation);
                    nameNode.Add(new XAttribute("Name", name));
                    typeNode.Add(nameNode);
                }
                else
                    return null;
            }

            return nameNode;
        }

        private static void SaveNameFile(XElement node, string folder)
        {
            if (folder == null)
                folder = uSyncSettings.Folder;  

            string renameFile = IOHelper.MapPath( string.Format("{0}\\fileops.config", folder));

            if ( !Directory.Exists( Path.GetDirectoryName(renameFile)))
                Directory.CreateDirectory(Path.GetDirectoryName(renameFile));

            node.Save(renameFile);
        }
    }
}
