using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using umbraco.BusinessLogic;
using umbraco.cms.businesslogic.template;
using Umbraco.Core;
using Umbraco.Core.Logging;

namespace jumps.umbraco.usync.Models
{
    public static class uTemplate
    {
        static User _user; 
        static uTemplate()
        {
            _user = new User(0);
        }

        public static XElement SyncExport(this Template item)
        {
            XmlDocument xmlDoc = helpers.XmlDoc.CreateDoc();
            xmlDoc.AppendChild(item.ToXml(xmlDoc));

            return XElement.Load(new XmlNodeReader(xmlDoc));

        }

        public static ChangeItem SyncImport(XElement node, bool postCheck = true)
        {
            var change = new ChangeItem
            {
                changeType = ChangeType.Success,
                itemType = ItemType.Template,
                name = node.Element("Alias").Value
            };

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(node.ToString());

            var xmlNode = xmlDoc.SelectSingleNode("//Template");

            Template t = Template.Import(xmlNode, _user);

            if (t != null)
            {
                var name = node.Element("Name");
                if (name != null && name.Value != t.Text)
                    t.Text = name.Value;

                change.id = t.Id;
                change.name = t.Text;

                var masterNode = node.Element("Master");
                if (masterNode != null && !string.IsNullOrEmpty(masterNode.Value))
                {
                    string master = masterNode.Value;

                    if (master.Trim() != "")
                    {
                        Template masterTemplate = Template.GetByAlias(master);
                        if (masterTemplate != null)
                        {
                            t.MasterTemplate = masterTemplate.Id;
                        }
                        t.Save();
                    }
                }
            }

            return change;

        }
        internal static Template FindTemplateByPath(string path)
        {
            var pathBits = path.Split('\\');
            var doc = Template.GetByAlias(pathBits[pathBits.Length - 1]);

            if (doc != null)
                return doc;

            return null;
        }


        internal static ChangeItem Delete(string path, bool reportOnly = false)
        {
            var change = ChangeItem.DeleteStub(path, ItemType.Template);

            var item = FindTemplateByPath(path);
            if (item != null)
            {
                if (!reportOnly)
                {
                    item.delete();
                    change.changeType = ChangeType.Delete;
                }
                else
                {
                    change.changeType = ChangeType.WillChange;
                }
            }


            return change;
        }

        internal static ChangeItem Rename(string oldPath, string newPath, bool reportOnly = false)
        {
            var oldName = System.IO.Path.GetFileName(oldPath);
            var newName = System.IO.Path.GetFileName(newPath);

            var change = ChangeItem.RenameStub(oldName, newName, ItemType.Template);

            var item = Template.GetByAlias(oldName);
            if (item != null)
            {
                if (!reportOnly)
                {
                    LogHelper.Info<SyncTemplate>("Renaming : {0} to {1}", () => oldName, () => newName);

                    change.changeType = ChangeType.Success;
                    item.Alias = newName;
                    item.Save();
                }
                else
                {
                    change.changeType = ChangeType.WillChange;
                }
            }

            return change;
        }


        static Template GetNewParent(string newPath)
        {
            return FindTemplateByPath(System.IO.Path.GetDirectoryName(newPath));

        }
    }
}
