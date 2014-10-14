using jumps.umbraco.usync.helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using umbraco.cms.businesslogic.macro;

namespace jumps.umbraco.usync.Models
{
    public static class uMacro
    {
        public static XElement SyncExport(this Macro item)
        {
            XmlDocument xmlDoc = helpers.XmlDoc.CreateDoc();
            xmlDoc.AppendChild(item.ToXml(xmlDoc));

            return XElement.Load(new XmlNodeReader(xmlDoc));
        }

        public static ChangeItem SyncImport(XElement node, bool postCheck = true )
        {
            var change = new ChangeItem
            {
                itemType = ItemType.Macro,
                changeType = ChangeType.Success,
                name = node.Element("alias").Value
            };

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(node.ToString());

            XmlNode xmlNode = xmlDoc.SelectSingleNode("//macro");
            
            var m = Macro.Import(xmlNode);
            if (m != null)
            {
                m.Save();

                if (postCheck && tracker.MacroChanged(node))
                {
                    change.changeType = ChangeType.Mismatch;
                }
            }

            return change ; 
        }
    }
}
