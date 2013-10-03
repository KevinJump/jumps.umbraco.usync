using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml;
using System.Xml.Linq;

using Umbraco.Core;
using Umbraco.Core.Services;
using Umbraco.Core.Models; 

using Umbraco.Core.Logging ; 

namespace jumps.umbraco.usync.SyncProviders
{
    public static class TemplateSyncProvider
    {
        static IFileService _fileService ;

        static TemplateSyncProvider()
        {
            _fileService = ApplicationContext.Current.Services.FileService;
        }

        public static XElement SyncExport(this ITemplate item, bool includeContents = false )
        {
            
            XElement element = new XElement("Template", 
                                            new XElement("Name", item.Name),
                                            new XElement("Alias", item.Alias),
                                            new XElement("Key", item.Key),
                                            new XElement("Updated", item.UpdateDate),
                                            new XElement("Path", item.Path));

            if ( includeContents ) {
                element.Add(new XElement("Design", item.Content));
            }

            return element;
        }

        public static string GetSyncPath(this ITemplate item)
        {
            // TODO: Template parent ID is internal.. so we can't do this with templates... 
            return path;
        }

    }
}
