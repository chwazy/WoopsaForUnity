﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Woopsa
{
    public class WoopsaJsonData
    {
        public static WoopsaJsonData CreateFromText(string jsonText)
        {
            object deserializedData = JsonSerializer.Deserialize<object>(jsonText);
            return new WoopsaJsonData(deserializedData, jsonText);
        }
        public static WoopsaJsonData CreateFromDeserializedData(object deserializedJson)
        {
            return new WoopsaJsonData(deserializedJson, null);
        }

        private WoopsaJsonData(object deserializedData, string serializedData)
        {
            _data = deserializedData;
            _serializedData = serializedData;
            _asDictionary = JsonSerializer.ToDictionnary(_data);
            _asArray = JsonSerializer.ToArray(_data);
        }

        public WoopsaJsonData this[string key]
        {
            get
            {
                WoopsaJsonData result;
                if (TryGetDictionaryKey(key, out result))
                    return result;
                else
                    throw new InvalidOperationException("String indexer is only available on WoopsaJsonData of type Object.");
            }
        }

        public bool TryGetDictionaryKey(string key, out WoopsaJsonData value)
        {
            if (IsDictionary)
            {
                object dictionnaryEntry;
                if (_asDictionary.TryGetValue(key, out dictionnaryEntry))
                {
                    value = CreateFromDeserializedData(_asDictionary[key]);
                    return true;
                }
            }

            value = null;
            return false;
        }

        public IEnumerable<string> Keys
        {
            get
            {
                if (IsDictionary)
                    return _asDictionary.Keys;
                else
                    return new string[0];
            }
        }

        public bool ContainsKey(string key)
        {
            if (IsDictionary)
                return _asDictionary.ContainsKey(key);
            else
                return false;
        }

        public WoopsaJsonData this[int index]
        {
            get
            {
                WoopsaJsonData result;
                if (TryGetArrayIndex(index, out result))
                    return result;
                else
                    throw new InvalidOperationException("Integer indexer is only available on WoopsaJsonData of type Array.");
            }
        }

        public bool TryGetArrayIndex(int index, out WoopsaJsonData result)
        {
            if (IsArray)
            {
                result = CreateFromDeserializedData(_asArray[index]);
                return true;
            }
            else
            {
                result = null;
                return false;
            }
        }

        public int Length
        {
            get
            {
                if (IsArray)
                    return _asArray.Length;
                else
                    throw new InvalidOperationException("Length is only available on WoopsaJsonData of type Array.");
            }
        }

        public bool IsArray { get { return _asArray != null; } }

        public bool IsDictionary { get { return _asDictionary != null; } }

        public bool IsSimple { get { return (_asArray == null) && (_asDictionary == null); } }

        public string AsText
        {
            get
            {
                if (_serializedData == null)
                {
                    if (IsSimple)
                    {
                        _serializedData = WoopsaFormat.ToStringWoopsa(_data);
                    }
                    else
                    {
                        _serializedData = JsonSerializer.Serialize(_data);
                    }
                }
                return _serializedData;
            }
        }

        public override string ToString()
        {
            return AsText;
        }

        internal object InternalObject { get { return _data; } }

        private readonly object _data;
        private Dictionary<string, object> _asDictionary;
        private object[] _asArray;
        private string _serializedData;

        #region Static methods for type casting
        public static implicit operator bool (WoopsaJsonData value)
        {
            return value.ToBool();
        }

        public static implicit operator sbyte (WoopsaJsonData value)
        {
            return value.ToSByte();
        }

        public static implicit operator Int16(WoopsaJsonData value)
        {
            return value.ToInt16();
        }

        public static implicit operator Int32(WoopsaJsonData value)
        {
            return value.ToInt32();
        }

        public static implicit operator Int64(WoopsaJsonData value)
        {
            return value.ToInt64();
        }

        public static implicit operator byte (WoopsaJsonData value)
        {
            return value.ToByte();
        }

        public static implicit operator UInt16(WoopsaJsonData value)
        {
            return value.ToUInt16();
        }

        public static implicit operator UInt32(WoopsaJsonData value)
        {
            return value.ToUInt32();
        }

        public static implicit operator UInt64(WoopsaJsonData value)
        {
            return value.ToUInt64();
        }

        public static implicit operator float (WoopsaJsonData value)
        {
            return value.ToFloat();
        }

        public static implicit operator double (WoopsaJsonData value)
        {
            return value.ToDouble();
        }

        public static implicit operator string (WoopsaJsonData value)
        {
            return value.AsText;
        }
        #endregion
    }

    public static class WoopsaJsonDataExtensions
    {
        public static bool ToBool(this WoopsaJsonData data)
        {
            if (data.IsSimple)
                return WoopsaFormat.ToBool(WoopsaFormat.ToStringWoopsa(data.InternalObject));
            else
                throw new WoopsaException(WoopsaExceptionMessage.WoopsaCastTypeMessage("bool", data.InternalObject.GetType().ToString()));
        }

        public static SByte ToSByte(this WoopsaJsonData data)
        {
            if (data.IsSimple)
                return Convert.ToSByte(data.InternalObject);
            else
                throw new WoopsaException(WoopsaExceptionMessage.WoopsaCastTypeMessage("SByte", data.InternalObject.GetType().ToString()));
        }

        public static Int16 ToInt16(this WoopsaJsonData data)
        {
            if (data.IsSimple)
                return Convert.ToInt16(data.InternalObject);
            else
                throw new WoopsaException(WoopsaExceptionMessage.WoopsaCastTypeMessage("Int16", data.InternalObject.GetType().ToString()));
        }

        public static Int32 ToInt32(this WoopsaJsonData data)
        {
            if (data.IsSimple)
                return Convert.ToInt32(data.InternalObject);
            else
                throw new WoopsaException(WoopsaExceptionMessage.WoopsaCastTypeMessage("Int32", data.InternalObject.GetType().ToString()));
        }

        public static Int64 ToInt64(this WoopsaJsonData data)
        {
            if (data.IsSimple)
                return Convert.ToInt64(data.InternalObject);
            else
                throw new WoopsaException(WoopsaExceptionMessage.WoopsaCastTypeMessage("Int64", data.InternalObject.GetType().ToString()));
        }

        public static Byte ToByte(this WoopsaJsonData data)
        {
            if (data.IsSimple)
                return Convert.ToByte(data.InternalObject);
            else
                throw new WoopsaException(WoopsaExceptionMessage.WoopsaCastTypeMessage("Byte", data.InternalObject.GetType().ToString()));
        }

        public static UInt16 ToUInt16(this WoopsaJsonData data)
        {
            if (data.IsSimple)
                return Convert.ToUInt16(data.InternalObject);
            else
                throw new WoopsaException(WoopsaExceptionMessage.WoopsaCastTypeMessage("UInt16", data.InternalObject.GetType().ToString()));
        }

        public static UInt32 ToUInt32(this WoopsaJsonData data)
        {
            if (data.IsSimple)
                return Convert.ToUInt32(data.InternalObject);
            else
                throw new WoopsaException(WoopsaExceptionMessage.WoopsaCastTypeMessage("UInt32", data.InternalObject.GetType().ToString()));
        }

        public static UInt64 ToUInt64(this WoopsaJsonData data)
        {
            if (data.IsSimple)
                return Convert.ToUInt64(data.InternalObject);
            else
                throw new WoopsaException(WoopsaExceptionMessage.WoopsaCastTypeMessage("UInt64", data.InternalObject.GetType().ToString()));
        }

        public static float ToFloat(this WoopsaJsonData data)
        {
            if (data.IsSimple)
                return (float)Convert.ToDouble(data.InternalObject);
            else
                throw new WoopsaException(WoopsaExceptionMessage.WoopsaCastTypeMessage("float", data.InternalObject.GetType().ToString()));
        }

        public static double ToDouble(this WoopsaJsonData data)
        {
            if (data.IsSimple)
                return Convert.ToDouble(data.InternalObject);
            else
                throw new WoopsaException(WoopsaExceptionMessage.WoopsaCastTypeMessage("double", data.InternalObject.GetType().ToString()));
        }

        public static string Serialize(this WoopsaJsonData data)
        {
            return JsonSerializer.Serialize(data.InternalObject);
        }
    }
}
