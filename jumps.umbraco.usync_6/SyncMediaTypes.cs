using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO; 

using Umbraco.Core; 
using Umbraco.Core.Events;
using Umbraco.Core.Services;
using Umbraco.Core.Serialization;
using Umbraco.Core.IO;

using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.UnitOfWork; 

namespace jumps.umbraco.usync
{
    public class SyncMediaTypes
    {
        public SyncMediaTypes()
        {

        }

        public void Write()
        {
            string uSyncRoot = "~/uSync6/" ;
            if (!Directory.Exists(IOHelper.MapPath(uSyncRoot)))
                Directory.CreateDirectory(IOHelper.MapPath(uSyncRoot));
          
            var xmlSerializer = new ServiceStackXmlSerializer();
            var fileSystem = new PhysicalFileSystem(uSyncRoot); 

            Umbraco.Core.Persistence.RepositoryFactory persistanceFactory = new RepositoryFactory();
            var db = new PetaPocoUnitOfWorkProvider(); 
            
            // var dataDefTypes = persistanceFactory.CreateDataTypeDefinitionRepository(db.GetUnitOfWork());
            var dataDefTypes = persistanceFactory.CreateMediaTypeRepository(db.GetUnitOfWork()); 
            foreach (var item in dataDefTypes.GetAll())
            {

                var serializer = new SerializationService(xmlSerializer); 
          
                var result = serializer.ToStream(item);
                fileSystem.AddFile( ScrubFileName(item.Name) + ".usync.xml", result.ResultStream, true) ; 
            }

        }

        public string ScrubFileName(string filename)
        {
            // TODO: a better scrub

            StringBuilder sb = new StringBuilder(filename);
            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char item in invalid)
            {
                sb.Replace(item.ToString(), "");
            }

            return sb.ToString();
        }
            



        public void Attach()
        {
        }

        void ContentTypeService_SavedMediaType(IContentTypeService sender, SaveEventArgs<Umbraco.Core.Models.IMediaType> e)
        {
        }

    }
}
