using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using umbraco.cms.businesslogic;
using umbraco.cms.businesslogic.language;
using Umbraco.Core.Logging;

namespace jumps.umbraco.usync.Models
{
    public static class uDictionaryItem 
    {
        public static XElement SyncExport(this Dictionary.DictionaryItem item)
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
                itemType = ItemType.Dictionary,
                name = node.Attribute("Key").Value
            };

            var item = AddOrUpdateDictionaryItem(node);

            if (item != null)
            {
                item.Save();
                change.id = item.id;
                change.name = item.key;
            }
            return change; 
        }

        private static Dictionary.DictionaryItem AddOrUpdateDictionaryItem(XElement node, Dictionary.DictionaryItem parent =null)
        {
            try
            {
                Dictionary.DictionaryItem item = null;
                var key = node.Attribute("Key").Value;

                LogHelper.Debug<SyncDictionary>("Updating Dictionary Item: {0}", () => key);

                LogHelper.Debug<SyncDictionary>("1.");
                if (!Dictionary.DictionaryItem.hasKey(key))
                {
                    if (parent != null)
                    {
                        LogHelper.Debug<SyncDictionary>("2.");
                        LogHelper.Debug<SyncDictionary>("Adding to Parent: {0}", () => parent.key);
                        Dictionary.DictionaryItem.addKey(key, " ", parent.key);
                    }
                    else
                    {
                        LogHelper.Debug<SyncDictionary>("3.");
                        LogHelper.Debug<SyncDictionary>("Adding to without Parent");
                        Dictionary.DictionaryItem.addKey(key, " ");
                    }
                }

                LogHelper.Debug<SyncDictionary>("4.");
                LogHelper.Debug<SyncDictionary>("Getting new Key: {0}", () => key);
                // creating new 
                item = new Dictionary.DictionaryItem(key);

                LogHelper.Debug<SyncDictionary>("5.");
                LogHelper.Debug<SyncDictionary>("Updating values: {0}", () => key);
                var values = node.Elements("Value");
                if (values.Any())
                {
                    // update values...
                    foreach (var val in values)
                    {
                        var alias = val.Attribute("LanguageCultureAlias").Value;
                        if (alias != null)
                        {
                            var lang = Language.GetByCultureCode(alias);
                            if (lang != null)
                            {
                                item.setValue(lang.id, val.Value);
                            }
                        }
                    }

                    //remove values we don't care about? 

                }
                var children = node.Elements("DictionaryItem");

                if (children.Any())
                {
                    // sae the parent ?
                    item.Save();

                    foreach (var child in children)
                    {
                        AddOrUpdateDictionaryItem(child, item);
                    }
                }

                return item;
            }
            catch (Exception ex)
            {
                LogHelper.Warn<SyncDictionary>("Error importing dictionary item:\n{0}", ()=> ex.ToString());
                return null;    
            }
        }
    }
}
