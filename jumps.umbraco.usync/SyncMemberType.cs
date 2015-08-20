﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Xml;
using umbraco;
using umbraco.BusinessLogic;
using umbraco.cms.businesslogic;
using umbraco.cms.businesslogic.member;
using umbraco.cms.businesslogic.propertytype;
using Umbraco.Core;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;
using Umbraco.Core.Services;

namespace jumps.umbraco.usync
{
    public class SyncMemberType
    {
        private static Timer _saveTimer;
        private static Queue<int> _saveQueue;
        private static object _saveLock;

        public static void AttachEvents()
        {
            MemberTypeService.Saved += MemberTypeService_Saved;
            MemberTypeService.Deleting += MemberTypeService_Deleting;

            _saveTimer = new Timer(8128);
            _saveTimer.Elapsed += _saveTimer_Elapsed;

            _saveQueue = new Queue<int>();
            _saveLock = new object();
        }

        static void _saveTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            lock(_saveLock)
            {
                while (_saveQueue.Count > 0)
                {
                    var memberId = _saveQueue.Dequeue();
                    SaveToDisk(new MemberType(memberId));
                }
            }
        }

        static void MemberTypeService_Deleting(IMemberTypeService sender, Umbraco.Core.Events.DeleteEventArgs<Umbraco.Core.Models.IMemberType> e)
        {            
            if (!uSync.EventsPaused)
            {
                LogHelper.Debug<SyncMediaTypes>("DeletingMemberType for {0} items", () => e.DeletedEntities.Count());
                foreach (var memberType in e.DeletedEntities)
                {
                    LogHelper.Debug<SyncMemberType>("Removing MemberType id: {0}", () => memberType.Id);
                    var path = GetMemberPath(new MemberType(memberType.Id));
                    helpers.XmlDoc.ArchiveFile("MemberType", path, "def");
                }
            }        
        }

        static void MemberTypeService_Saved(IMemberTypeService sender, Umbraco.Core.Events.SaveEventArgs<Umbraco.Core.Models.IMemberType> e)
        {
            if (!uSync.EventsPaused)
            {
                if (e.SavedEntities.Any())
                {
                    _saveTimer.Stop();
                    LogHelper.Debug<SyncMemberType>("MemberTypeService.Saved Fired for {0} types", () => e.SavedEntities.Count());
                    foreach (var memberType in e.SavedEntities)
                    {
                        // like data types, the property saves are fired after
                        // the membership save, so we wait a bit.

                        _saveQueue.Enqueue(memberType.Id);
                        
                        // SaveToDisk(new MemberType(memberType.Id));
                    }
                    _saveTimer.Start();
                }
            }
        }

        private static string GetMemberPath(MemberType item)
        {
            string path = "";

            if (item != null)
            {
                // does this documentType have a parent 
                if (item.MasterContentType != 0)
                {
                    // recurse in to the parent to build the path
                    path = GetMemberPath(new MemberType(item.MasterContentType));
                }

                // buld the final path (as path is "" to start with we always get
                // a preceeding '/' on the path, which is nice
                path = string.Format(@"{0}\{1}", path, helpers.XmlDoc.ScrubFile(item.Alias));
            }

            return path;
        }

        public static void SaveToDisk(MemberType item)
        {
            if (item != null)
            {
                try
                {
                    XmlDocument xmlDoc = helpers.XmlDoc.CreateDoc();
                    xmlDoc.AppendChild(MemberTypeHelper.ToXml(xmlDoc, item));
                    // xmlDoc.AddMD5Hash();
                    helpers.XmlDoc.SaveXmlDoc(item.GetType().ToString(), GetMemberPath(item), "def", xmlDoc);
                }
                catch (Exception ex)
                {
                    LogHelper.Info<SyncMemberType>("uSync: Error Saving Member Type {0}, {1}",
                        () => item.Text, () => ex.ToString());
                }
            }
        }


        public static void SaveAllToDisk()
        {
            try
            {
                
                foreach (MemberType item in MemberType.GetAll)
                {
                    SaveToDisk(item);
                }
            }
            catch (Exception ex)
            {
                LogHelper.Info<SyncMemberType>("Error saving all member types {0}", () => ex.ToString());
            }
        }

        public static void ReadAllFromDisk()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            string path = IOHelper.MapPath(string.Format("{0}{1}",
                helpers.uSyncIO.RootFolder,
                "MemberType"));

            ReadFromDisk(path, false);
            ReadFromDisk(path, true);

            sw.Stop();
            LogHelper.Info<uSync>("Processed Member types ({0}ms)", () => sw.ElapsedMilliseconds);
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

                        XmlNode node = xmlDoc.SelectSingleNode("//MemberType");

                        if (node != null)
                        {
                            MemberTypeHelper.Import(node, structure);
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
                LogHelper.Info<SyncMemberType>("Read MemberType Failed {0}", () => ex.ToString());
                throw new SystemException(String.Format("Read MemberType failed {0}", ex.ToString()));
            }
        }



    }

    public class MemberTypeHelper
    {
        public static XmlElement ToXml(XmlDocument xd, MemberType mt)
        {
            if (mt == null)
                throw new ArgumentNullException("Membertype cannot be null");

            if (xd == null)
                throw new ArgumentNullException("XmlDocument cannot be null");


            XmlElement doc = xd.CreateElement("MemberType");

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
                structure.AppendChild(XmlHelper.AddTextNode(xd, "MemberType", new MemberType(child).Alias));
            }
            doc.AppendChild(structure);

            //
            // in v6 - Member types can be nested. 
            //
            if (mt.MasterContentType > 0)
            {
                MemberType pmt = new MemberType(mt.MasterContentType);

                if (pmt != null)
                    info.AppendChild(XmlHelper.AddTextNode(xd, "Master", pmt.Alias));
            }

            // stuff in the generic properties tab
            XmlElement props = xd.CreateElement("GenericProperties");
            foreach (PropertyType pt in mt.PropertyTypes)
            {
                // we only add properties that arn't in a parent (although member types are flat at the mo)
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


                    prop.AppendChild(XmlHelper.AddTextNode(xd, "CanEdit", mt.MemberCanEdit(pt) ? "True" : "False"));
                    prop.AppendChild(XmlHelper.AddTextNode(xd, "CanView", mt.ViewOnProfile(pt) ? "True" : "False"));

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
            // can also do member types, but at the mo, it's uSync's until i read 
            // about contributing to the core.
            //

            // using user 0 will come unstuck oneday
            User u = new User(0);

            // does this member type already exist ?
            string alias = xmlHelper.GetNodeValue(n.SelectSingleNode("Info/Alias"));
            if (String.IsNullOrEmpty(alias))
                throw new Exception("no alias in sync file");

            MemberType mt = null;

            try
            {
                mt = MemberType.GetByAlias(alias);
            }
            catch (Exception ex)
            {
                LogHelper.Debug<SyncMemberType>("Member type corrupt? {0}", () => ex.ToString());
            }

            if (mt == null)
            {
                // we are new 
                mt = MemberType.MakeNew(u, xmlHelper.GetNodeValue(n.SelectSingleNode("Info/Name")));
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
                    MemberType pmt = MemberType.GetByAlias(master);
                    if (pmt != null)
                        mt.MasterContentType = pmt.Id;
                }
                catch (Exception ex)
                {
                    LogHelper.Debug<SyncMemberType>("Member type corrupt? {0}", () => ex.ToString());
                }

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
                        LogHelper.Debug<SyncMemberType>("Packager: Error assigning property to tab: {0}", () => ee.ToString());
                    }
                    pt.Save();

                    // membertype stuff
                    var canEdit = bool.Parse(xmlHelper.GetNodeValue(gp.SelectSingleNode("CanEdit")));
                    mt.setMemberCanEdit(pt, canEdit);

                    var canView = bool.Parse(xmlHelper.GetNodeValue(gp.SelectSingleNode("CanView")));
                    mt.setMemberViewOnProfile(pt, canView);


                }
            }

            if (ImportStructure)
            {
                if (mt != null)
                {
                    ArrayList allowed = new ArrayList();
                    foreach (XmlNode structure in n.SelectNodes("Structure/MemberType"))
                    {
                        try
                        {
                            MemberType dtt = MemberType.GetByAlias(xmlHelper.GetNodeValue(structure));
                            if (dtt != null)
                                allowed.Add(dtt.Id);
                        }
                        catch (Exception ex)
                        {
                            LogHelper.Info<uSync>("Can't find structure MemberType - so skipping");
                        }
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
