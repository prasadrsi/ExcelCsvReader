using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections;
using System.Data;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Globalization;
using System.Resources;
using System.Data.Common;
using Aspen.Web.FileProcessor.FileParserEngine;
using Aspen.Web.FileProcessor.FileParserEngine.Resources;
using System.Xml;

namespace Aspen.Web.FileProcessor.FileParserEngine
{
    

    public class CsvReader : FileProvider, IDataReader, IDataRecord, IEnumerable<string[]>, IEnumerable, IDisposable
    {
        // Fields
        private char[] _buffer;
        private int _bufferLength;
        private int _bufferSize;
        private char _comment;
        private long _currentRecordIndex;
        private long _totalRecords;
        private bool _endLineReading;
        private ParseErrorAction _defaultParseErrorAction;
        private char _delimiter;
        private bool _eof;
        private bool _eol;
        private char _escape;
        private int _fieldCount;
        private static readonly StringComparer _fieldHeaderComparer = StringComparer.CurrentCultureIgnoreCase;
        private Dictionary<string, int> _fieldHeaderIndexes;
        private string[] _fieldHeaders;
        private string[] _fields;
        private bool _firstRecordInCache;
        private bool _hasHeaders;
        private bool _initialized;
        private bool _isDisposed;
        private readonly object _lock;
        private MissingFieldAction _missingFieldAction;
        private bool _missingFieldFlag;
        private int _nextFieldIndex;
        private int _nextFieldStart;
        private bool _parseErrorFlag;
        private char _quote;
        private TextReader _reader;
        private bool _skipEmptyLines;
        private bool _supportsMultiline;
        private ValueTrimmingOptions _trimmingOptions;
        public const int DefaultBufferSize = 0x1000;

        public const char DefaultComment = '#';
        public const char DefaultDelimiter = ',';
        public const char DefaultEscape = '"';
        public const char DefaultQuote = '"';


        // Methods
        public CsvReader(TextReader reader, bool hasHeaders)
            : this(reader, hasHeaders, ',', '"', '"', '#', ValueTrimmingOptions.UnquotedOnly, 0x1000)
        {
        }

        public CsvReader(TextReader reader, bool hasHeaders, char delimiter)
            : this(reader, hasHeaders, delimiter, '"', '"', '#', ValueTrimmingOptions.UnquotedOnly, 0x1000)
        {
        }

        public CsvReader(TextReader reader, bool hasHeaders, int bufferSize)
            : this(reader, hasHeaders, ',', '"', '"', '#', ValueTrimmingOptions.UnquotedOnly, bufferSize)
        {
        }

        public CsvReader(TextReader reader, bool hasHeaders, char delimiter, int bufferSize)
            : this(reader, hasHeaders, delimiter, '"', '"', '#', ValueTrimmingOptions.UnquotedOnly, bufferSize)
        {
        }

        public CsvReader(TextReader reader, bool hasHeaders, char delimiter, char quote, char escape, char comment, ValueTrimmingOptions trimmingOptions)
            : this(reader, hasHeaders, delimiter, quote, escape, comment, trimmingOptions, 0x1000)
        {
        }

        public CsvReader(TextReader reader, bool hasHeaders, char delimiter, char quote, char escape, char comment, ValueTrimmingOptions trimmingOptions, int bufferSize)
        {
            this._lock = new object();
            if (reader == null)
            {
                throw new ArgumentNullException("reader");
            }
            if (bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException("bufferSize", bufferSize, ExceptionMessage.BufferSizeTooSmall);
            }
            this._bufferSize = bufferSize;
            if (reader is StreamReader)
            {
                Stream baseStream = ((StreamReader)reader).BaseStream;
                if (baseStream.CanSeek && (baseStream.Length > 0L))
                {
                    this._bufferSize = (int)Math.Min((long)bufferSize, baseStream.Length);
                }
            }
            this._reader = reader;
            this._delimiter = delimiter;
            this._quote = quote;
            this._escape = escape;
            this._comment = comment;
            this._hasHeaders = hasHeaders;
            this._trimmingOptions = trimmingOptions;
            this._supportsMultiline = true;
            this._skipEmptyLines = true;
            this.DefaultHeaderName = "Column";
            this._currentRecordIndex = -1L;
            this._defaultParseErrorAction = ParseErrorAction.RaiseEvent;
            getRecordCount();
        }

        protected void CheckDisposed()
        {
            if (this._isDisposed)
            {
                throw new ObjectDisposedException(base.GetType().FullName);
            }
        }

        public void CopyCurrentRecordTo(string[] array)
        {
            this.CopyCurrentRecordTo(array, 0);
        }

        public void CopyCurrentRecordTo(string[] array, int index)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }
            if ((index < 0) || (index >= array.Length))
            {
                throw new ArgumentOutOfRangeException("index", index, string.Empty);
            }
            if ((this._currentRecordIndex < 0L) || !this._initialized)
            {
                throw new InvalidOperationException(ExceptionMessage.NoCurrentRecord);
            }
            if ((array.Length - index) < this._fieldCount)
            {
                throw new ArgumentException(ExceptionMessage.NotEnoughSpaceInArray, "array");
            }
            for (int i = 0; i < this._fieldCount; i++)
            {
                if (this._parseErrorFlag)
                {
                    array[index + i] = null;
                }
                else
                {
                    array[index + i] = this[i];
                }
            }
        }

        private long CopyFieldToArray(int field, long fieldOffset, Array destinationArray, int destinationOffset, int length)
        {
            this.EnsureInitialize();
            if ((field < 0) || (field >= this._fieldCount))
            {
                throw new ArgumentOutOfRangeException("field", field, string.Format(CultureInfo.InvariantCulture, ExceptionMessage.FieldIndexOutOfRange, new object[] { field }));
            }
            if ((fieldOffset < 0L) || (fieldOffset >= 0x7fffffffL))
            {
                throw new ArgumentOutOfRangeException("fieldOffset");
            }
            if (length == 0)
            {
                return 0L;
            }
            string str = this[field];
            if (str == null)
            {
                str = string.Empty;
            }
            if (destinationArray.GetType() == typeof(char[]))
            {
                Array.Copy(str.ToCharArray((int)fieldOffset, length), 0, destinationArray, destinationOffset, length);
            }
            else
            {
                char[] chArray = str.ToCharArray((int)fieldOffset, length);
                byte[] sourceArray = new byte[chArray.Length];
                for (int i = 0; i < chArray.Length; i++)
                {
                    sourceArray[i] = Convert.ToByte(chArray[i]);
                }
                Array.Copy(sourceArray, 0, destinationArray, destinationOffset, length);
            }
            return (long)length;
        }

        public void Dispose()
        {
            if (!this._isDisposed)
            {
                this.Dispose(true);
                GC.SuppressFinalize(this);
            }
        }


        private void DoSkipEmptyAndCommentedLines(ref int pos)
        {
            while (pos < this._bufferLength)
            {
                if (this._buffer[pos] == this._comment)
                {
                    pos++;
                    this.SkipToNextLine(ref pos);
                }
                else if (!this._skipEmptyLines || !this.ParseNewLine(ref pos))
                {
                    break;
                }
            }
        }

        private void EnsureInitialize()
        {
            if (!this._initialized)
            {
                this.ReadNextRecord(true, false);
            }
        }

        ~CsvReader()
        {
            this.Dispose(false);
        }

        public string GetCurrentRawData()
        {
            if ((this._buffer != null) && (this._bufferLength > 0))
            {
                return new string(this._buffer, 0, this._bufferLength);
            }
            return string.Empty;
        }

        public RecordEnumerator GetEnumerator()
        {
            return new RecordEnumerator(this);
        }

        public override string[] GetFieldHeaders()
        {
            this.EnsureInitialize();
            string[] strArray = new string[this._fieldHeaders.Length - 1];
            for (int i = 0; i < strArray.Length; i++)
            {
                strArray[i] = this._fieldHeaders[i];
            }
            return strArray;
        }

        public int GetFieldIndex(string header)
        {
            int num;
            this.EnsureInitialize();
            if ((this._fieldHeaderIndexes != null) && this._fieldHeaderIndexes.TryGetValue(header, out num))
            {
                return num;
            }
            return -1;
        }

        private void getRecordCount()
        {
            _totalRecords = 0;
            this.EnsureInitialize();


            while (ReadNextRecord())
            {
                _totalRecords++;
            }
            if (_totalRecords > 0)
            {
                _initialized = false;


                _firstRecordInCache = false;
                _currentRecordIndex = 0;
                _nextFieldIndex = 0;
                _eof = false;
                _eol = false;
                ((System.IO.StreamReader)((this)._reader)).BaseStream.Seek(0, 0);
            }
        }

        private string HandleMissingField(string value, int fieldIndex, ref int currentPosition)
        {
            if ((fieldIndex < 0) || (fieldIndex >= this._fieldCount))
            {
                throw new ArgumentOutOfRangeException("fieldIndex", fieldIndex, string.Format(CultureInfo.InvariantCulture, ExceptionMessage.FieldIndexOutOfRange, new object[] { fieldIndex }));
            }
            this._missingFieldFlag = true;
            for (int i = fieldIndex + 1; i < this._fieldCount; i++)
            {
                this._fields[i] = null;
            }
            if (value != null)
            {
                return value;
            }
            switch (this._missingFieldAction)
            {
                case MissingFieldAction.ParseError:
                    this.HandleParseError(new MissingFieldException(this.GetCurrentRawData(), currentPosition, Math.Max(0L, this._currentRecordIndex), fieldIndex), ref currentPosition);
                    return value;

                case MissingFieldAction.ReplaceByEmpty:
                    return string.Empty;

                case MissingFieldAction.ReplaceByNull:
                    return null;
            }
            throw new NotSupportedException(string.Format(CultureInfo.InvariantCulture, ExceptionMessage.MissingFieldActionNotSupported, new object[] { this._missingFieldAction }));
        }

        private void HandleParseError(MalformedFileException error, ref int pos)
        {
            if (error == null)
            {
                throw new ArgumentNullException("error");
            }
            this._parseErrorFlag = true;
            switch (this._defaultParseErrorAction)
            {
                case ParseErrorAction.RaiseEvent:
                    {
                        ParseErrorEventArgs e = new ParseErrorEventArgs(error, ParseErrorAction.ThrowException);
                        this.OnParseError(e);
                        switch (e.Action)
                        {
                            case ParseErrorAction.RaiseEvent:
                                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, ExceptionMessage.ParseErrorActionInvalidInsideParseErrorEvent, new object[] { e.Action }), e.Error);

                            case ParseErrorAction.AdvanceToNextLine:
                                if (this._missingFieldFlag || (pos < 0))
                                {
                                    break;
                                }
                                this.SkipToNextLine(ref pos);
                                return;

                            case ParseErrorAction.ThrowException:
                                throw e.Error;
                        }
                        throw new NotSupportedException(string.Format(CultureInfo.InvariantCulture, ExceptionMessage.ParseErrorActionNotSupported, new object[] { e.Action }), e.Error);
                    }
                case ParseErrorAction.AdvanceToNextLine:
                    if (this._missingFieldFlag || (pos < 0))
                    {
                        break;
                    }
                    this.SkipToNextLine(ref pos);
                    return;

                case ParseErrorAction.ThrowException:
                    throw error;

                default:
                    throw new NotSupportedException(string.Format(CultureInfo.InvariantCulture, ExceptionMessage.ParseErrorActionNotSupported, new object[] { this._defaultParseErrorAction }), error);
            }
        }

        private bool IsNewLine(int pos)
        {
            char ch = this._buffer[pos];
            return ((ch == '\n') || ((ch == '\r') && (this._delimiter != '\r')));
        }

        private bool IsWhiteSpace(char c)
        {
            if (c == this._delimiter)
            {
                return false;
            }
            if (c > '\x00ff')
            {
                return (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.SpaceSeparator);
            }
            if (c != ' ')
            {
                return (c == '\t');
            }
            return true;
        }

        public virtual bool MoveTo(long record)
        {
            if (record < this._currentRecordIndex)
            {
                return false;
            }
            for (long i = record - this._currentRecordIndex; i > 0L; i -= 1L)
            {
                if (!this.ReadNextRecord())
                {
                    return false;
                }
            }
            return true;
        }

        private bool ParseNewLine(ref int pos)
        {
            if (pos == this._bufferLength)
            {
                pos = 0;
                if (!this.ReadBuffer())
                {
                    return false;
                }
            }
            char ch = this._buffer[pos];
            if ((ch == '\r') && (this._delimiter != '\r'))
            {
                pos++;
                if (pos < this._bufferLength)
                {
                    if (this._buffer[pos] == '\n')
                    {
                        pos++;
                    }
                }
                else if (this.ReadBuffer())
                {
                    if (this._buffer[0] == '\n')
                    {
                        pos = 1;
                    }
                    else
                    {
                        pos = 0;
                    }
                }
                if (pos >= this._bufferLength)
                {
                    this.ReadBuffer();
                    pos = 0;
                }
                return true;
            }
            if (ch != '\n')
            {
                return false;
            }
            pos++;
            if (pos >= this._bufferLength)
            {
                this.ReadBuffer();
                pos = 0;
            }
            return true;
        }

        private bool ReadBuffer()
        {
            if (!this._eof)
            {
                this.CheckDisposed();

                this._bufferLength = this._reader.Read(this._buffer, 0, this._bufferSize);
                if (this._bufferLength > 0)
                {
                    return true;
                }
                this._eof = true;
                this._buffer = null;
            }
            return false;
        }

        private string ReadField(int field, bool initializing, bool discardValue)
        {
            if (!initializing)
            {
                if ((field < 0) || (field >= this._fieldCount))
                {
                    throw new ArgumentOutOfRangeException("field", field, string.Format(CultureInfo.InvariantCulture, ExceptionMessage.FieldIndexOutOfRange, new object[] { field }));
                }
                if (this._currentRecordIndex < 0L)
                {
                    throw new InvalidOperationException(ExceptionMessage.NoCurrentRecord);
                }
                if (this._fields[field] != null)
                {
                    return this._fields[field];
                }
                if (this._missingFieldFlag)
                {
                    return this.HandleMissingField(null, field, ref this._nextFieldStart);
                }
            }
            this.CheckDisposed();
            int fieldIndex = this._nextFieldIndex;
            while (fieldIndex < (field + 1))
            {
                int num4;
                if (this._nextFieldStart == this._bufferLength)
                {
                    this._nextFieldStart = 0;
                    this.ReadBuffer();
                }
                string str = null;
                if (this._missingFieldFlag)
                {
                    str = this.HandleMissingField(str, fieldIndex, ref this._nextFieldStart);
                    goto Label_05C9;
                }
                if (this._nextFieldStart == this._bufferLength)
                {
                    if (fieldIndex == field)
                    {
                        if (!discardValue)
                        {
                            str = string.Empty;
                            this._fields[fieldIndex] = str;
                        }
                        this._missingFieldFlag = true;
                    }
                    else
                    {
                        str = this.HandleMissingField(str, fieldIndex, ref this._nextFieldStart);
                    }
                    goto Label_05C9;
                }
                if ((this._trimmingOptions & ValueTrimmingOptions.UnquotedOnly) != ValueTrimmingOptions.None)
                {
                    this.SkipWhiteSpaces(ref this._nextFieldStart);
                }
                if (this._eof)
                {
                    str = string.Empty;
                    this._fields[field] = str;
                    goto Label_05C9;
                }
                if (this._buffer[this._nextFieldStart] == this._quote)
                {
                    goto Label_0326;
                }
                int startIndex = this._nextFieldStart;
                int index = this._nextFieldStart;
            Label_01B0:
                while (index < this._bufferLength)
                {
                    char ch = this._buffer[index];
                    if (ch == this._delimiter)
                    {
                        this._nextFieldStart = index + 1;
                        break;
                    }
                    if ((ch == '\r') || (ch == '\n'))
                    {
                        this._nextFieldStart = index;
                        this._eol = true;
                        break;
                    }
                    index++;
                }
                if (index >= this._bufferLength)
                {
                    if (!discardValue)
                    {
                        str = str + new string(this._buffer, startIndex, index - startIndex);
                    }
                    startIndex = 0;
                    index = 0;
                    this._nextFieldStart = 0;
                    if (this.ReadBuffer())
                    {
                        goto Label_01B0;
                    }
                }
                if (!discardValue)
                {
                    if ((this._trimmingOptions & ValueTrimmingOptions.UnquotedOnly) == ValueTrimmingOptions.None)
                    {
                        if (!this._eof && (index > startIndex))
                        {
                            str = str + new string(this._buffer, startIndex, index - startIndex);
                        }
                    }
                    else
                    {
                        if (!this._eof && (index > startIndex))
                        {
                            index--;
                            while ((index > -1) && this.IsWhiteSpace(this._buffer[index]))
                            {
                                index--;
                            }
                            index++;
                            if (index > 0)
                            {
                                str = str + new string(this._buffer, startIndex, index - startIndex);
                            }
                        }
                        else
                        {
                            index = -1;
                        }
                        if (index <= 0)
                        {
                            index = (str == null) ? -1 : (str.Length - 1);
                            while ((index > -1) && this.IsWhiteSpace(str[index]))
                            {
                                index--;
                            }
                            index++;
                            if ((index > 0) && (index != str.Length))
                            {
                                str = str.Substring(0, index);
                            }
                        }
                    }
                    if (str == null)
                    {
                        str = string.Empty;
                    }
                }
                if (this._eol || this._eof)
                {
                    this._eol = this.ParseNewLine(ref this._nextFieldStart);
                    if (!initializing && (fieldIndex != (this._fieldCount - 1)))
                    {
                        if ((str != null) && (str.Length == 0))
                        {
                            str = null;
                        }
                        str = this.HandleMissingField(str, fieldIndex, ref this._nextFieldStart);
                    }
                }
                if (!discardValue)
                {
                    this._fields[fieldIndex] = str;
                }
                goto Label_05C9;
            Label_0326:
                num4 = this._nextFieldStart + 1;
                int num5 = num4;
                bool flag = true;
                bool flag2 = false;
                if ((this._trimmingOptions & ValueTrimmingOptions.QuotedOnly) != ValueTrimmingOptions.None)
                {
                    this.SkipWhiteSpaces(ref num4);
                    num5 = num4;
                }
            Label_0400:
                while (num5 < this._bufferLength)
                {
                    char ch2 = this._buffer[num5];
                    if (flag2)
                    {
                        flag2 = false;
                        num4 = num5;
                    }
                    else if ((ch2 == this._escape) && (((this._escape != this._quote) || (((num5 + 1) < this._bufferLength) && (this._buffer[num5 + 1] == this._quote))) || (((num5 + 1) == this._bufferLength) && (this._reader.Peek() == this._quote))))
                    {
                        if (!discardValue)
                        {
                            str = str + new string(this._buffer, num4, num5 - num4);
                        }
                        flag2 = true;
                    }
                    else if (ch2 == this._quote)
                    {
                        flag = false;
                        break;
                    }
                    num5++;
                }
                if (flag)
                {
                    if (!discardValue && !flag2)
                    {
                        str = str + new string(this._buffer, num4, num5 - num4);
                    }
                    num4 = 0;
                    num5 = 0;
                    this._nextFieldStart = 0;
                    if (!this.ReadBuffer())
                    {
                        this.HandleParseError(new MalformedFileException(this.GetCurrentRawData(), this._nextFieldStart, Math.Max(0L, this._currentRecordIndex), fieldIndex), ref this._nextFieldStart);
                        return null;
                    }
                    goto Label_0400;
                }
                if (!this._eof)
                {
                    bool flag3;
                    if (!discardValue && (num5 > num4))
                    {
                        str = str + new string(this._buffer, num4, num5 - num4);
                    }
                    if ((!discardValue && (str != null)) && ((this._trimmingOptions & ValueTrimmingOptions.QuotedOnly) != ValueTrimmingOptions.None))
                    {
                        int length = str.Length;
                        while ((length > 0) && this.IsWhiteSpace(str[length - 1]))
                        {
                            length--;
                        }
                        if (length < str.Length)
                        {
                            str = str.Substring(0, length);
                        }
                    }
                    this._nextFieldStart = num5 + 1;
                    this.SkipWhiteSpaces(ref this._nextFieldStart);
                    if ((this._nextFieldStart < this._bufferLength) && (this._buffer[this._nextFieldStart] == this._delimiter))
                    {
                        this._nextFieldStart++;
                        flag3 = true;
                    }
                    else
                    {
                        flag3 = false;
                    }
                    if ((!this._eof && !flag3) && (initializing || (fieldIndex == (this._fieldCount - 1))))
                    {
                        this._eol = this.ParseNewLine(ref this._nextFieldStart);
                    }
                    if ((!flag3 && !this._eof) && (!this._eol && !this.IsNewLine(this._nextFieldStart)))
                    {
                        this.HandleParseError(new MalformedFileException(this.GetCurrentRawData(), this._nextFieldStart, Math.Max(0L, this._currentRecordIndex), fieldIndex), ref this._nextFieldStart);
                    }
                }
                if (!discardValue)
                {
                    if (str == null)
                    {
                        str = string.Empty;
                    }
                    this._fields[fieldIndex] = str;
                }
            Label_05C9:
                this._nextFieldIndex = Math.Max(fieldIndex + 1, this._nextFieldIndex);
                if (fieldIndex == field)
                {
                    if (!initializing)
                    {
                        return str;
                    }
                    if (this._eol || this._eof)
                    {
                        _endLineReading = true;
                        _nextFieldIndex = 0;
                        return str;
                    }
                    if (!string.IsNullOrEmpty(str))
                    {
                        return str;
                    }


                    return string.Empty;
                }
                fieldIndex++;
            }
            this.HandleParseError(new MalformedFileException(this.GetCurrentRawData(), this._nextFieldStart, Math.Max(0L, this._currentRecordIndex), fieldIndex), ref this._nextFieldStart);
            return null;
        }

        public override string ReadField(int field)
        {
            return this.ReadField(field, true, false);
        }

        public bool ReadNextRecord()
        {
            return this.ReadNextRecord(false, false);
        }

        protected override bool ReadNextRecord(bool onlyReadHeaders, bool skipToNextLine)
        {
            if (this._eof)
            {
                if (this._firstRecordInCache)
                {
                    this._firstRecordInCache = false;
                    this._currentRecordIndex += 1L;
                    return true;
                }
                return false;
            }
            this.CheckDisposed();
            if (!this._initialized)
            {
                this._buffer = new char[this._bufferSize];
                this._fieldHeaders = new string[0];
                if (!this.ReadBuffer())
                {
                    return false;
                }
                if (!this.SkipEmptyAndCommentedLines(ref this._nextFieldStart))
                {
                    return false;
                }
                this._fieldCount = 0;
                this._fields = new string[0x10];
                while ( !_endLineReading)
                {
                    this.ReadField(this._fieldCount, true, false);
                    if (this._parseErrorFlag)
                    {
                        this._fieldCount = 0;
                        Array.Clear(this._fields, 0, this._fields.Length);
                        this._parseErrorFlag = false;
                        this._nextFieldIndex = 0;
                    }
                    else
                    {
                        this._fieldCount++;
                        if (this._fieldCount == this._fields.Length)
                        {
                            Array.Resize<string>(ref this._fields, (this._fieldCount + 1) * 2);
                        }
                    }
                }
                _endLineReading = false;
                this._fieldCount++;
                if (this._fields.Length != this._fieldCount)
                {
                    Array.Resize<string>(ref this._fields, this._fieldCount-1);
                }
                this._initialized = true;
                if (this._hasHeaders)
                {
                    this._currentRecordIndex = -1L;
                    this._firstRecordInCache = false;
                    this._fieldHeaders = new string[this._fieldCount];
                    this._fieldHeaderIndexes = new Dictionary<string, int>(this._fieldCount, _fieldHeaderComparer);
                    for (int i = 0; i < this._fields.Length; i++)
                    {
                        string str = this._fields[i];
                        if (string.IsNullOrEmpty(str) || (str.Trim().Length == 0))
                        {
                            str = this.DefaultHeaderName + i.ToString();
                        }
                        this._fieldHeaders[i] = str;
                        this._fieldHeaderIndexes.Add(str, i);
                    }
                    if (!onlyReadHeaders)
                    {
                        if (!this.SkipEmptyAndCommentedLines(ref this._nextFieldStart))
                        {
                            return false;
                        }
                        Array.Clear(this._fields, 0, this._fields.Length);
                        this._nextFieldIndex = 0;
                        this._eol = false;
                        this._currentRecordIndex += 1L;
                        return true;
                    }
                }
                else if (onlyReadHeaders)
                {
                    this._firstRecordInCache = true;
                    this._currentRecordIndex = -1L;
                }
                else
                {
                    this._firstRecordInCache = false;
                    this._currentRecordIndex = 0L;
                }
            }
            else
            {
                if (skipToNextLine)
                {
                    this.SkipToNextLine(ref this._nextFieldStart);
                }
                else if (((this._currentRecordIndex > -1L) && !this._missingFieldFlag) && (!this._eol && !this._eof))
                {
                    if (!this._supportsMultiline)
                    {
                        this.SkipToNextLine(ref this._nextFieldStart);
                    }
                    else
                    {
                        while (!_endLineReading)
                        {
                            this.ReadField(this._nextFieldIndex, true, true);
                        
                        }
                        _endLineReading = false;
                        
                    }
                }
                if (!this._firstRecordInCache && !this.SkipEmptyAndCommentedLines(ref this._nextFieldStart))
                {
                    return false;
                }
                if (this._hasHeaders || !this._firstRecordInCache)
                {
                    this._eol = false;
                }
                if (this._firstRecordInCache)
                {
                    this._firstRecordInCache = false;
                }
                else
                {
                    Array.Clear(this._fields, 0, this._fields.Length);
                    this._nextFieldIndex = 0;
                    _endLineReading = false;
                }
                this._missingFieldFlag = false;
                this._parseErrorFlag = false;
                this._currentRecordIndex += 1L;
            }
            return true;
        }

        private bool SkipEmptyAndCommentedLines(ref int pos)
        {
            if (pos < this._bufferLength)
            {
                this.DoSkipEmptyAndCommentedLines(ref pos);
            }
            while ((pos >= this._bufferLength) && !this._eof)
            {
                if (this.ReadBuffer())
                {
                    pos = 0;
                    this.DoSkipEmptyAndCommentedLines(ref pos);
                }
                else
                {
                    return false;
                }
            }
            return !this._eof;
        }

        private bool SkipToNextLine(ref int pos)
        {
            while (((pos < this._bufferLength) || (this.ReadBuffer() && ((pos = 0) == 0))) && !this.ParseNewLine(ref pos))
            {
                pos++;
            }
            return !this._eof;
        }

        private bool SkipWhiteSpaces(ref int pos)
        {
        Label_0008:
            while ((pos < this._bufferLength) && this.IsWhiteSpace(this._buffer[pos]))
            {
                pos++;
            }
            if (pos >= this._bufferLength)
            {
                pos = 0;
                if (!this.ReadBuffer())
                {
                    return false;
                }
                goto Label_0008;
            }
            return true;
        }

        IEnumerator<string[]> IEnumerable<string[]>.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        void IDataReader.Close()
        {
            this.Dispose();
        }

        DataTable IDataReader.GetSchemaTable()
        {
            string[] strArray;
            this.EnsureInitialize();
            this.ValidateDataReader(DataReaderValidations.IsNotClosed);
            DataTable table = new DataTable("SchemaTable");
            table.Locale = CultureInfo.InvariantCulture;
            table.MinimumCapacity = this._fieldCount;
            table.Columns.Add(SchemaTableColumn.AllowDBNull, typeof(bool)).ReadOnly = true;
            table.Columns.Add(SchemaTableColumn.BaseColumnName, typeof(string)).ReadOnly = true;
            table.Columns.Add(SchemaTableColumn.BaseSchemaName, typeof(string)).ReadOnly = true;
            table.Columns.Add(SchemaTableColumn.BaseTableName, typeof(string)).ReadOnly = true;
            table.Columns.Add(SchemaTableColumn.ColumnName, typeof(string)).ReadOnly = true;
            table.Columns.Add(SchemaTableColumn.ColumnOrdinal, typeof(int)).ReadOnly = true;
            table.Columns.Add(SchemaTableColumn.ColumnSize, typeof(int)).ReadOnly = true;
            table.Columns.Add(SchemaTableColumn.DataType, typeof(object)).ReadOnly = true;
            table.Columns.Add(SchemaTableColumn.IsAliased, typeof(bool)).ReadOnly = true;
            table.Columns.Add(SchemaTableColumn.IsExpression, typeof(bool)).ReadOnly = true;
            table.Columns.Add(SchemaTableColumn.IsKey, typeof(bool)).ReadOnly = true;
            table.Columns.Add(SchemaTableColumn.IsLong, typeof(bool)).ReadOnly = true;
            table.Columns.Add(SchemaTableColumn.IsUnique, typeof(bool)).ReadOnly = true;
            table.Columns.Add(SchemaTableColumn.NumericPrecision, typeof(short)).ReadOnly = true;
            table.Columns.Add(SchemaTableColumn.NumericScale, typeof(short)).ReadOnly = true;
            table.Columns.Add(SchemaTableColumn.ProviderType, typeof(int)).ReadOnly = true;
            table.Columns.Add(SchemaTableOptionalColumn.BaseCatalogName, typeof(string)).ReadOnly = true;
            table.Columns.Add(SchemaTableOptionalColumn.BaseServerName, typeof(string)).ReadOnly = true;
            table.Columns.Add(SchemaTableOptionalColumn.IsAutoIncrement, typeof(bool)).ReadOnly = true;
            table.Columns.Add(SchemaTableOptionalColumn.IsHidden, typeof(bool)).ReadOnly = true;
            table.Columns.Add(SchemaTableOptionalColumn.IsReadOnly, typeof(bool)).ReadOnly = true;
            table.Columns.Add(SchemaTableOptionalColumn.IsRowVersion, typeof(bool)).ReadOnly = true;
            if (this._hasHeaders)
            {
                strArray = this._fieldHeaders;
            }
            else
            {
                strArray = new string[this._fieldCount];
                for (int j = 0; j < this._fieldCount; j++)
                {
                    strArray[j] = "Column" + j.ToString(CultureInfo.InvariantCulture);
                }
            }
            object[] objArray2 = new object[0x16];
            objArray2[0] = true;
            objArray2[2] = string.Empty;
            objArray2[3] = string.Empty;
            objArray2[6] = 0x7fffffff;
            objArray2[7] = typeof(string);
            objArray2[8] = false;
            objArray2[9] = false;
            objArray2[10] = false;
            objArray2[11] = false;
            objArray2[12] = false;
            objArray2[13] = DBNull.Value;
            objArray2[14] = DBNull.Value;
            objArray2[15] = 0x10;
            objArray2[0x10] = string.Empty;
            objArray2[0x11] = string.Empty;
            objArray2[0x12] = false;
            objArray2[0x13] = false;
            objArray2[20] = true;
            objArray2[0x15] = false;
            object[] values = objArray2;
            for (int i = 0; i < strArray.Length; i++)
            {
                values[1] = strArray[i];
                values[4] = strArray[i];
                values[5] = i;
                table.Rows.Add(values);
            }
            return table;
        }

        bool IDataReader.NextResult()
        {
            this.ValidateDataReader(DataReaderValidations.IsNotClosed);
            return false;
        }

        bool IDataReader.Read()
        {
            this.ValidateDataReader(DataReaderValidations.IsNotClosed);
            return this.ReadNextRecord();
        }

        bool IDataRecord.GetBoolean(int i)
        {
            int num;
            this.ValidateDataReader(DataReaderValidations.IsNotClosed | DataReaderValidations.IsInitialized);
            string s = this[i];
            if (int.TryParse(s, out num))
            {
                return (num != 0);
            }
            return bool.Parse(s);
        }

        byte IDataRecord.GetByte(int i)
        {
            this.ValidateDataReader(DataReaderValidations.IsNotClosed | DataReaderValidations.IsInitialized);
            return byte.Parse(this[i], CultureInfo.CurrentCulture);
        }

        long IDataRecord.GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length)
        {
            this.ValidateDataReader(DataReaderValidations.IsNotClosed | DataReaderValidations.IsInitialized);
            return this.CopyFieldToArray(i, fieldOffset, buffer, bufferoffset, length);
        }

        char IDataRecord.GetChar(int i)
        {
            this.ValidateDataReader(DataReaderValidations.IsNotClosed | DataReaderValidations.IsInitialized);
            return char.Parse(this[i]);
        }

        long IDataRecord.GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length)
        {
            this.ValidateDataReader(DataReaderValidations.IsNotClosed | DataReaderValidations.IsInitialized);
            return this.CopyFieldToArray(i, fieldoffset, buffer, bufferoffset, length);
        }

        IDataReader IDataRecord.GetData(int i)
        {
            this.ValidateDataReader(DataReaderValidations.IsNotClosed | DataReaderValidations.IsInitialized);
            if (i == 0)
            {
                return this;
            }
            return null;
        }

        string IDataRecord.GetDataTypeName(int i)
        {
            this.ValidateDataReader(DataReaderValidations.IsNotClosed | DataReaderValidations.IsInitialized);
            return typeof(string).FullName;
        }

        DateTime IDataRecord.GetDateTime(int i)
        {
            this.ValidateDataReader(DataReaderValidations.IsNotClosed | DataReaderValidations.IsInitialized);
            return DateTime.Parse(this[i], CultureInfo.CurrentCulture);
        }

        decimal IDataRecord.GetDecimal(int i)
        {
            this.ValidateDataReader(DataReaderValidations.IsNotClosed | DataReaderValidations.IsInitialized);
            return decimal.Parse(this[i], CultureInfo.CurrentCulture);
        }

        double IDataRecord.GetDouble(int i)
        {
            this.ValidateDataReader(DataReaderValidations.IsNotClosed | DataReaderValidations.IsInitialized);
            return double.Parse(this[i], CultureInfo.CurrentCulture);
        }

        Type IDataRecord.GetFieldType(int i)
        {
            this.EnsureInitialize();
            this.ValidateDataReader(DataReaderValidations.IsNotClosed | DataReaderValidations.IsInitialized);
            if ((i < 0) || (i >= this._fieldCount))
            {
                throw new ArgumentOutOfRangeException("i", i, string.Format(CultureInfo.InvariantCulture, ExceptionMessage.FieldIndexOutOfRange, new object[] { i }));
            }
            return typeof(string);
        }

        float IDataRecord.GetFloat(int i)
        {
            this.ValidateDataReader(DataReaderValidations.IsNotClosed | DataReaderValidations.IsInitialized);
            return float.Parse(this[i], CultureInfo.CurrentCulture);
        }

        Guid IDataRecord.GetGuid(int i)
        {
            this.ValidateDataReader(DataReaderValidations.IsNotClosed | DataReaderValidations.IsInitialized);
            return new Guid(this[i]);
        }

        short IDataRecord.GetInt16(int i)
        {
            this.ValidateDataReader(DataReaderValidations.IsNotClosed | DataReaderValidations.IsInitialized);
            return short.Parse(this[i], CultureInfo.CurrentCulture);
        }

        int IDataRecord.GetInt32(int i)
        {
            this.ValidateDataReader(DataReaderValidations.IsNotClosed | DataReaderValidations.IsInitialized);
            string str = this[i];
            return int.Parse((str == null) ? string.Empty : str, CultureInfo.CurrentCulture);
        }

        long IDataRecord.GetInt64(int i)
        {
            this.ValidateDataReader(DataReaderValidations.IsNotClosed | DataReaderValidations.IsInitialized);
            return long.Parse(this[i], CultureInfo.CurrentCulture);
        }

        string IDataRecord.GetName(int i)
        {
            this.EnsureInitialize();
            this.ValidateDataReader(DataReaderValidations.IsNotClosed);
            if ((i < 0) || (i >= this._fieldCount))
            {
                throw new ArgumentOutOfRangeException("i", i, string.Format(CultureInfo.InvariantCulture, ExceptionMessage.FieldIndexOutOfRange, new object[] { i }));
            }
            if (this._hasHeaders)
            {
                return this._fieldHeaders[i];
            }
            return ("Column" + i.ToString(CultureInfo.InvariantCulture));
        }

        int IDataRecord.GetOrdinal(string name)
        {
            int num;
            this.EnsureInitialize();
            this.ValidateDataReader(DataReaderValidations.IsNotClosed);
            if (!this._fieldHeaderIndexes.TryGetValue(name, out num))
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, ExceptionMessage.FieldHeaderNotFound, new object[] { name }), "name");
            }
            return num;
        }

        string IDataRecord.GetString(int i)
        {
            this.ValidateDataReader(DataReaderValidations.IsNotClosed | DataReaderValidations.IsInitialized);
            return this[i];
        }

        object IDataRecord.GetValue(int i)
        {
            this.ValidateDataReader(DataReaderValidations.IsNotClosed | DataReaderValidations.IsInitialized);
            if (((IDataRecord)this).IsDBNull(i))
            {
                return DBNull.Value;
            }
            return this[i];
        }

        int IDataRecord.GetValues(object[] values)
        {
            this.ValidateDataReader(DataReaderValidations.IsNotClosed | DataReaderValidations.IsInitialized);
            IDataRecord record = this;
            for (int i = 0; i < this._fieldCount; i++)
            {
                values[i] = record.GetValue(i);
            }
            return this._fieldCount;
        }

        bool IDataRecord.IsDBNull(int i)
        {
            this.ValidateDataReader(DataReaderValidations.IsNotClosed | DataReaderValidations.IsInitialized);
            return string.IsNullOrEmpty(this[i]);
        }

        private void ValidateDataReader(DataReaderValidations validations)
        {
            if (((validations & DataReaderValidations.IsInitialized) != DataReaderValidations.None) && !this._initialized)
            {
                throw new InvalidOperationException(ExceptionMessage.NoCurrentRecord);
            }
            if (((validations & DataReaderValidations.IsNotClosed) != DataReaderValidations.None) && this._isDisposed)
            {
                throw new InvalidOperationException(ExceptionMessage.ReaderClosed);
            }
        }

        // Properties
        public int BufferSize
        {
            get
            {
                return this._bufferSize;
            }
        }

        public char Comment
        {
            get
            {
                return this._comment;
            }
        }

        public override long CurrentRecordIndex
        {
            get
            {
                return this._currentRecordIndex;
            }
        }

        public string DefaultHeaderName { get; set; }

        public ParseErrorAction DefaultParseErrorAction
        {
            get
            {
                return this._defaultParseErrorAction;
            }
            set
            {
                this._defaultParseErrorAction = value;
            }
        }

        public char Delimiter
        {
            get
            {
                return this._delimiter;
            }
        }

        public virtual bool EndOfStream
        {
            get
            {
                return this._eof;
            }
        }

        public char Escape
        {
            get
            {
                return this._escape;
            }
        }

        public override int FieldCount
        {
            get
            {
                this.EnsureInitialize();
                return this._fieldCount-1;
            }
        }

        public bool HasHeaders
        {
            get
            {
                return this._hasHeaders;
            }
        }

        [Browsable(false)]
        public bool IsDisposed
        {
            get
            {
                return this._isDisposed;
            }
        }

        public string this[int record, int field]
        {
            get
            {
                if (!this.MoveTo((long)record))
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, ExceptionMessage.CannotReadRecordAtIndex, new object[] { record }));
                }
                return this[field];
            }
        }

        public string this[int record, string field]
        {
            get
            {
                if (!this.MoveTo((long)record))
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, ExceptionMessage.CannotReadRecordAtIndex, new object[] { record }));
                }
                return this[field];
            }
        }

        public string this[string field]
        {
            get
            {
                if (string.IsNullOrEmpty(field))
                {
                    throw new ArgumentNullException("field");
                }
                if (!this._hasHeaders)
                {
                    throw new InvalidOperationException(ExceptionMessage.NoHeaders);
                }
                int fieldIndex = this.GetFieldIndex(field);
                if (fieldIndex < 0)
                {
                    throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, ExceptionMessage.FieldHeaderNotFound, new object[] { field }), "field");
                }
                return this[fieldIndex];
            }
        }

        public virtual string this[int field]
        {
            get
            {
                return this.ReadField(field, false, false);
            }
        }

        public MissingFieldAction MissingFieldAction
        {
            get
            {
                return this._missingFieldAction;
            }
            set
            {
                this._missingFieldAction = value;
            }
        }

        public bool MissingFieldFlag
        {
            get
            {
                return this._missingFieldFlag;
            }
        }

        public bool ParseErrorFlag
        {
            get
            {
                return this._parseErrorFlag;
            }
        }

        public char Quote
        {
            get
            {
                return this._quote;
            }
        }

        public bool SkipEmptyLines
        {
            get
            {
                return this._skipEmptyLines;
            }
            set
            {
                this._skipEmptyLines = value;
            }
        }

        public bool SupportsMultiline
        {
            get
            {
                return this._supportsMultiline;
            }
            set
            {
                this._supportsMultiline = value;
            }
        }

        int IDataReader.Depth
        {
            get
            {
                this.ValidateDataReader(DataReaderValidations.IsNotClosed);
                return 0;
            }
        }

        bool IDataReader.IsClosed
        {
            get
            {
                return this._eof;
            }
        }

        int IDataReader.RecordsAffected
        {
            get
            {
                return -1;
            }
        }

        object IDataRecord.this[int i]
        {
            get
            {
                this.ValidateDataReader(DataReaderValidations.IsNotClosed | DataReaderValidations.IsInitialized);
                return this[i];
            }
        }

        object IDataRecord.this[string name]
        {
            get
            {
                this.ValidateDataReader(DataReaderValidations.IsNotClosed | DataReaderValidations.IsInitialized);
                return this[name];
            }
        }

        public ValueTrimmingOptions TrimmingOption
        {
            get
            {
                return this._trimmingOptions;
            }
        }

        // Nested Types
        [Flags]
        private enum DataReaderValidations
        {
            None,
            IsInitialized,
            IsNotClosed
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RecordEnumerator : IEnumerator<string[]>, IDisposable, IEnumerator
        {
            private CsvReader _reader;
            private string[] _current;
            private long _currentRecordIndex;
            public RecordEnumerator(CsvReader reader)
            {
                if (reader == null)
                {
                    throw new ArgumentNullException("reader");
                }
                this._reader = reader;
                this._current = null;
                this._currentRecordIndex = reader._currentRecordIndex;
            }

            public string[] Current
            {
                get
                {
                    return this._current;
                }
            }
            public bool MoveNext()
            {
                if (this._reader._currentRecordIndex != this._currentRecordIndex)
                {
                    throw new InvalidOperationException(ExceptionMessage.EnumerationVersionCheckFailed);
                }
                if (this._reader.ReadNextRecord())
                {
                    this._current = new string[this._reader._fieldCount];
                    this._reader.CopyCurrentRecordTo(this._current);
                    this._currentRecordIndex = this._reader._currentRecordIndex;
                    return true;
                }
                this._current = null;
                this._currentRecordIndex = this._reader._currentRecordIndex;
                return false;
            }

            public void Reset()
            {
                if (this._reader._currentRecordIndex != this._currentRecordIndex)
                {
                    throw new InvalidOperationException(ExceptionMessage.EnumerationVersionCheckFailed);
                }
                this._reader.MoveTo(-1L);
                this._current = null;
                this._currentRecordIndex = this._reader._currentRecordIndex;

            }

            object IEnumerator.Current
            {
                get
                {
                    if (this._reader._currentRecordIndex != this._currentRecordIndex)
                    {
                        throw new InvalidOperationException(ExceptionMessage.EnumerationVersionCheckFailed);
                    }
                    return this.Current;
                }
            }
            public void Dispose()
            {
                this._reader = null;
                this._current = null;
            }
        }

        public override bool ReadNextLine()
        {
            return this.ReadNextRecord(false, false);
        }

        public override long TotalRecords
        {
            get
            {
                return _totalRecords;

            }
        }
    }

}




