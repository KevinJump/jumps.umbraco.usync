using jumps.umbraco.usync.helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using umbraco.cms.businesslogic.macro;
using Umbraco.Core.Logging;

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
                m.Name = node.Element("name").Value;

                RemoveMissingProperties(m, node);

                m.Save();

                if (postCheck && tracker.MacroChanged(node))
                {
                    change.changeType = ChangeType.Mismatch;
                }
            }

            return change ; 
        }

        static void RemoveMissingProperties(Macro macro, XElement node)
        {
            List<MacroProperty> macroProperties = new List<MacroProperty>();

            var props = node.Element("properties");
            if (props == null)
                return;

            foreach(var property in macro.Properties.ToList())
            {
                if ( props.Elements("property").Where(x => x.Attribute("alias").Value == property.Alias ).FirstOrDefault() == null )
                {
                    // not in the xml file.
                    macroProperties.Add(property);
                }
            }

            foreach(var property in macroProperties)
            {
                property.Delete();
            }
        }

        public static ChangeItem Rename(string oldName, string newName, bool reportOnly = false)
        {
            var change = ChangeItem.RenameStub(oldName, newName, ItemType.Macro);

            var macro = Macro.GetByAlias(oldName);
            if ( macro != null )
            {
                if (!reportOnly)
                {
                    LogHelper.Info<SyncMacro>("Renaming {0} to {1}", () => oldName, () => newName);

                    change.changeType = ChangeType.Success;
                    macro.Alias = newName;
                    macro.Save();
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
            var change = ChangeItem.DeleteStub(name, ItemType.Macro);

            var macro = Macro.GetByAlias(name);
            if (macro != null)
            {
                if (!reportOnly)
                {
                    LogHelper.Info<SyncMacro>("Deleting: {0}", () => name);
                    
                    change.changeType = ChangeType.Delete;
                    macro.Delete();
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
