using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using umbraco.BusinessLogic;
using umbraco.cms.businesslogic.web;
using Umbraco.Core.Logging;

namespace jumps.umbraco.usync.Models
{
    public static class uStylesheet
    {
        static User _user; 
        static uStylesheet()
        {
            _user = new User(0);
        }

        public static XElement SyncExport(this StyleSheet item)
        {
            XmlDocument xmlDoc = helpers.XmlDoc.CreateDoc();
            xmlDoc.AppendChild(item.ToXml(xmlDoc));

            return XElement.Load(new XmlNodeReader(xmlDoc));
        }

        public static ChangeItem SyncImport(XElement node, bool postCheck = true)
        {
            var change = new ChangeItem
            {
                itemType = ItemType.Stylesheet,
                changeType = ChangeType.Success,
                name = node.Element("Name").Value
            };

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(node.ToString());

            var xmlNode = xmlDoc.SelectSingleNode("//Stylesheet");

            var s = StyleSheet.Import(xmlNode, _user);
            if (s != null)
            {
                s.Save();
                change.id = s.Id;
            }

            return change; 
        }

        public static ChangeItem Rename(string oldName, string newName, bool reportOnly = false)
        {
            var change = ChangeItem.RenameStub(oldName, newName, ItemType.Stylesheet);

            var stylesheet = StyleSheet.GetByName(oldName);
            if (stylesheet != null)
            {
                if (!reportOnly)
                {
                    LogHelper.Info<SyncStylesheet>("Renaming {0} to {1}", () => oldName, () => newName);

                    change.changeType = ChangeType.Success;
                   
                    // rename (might be a bit harder to do - given their is a file behind it?)
                    stylesheet.Text = newName;
                    stylesheet.Save();
                }
                else
                {
                    change.changeType = ChangeType.WillChange;
                }
            }

            return change;
        }

        public static ChangeItem Delete(string name, bool reportOnly = false)
        {
            var change = ChangeItem.DeleteStub(name, ItemType.Stylesheet);

            var stylesheet = StyleSheet.GetByName(name);
            if (stylesheet != null)
            {
                if (!reportOnly)
                {
                    LogHelper.Info<SyncStylesheet>("Deleting {0}", () => name);

                    change.changeType = ChangeType.Delete;
                    stylesheet.delete();
                }
                else
                {
                    change.changeType = ChangeType.WillChange;
                }
            }

            return change;

        }
    }
}
