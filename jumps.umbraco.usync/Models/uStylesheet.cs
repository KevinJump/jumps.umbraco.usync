using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using umbraco.BusinessLogic;
using umbraco.cms.businesslogic.web;

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
    }
}
