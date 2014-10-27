using jumps.umbraco.usync.helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using umbraco.cms.businesslogic.language;
using Umbraco.Core.Logging;

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
                name = node.Attribute("CultureAlias").Value
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

        public static ChangeItem Rename(string oldName, string newName, bool reportOnly = false)
        {
            var change = ChangeItem.RenameStub(oldName, newName, ItemType.Languages);

            var lang = Language.GetByCultureCode(oldName);
            if (lang != null)
            {
                if (!reportOnly)
                {
                    LogHelper.Info<SyncLanguage>("Renaming {0} to {1}", () => oldName, () => newName);

                    change.changeType = ChangeType.Success;
                    lang.CultureAlias = newName;
                    lang.Save();
                }
                else
                {
                    change.changeType = ChangeType.WillChange;
                    change.message = "Will rename";
                }
            }

            return change;

        }

        public static ChangeItem Delete(string name, bool reportOnly = false)
        {
            var change = ChangeItem.DeleteStub(name, ItemType.Languages);

            var lang = Language.GetByCultureCode(name);
            if ( lang != null )
            {
                if ( !reportOnly )
                {
                    LogHelper.Info<SyncLanguage>("Deleting {0}", () => name);

                    lang.Delete();
                    change.changeType = ChangeType.Delete;
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
