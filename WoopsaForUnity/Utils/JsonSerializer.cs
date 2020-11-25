﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Woopsa
{
    public static class JsonSerializer
    {
        public static string Serialize(object obj)
        {
            return JsonConvert.SerializeObject(obj);
        }

        public static Dictionary<string, object> ToDictionnary(object obj)
        {
            var jobect = obj as JObject;
            if (jobect != null)
                return jobect.ToObject<Dictionary<string, object>>();

            return null;
        }

        public static object[] ToArray(object obj)
        {
            var jarray = obj as JArray;
            if (jarray != null)
                return jarray.ToObject<object[]>();

            return null;
        }

        public static T Deserialize<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }

        public static T Deserialize<T>(string json, JsonConverter converter)
        {
            return JsonConvert.DeserializeObject<T>(json, converter);
        }
    }
}