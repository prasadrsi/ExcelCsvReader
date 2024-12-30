using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Aspen.Web.FileProcessor.FileParserEngine
{
    
        public enum MissingFieldAction
        {
            ParseError,
            ReplaceByEmpty,
            ReplaceByNull
        }
        public enum ParseErrorAction
        {
            RaiseEvent,
            AdvanceToNextLine,
            ThrowException
        }

        [Flags]
        public enum ValueTrimmingOptions
        {
            None,
            UnquotedOnly,
            QuotedOnly,
            All
        }
    }
