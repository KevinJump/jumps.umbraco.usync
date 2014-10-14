using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using umbraco.BusinessLogic;
using umbraco.cms.businesslogic.template;
using Umbraco.Core;

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
    }
}
