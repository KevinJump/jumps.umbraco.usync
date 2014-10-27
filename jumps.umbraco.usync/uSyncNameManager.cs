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

        public static void SaveRename(string type, string oldName, string newName)
        {
            lock (_fileLock)
            {
                var node = LoadNameFile();

                var renameNode = GetNamedNode(node, "Rename", type, oldName, true);
                if (renameNode != null)
                {
                    renameNode.Value = newName;
                }

                SaveNameFile(node);
            }
        }

        public static void SaveDelete(string type, string name, string id = null)
        {
            lock (_fileLock)
            {
                var node = LoadNameFile();
                var delNode = GetNamedNode(node, "Delete", type, name, true);
                if (delNode != null)
                {
                    if (id == null)
                        id = name;
                    delNode.Value = id;
                }

                SaveNameFile(node);
            }
        }

        public static void CleanFileOps(string type, string name)
        {
            lock (_fileLock)
            {
                bool change = false;
                var node = LoadNameFile();
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
                    SaveNameFile(node);

            }
        }

        public static Dictionary<string, string> GetRenames(string type)
        {
            return GetFileOperation(type, "Rename");
        }

        public static Dictionary<string, string> GetDeletes(string type)
        {
            return GetFileOperation(type, "Delete");
        }

        private static Dictionary<string, string> GetFileOperation(string type, string operation)
        {
            Dictionary<string, string> ops = new Dictionary<string, string>();
            var node = LoadNameFile();

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

        private static XElement LoadNameFile()
        {
            string nameFile = IOHelper.MapPath( string.Format("{0}\\fileops.xml", uSyncSettings.Folder));
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

        private static void SaveNameFile(XElement node)
        {
            string renameFile = IOHelper.MapPath( string.Format("{0}\\fileops.xml", uSyncSettings.Folder));

            if ( !Directory.Exists( Path.GetDirectoryName(renameFile)))
                Directory.CreateDirectory(Path.GetDirectoryName(renameFile));

            node.Save(renameFile);
        }
    }
}
