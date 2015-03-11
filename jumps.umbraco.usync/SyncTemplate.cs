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

using System.Diagnostics;

using jumps.umbraco.usync.helpers;
using jumps.umbraco.usync.Extensions;
using System.Text.RegularExpressions;

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

                    if ( node.Element("Master") == null)
                    {
                        // manually add the master..
                        int masterId = GetMasterId(item);
                        if ( masterId > 0 )
                        {
                            var master = ApplicationContext.Current.Services.FileService.GetTemplate(masterId);
                            if ( master != null )
                            {
                                node.Add(new XElement("Master", master.Alias));
                            }
                        }
                    }
                    // node.AddMD5Hash(item.Alias + item.Name);

                    XmlDoc.SaveElement("Template", GetTemplatePath(item), XmlDoc.ScrubFile(item.Alias) , node);
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

        private static int GetMasterId(ITemplate item)
        {
            // go old school to the get the master id.
            global::umbraco.cms.businesslogic.template.Template t = new global::umbraco.cms.businesslogic.template.Template(item.Id);

            if (t.MasterTemplate > 0)
                return t.MasterTemplate;

            return -1;
        }
        
        
        private static string GetTemplatePath(ITemplate item)
        {
            return GetDocPath(new global::umbraco.cms.businesslogic.template.Template(item.Id));
        }

        private static string GetDocPath(global::umbraco.cms.businesslogic.template.Template item)
        {
            string path = "";
            if (item != null)
            {
                if (item.MasterTemplate > 0)
                {
                    path = GetDocPath(new global::umbraco.cms.businesslogic.template.Template(item.MasterTemplate));
                }

                path = string.Format("{0}//{1}", path, helpers.XmlDoc.ScrubFile(item.Alias));
            }
            return path;
        }
        

        public static void ReadAllFromDisk()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            string path = IOHelper.MapPath(string.Format("{0}{1}",
                helpers.uSyncIO.RootFolder,
                "Template"));

            ReadFromDisk(path);

            sw.Stop();
            LogHelper.Info<uSync>("Processed Templates ({0}ms)", () => sw.ElapsedMilliseconds);
        }

        public static void ReadFromDisk(string path)
        {
            if (Directory.Exists(path))
            {

                foreach (string file in Directory.GetFiles(path, "*.config"))
                {
                    XElement node = XElement.Load(file) ;                                                    
                    if (node != null ) {

                        if (Tracker.TemplateChanged(node))
                        {
                            LogHelper.Info<SyncTemplate>("Importing template {0}", () => path);
                            ImportTemplate(node);
                        }
                    }
                }

                foreach (string folder in Directory.GetDirectories(path))
                {
                    ReadFromDisk(folder);
                }
            }
        }

        /// <summary>
        ///  replacing packagingService, template import. 
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private static void ImportTemplate(XElement node)
        {
            if (node.Name.LocalName != "Template")
            {
                LogHelper.Warn<SyncTemplate>("Template file not properly formatted");
                return;
            }

            var name = node.Element("Name") != null ? node.Element("Name").Value : string.Empty;
            var alias = node.Element("Alias") != null ? node.Element("Alias").Value : string.Empty;
            var master = node.Element("Master") != null ? node.Element("Master").Value : string.Empty;
            var design = node.Element("Design") != null ? node.Element("Design").Value : string.Empty;

            LogHelper.Debug<SyncTemplate>("Template Values: {0}, {1}, {2}", () => name, () => alias, () => master);

            var masterpage = MasterpagePath(alias);
            var view = ViewPath(alias);

            LogHelper.Debug<SyncTemplate>("Looking for existing template file: {0} and {1}", ()=> masterpage, ()=> view);

            ITemplate template = null;

            if (!global::System.IO.File.Exists(masterpage) && !global::System.IO.File.Exists(view))
            {
                LogHelper.Debug<SyncTemplate>("No Template files calling Package Service to Import {0}", () => alias);
                // no master page or view - use import to create the template
                var packagingService = ApplicationContext.Current.Services.PackagingService;
                var templates = packagingService.ImportTemplates(node);
                template = templates.FirstOrDefault();
            }
            else
            {
                template = ApplicationContext.Current.Services.FileService.GetTemplate(alias);

                if (template == null)
                {
                    LogHelper.Debug<SyncTemplate>("New Template but files exist {0}", () => alias);

                    var isMasterPage = IsMasterPageSyntax(design);
                    var path = isMasterPage ? MasterpagePath(alias) : ViewPath(alias);

                    template = new Template(path, name, alias);
                    if (template != null)
                    {
                        // need to load the file into template.content, so the save doesn't 
                        // destroy it.
                        LogHelper.Debug<SyncTemplate>("Importing content from disk, to preserve changes: {0}", () => path);
                        var content = global::System.IO.File.ReadAllText(path);
                        template.Content = content; 

                        SetMaster(template, master);
                        ApplicationContext.Current.Services.FileService.SaveTemplate(template);
                    }
                    else
                    {
                        LogHelper.Warn<SyncTemplate>("Error creating template (for existing files), {0}", () => alias);
                    }
                }
                else { 
                    LogHelper.Debug<SyncTemplate>("Existing Template updating stuff.. {0}", () => alias);

                    // TODO - renames - but they can't happen in the import have to be part of file ops.
                    SetMaster(template, master);
                    ApplicationContext.Current.Services.FileService.SaveTemplate(template);

                    if (template.Name != name)
                    {
                        // can't change things, because ITemplate is mainly readonly, need to use old api
                        // to do the rename
                        var t = new global::umbraco.cms.businesslogic.template.Template(template.Id);
                        t.Text = name;
                        t.Save();
                    }
                }
            }
        }

        private static void SetMaster(ITemplate template, string master)
        {
            if (!string.IsNullOrEmpty(master))
            {
                var masterTemplate = ApplicationContext.Current.Services.FileService.GetTemplate(master);
                if (masterTemplate != null)
                {
                    template.SetMasterTemplate(masterTemplate);
                }
            }
        }

        //
        // from the core 
        //
        private static bool IsMasterPageSyntax(string code)
        {
            return Regex.IsMatch(code, @"<%@\s*Master", RegexOptions.IgnoreCase) ||
                code.InvariantContains("<umbraco:Item") || code.InvariantContains("<asp:") || code.InvariantContains("<umbraco:Macro");
        }

        private static string ViewPath(string alias)
        {
            return IOHelper.MapPath(SystemDirectories.MvcViews + "/" + alias.Replace(" ", "") + ".cshtml");
        }

        private static string MasterpagePath(string alias)
        {
            return IOHelper.MapPath(SystemDirectories.Masterpages + "/" + alias.Replace(" ", "") + ".master");
        }
        //
        // end core lifting
        //

        public static void AttachEvents()
        {
            global::umbraco.cms.businesslogic.template.Template.AfterDelete += Template_AfterDelete;
            global::umbraco.cms.businesslogic.template.Template.AfterSave += Template_AfterSave;

            global::umbraco.cms.businesslogic.template.Template.New +=Template_New;;

            /*
            FileService.SavedTemplate += FileService_SavedTemplate;
            FileService.DeletedTemplate += FileService_DeletedTemplate;
             */

        }

        static void Template_New(global::umbraco.cms.businesslogic.template.Template sender, global::umbraco.cms.businesslogic.NewEventArgs e)
        {
            if ( !uSync.EventsPaused)
            {
                LogHelper.Info<uSync>("Template New");
                SaveToDisk(ApplicationContext.Current.Services.FileService.GetTemplate(sender.Alias));
            }
        }

        static void Template_AfterSave(global::umbraco.cms.businesslogic.template.Template sender, global::umbraco.cms.businesslogic.SaveEventArgs e)
        {
            if (!uSync.EventsPaused)
            {
                LogHelper.Info<uSync>("Template After Save");
                SaveToDisk(ApplicationContext.Current.Services.FileService.GetTemplate(sender.Alias));
            }
        }

        static void Template_AfterDelete(global::umbraco.cms.businesslogic.template.Template sender, global::umbraco.cms.businesslogic.DeleteEventArgs e)
        {
            if (!uSync.EventsPaused)
            {
                LogHelper.Info<uSync>("Template after delete");
                XmlDoc.ArchiveFile("Template", GetDocPath(sender), XmlDoc.ScrubFile(sender.Alias));
            }
        }

        static void FileService_DeletedTemplate(IFileService sender, Umbraco.Core.Events.DeleteEventArgs<ITemplate> e)
        {
            if (!uSync.EventsPaused)
            {
                foreach (var item in e.DeletedEntities)
                {
                    XmlDoc.ArchiveFile("Template", GetTemplatePath(item), XmlDoc.ScrubFile(item.Alias));
                }
            }
        }

        static void FileService_SavedTemplate(IFileService sender, Umbraco.Core.Events.SaveEventArgs<ITemplate> e)
        {
            if (!uSync.EventsPaused)
            {
                LogHelper.Info<uSync>("Saving Templates");
                foreach (var item in e.SavedEntities)
                {
                    SaveToDisk(item);
                }
            }
        }
    }
}
