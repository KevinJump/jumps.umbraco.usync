using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml;
using System.Xml.Linq;

using System.IO; 

using Umbraco.Core;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;

using Umbraco.Core.Models;
using Umbraco.Core.Services;

using jumps.umbraco.usync.helpers;

namespace jumps.umbraco.usync
{
    /// <summary>
    ///  syncornizes the templates with the usync folder
    ///  
    /// templates / partial views are almost compleatly
    /// stored on disk, but the umbraco database stores
    /// and ID, alias and parent, to maintain structure
    /// 
    /// SyncTemplate uses the packaging API to import and
    /// export the templates. 
    /// </summary>
    public class SyncTemplate
    {

        public static void SaveToDisk(ITemplate item)
        {
            if (item != null)
            {
                try
                {
                    var packagingService = ApplicationContext.Current.Services.PackagingService;

                    XElement node = packagingService.Export(item, true);

                    XmlDoc.SaveElement("Template", XmlDoc.ScrubFile(item.Alias) , node);
                }
                catch (Exception ex)
                {
                    LogHelper.Info<SyncTemplate>("uSync: Error Saving Template {0} - {1}", 
                        ()=>item.Name, ()=>ex.ToString());
                }
            }
        }

        public static void SaveAllToDisk()
        {
            var fileService = ApplicationContext.Current.Services.FileService; 

            try
            {
                foreach(Template item in fileService.GetTemplates() )
                { 
                    SaveToDisk(item);
                }
            }
            catch( Exception ex )
            {
                LogHelper.Info<SyncTemplate>("uSync: Error saving all templates {0}", ()=> ex.ToString()); 
            }
        }

        /*
        private static string GetDocPath(Template item)
        {
            string path = "";
            if (item != null)
            {
                if (item != 0)
                {
                    path = GetDocPath(new Template(item.MasterTemplate));
                }

                path = string.Format("{0}//{1}", path, helpers.XmlDoc.ScrubFile(item.Alias));
            }
            return path;
        }
        */

        public static void ReadAllFromDisk()
        {

            string path = IOHelper.MapPath(string.Format("{0}{1}",
                helpers.uSyncIO.RootFolder,
                "Template"));

            ReadFromDisk(path);
        }

        public static void ReadFromDisk(string path)
        {
            if (Directory.Exists(path))
            {
                var packagingService = ApplicationContext.Current.Services.PackagingService ; 

                foreach (string file in Directory.GetFiles(path, "*.config"))
                {
                    XElement node = XElement.Load(file) ;                                                    
                    if (node != null ) {
                        LogHelper.Debug<SyncTemplate>("Importing template {0}", ()=> path);
                        
                        var templates = packagingService.ImportTemplates(node);
                    }
                }

                foreach (string folder in Directory.GetDirectories(path))
                {
                    ReadFromDisk(folder);
                }
            }
        }

        public static void AttachEvents()
        {
            global::umbraco.cms.businesslogic.template.Template.AfterDelete += Template_AfterDelete;
            global::umbraco.cms.businesslogic.template.Template.AfterSave += Template_AfterSave;

            /*
            FileService.SavedTemplate += FileService_SavedTemplate;
            FileService.DeletedTemplate += FileService_DeletedTemplate;
             */

        }

        static void Template_AfterSave(global::umbraco.cms.businesslogic.template.Template sender, global::umbraco.cms.businesslogic.SaveEventArgs e)
        {
            LogHelper.Info<uSync>("Template After Save");
            SaveToDisk(ApplicationContext.Current.Services.FileService.GetTemplate(sender.Alias)); 
        }

        static void Template_AfterDelete(global::umbraco.cms.businesslogic.template.Template sender, global::umbraco.cms.businesslogic.DeleteEventArgs e)
        {
            LogHelper.Info<uSync>("Template after delete");
            XmlDoc.ArchiveFile("Template", XmlDoc.ScrubFile(sender.Alias)); 
        }

        static void FileService_DeletedTemplate(IFileService sender, Umbraco.Core.Events.DeleteEventArgs<ITemplate> e)
        {
            foreach(var item in e.DeletedEntities )
            {
                XmlDoc.ArchiveFile("Template", XmlDoc.ScrubFile(item.Alias));
            }
        }

        static void FileService_SavedTemplate(IFileService sender, Umbraco.Core.Events.SaveEventArgs<ITemplate> e)
        {
            LogHelper.Info<uSync>("Saving Templates"); 
            foreach(var item in e.SavedEntities)
            {
                SaveToDisk(item);
            }
        }
    }
}
