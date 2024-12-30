//------------------------------------------------------ //
// Author           : RSystems International LTD.        //
// Date	            : <<Current Date>>                   //
// Version          : 1.000                              //
// Website          : www.rsystems.com                   // 
// Developer        : NT                                 //
// Last Modified Date: 06-Sep-2011                       //
//                                                       //
//------------------------------------------------------ //


#region Namespace

using System;
using System.Diagnostics;
using System.Text;
using System.Xml;
using System.Runtime.InteropServices;
using System.Reflection;
using System.IO;
using Excel;
using System.Collections.Generic;
using System.Data;
#endregion


namespace Aspen.Web.FileProcessor.FileParserEngine
{
    /// <summary>
    /// DBSQLNet performs database operations.
    /// </summary>
    public class ExcelFileParser : FileProvider
    {
        
        IExcelDataReader excelReader = null;
        object[,] values;
        private string[] currentLine;
        private string[] headers;
        private long currentIndex = 0;
        private long currentRecordIndex;
        private int totalRecords;
        private string currentRecord;
        private int fieldHeaders;

        public int RowCount { get; set; }
        public int ColCount { get; set; }

        protected override bool ReadNextRecord(bool onlyReadHeaders, bool skipToNextLine)
        {
            throw new NotImplementedException();
        }

        public override long TotalRecords
        {
            get
            {


                return excelReader.RecordCount;// totalRecords;
            }
        }

        public override long CurrentRecordIndex
        {
            get
            {

                return currentIndex - 1;
            }
        }



        public override string[] GetFieldHeaders()
        {
            return excelReader.GetHeaders();
        }

        public override string ReadField(int i)
        {
            return Convert.ToString(excelReader[i]);
        }

        public ExcelFileParser()
        {
           

        }

        public ExcelFileParser(byte[] textReader)
        {
            MemoryStream stream = new MemoryStream(textReader);
            ExcelOpenSpreadsheets(stream);
            

        }

        public ExcelFileParser(Stream textReader)
        {
            

            ExcelOpenSpreadsheets(textReader);

        }

        public override bool ReadNextLine()
        {
            currentIndex++;
            if (currentIndex > excelReader.RecordCount) return false;
            excelReader.Read();

        
            return true;
        }

        public void ExcelOpenSpreadsheets(Stream thisFileName)
        {
            //char[] array = new char[thisFileName.Length];


            excelReader = ExcelReaderFactory.CreateBinaryReader(thisFileName);
            excelReader.Initialize(thisFileName);
        }

        public override int FieldCount
        {
            get { return excelReader.FieldCount; }
        }
    }
}
