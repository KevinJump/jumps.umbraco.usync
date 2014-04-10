using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml;
using System.Xml.Linq;

using System.Text.RegularExpressions;

using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Services;

using Umbraco.Core.Logging;

using jumps.umbraco.usync.helpers;


namespace jumps.umbraco.usync.Extensions
{
    /*
    public static class ITemplateExtensions
    {
        public static XElement ExportToXML(this ITemplate template)
        {
            var _fileService = ApplicationContext.Current.Services.FileService;
            
            XElement xml = new XElement("Template");
            xml.Add(new XElement("Name", template.Name));
            xml.Add(new XElement("Alias", template.Alias));

            string render = IsMasterPageSyntax(template.Content) ? "WebForms" : "Mvc";
            xml.Add(new XElement("RenderEngine", render));

            TemplateNode node = ApplicationContext.Current.Services.FileService.GetTemplateNode(template.Alias);
            if ( node != null )
            {
                if ( node.Parent != null )
                {
                    xml.Add(new XElement("Master", node.Parent.Template.Id.ToString()));
                    xml.Add(new XElement("MasterAlias", node.Parent.Template.Alias));
                }
            }

            return xml;
        }

        /// <summary>
        ///  import a template
        /// </summary>
        /// <param name="template"></param>
        public static IEnumerable<ITemplate> ImportTemplate(this XElement element)
        {
            var fileService = ApplicationContext.Current.Services.FileService;

            var name = element.Name.LocalName;
            if (name.Equals("Templates") == false && name.Equals("Template") == false)
            {
                throw new ArgumentException("The passed in XElement is not valid! It does not contain a root element called 'Templates' for multiple imports or 'Template' for a single import.");
            }

            var templates = new List<ITemplate>();
            var templateElements = name.Equals("Templates")
                                       ? (from doc in element.Elements("Template") select doc).ToList()
                                       : new List<XElement> { element };

            foreach (XElement templateElement in templateElements)
            {
                var alias = templateElement.Element("Alias").Value;
                var render = templateElement.Element("RenderEngine").Value;


                ITemplate existing = fileService.GetTemplate(alias);

                var template = existing ?? new Template(path, name, alias);

            }

            return null;
        }

        /// <summary>
        /// from the core - is this a master page? - i would prefere to check path but i don't get it on save?
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        private static bool IsMasterPageSyntax(string code)
        {
            return Regex.IsMatch(code, @"<%@\s*Master", RegexOptions.IgnoreCase) ||
                code.InvariantContains("<umbraco:Item") || code.InvariantContains("<asp:") || code.InvariantContains("<umbraco:Macro");
        }

    }
     */
}
