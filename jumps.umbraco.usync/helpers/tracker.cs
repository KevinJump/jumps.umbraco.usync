using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Umbraco.Core;
using Umbraco.Core.Services;
using Umbraco.Core.Models;

using System.IO;

using System.Xml;
using System.Xml.Linq;

using jumps.umbraco.usync.Extensions;

namespace jumps.umbraco.usync.helpers
{
    /// <summary>
    /// tracks the updates (where it can) so you can
    /// only run the changes where they might have happened
    /// </summary>
    public static class Tracker
    {
        public static bool IsContentTypeOlder(XElement node)
        {
            XElement updated = node.Element("Info").Element("Updated");
            if ( updated == null )
                return true ; 

            XElement idElement = node.Element("Info").Element("Id");
            if (idElement == null)
                return true;

            var _contentService = ApplicationContext.Current.Services.ContentTypeService;
            var item = _contentService.GetContentType(int.Parse(idElement.Value));

            if (item == null)
                return true;

            DateTime fileUpdate = DateTime.Parse(updated.Value);

            return (fileUpdate > item.UpdateDate);
        }

    }
}
