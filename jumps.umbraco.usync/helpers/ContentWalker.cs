using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using Umbraco.Core.Logging;
using Umbraco.Core.Models.EntityBase;

namespace jumps.umbraco.usync.helpers
{
    /// <summary>
    ///  walks the content gets IDs
    ///  and gets IDs and gives you paths...
    /// </summary>
    public class ContentWalker
    {
        public string GetPathFromID(int id)
        {
            var _contentService = ApplicationContext.Current.Services.ContentService;

            LogHelper.Info<ContentWalker>("Walking the path for node id: {0}", () => id);
            var content = _contentService.GetById(id);
            if (content != null)
            {
                return GetContentPath(content);
            }

            return "";
        }

        private string GetContentPath(IContent content)
        {
            var path = content.Name;
            if ( content.ParentId != -1 )
            {
                path = GetContentPath(content.Parent()) + "\\" + path ;
            }

            return path ; 

        }

        public int GetIdFromPath(string path)
        {
            var _contentService = ApplicationContext.Current.Services.ContentService;

            LogHelper.Info<ContentWalker>("Getting the id for path: {0}", () => path);

            if (!string.IsNullOrWhiteSpace(path))
            {
                var bits = path.Split('\\');                
                var rootName = bits[0];

                foreach(var contentNode in _contentService.GetByLevel(1))
                {
                    LogHelper.Info<ContentWalker>("Content at Root: {0}", ()=> contentNode.Name);
                }

                var root = _contentService.GetByLevel(1).Where(x => x.Name == rootName).FirstOrDefault();
                if ( root != null )
                {
                    LogHelper.Info<ContentWalker>("We have a matching root");
                    // recurse into the rest of it...
                    return GetLastId(_contentService, root.Id, bits, 2);
                }
            }
            return -1; 

        }

        private int GetLastId(IContentService _service, int parentId, string[] bits, int level)
        {
            LogHelper.Info<ContentWalker>("Recursing {0} - {1}", () => level, () => parentId);

            var here = _service.GetChildrenByName(parentId, bits[level - 1]).FirstOrDefault();
            // var here = node.Children().Where(x => x.Name == bits[level-1]).FirstOrDefault();            
            if (here != null)
            {
                if (bits.Length == level)
                {
                    LogHelper.Info<ContentWalker>("We are at level {0} we thing {1} is our node", () => level, () => here.Name);
                    return here.Id;
                }
                else if (bits.Length > level)
                {
                    return GetLastId(_service, here.Id, bits, level+1);
                }
                else
                {
                    // we've gone to far if we get here...
                    LogHelper.Info<ContentWalker>("Somethings gone wrong, we've gone to far...."); 
                    return -1;
                }
            }
            else
            {
                LogHelper.Info<ContentWalker>("Couldn't find {0}", () => bits[level - 1]);
                return -1; 
            }
        }
    }

    /// <summary>
    ///  the same but for media (these should be generic but i can't find the right base class...)
    /// </summary>
    public class MediaWalker
    {
        public string GetPathFromID(int id)
        {
            var _mediaService = ApplicationContext.Current.Services.MediaService;

            LogHelper.Info<MediaWalker>("Walking the path for node id: {0}", () => id);
            var media = _mediaService.GetById(id);
            if (media != null)
            {
                return GetMediaPath(media);
            }

            return "";
        }

        private string GetMediaPath(IMedia content)
        {
            var path = content.Name;
            if (content.ParentId != -1)
            {
                path = GetMediaPath(content.Parent()) + "\\" + path;
            }

            return path;

        }

        public int GetIdFromPath(string path)
        {
            var _mediaService = ApplicationContext.Current.Services.MediaService;

            LogHelper.Info<MediaWalker>("Getting the id for path: {0}", () => path);

            if (!string.IsNullOrWhiteSpace(path))
            {
                var bits = path.Split('\\');
                var rootName = bits[0];

                foreach (var contentNode in _mediaService.GetByLevel(1))
                {
                    LogHelper.Info<MediaWalker>("Content at Root: {0}", () => contentNode.Name);
                }

                var root = _mediaService.GetByLevel(1).Where(x => x.Name == rootName).FirstOrDefault();
                if (root != null)
                {
                    LogHelper.Info<MediaWalker>("We have a matching root");
                    // recurse into the rest of it...
                    return GetLastId(_mediaService, root.Id, bits, 2);
                }
            }
            return -1;

        }

        private int GetLastId(IMediaService _service, int parentId, string[] bits, int level)
        {
            LogHelper.Info<ContentWalker>("Recursing {0} - {1}", () => level, () => parentId);

            var here = _service.GetChildren(parentId).Where(x => x.Name == bits[level - 1]).FirstOrDefault();
            // var here = _service.GetChildrenByName(parentId, bits[level - 1]).FirstOrDefault();
            // var here = node.Children().Where(x => x.Name == bits[level-1]).FirstOrDefault();            
            if (here != null)
            {
                if (bits.Length == level)
                {
                    LogHelper.Info<MediaWalker>("We are at level {0} we thing {1} is our node", () => level, () => here.Name);
                    return here.Id;
                }
                else if (bits.Length > level)
                {
                    return GetLastId(_service, here.Id, bits, level + 1);
                }
                else
                {
                    // we've gone to far if we get here...
                    LogHelper.Info<MediaWalker>("Somethings gone wrong, we've gone to far....");
                    return -1;
                }
            }
            else
            {
                LogHelper.Info<MediaWalker>("Couldn't find {0}", () => bits[level - 1]);
                return -1;
            }
        }

    }
}


