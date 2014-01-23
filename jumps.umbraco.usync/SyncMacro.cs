using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml ;
using System.Xml.Linq;

using System.IO;

using Umbraco.Core;
using Umbraco.Core.Services;
using Umbraco.Core.Models;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;

using jumps.umbraco.usync.helpers;

namespace jumps.umbraco.usync
{
    /// <summary>
    /// Sycronizes all the macros to/from the usync folder
    /// 
    /// the macros definitions are stored compleatly in the 
    /// database although they often point to files on the 
    /// disk (scrips, user controls). 
    /// 
    /// SyncMacro uses the package API to read write the xml
    /// files for macros. no structure in macros.
    /// </summary>
    public class SyncMacro
    {
        public static void SaveToDisk(IMacro item)
        {
            if (item != null)
            {
                try
                {
                    var packagingService = ApplicationContext.Current.Services.PackagingService;

                    XElement node = packagingService.Export(item);

                    XmlDoc.SaveElement("Macro", XmlDoc.ScrubFile(item.Alias), node);
                }
                catch (Exception ex)
                {
                    LogHelper.Info<SyncMacro>("uSync: Error Saving Macro {0} - {1}", ()=> item.Name, ()=> ex.ToString());
                }
            }
        }

        public static void SaveAllToDisk()
        {
            try
            {
                var macroService = ApplicationContext.Current.Services.MacroService;

                
                foreach (var item in macroService.GetAll())
                {
                    SaveToDisk(item);
                }
            }
            catch (Exception ex)
            {
                LogHelper.Info<SyncMacro>("uSync: Error Saving All Macros {0}", ()=> ex.ToString());
            }
        }

        public static void ReadAllFromDisk()
        {
            string path = IOHelper.MapPath(string.Format("{0}{1}",
                helpers.uSyncIO.RootFolder,
                "Macro"));

            ReadFromDisk(path); 

        }

        public static void ReadFromDisk(string path)
        {
            if ( Directory.Exists(path) )
            {
                var packagingService = ApplicationContext.Current.Services.PackagingService;

                foreach (string file in Directory.GetFiles(path, "*.config"))
                {
                    XElement node = XElement.Load(file);

                    if (node != null)
                    {
                        var macros = packagingService.ImportMacros(node);
                    }
                }
            }
        }

        public static void AttachEvents()
        {
            MacroService.Saved += MacroService_Saved;
            MacroService.Deleted += MacroService_Deleted;
        }

        static void MacroService_Deleted(IMacroService sender, Umbraco.Core.Events.DeleteEventArgs<IMacro> e)
        {
            foreach(var macro in e.DeletedEntities)
            {
                XmlDoc.ArchiveFile("Macro", XmlDoc.ScrubFile(macro.Alias));
            }
        }

        static void MacroService_Saved(IMacroService sender, Umbraco.Core.Events.SaveEventArgs<IMacro> e)
        {
            foreach(var macro in e.SavedEntities)
            {
                SaveToDisk(macro);
            }
        }
     }
}
