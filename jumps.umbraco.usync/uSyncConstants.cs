using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace jumps.umbraco.usync
{
    public static class Constants
    {
        public static class ObjectTypes
        {
            public const string DataType = "DataTypeDefinition";
            public const string DocType = "DocumentType";
            public const string Dictionary = "Dictionary";
            public const string Language = "Language";
            public const string Macro = "Macro";
            public const string MediaType = "MediaType";
            public const string Stylesheet = "StyleSheet";
            public const string Template = "Template";
        }

        public const string SyncFileMask = "*.config";
    }
}
