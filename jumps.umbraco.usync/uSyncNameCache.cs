using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using umbraco.cms.businesslogic.datatype;
using umbraco.cms.businesslogic.macro ;
using umbraco.cms.businesslogic.web;
using umbraco.cms.businesslogic.template;
using jumps.umbraco.usync.helpers;
using umbraco.cms.businesslogic.language;
using umbraco.cms.businesslogic.media;

namespace jumps.umbraco.usync
{
    internal static class uSyncNameCache
    {
        internal static Dictionary<int, string> Templates;
        internal static Dictionary<int, string> Stylesheets;
        internal static Dictionary<int, string> DataTypes;
        internal static Dictionary<int, string> DocumentTypes;
        internal static Dictionary<int, string> MediaTypes;
        internal static Dictionary<int, string> Macros;
        internal static Dictionary<int, string> Languages;
        // internal static Dictionary<int, string> DictionaryItems;

        internal static bool IsRenamed(DataTypeDefinition dt)
        {
            if ( dt != null )
                return HasChanged(DataTypes, dt.Id, dt.Text);
            return false;
        }

        internal static bool IsRenamed(Macro macro)
        {
            if (macro != null)
                return HasChanged(Macros, macro.Id, macro.Alias);

            return false; 
        }

        internal static bool IsRenamed(StyleSheet stylesheet)
        {
            if (stylesheet != null)
                return HasChanged(Stylesheets, stylesheet.Id, stylesheet.Text);

            return false;
        }

        internal static bool IsRenamed(Template template)
        {
            if (template != null)
            {
                var tSync = new SyncTemplate();
                var path = tSync.GetDocPath(template);
                return HasChanged(Templates, template.Id, path);
            }

            return false;
        }


        internal static bool IsRenamed(Language language)
        {
            if (language != null)
                return HasChanged(Languages, language.id, language.CultureAlias);

            return false;
        }

        internal static bool IsRenamed(DocumentType item)
        {
            if ( item != null )
            {
                var dSync = new SyncDocType();
                var path = dSync.GetDocPath(item);
                return HasChanged(DocumentTypes, item.Id, path);
            }

            return false;
        }

        internal static bool IsRenamed(MediaType item)
        {
            if (item != null)
            {
                var mSync = new SyncMediaTypes();
                var path = mSync.GetMediaPath(item);
                return HasChanged(MediaTypes, item.Id, path);
            }

            return false;

        }

        private static bool HasChanged(Dictionary<int, string> cache, int key, string newName)
        {
            if ( cache != null && cache.ContainsKey(key))
                return (!cache[key].Equals(newName));

            return false; 
        }


        internal static void UpdateCache(DataTypeDefinition dt, bool clean = true)
        {
            UpdateCache(DataTypes, dt.Id, dt.Text);

            if (clean)
                uSyncNameManager.CleanFileOps(Constants.ObjectTypes.DataType, dt.Text);
        }

        internal static void UpdateCache(Macro macro, bool clean = true)
        {
            UpdateCache(Macros, macro.Id, macro.Alias);

            if (clean)
                uSyncNameManager.CleanFileOps(Constants.ObjectTypes.Macro, macro.Alias);
        }

        internal static void UpdateCache(StyleSheet stylesheet, bool clean = true)
        {
            UpdateCache(Stylesheets, stylesheet.Id, stylesheet.Text);

            if (clean)
                uSyncNameManager.CleanFileOps(Constants.ObjectTypes.Stylesheet, stylesheet.Text);
        }

        internal static void UpdateCache(Template template, bool clean = true)
        {
            var tSync = new SyncTemplate();
            var path = tSync.GetDocPath(template);
            UpdateCache(Templates, template.Id, path);

            if (clean)
                uSyncNameManager.CleanFileOps(Constants.ObjectTypes.Template, template.Text);
        }

        internal static void UpdateCache(Language language, bool clean = true)
        {
            UpdateCache(Languages, language.id, language.CultureAlias);

            if (clean)
                uSyncNameManager.CleanFileOps(Constants.ObjectTypes.Language, language.CultureAlias);
        }

        internal static void UpdateCache(DocumentType item, bool clean = true)
        {
            var dSync = new SyncDocType();
            var path = dSync.GetDocPath(item);
            UpdateCache(DocumentTypes, item.Id, path);

            if (clean)
                uSyncNameManager.CleanFileOps(Constants.ObjectTypes.DocType, path);
        }

        internal static void UpdateCache(MediaType item, bool clean = true)
        {
            var mSync = new SyncMediaTypes();
            var path = mSync.GetMediaPath(item);
            UpdateCache(MediaTypes, item.Id, path);

            if (clean)
                uSyncNameManager.CleanFileOps(Constants.ObjectTypes.MediaType, path);
        }

        private static void UpdateCache(Dictionary<int, string> cache, int key, string name)
        {
            if (cache != null && cache.ContainsKey(key))
                cache[key] = name;
            else
                cache.Add(key, name);
        }
       
    }
}
