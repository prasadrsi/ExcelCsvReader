//------------------------------------------------------ //
// Author           : RSystems International LTD.        //
// Date	            : <<Current Date>>                   //
// Version          : 1.000                              //
// Website          : www.rsystems.com                   // 
// Developer        : NT                                 //
// Last Modified Date: 06-Sep-2011                       //
//                                                       //
//------------------------------------------------------ //


#region Namespaces

using System;
using System.Diagnostics;
using System.Globalization;
using Aspen.Web.FileProcessor.FileParserEngine;
using Aspen.Web.FileProcessor.FileParserEngine.Resources;
using System.Runtime.Serialization;
using System.IO;


#endregion

namespace Aspen.Web.FileProcessor
{
	/// <summary>
	///
    /// This class contains abstract methods which are implemented in DBSQLNet class
	/// </summary>
    public abstract class FileProvider
	{

        private bool _isDisposed;
        private readonly object _lock;
        private TextReader _reader;
        private bool _eof;
        private char[] _buffer;

        // Events
        public event EventHandler Disposed;

        public  abstract long CurrentRecordIndex {get;}

        public abstract long TotalRecords { get; }

        public event EventHandler<ParseErrorEventArgs> ParseError;

        protected abstract bool ReadNextRecord(bool onlyReadHeaders, bool skipToNextLine);

		public FileProvider()
		{
		}


        public abstract bool ReadNextLine();
       
        protected virtual void OnParseError(ParseErrorEventArgs e)
        {
            EventHandler<ParseErrorEventArgs> parseError = this.ParseError;
            if (parseError != null)
            {
                parseError(this, e);
            }
        }

        protected virtual void OnDisposed(EventArgs e)
        {
            EventHandler disposed = this.Disposed;
            if (disposed != null)
            {
                disposed(this, e);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this._isDisposed)
            {
                try
                {
                    if (disposing && (this._reader != null))
                    {
                        lock (this._lock)
                        {
                            if (this._reader != null)
                            {
                                this._reader.Dispose();
                                this._reader = null;
                                this._buffer = null;
                                this._eof = true;
                            }
                        }
                    }
                }
                finally
                {
                    this._isDisposed = true;
                    try
                    {
                        this.OnDisposed(EventArgs.Empty);
                    }
                    catch
                    {
                    }
                }
            }
        }

        public abstract int FieldCount { get; }

        public abstract string[] GetFieldHeaders();

        public abstract string ReadField(int field);
        
    }

    public class ParseErrorEventArgs : EventArgs
    {
        // Fields
        private ParseErrorAction _action;
        private MalformedFileException _error;

        // Methods
        public ParseErrorEventArgs(MalformedFileException error, ParseErrorAction defaultAction)
        {
            this._error = error;
            this._action = defaultAction;
        }

        // Properties
        public ParseErrorAction Action
        {
            get
            {
                return this._action;
            }
            set
            {
                this._action = value;
            }
        }

        public MalformedFileException Error
        {
            get
            {
                return this._error;
            }
        }
    }

    [Serializable]
    public class MalformedFileException : Exception
    {
        // Fields
        private int _currentFieldIndex;
        private int _currentPosition;
        private long _currentRecordIndex;
        private string _message;
        private string _rawData;

        // Methods
        public MalformedFileException()
            : this((string)null, (Exception)null)
        {
        }

        public MalformedFileException(string message)
            : this(message, null)
        {
        }

        protected MalformedFileException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            this._message = info.GetString("MyMessage");
            this._rawData = info.GetString("RawData");
            this._currentPosition = info.GetInt32("CurrentPosition");
            this._currentRecordIndex = info.GetInt64("CurrentRecordIndex");
            this._currentFieldIndex = info.GetInt32("CurrentFieldIndex");
        }

        public MalformedFileException(string message, Exception innerException)
            : base(string.Empty, innerException)
        {
            this._message = (message == null) ? string.Empty : message;
            this._rawData = string.Empty;
            this._currentPosition = -1;
            this._currentRecordIndex = -1L;
            this._currentFieldIndex = -1;
        }

        public MalformedFileException(string rawData, int currentPosition, long currentRecordIndex, int currentFieldIndex)
            : this(rawData, currentPosition, currentRecordIndex, currentFieldIndex, null)
        {
        }

        public MalformedFileException(string rawData, int currentPosition, long currentRecordIndex, int currentFieldIndex, Exception innerException)
            : base(string.Empty, innerException)
        {
            this._rawData = (rawData == null) ? string.Empty : rawData;
            this._currentPosition = currentPosition;
            this._currentRecordIndex = currentRecordIndex;
            this._currentFieldIndex = currentFieldIndex;
            this._message = string.Format(CultureInfo.InvariantCulture, ExceptionMessage.MalformedFileException, new object[] { this._currentRecordIndex, this._currentFieldIndex, this._currentPosition, this._rawData });
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("MyMessage", this._message);
            info.AddValue("RawData", this._rawData);
            info.AddValue("CurrentPosition", this._currentPosition);
            info.AddValue("CurrentRecordIndex", this._currentRecordIndex);
            info.AddValue("CurrentFieldIndex", this._currentFieldIndex);
        }

        // Properties
        public int CurrentFieldIndex
        {
            get
            {
                return this._currentFieldIndex;
            }
        }

        public int CurrentPosition
        {
            get
            {
                return this._currentPosition;
            }
        }

        public long CurrentRecordIndex
        {
            get
            {
                return this._currentRecordIndex;
            }
        }

        public override string Message
        {
            get
            {
                return this._message;
            }
        }

        public string RawData
        {
            get
            {
                return this._rawData;
            }
        }
    }

    [Serializable]
    public class MissingFieldException : MalformedFileException
    {
        // Methods
        public MissingFieldException()
        {
        }

        public MissingFieldException(string message)
            : base(message)
        {
        }

        protected MissingFieldException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        public MissingFieldException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public MissingFieldException(string rawData, int currentPosition, long currentRecordIndex, int currentFieldIndex)
            : base(rawData, currentPosition, currentRecordIndex, currentFieldIndex)
        {
        }

        public MissingFieldException(string rawData, int currentPosition, long currentRecordIndex, int currentFieldIndex, Exception innerException)
            : base(rawData, currentPosition, currentRecordIndex, currentFieldIndex, innerException)
        {
        }
    }

 

}
