using jumps.umbraco.usync.helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using umbraco.cms.businesslogic.language;

namespace jumps.umbraco.usync.Models
{
    public static class uLanguage
    {
        public static XElement SyncExport(this Language item)
        {
            XmlDocument xmlDoc = helpers.XmlDoc.CreateDoc();
            xmlDoc.AppendChild(item.ToXml(xmlDoc));

            return XElement.Load(new XmlNodeReader(xmlDoc));
        }

        public static ChangeItem SyncImport(XElement node, bool postCheck = true)
        {
            var change = new ChangeItem
            {
                itemType = ItemType.Languages,
                changeType = ChangeType.Success,
                name = node.Element("CultureAlias").Value
            };

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(node.ToString());

            var xmlNode = xmlDoc.SelectSingleNode("//Language");

            var l = Language.Import(xmlNode);

            if ( l != null )
            {
                l.Save();

                if ( postCheck && tracker.LanguageChanged(node))
                {
                    change.changeType = ChangeType.Mismatch;
                }
            }

            return change;
        }
    }
}
