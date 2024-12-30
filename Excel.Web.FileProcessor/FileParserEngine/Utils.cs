//------------------------------------------------------ //
// Date	            : <<Current Date>>                   //
// Version          : 1.000                              //
// Developer        : <<Your Initial>>                   //
// Last Modified By : <<Initial>>                        //
// Last                                                  //
//------------------------------------------------------ //

using System;
using System.Data;
using System.IO;
using System.Xml.Serialization;
using System.Text;
using System.Xml;
using System.Net;
using System.Configuration;
using System.Collections.Generic;

namespace Aspen.Web.FileProcessor.Utils
{
    
    /// <summary>
    /// Class for Serlize and Deserlize objects
    /// </summary>
    /// <typeparam name="objectToSerlize"></typeparam>
    public class Serializer<objectToSerlize>
    {
        /// <summary>
        /// Serialize the object into XML
        /// </summary>
        /// <param name="t">Type of object</param>
        /// <returns>XML as string</returns>
        public string Serialize(objectToSerlize t)
        {
            MemoryStream stream = null;
            TextWriter writer = null;
            try
            {
                using (stream = new MemoryStream()) // read xml in memory
                {
                    using (writer = new StreamWriter(stream, Encoding.UTF8))
                    {
                        // get serialise object
                        XmlSerializer serializer = new XmlSerializer(typeof(objectToSerlize));

                        serializer.Serialize(writer, t); // read object
                        int count = (int)stream.Length; // saves object in memory stream
                        byte[] arr = new byte[count];
                        stream.Seek(0, SeekOrigin.Begin);
                        // copy stream contents in byte array
                        stream.Read(arr, 0, count);
                        UTF8Encoding utf = new UTF8Encoding(); // convert byte array to string
                        return utf.GetString(arr).Trim();
                    }
                }
            }
            catch
            {
                return string.Empty;
            }
            finally
            {
                if (stream != null) stream.Close();
                if (writer != null) writer.Close();
            }
        }
        /// <summary>
        /// DeSerliaze Objects From XML
        /// </summary>
        /// <param name="xml"></param>
        /// <returns></returns>
        public objectToSerlize Deserialize(string xml)
        {
            StringReader stream = null;
            XmlTextReader reader = null;
            try
            {
                // serialise to object
                XmlSerializer serializer = new XmlSerializer(typeof(objectToSerlize));
                using (stream = new StringReader(xml))
                // read xml data
                {
                    reader = new XmlTextReader(stream);  // create reader

                    // covert reader to object
                    return (objectToSerlize)serializer.Deserialize(reader);
                }
            }
            catch
            {
                return default(objectToSerlize);
            }
            finally
            {
                if (stream != null) stream.Close();
                if (reader != null) reader.Close();
            }
        }

    }



}
