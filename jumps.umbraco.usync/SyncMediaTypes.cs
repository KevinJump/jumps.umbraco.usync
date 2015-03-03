//using Umbraco.Core.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

using umbraco;
using umbraco.BusinessLogic;
using umbraco.cms.businesslogic;
using umbraco.cms.businesslogic.media;
using umbraco.cms.businesslogic.propertytype;

using Umbraco.Core;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;
using Umbraco.Core.Services;

using jumps.umbraco.usync.helpers;
using jumps.umbraco.usync.Models;
using System.Timers;

namespace jumps.umbraco.usync
{
    public class SyncMediaTypes : SyncItemBase<MediaType>
    {
        public SyncMediaTypes() :
            base() { }

        public SyncMediaTypes(ImportSettings settings) :
            base(settings) { }

        public override void ExportAll()
        {
            foreach (MediaType item in MediaType.GetAllAsList())
            {
                ExportToDisk(item);
            }
        }

        public override void ExportToDisk(MediaType item, string folder = null)
        {
            if (item == null)
                throw new ArgumentNullException("item");

            if (string.IsNullOrEmpty(folder))
                folder = _settings.Folder;

            XElement node = item.SyncExport();

            XmlDoc.SaveNode(folder, GetMediaPath(item), "def", node, Constants.ObjectTypes.MediaType);
        }

        Dictionary<string, string> updates; 

        public override void ImportAll()
        {
            foreach (var rename in uSyncNameManager.GetRenames(Constants.ObjectTypes.MediaType))
            {
                AddChange(
                    uMediaType.Rename(rename.Key, rename.Value, _settings.ReportOnly)
                );
            }

            foreach (var delete in uSyncNameManager.GetDeletes(Constants.ObjectTypes.MediaType))
            {
                AddChange(
                    uMediaType.Delete(delete.Value, _settings.ReportOnly)
                );
            }

            string rootFolder = IOHelper.MapPath(String.Format("{0}\\{1}", _settings.Folder, Constants.ObjectTypes.MediaType));
            updates = new Dictionary<string, string>();

            base.ImportFolder(rootFolder);

            foreach (var update in updates)
            {
                SecondPass(update.Key, update.Value);
            }
          
        }

        public override void Import(string filePath)
        {
            // LogHelper.Info<SyncMediaTypes>("Base Import {0}", ()=> filePath);
            if (!File.Exists(filePath))
                throw new ArgumentNullException("filePath");

            XElement node = XElement.Load(filePath);

            if (node.Name.LocalName != "MediaType")
                throw new ArgumentException("Not a MediaType File", filePath);

            if (_settings.ForceImport || tracker.MediaTypeChanged(node))
            {
                if (!_settings.ReportOnly)
                {
                    var backup = Backup(node);

                    LogHelper.Info<SyncMediaTypes>("SyncImport < in");
                    ChangeItem change = uMediaType.SyncImport(node);
                    LogHelper.Info<SyncMediaTypes>("SyncImport > out");

                    if (change.changeType == ChangeType.Success)
                    {
                        // add to updates for second pass.
                        updates.Add(filePath, backup);
                    }
                }
                else
                {
                    AddChange(new ChangeItem
                    {
                        changeType = ChangeType.WillChange,
                        itemType = ItemType.MediaItem,
                        name = node.Element("Info").Element("Name").Value,
                        message = "Reporting: will update"
                    });
                }
            }
            else
            {
                AddNoChange(ItemType.MediaItem, filePath);
            }
        }

        private void SecondPass(string filePath, string backup)
        {
            LogHelper.Info<SyncMediaTypes>("Second Pass");
            if (!File.Exists(filePath))
                throw new ArgumentNullException("filePath");

            XElement node = XElement.Load(filePath);

            var change = uMediaType.SyncImportFitAndFix(node);

            if (uSyncSettings.ItemRestore && change.changeType == ChangeType.Mismatch)
            {
                Restore(backup);
                change.changeType = ChangeType.RolledBack;
            }
            uSyncReporter.WriteToLog("Imported MediaType [{0}] {1}", change.name, change.changeType.ToString());

            AddChange(change);
        }


        protected override string Backup(XElement node)
        {
            if (uSyncSettings.ItemRestore || uSyncSettings.FullRestore || uSyncSettings.BackupOnImport)
            {

                var alias = node.Element("Info").Element("Alias").Value;
                var mediaType = MediaType.GetByAlias(alias);

                if (mediaType != null)
                {
                    ExportToDisk(mediaType, _settings.BackupPath);
                    return XmlDoc.GetSavePath(_settings.BackupPath, GetMediaPath(mediaType), "def", Constants.ObjectTypes.MediaType);
                }
            }

            return "";
        }

        protected override void Restore(string backup)
        {
            XElement backupNode = XmlDoc.GetBackupNode(backup);
            if ( backupNode != null)
            {
                uMediaType.SyncImportFitAndFix(backupNode, false);
            }
        }

        internal string GetMediaPath(MediaType item)
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

        private static Timer _saveTimer;
        private static Queue<int> _saveQueue = new Queue<int>();
        private static object _saveLock = new object();
        private static string _eventFolder = "";

        public static void AttachEvents(string folder)
        {
            InitNameCache();
            _eventFolder = folder;
            ContentTypeService.SavedMediaType += ContentTypeService_SavedMediaType;
            ContentTypeService.DeletingMediaType += ContentTypeService_DeletingMediaType;

            _saveTimer = new Timer(2048);
            _saveTimer.Elapsed += _saveTimer_Elapsed;

        }

        static void _saveTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            lock (_saveLock)
            {
                var syncMedia = new SyncMediaTypes();
                while (_saveQueue.Count > 0)
                {
                    int mediaTypeId = _saveQueue.Dequeue();
                    var mt = new MediaType(mediaTypeId);

                    if (uSyncNameCache.IsRenamed(mt))
                    {
                        var newPath = syncMedia.GetMediaPath(mt);

                        uSyncNameManager.SaveRename(Constants.ObjectTypes.MediaType,
                            uSyncNameCache.MediaTypes[mt.Id], newPath);

                        XmlDoc.ArchiveFile(XmlDoc.GetSavePath(_eventFolder, uSyncNameCache.MediaTypes[mt.Id], "def", Constants.ObjectTypes.MediaType), true);

                        XmlDoc.MoveChildren(
                            XmlDoc.GetSavePath(_eventFolder, uSyncNameCache.MediaTypes[mt.Id], "def", Constants.ObjectTypes.MediaType),
                            XmlDoc.GetSavePath(_eventFolder, newPath, "def", Constants.ObjectTypes.MediaType)
                            );

                        if (mt.HasChildren)
                        {
                            foreach (var child in mt.GetChildTypes())
                            {
                                var childType = new MediaType(child.Id);
                                syncMedia.ExportToDisk(childType, _eventFolder);
                            }
                        }

                    }

                    uSyncNameCache.UpdateCache(mt);

                    syncMedia.ExportToDisk(mt, _eventFolder);

                }
            }
        }

        static void ContentTypeService_DeletingMediaType(IContentTypeService sender, Umbraco.Core.Events.DeleteEventArgs<Umbraco.Core.Models.IMediaType> e)
        {
            if (!uSync.EventPaused)
            {
                LogHelper.Debug<SyncMediaTypes>("DeletingMediaType for {0} items", () => e.DeletedEntities.Count());
                if (e.DeletedEntities.Count() > 0)
                {
                    var syncMedia = new SyncMediaTypes();

                    foreach (var mediaType in e.DeletedEntities)
                    {
                        var savePath = syncMedia.GetMediaPath(new MediaType(mediaType.Id));
                        XmlDoc.ArchiveFile(XmlDoc.GetSavePath(_eventFolder, savePath, "def", Constants.ObjectTypes.MediaType), true);
                    }
                }
            }
        }

        static void ContentTypeService_SavedMediaType(IContentTypeService sender, Umbraco.Core.Events.SaveEventArgs<Umbraco.Core.Models.IMediaType> e)
        {
            if (!uSync.EventPaused)
            {
                lock (_saveLock)
                {
                    if (e.SavedEntities.Count() > 0)
                    {
                        _saveTimer.Stop();

                        foreach (var mediaType in e.SavedEntities)
                        {
                            _saveQueue.Enqueue(mediaType.Id);
                        }

                        _saveTimer.Start();
                    }

                }
            }
        }

        private static void InitNameCache()
        {
            if ( uSyncNameCache.MediaTypes == null)
            {
                uSyncNameCache.MediaTypes = new Dictionary<int,string>();

                var mediaSync = new SyncMediaTypes();

                foreach (MediaType item in MediaType.GetAllAsList())
                {
                    var path = mediaSync.GetMediaPath(item);
                    uSyncNameCache.MediaTypes.Add(item.Id, path);
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
                var media = ApplicationContext.Current.Services.ContentTypeService.GetMediaType(alias);
                if (media != null)
                {
                    mt = new MediaType(media.Id);
                }
            }
            catch (Exception ex)
            {
                LogHelper.Debug<SyncMediaTypes>("Media type corrupt? {0}", ()=> ex.ToString()); 
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
                MediaType pmt = MediaType.GetByAlias(master);
                if (pmt != null)
                    mt.MasterContentType = pmt.Id;
                
            }

            //tabs
            ContentType.TabI[] tabs = mt.getVirtualTabs;

            // load the current tabs
            string tabnames = ";";
            for (int t = 0; t < tabs.Length; t++)
            {
                tabnames += tabs[t].Caption + ";";
            }

            Hashtable ht = new Hashtable();
            foreach (XmlNode t in n.SelectNodes("Tabs/Tab"))
            {
                // is this a new tab?
                // if ( tabnames.IndexOf(";" + xmlHelper.GetNodeValue(t.SelectSingleNode("Caption")) + ";" == -1)
                if (!tabnames.Contains(";" + xmlHelper.GetNodeValue(t.SelectSingleNode("Caption")) + ";"))
                {
                    ht.Add(int.Parse(xmlHelper.GetNodeValue(t.SelectSingleNode("Id"))),
                        mt.AddVirtualTab(xmlHelper.GetNodeValue(t.SelectSingleNode("Caption"))));

                }
            }
            // clear cache  
            mt.ClearVirtualTabs();

            // put tabs in a hashtable, so we can check they exist when we add properties.
            Hashtable tabList = new Hashtable();
            foreach (ContentType.TabI t in mt.getVirtualTabs.ToList())
            {
                if (!tabList.ContainsKey(t.Caption))
                    tabList.Add(t.Caption, t.Id);
            }

            // properties..
            global::umbraco.cms.businesslogic.datatype.controls.Factory f =
                new global::umbraco.cms.businesslogic.datatype.controls.Factory();

            foreach (XmlNode gp in n.SelectNodes("GenericProperties/GenericProperty"))
            {
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

                    // tab
                    try
                    {
                        if (tabList.ContainsKey(xmlHelper.GetNodeValue(gp.SelectSingleNode("Tab"))))
                            pt.TabId = (int)tabList[xmlHelper.GetNodeValue(gp.SelectSingleNode("Tab"))];
                    }
                    catch (Exception ee)
                    {
                        LogHelper.Debug<SyncMediaTypes>("Packager: Error assigning property to tab: {0}", ()=> ee.ToString());
                    }
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
                        MediaType dtt = MediaType.GetByAlias(xmlHelper.GetNodeValue(structure));
                        if (dtt != null)
                            allowed.Add(dtt.Id);
                    }
                    int[] adt = new int[allowed.Count];
                    for (int i = 0; i < allowed.Count; i++)
                        adt[i] = (int)allowed[i];
                    mt.AllowedChildContentTypeIDs = adt;
                }
            }

            mt.Save();
            /*
            foreach (MediaType.TabI t in mt.getVirtualTabs.ToList())
            {
                MediaType.FlushTabCache(t.Id, mt.Id);
            }

            // need to do this more i think
            MediaType.FlushFromCache(mt.Id); 
             */

        }


        public static ChangeItem SyncImportFitAndFix(Umbraco.Core.Models.IMediaType item, XElement node, bool postCheck = true)
        {
            LogHelper.Info<SyncMediaTypes>("Fix and Fix");
            var change = new ChangeItem
            {
                itemType = ItemType.MediaItem,
                changeType = ChangeType.Success
            };

            if (item != null)
            {
                change.id = item.Id;
                change.name = item.Name;

                var iNode = node.Element("Info");
                if (iNode != null && iNode.HasElements)
                {
                    item.Icon = iNode.Element("Icon").Value;
                    item.Thumbnail = iNode.Element("Thumbnail").Value;
                    item.Description = iNode.Element("Description").Value;
                    item.AllowedAsRoot = bool.Parse(iNode.Element("AllowAtRoot").Value);
                }

                var sNode = node.Element("Structure");
                if (sNode != null && sNode.HasElements)
                {
                    // structure...
                    uDocType.ImportStructure(item, node, "MediaType");
                }

                var pNode = node.Element("GenericProperties");
                if (pNode != null && pNode.HasElements)
                {
                    // properties...
                    uDocType.RemoveMissingProperties(item, node);
                    uDocType.UpdateExistingProperties(item, node);
                }

                var tNode = node.Element("Tabs");
                if (tNode != null && tNode.HasElements)
                {
                    uDocType.TabSortOrder(item, node);
                }
            }
            return null;
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
