using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Resources;
using System.ComponentModel;
using System.Globalization;

namespace Aspen.Web.FileProcessor.FileParserEngine.Resources
{
    //[CompilerGenerated, GeneratedCode("System.Resources.Tools.StronglyTypedResourceBuilder", "2.0.0.0"), DebuggerNonUserCode]
    internal class ExceptionMessage
    {
        // Fields
        private static CultureInfo resourceCulture;
        private static ResourceManager resourceMan;

        // Methods
        internal ExceptionMessage()
        {
        }

        // Properties
        internal static string BufferSizeTooSmall
        {
            get
            {
                return ResourceManager.GetString("BufferSizeTooSmall", resourceCulture);
            }
        }

        internal static string CannotMovePreviousRecordInForwardOnly
        {
            get
            {
                return ResourceManager.GetString("CannotMovePreviousRecordInForwardOnly", resourceCulture);
            }
        }

        internal static string CannotReadRecordAtIndex
        {
            get
            {
                return ResourceManager.GetString("CannotReadRecordAtIndex", resourceCulture);
            }
        }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        internal static CultureInfo Culture
        {
            get
            {
                return resourceCulture;
            }
            set
            {
                resourceCulture = value;
            }
        }

        internal static string EnumerationFinishedOrNotStarted
        {
            get
            {
                return ResourceManager.GetString("EnumerationFinishedOrNotStarted", resourceCulture);
            }
        }

        internal static string EnumerationVersionCheckFailed
        {
            get
            {
                return ResourceManager.GetString("EnumerationVersionCheckFailed", resourceCulture);
            }
        }

        internal static string FieldHeaderNotFound
        {
            get
            {
                return ResourceManager.GetString("FieldHeaderNotFound", resourceCulture);
            }
        }

        internal static string FieldIndexOutOfRange
        {
            get
            {
                return ResourceManager.GetString("FieldIndexOutOfRange", resourceCulture);
            }
        }

        internal static string MalformedFileException
        {
            get
            {
                return ResourceManager.GetString("MalformedCsvException", resourceCulture);
            }
        }

        internal static string MissingFieldActionNotSupported
        {
            get
            {
                return ResourceManager.GetString("MissingFieldActionNotSupported", resourceCulture);
            }
        }

        internal static string NoCurrentRecord
        {
            get
            {
                return ResourceManager.GetString("NoCurrentRecord", resourceCulture);
            }
        }

        internal static string NoHeaders
        {
            get
            {
                return ResourceManager.GetString("NoHeaders", resourceCulture);
            }
        }

        internal static string NotEnoughSpaceInArray
        {
            get
            {
                return ResourceManager.GetString("NotEnoughSpaceInArray", resourceCulture);
            }
        }

        internal static string ParseErrorActionInvalidInsideParseErrorEvent
        {
            get
            {
                return ResourceManager.GetString("ParseErrorActionInvalidInsideParseErrorEvent", resourceCulture);
            }
        }

        internal static string ParseErrorActionNotSupported
        {
            get
            {
                return ResourceManager.GetString("ParseErrorActionNotSupported", resourceCulture);
            }
        }

        internal static string ReaderClosed
        {
            get
            {
                return ResourceManager.GetString("ReaderClosed", resourceCulture);
            }
        }

        internal static string RecordIndexLessThanZero
        {
            get
            {
                return ResourceManager.GetString("RecordIndexLessThanZero", resourceCulture);
            }
        }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        internal static ResourceManager ResourceManager
        {
            get
            {
                if (object.ReferenceEquals(resourceMan, null))
                {
                    ResourceManager manager = new ResourceManager("Resources.ExceptionMessage", typeof(ExceptionMessage).Assembly);
                    resourceMan = manager;
                }
                return resourceMan;
            }
        }
    }

}