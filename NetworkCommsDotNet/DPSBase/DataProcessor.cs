﻿//  Copyright 2009-2014 Marc Fletcher, Matthew Dean
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
//
//  Non-GPL versions of this software can also be purchased. 
//  Please see <http://www.networkcomms.net> for details.

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Reflection;

#if NETFX_CORE
using System.Linq;
#endif

namespace NetworkCommsDotNet.DPSBase
{
    /// <summary>
    /// Provides methods that process data in a <see cref="System.IO.Stream"/> into another <see cref="System.IO.Stream"/>.  Can be used to provide features such as data compression or encryption
    /// </summary>
    /// <example>
    /// <code source="..\NetworkCommsDotNet\DPSBase\RijndaelPSKEncrypter.cs" lang="cs" title="Implementation Example" />
    /// </example>
    public abstract class DataProcessor
    {
        private static Dictionary<Type, byte> cachedIdentifiers = new Dictionary<Type, byte>();
        private static Dictionary<Type, bool> cachedIsSecurity = new Dictionary<Type, bool>();
        private static object locker = new object();

        /// <summary>
        /// Helper function to allow a <see cref="DataProcessor"/> to be implemented as a singleton.  Returns the singleton instance generated by the <see cref="DPSManager"/>
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of the <see cref="DataProcessor"/> to retrieve from the <see cref="DPSManager"/></typeparam>
        /// <returns>The singleton instance generated by the <see cref="DPSManager"/></returns>
        [Obsolete("Instances of singleton DataProcessors should be accessed via the DPSManager")]
        protected static T GetInstance<T>() where T : DataProcessor
        {
            //this forces helper static constructor to be called and gets us an instance if composition worked
            var instance = DPSManager.GetDataProcessor<T>() as T;

            if (instance == null)
            {
                //if the instance is null the type was not added as part of composition
                //create a new instance of T and add it to helper as a compressor
#if NETFX_CORE
                var construct = (from constructor in typeof(T).GetTypeInfo().DeclaredConstructors
                                 where constructor.GetParameters().Length == 0
                                 select constructor).FirstOrDefault();
#else
                var construct = typeof(T).GetConstructor(new Type[] { });
                if (construct == null)
                    construct = typeof(T).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[0], null);
#endif
                if (construct == null)
                    throw new Exception();

                instance = construct.Invoke(new object[] { }) as T;
                
                DPSManager.AddDataProcessor(instance);
            }

            return instance;
        }

        /// <summary>
        /// Returns a unique identifier for the compressor type. Used in automatic serialization/compression detection
        /// </summary>
        public byte Identifier
        {
            get
            {
                lock (locker)
                {
                    Type typeOfThis = this.GetType();

                    if (!cachedIdentifiers.ContainsKey(typeOfThis))
                    {
#if NETFX_CORE
                        var attributes = this.GetType().GetTypeInfo().GetCustomAttributes(typeof(DataSerializerProcessorAttribute), false).ToArray();
#else
                        var attributes = this.GetType().GetCustomAttributes(typeof(DataSerializerProcessorAttribute), false);
#endif
                        if (attributes.Length == 1)
                            cachedIdentifiers[typeOfThis] = (attributes[0] as DataSerializerProcessorAttribute).Identifier;
                        else
                            throw new Exception("Data serializer and processor types must have a DataSerializerProcessorAttribute specifying a unique id");
                    }

                    return cachedIdentifiers[typeOfThis];
                }
            }
        }

        /// <summary>
        /// Returns a boolian stating whether this data processor is security critical
        /// </summary>
        public bool IsSecurityCritical
        {
            get
            {
                lock (locker)
                {
                    Type typeOfThis = this.GetType();

                    if (!cachedIdentifiers.ContainsKey(typeOfThis))
                    {
#if NETFX_CORE
                        var attributes = this.GetType().GetTypeInfo().GetCustomAttributes(typeof(SecurityCriticalDataProcessorAttribute), false).ToArray();
#else
                        var attributes = this.GetType().GetCustomAttributes(typeof(SecurityCriticalDataProcessorAttribute), false);
#endif
                        if (attributes != null && attributes.Length > 0)
                            cachedIsSecurity[typeOfThis] = (attributes[0] as SecurityCriticalDataProcessorAttribute).IsSecurityCritical;
                        else
                            return false;
                    }

                    return cachedIsSecurity[typeOfThis];
                }
            }
        }

        /// <summary>
        /// Processes data held in a stream and outputs it to another stream
        /// </summary>
        /// <param name="inStream">An input stream containing data to be processed</param>
        /// <param name="outStream">An output stream to which the processed data is written</param>
        /// <param name="options">Options dictionary for serialisation/data processing</param>
        /// <param name="writtenBytes">The size of the data written to the output stream</param>        
        public abstract void ForwardProcessDataStream(Stream inStream, Stream outStream, Dictionary<string, string> options, out long writtenBytes);

        /// <summary>
        /// Processes data, in reverse, that is held in a stream and outputs it to another stream
        /// </summary>
        /// <param name="inStream">An input stream containing data to be processed</param>
        /// <param name="outStream">An output stream to which the processed data is written</param>
        /// <param name="options">Options dictionary for serialisation/data processing</param>
        /// <param name="writtenBytes">The size of the data written to the output stream</param>                
        public abstract void ReverseProcessDataStream(Stream inStream, Stream outStream, Dictionary<string, string> options, out long writtenBytes);
    }
}
