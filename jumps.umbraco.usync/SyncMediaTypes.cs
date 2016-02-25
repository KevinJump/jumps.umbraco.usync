﻿using System;
using System.Collections; 
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml; 
using System.IO ;

using umbraco; 
using umbraco.cms.businesslogic;
using umbraco.cms.businesslogic.datatype;
using umbraco.cms.businesslogic.media ;
using umbraco.cms.businesslogic.propertytype;
using umbraco.DataLayer;
using umbraco.cms.businesslogic.template;
using umbraco.BusinessLogic ; 

using Umbraco.Core;
using Umbraco.Core.IO;
using Umbraco.Core.Services;
using Umbraco.Core.Logging;
//using Umbraco.Core.Models;

using System.Diagnostics;
using jumps.umbraco.usync.helpers;

namespace jumps.umbraco.usync
{
    public class SyncMediaTypes
    {
        public static void SaveToDisk(MediaType item)
        {
            if (item != null)
            {
                try
                {
                    XmlDocument xmlDoc = helpers.XmlDoc.CreateDoc();
                    xmlDoc.AppendChild(MediaTypeHelper.ToXml(xmlDoc, item));
                    // xmlDoc.AddMD5Hash();
                    helpers.XmlDoc.SaveXmlDoc(item.GetType().ToString(), GetMediaPath(item), "def", xmlDoc);
                }
                catch (Exception ex)
                {
                    LogHelper.Info<SyncMediaTypes>("uSync: Error Saving Media Type {0}, {1}", 
                        ()=> item.Text, ()=> ex.ToString()); 
                }
            }
        }

        public static void SaveAllToDisk()
        {
            try
            {


                foreach (MediaType item in MediaType.GetAllAsList())
                {
                    SaveToDisk(item);
                }
            }
            catch (Exception ex)
            {
                LogHelper.Info<SyncMediaTypes>("Error saving all media types {0}", ()=> ex.ToString());
            }
        }

        private static string GetMediaPath(MediaType item)
        {
            string path = "";

            if (item != null)
            {
                // does this documentType have a parent 
                if (item.MasterContentType != 0)
                {
                    // recurse in to the parent to build the path
                    path = GetMediaPath(new MediaType(item.MasterContentType));
                }

                // buld the final path (as path is "" to start with we always get
                // a preceeding '/' on the path, which is nice
                path = string.Format(@"{0}\{1}", path, helpers.XmlDoc.ScrubFile(item.Alias));
            }

            return path; 
        }

        public static void ReadAllFromDisk()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            string path = IOHelper.MapPath(string.Format("{0}{1}",
                helpers.uSyncIO.RootFolder,
                "MediaType"));

            ReadFromDisk(path, false);
            ReadFromDisk(path, true);

            sw.Stop();
            LogHelper.Info<uSync>("Processed Media types ({0}ms)", () => sw.ElapsedMilliseconds);
        }

        
        public static void ReadFromDisk(string path, bool structure)
        {
            try
            {
                // actually read it in....
                if (Directory.Exists(path))
                {
                    foreach (string file in Directory.GetFiles(path, "*.config"))
                    {
                        XmlDocument xmlDoc = new XmlDocument();
                        xmlDoc.Load(file);

                        XmlNode node = xmlDoc.SelectSingleNode("//MediaType");

                        if (node != null)
                        {
                            MediaTypeHelper.Import(node, structure);
                        }
                    }

                    foreach (string folder in Directory.GetDirectories(path))
                    {
                        ReadFromDisk(folder, structure);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.Info<SyncMediaTypes>("Read MediaType Failed {0}", ()=> ex.ToString());
                throw new SystemException(String.Format("Read MediaType failed {0}", ex.ToString()));
            }
        }

        public static void AttachEvents()
        {
            ContentTypeService.SavedMediaType += ContentTypeService_SavedMediaType;
            ContentTypeService.DeletingMediaType += ContentTypeService_DeletingMediaType;
        }

        static void ContentTypeService_DeletingMediaType(IContentTypeService sender, Umbraco.Core.Events.DeleteEventArgs<Umbraco.Core.Models.IMediaType> e)
        {
            if (!uSync.EventsPaused)
            {
                LogHelper.Debug<SyncMediaTypes>("DeletingMediaType for {0} items", () => e.DeletedEntities.Count());
                foreach (var mediaType in e.DeletedEntities)
                {
                    helpers.XmlDoc.ArchiveFile("MediaType", GetMediaPath(new MediaType(mediaType.Id)), "def");
                }
            }
        }

        static void ContentTypeService_SavedMediaType(IContentTypeService sender, Umbraco.Core.Events.SaveEventArgs<Umbraco.Core.Models.IMediaType> e)
        {
            if (!uSync.EventsPaused)
            {
                LogHelper.Debug<SyncMediaTypes>("SaveContent Type Fired for {0} types", () => e.SavedEntities.Count());
                foreach (var mediaType in e.SavedEntities)
                {
                    SaveToDisk(new MediaType(mediaType.Id));
                }
            }
        }
    }

    public class MediaTypeHelper
    {
        public static XmlElement ToXml(XmlDocument xd, MediaType mt)
        {
            if (mt == null)
                throw new ArgumentNullException("Mediatype cannot be null");

            if (xd == null)
                throw new ArgumentNullException("XmlDocument cannot be null");


            XmlElement doc = xd.CreateElement("MediaType");

            // build the info section (name and stuff)
            XmlElement info = xd.CreateElement("Info");
            doc.AppendChild(info);

            info.AppendChild(XmlHelper.AddTextNode(xd, "Name", mt.Text));
            info.AppendChild(XmlHelper.AddTextNode(xd, "Alias", mt.Alias));
            info.AppendChild(XmlHelper.AddTextNode(xd, "Icon", mt.IconUrl));
            info.AppendChild(XmlHelper.AddTextNode(xd, "Thumbnail", mt.Thumbnail));
            info.AppendChild(XmlHelper.AddTextNode(xd, "Description", mt.Description));

            // v6 property 
            info.AppendChild(XmlHelper.AddTextNode(xd, "AllowAtRoot", mt.AllowAtRoot.ToString()));
            XmlElement structure = xd.CreateElement("Structure");
            foreach (int child in mt.AllowedChildContentTypeIDs.ToList())
            {
                structure.AppendChild(XmlHelper.AddTextNode(xd, "MediaType", new MediaType(child).Alias));
            }
            doc.AppendChild(structure);

            //
            // in v6 - media types can be nested. 
            //
            if (mt.MasterContentType > 0)
            {
                MediaType pmt = new MediaType(mt.MasterContentType);

                if (pmt != null)
                    info.AppendChild(XmlHelper.AddTextNode(xd, "Master", pmt.Alias));
            }

            // stuff in the generic properties tab
            XmlElement props = xd.CreateElement("GenericProperties");
            foreach (PropertyType pt in mt.PropertyTypes)
            {
                // we only add properties that arn't in a parent (although media types are flat at the mo)
                if (pt.ContentTypeId == mt.Id)
                {
                    XmlElement prop = xd.CreateElement("GenericProperty");
                    prop.AppendChild(XmlHelper.AddTextNode(xd, "Name", pt.Name));
                    prop.AppendChild(XmlHelper.AddTextNode(xd, "Alias", pt.Alias));
                    prop.AppendChild(XmlHelper.AddTextNode(xd, "Type", pt.DataTypeDefinition.DataType.Id.ToString()));

                    prop.AppendChild(XmlHelper.AddTextNode(xd, "Definition", pt.DataTypeDefinition.UniqueId.ToString()));
                    prop.AppendChild(XmlHelper.AddTextNode(xd, "Tab", ContentType.Tab.GetCaptionById(pt.TabId)));
                    prop.AppendChild(XmlHelper.AddTextNode(xd, "Mandatory", pt.Mandatory.ToString()));
                    prop.AppendChild(XmlHelper.AddTextNode(xd, "Validation", pt.ValidationRegExp));
                    prop.AppendChild(XmlHelper.AddCDataNode(xd, "Description", pt.Description));

                    // add this property to the tree
                    props.AppendChild(prop);
                }


            }
            // add properties to the doc
            doc.AppendChild(props);

            // tabs
            XmlElement tabs = xd.CreateElement("Tabs");
            foreach (ContentType.TabI t in mt.getVirtualTabs.ToList())
            {
                //only add tabs that aren't from a master doctype
                if (t.ContentType == mt.Id)
                {
                    XmlElement tabx = xd.CreateElement("Tab");
                    tabx.AppendChild(xmlHelper.addTextNode(xd, "Id", t.Id.ToString()));
                    tabx.AppendChild(xmlHelper.addTextNode(xd, "Caption", t.Caption));
                    tabx.AppendChild(xmlHelper.addTextNode(xd, "Sort", t.SortOrder.ToString()));
                    tabs.AppendChild(tabx);
                }
            }
            doc.AppendChild(tabs);

            return doc;
        }

        public static void Import(XmlNode n, bool ImportStructure)
        {
            if (n == null)
                throw new ArgumentNullException("Node cannot be null");

            //
            // using xmlHelper not XmlHelper because GetNodeValue has gone all 
            // Internall on us, this function probibly does belong in the core
            // (umbraco.cms.buisnesslogic.packageInstaller) so that packages
            // can also do media types, but at the mo, it's uSync's until i read 
            // about contributing to the core.
            //

            // using user 0 will come unstuck oneday
            User u = new User(0);

            // does this media type already exist ?
            string alias = xmlHelper.GetNodeValue(n.SelectSingleNode("Info/Alias"));
            if (String.IsNullOrEmpty(alias))
                throw new Exception("no alias in sync file");

            MediaType mt = null;

            try
            {
                mt = MediaType.GetByAlias(alias);
            }
            catch (Exception ex)
            {
                LogHelper.Info<SyncMediaTypes>("Media type corrupt? {0}", () => ex.ToString());
            }

            if (mt == null)
            {
                // we are new 
                mt = MediaType.MakeNew(u, xmlHelper.GetNodeValue(n.SelectSingleNode("Info/Name")));
                mt.Alias = xmlHelper.GetNodeValue(n.SelectSingleNode("Info/Alias"));
            }
            else
            {
                mt.Text = xmlHelper.GetNodeValue(n.SelectSingleNode("Info/Name"));
            }

            // core 
            mt.IconUrl = xmlHelper.GetNodeValue(n.SelectSingleNode("Info/Icon"));
            mt.Thumbnail = xmlHelper.GetNodeValue(n.SelectSingleNode("Info/Thumbnail"));
            mt.Description = xmlHelper.GetNodeValue(n.SelectSingleNode("Info/Description"));

            // v6 you can have allow at root. 
            // Allow at root (check for node due to legacy)
            bool allowAtRoot = false;
            string allowAtRootNode = xmlHelper.GetNodeValue(n.SelectSingleNode("Info/AllowAtRoot"));
            if (!String.IsNullOrEmpty(allowAtRootNode))
            {
                bool.TryParse(allowAtRootNode, out allowAtRoot);
            }
            mt.AllowAtRoot = allowAtRoot;

            //Master content type
            string master = xmlHelper.GetNodeValue(n.SelectSingleNode("Info/Master"));


            if (!String.IsNullOrEmpty(master))
            {
                // throw new System.Exception(String.Format("Throwing in {0}, master {1}", mt.Text, master));
                try
                {
                    MediaType pmt = MediaType.GetByAlias(master);
                    if (pmt != null)
                        mt.MasterContentType = pmt.Id;
                }
                catch (Exception ex)
                {
                    LogHelper.Debug<SyncMediaTypes>("Media type corrupt? {0}", () => ex.ToString());
                }

            }


            /// TABS TAKE 2: 
            var localTabs = mt.PropertyTypeGroups;
            var allTabs = mt.getVirtualTabs;

            // tab fupfix
            foreach (var vt in allTabs)
            {
                if (localTabs.Any(x => x.Name == vt.Caption && x.ContentTypeId != vt.ContentType))
                {

                    LogHelper.Debug<SyncMediaTypes>("Detected a broken tab (in both parent and child) - removing: {0}", () => vt.Caption);
                    try
                    {
                        var lt = localTabs.SingleOrDefault(x => x.Name == vt.Caption);
                        LogHelper.Debug<SyncMediaTypes>("Local: {0}->{1} : Virtual: {2}->{3}", () => lt.Id, () => lt.Name, () => vt.Id, () => vt.Caption);
                        // delete the tab from the local lot..
                        // mt.DeleteVirtualTab(lt.Id);
                        mt.Save();
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Warn<SyncMediaTypes>("Failed tring to fix the borked tab: {0}\n{1}", () => vt.Caption, () => ex.ToString());
                    }

                }

            }

            //Hashtable ht = new Hashtable();
            foreach (XmlNode t in n.SelectNodes("Tabs/Tab"))
            {
                var caption = xmlHelper.GetNodeValue(t.SelectSingleNode("Caption"));
                // is this a new tab?
                // if ( tabnames.IndexOf(";" + xmlHelper.GetNodeValue(t.SelectSingleNode("Caption")) + ";" == -1)
                if (!localTabs.Any(x => x.Name == caption))
                {
                    LogHelper.Debug<SyncMediaTypes>("This tab does not exsit at this level: {0} {1}", () => mt.Alias, () => caption);
                    // only add the tab if one of the parents doesn't have it...
                    if (!allTabs.Any(x => x.Caption == caption))
                    {
                        LogHelper.Debug<SyncMediaTypes>("This is not a tab for any parents - adding: {0}", () => caption);
                        mt.AddVirtualTab(caption);
                    }
                }
            }

            // clear cache  
            mt.ClearVirtualTabs();

            // properties..
            var propertiesToMove = new Dictionary<string, KeyValuePair<string, int>>();


            global::umbraco.cms.businesslogic.datatype.controls.Factory f =
                new global::umbraco.cms.businesslogic.datatype.controls.Factory();

            foreach (XmlNode gp in n.SelectNodes("GenericProperties/GenericProperty"))
            {
                LogHelper.Debug<SyncMediaTypes>(" >> Processing Properties: {0} -> {1}", () => mt.Alias, () => xmlHelper.GetNodeValue(gp.SelectSingleNode("Name")));
                int dfId = 0;
                Guid dtId = new Guid(xmlHelper.GetNodeValue(gp.SelectSingleNode("Type")));

                if (gp.SelectSingleNode("Definition") != null && !string.IsNullOrEmpty(xmlHelper.GetNodeValue(gp.SelectSingleNode("Definition"))))
                {
                    Guid dtdId = new Guid(xmlHelper.GetNodeValue(gp.SelectSingleNode("Definition")));
                    if (CMSNode.IsNode(dtdId))
                        dfId = new CMSNode(dtdId).Id;
                }

                if (dfId == 0)
                {
                    try
                    {
                        dfId = findDataTypeDefinitionFromType(ref dtId);
                    }
                    catch
                    {
                        throw new Exception(String.Format("Cound not find datatype with id {0}.", dtId));
                    }
                }

                //fix for ritch text editor
                if (dfId == 0 && dtId == new Guid("a3776494-0574-4d93-b7de-efdfdec6f2d1"))
                {
                    dtId = new Guid("83722133-f80c-4273-bdb6-1befaa04a612");
                    dfId = findDataTypeDefinitionFromType(ref dtId);
                }

                if (dfId != 0)
                {
                    PropertyType pt = mt.getPropertyType(xmlHelper.GetNodeValue(gp.SelectSingleNode("Alias")));
                    if (pt == null)
                    {
                        mt.AddPropertyType(
                            global::umbraco.cms.businesslogic.datatype.DataTypeDefinition.GetDataTypeDefinition(dfId),
                            xmlHelper.GetNodeValue(gp.SelectSingleNode("Alias")),
                            xmlHelper.GetNodeValue(gp.SelectSingleNode("Name"))
                            );
                        pt = mt.getPropertyType(xmlHelper.GetNodeValue(gp.SelectSingleNode("Alias")));
                    }
                    else
                    {
                        pt.DataTypeDefinition = global::umbraco.cms.businesslogic.datatype.DataTypeDefinition.GetDataTypeDefinition(dfId);
                        pt.Name = xmlHelper.GetNodeValue(gp.SelectSingleNode("Name"));
                    }

                    pt.Mandatory = bool.Parse(xmlHelper.GetNodeValue(gp.SelectSingleNode("Mandatory")));
                    pt.ValidationRegExp = xmlHelper.GetNodeValue(gp.SelectSingleNode("Validation"));
                    pt.Description = xmlHelper.GetNodeValue(gp.SelectSingleNode("Description"));

                    var tabName = xmlHelper.GetNodeValue(gp.SelectSingleNode("Tab"));
                    propertiesToMove.Add(pt.Alias, new KeyValuePair<string, int>(tabName, pt.PropertyTypeGroup));

                    pt.Save();

                }
            }

            if (ImportStructure)
            {
                if (mt != null)
                {
                    ArrayList allowed = new ArrayList();
                    foreach (XmlNode structure in n.SelectNodes("Structure/MediaType"))
                    {
                        try
                        {
                            MediaType dtt = MediaType.GetByAlias(xmlHelper.GetNodeValue(structure));
                            if (dtt != null)
                                allowed.Add(dtt.Id);
                        }
                        catch (Exception ex)
                        {
                            LogHelper.Warn<uSync>("Can't find structure mediatype - so skipping");
                        }
                    }

                    int[] adt = new int[allowed.Count];
                    for (int i = 0; i < allowed.Count; i++)
                        adt[i] = (int)allowed[i];
                    mt.AllowedChildContentTypeIDs = adt;
                }
            }

            LogHelper.Info<SyncMediaTypes>("Saving Media Type");
            mt.Save();

            if (propertiesToMove.Any())
            {
                LogHelper.Debug<SyncMediaTypes>(">> Moving Properties into the right tabs (as needed)");
                var _contentTypeService = ApplicationContext.Current.Services.ContentTypeService;
                var mediaType = _contentTypeService.GetMediaType(mt.Id);
                if (mediaType != null)
                {
                    var tabs = mediaType.PropertyGroups.ToList();
                    var moves = 0;

                    foreach (var ptmove in propertiesToMove)
                    {
                        var targetTab = tabs.FirstOrDefault(x => x.Name == ptmove.Value.Key);
                        if (targetTab == null)
                        {
                            // is it a parent 
                            if (allTabs.Any(x => x.Caption == ptmove.Value.Key))
                            {
                                // create it at this level too!
                                if (mediaType.AddPropertyGroup(ptmove.Value.Key))
                                {
                                    _contentTypeService.Save(mediaType);
                                    tabs = mediaType.PropertyGroups.ToList();
                                    targetTab = tabs.FirstOrDefault(x => x.Name == ptmove.Value.Key);
                                }
                            }
                        }

                        if (targetTab != null)
                        {
                            if (targetTab.Id != ptmove.Value.Value)
                            {
                                // it's different that we had before, perform a move.
                                mediaType.MovePropertyType(ptmove.Key, ptmove.Value.Key);
                                moves++;
                            }
                        }
                    }

                    if (moves > 0)
                    {
                        LogHelper.Debug<SyncMediaTypes>("Saving {0} tab moves", () => moves);
                        _contentTypeService.Save(mediaType);
                    }
                }
                else
                {
                    LogHelper.Warn<SyncMediaTypes>("Couldn't get the media type from the media type service;");
                }
            }
            
            /*
            foreach (MediaType.TabI t in mt.getVirtualTabs.ToList())
            {
                MediaType.FlushTabCache(t.Id, mt.Id);
            }
            */
            /*
            // need to do this more i think
            MediaType.FlushFromCache(mt.Id); 
             */

        }

        private static int findDataTypeDefinitionFromType(ref Guid dtId)
        {
            int dfId = 0;
            foreach (global::umbraco.cms.businesslogic.datatype.DataTypeDefinition df in global::umbraco.cms.businesslogic.datatype.DataTypeDefinition.GetAll())
                if (df.DataType.Id == dtId)
                {
                    dfId = df.Id;
                    break;
                }
            return dfId;
        }

    }
}
