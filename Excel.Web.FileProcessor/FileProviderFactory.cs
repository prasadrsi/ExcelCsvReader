//------------------------------------------------------ //
// Author           : RSystems International LTD.        //
// Date	            : <<Current Date>>                   //
// Version          : 1.000                              //
// Website          : www.rsystems.com                   // 
// Developer        : NT                                 //
// Last Modified Date: 06-Sep-2011                       //
//                                                       //
//------------------------------------------------------ //

// ===============================================================================
// Data Access Layer 
//
// DbProviderFactory.cs
//
// This file contains an abstract implementations of the DbProviderFactory class.
//
// DbProviderFactory Class : This class implements Create method for the factory class 
// which returns the right type of the DataBase provider object according to the 
// Provider string , which is passed in signature of the method.
// ===============================================================================

#region Namespace

using System;
using System.IO;
using Aspen.Web.FileProcessor.FileParserEngine;

#endregion

namespace Aspen.Web.FileProcessor
{
    /// <summary>
    ///     This class implements Create method for the factory class
    ///     which returns the right type of the DataBase provider object according to the
    ///     Provider string , which is passed in signature of the method.
    /// </summary>
    public class FileProviderFactory
    {
        /// <summary>
        /// </summary>
        /// <param name="filePath">Full file path with extention</param>
        /// <returns></returns>
        public static FileProvider Create(string filePath)
        {
            //TODO:: Read and create a streama and extension can be read here..
            return Create(null, FileType.CSV);
        }

        public static FileProvider Create(TextReader fs, FileType fileType)
        {
            //TODO:: Read and create a streama and extension can be read here..
            return Create(fs, fileType, null);
        }

        /// <summary>
        ///     <Purpose>This function will open a database connection.</Purpose>
        /// </summary>
        public static FileProvider Create(TextReader fs, FileType fileType, Stream stream, bool csvInculdeHeader=false)
        {
            // Check if agency ticket not found then get it from server.
            FileProvider fileProvider = null;
            switch (fileType)
            {
                case FileType.CSV:

                    fileProvider = new CsvReader(fs, csvInculdeHeader);
                    return fileProvider;

                case FileType.EXCEL:
                    fileProvider = new ExcelFileParser(stream);
                    return fileProvider;

                case FileType.EXCELX:
                    fileProvider = new ExcelXFileParser(stream);
                    return fileProvider;

                case FileType.XML:

                    //throw new NotImplementedException();
                    // fileProvider = new XMLFileParser(fs);
                    return fileProvider;

                default:
                    throw new NotImplementedException();
            }
        }
    }

    public enum FileType
    {
        EXCELX,
        EXCEL,
        XML,
        CSV
    }
}