using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Umbraco.Core;
using System.IO; 

namespace jumps.umbraco.usync
{
    class SyncScripts
    {
        public SyncScripts()
        {

        }

        public static void Attach()
        {
            Umbraco.Core.Services.FileService.SavedScript += FileService_SavedScript;
            Umbraco.Core.Services.FileService.SavedStylesheet += FileService_SavedStylesheet;
        }

        static void FileService_SavedStylesheet(Umbraco.Core.Services.IFileService sender, Umbraco.Core.Events.SaveEventArgs<Umbraco.Core.Models.Stylesheet> e)
        {
            // save triggered - need to work out for what
            foreach (var thing in e.SavedEntities)
            {
                string path = string.Format("~/usync6/{0}/{1}", thing.GetRealType().ToString(), thing.Name);
                string realpath = Umbraco.Core.IO.IOHelper.MapPath(path);

                if (!Directory.Exists(realpath))
                    Directory.CreateDirectory(realpath);
            }
        }

        public static void FileService_SavedScript(Umbraco.Core.Services.IFileService sender, Umbraco.Core.Events.SaveEventArgs<Umbraco.Core.Models.Script> e)
        {
            // save triggered - need to work out for what
            foreach (var thing in e.SavedEntities)
            {
                string path = string.Format("~/usync6/{0}/{1}", thing.GetRealType().ToString(), thing.Name ) ;
                string realpath = Umbraco.Core.IO.IOHelper.MapPath(path);

                if (!Directory.Exists(realpath))
                    Directory.CreateDirectory(realpath); 
            }
        }
    }
}
