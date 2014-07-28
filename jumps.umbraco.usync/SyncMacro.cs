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

using System.Diagnostics;

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
                    node.AddMD5Hash();

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
            Stopwatch sw = new Stopwatch();
            sw.Start();

            string path = IOHelper.MapPath(string.Format("{0}{1}",
                helpers.uSyncIO.RootFolder,
                "Macro"));

            ReadFromDisk(path);

            sw.Stop();
            LogHelper.Info<uSync>("Processed Macros ({0}ms)", () => sw.ElapsedMilliseconds);

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
                        foreach( var macro in macros)
                        {
                            // second pass. actually make some changes...
                            ApplyUpdates(macro, node);
                        }
                    }
                }
            }
        }

        private static void ApplyUpdates(IMacro macro, XElement node)
        {

            if ( macro != null )
            {

                macro.Name = node.Element("name").Value;
                macro.ControlType = node.Element("scriptType").Value;
                macro.ControlAssembly = node.Element("scriptAssembly").Value;
                macro.XsltPath = node.Element("xslt").Value;
                macro.ScriptPath = node.Element("scriptingFile").Value;

                macro.UseInEditor = 
                    XmlDoc.GetValueOrDefault(node.Element("useInEditor"), false);

                macro.CacheDuration = 
                    XmlDoc.GetValueOrDefault(node.Element("refreshRate"), 0);

                macro.CacheByMember =
                    XmlDoc.GetValueOrDefault(node.Element("cacheByMember"), false);

                macro.CacheByPage =
                    XmlDoc.GetValueOrDefault(node.Element("cacheByPage"), false);

                macro.DontRender =
                    XmlDoc.GetValueOrDefault(node.Element("dontRender"), true);

                var macroService = ApplicationContext.Current.Services.MacroService;

                // update properties (the package service will add new ones)

                var properties = node.Element("properties");
                if ( properties != null )
                {
                    foreach(var property in properties.Elements())
                    {
                        var propertyAlias = property.Attribute("alias").Value;

                        var prop = macro.Properties.First(x => x.Alias == propertyAlias);

                        if ( prop != null)
                        {
                            prop.Name = property.Attribute("name").Value;
                            prop.EditorAlias = property.Attribute("propertyType").Value;
                        }
                    }

                }

                // remove 
                List<string> propertiesToRemove = new List<string>();

                foreach(var currentProperty in macro.Properties)
                {
                    XElement propertyNode = node.Element("properties")
                                                .Elements("property")
                                                .Where(x => x.Attribute("alias").Value == currentProperty.Alias)
                                                .SingleOrDefault();

                    if ( propertyNode == null)
                    {
                        LogHelper.Info<uSync>("Removing {0}", ()=> currentProperty.Alias);
                        propertiesToRemove.Add(currentProperty.Alias);
                    }

                }

                foreach(string alias in propertiesToRemove)
                {
                    macro.Properties.Remove(alias);
                }

                macroService.Save(macro);
            }
        }

        public static void AttachEvents()
        {
            MacroService.Saved += MacroService_Saved;
            MacroService.Deleted += MacroService_Deleted;
        }

        static void MacroService_Deleted(IMacroService sender, Umbraco.Core.Events.DeleteEventArgs<IMacro> e)
        {
            if (!uSync.EventsPaused)
            {
                foreach (var macro in e.DeletedEntities)
                {
                    XmlDoc.ArchiveFile("Macro", XmlDoc.ScrubFile(macro.Alias));
                }
            }
        }

        static void MacroService_Saved(IMacroService sender, Umbraco.Core.Events.SaveEventArgs<IMacro> e)
        {
            if (!uSync.EventsPaused)
            {
                foreach (var macro in e.SavedEntities)
                {
                    SaveToDisk(macro);
                }
            }
        }
     }
}
