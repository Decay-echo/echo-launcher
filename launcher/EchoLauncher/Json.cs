// Echo Launcher -- JSON through the framework's JavaScriptSerializer (System.Web.Extensions, part of .NET
// Framework 4.8, so nothing to ship). Objects come back as Dictionary<string, object>, arrays as object[],
// numbers as int/long/decimal/double. The helpers below read them without casting accidents.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;

namespace EchoLauncher
{
    public static class Json
    {
        static JavaScriptSerializer NewSerializer()
        {
            return new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024, RecursionLimit = 64 };
        }

        public static object Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            return NewSerializer().DeserializeObject(text);
        }

        public static string Stringify(object value)
        {
            return NewSerializer().Serialize(value);
        }

        public static Dictionary<string, object> AsObject(object v)
        {
            return v as Dictionary<string, object>;
        }

        public static object[] AsArray(object v)
        {
            var arr = v as object[];
            if (arr != null) return arr;
            var list = v as System.Collections.ArrayList;
            if (list != null) return list.ToArray();
            return null;
        }

        public static object Get(object obj, string key)
        {
            var d = AsObject(obj);
            if (d == null) return null;
            object v;
            return d.TryGetValue(key, out v) ? v : null;
        }

        public static string Str(object obj, string key, string fallback = "")
        {
            var v = Get(obj, key);
            if (v == null) return fallback;
            return v as string ?? Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        public static long Int(object obj, string key, long fallback = 0)
        {
            var v = Get(obj, key);
            if (v == null) return fallback;
            try { return Convert.ToInt64(v, CultureInfo.InvariantCulture); } catch { return fallback; }
        }

        public static bool Bool(object obj, string key, bool fallback = false)
        {
            var v = Get(obj, key);
            if (v is bool) return (bool)v;
            return fallback;
        }

        public static object[] Arr(object obj, string key)
        {
            return AsArray(Get(obj, key)) ?? new object[0];
        }

        public static List<string> StrList(object obj, string key)
        {
            var out_ = new List<string>();
            foreach (var item in Arr(obj, key))
            {
                var s = item as string;
                if (!string.IsNullOrEmpty(s)) out_.Add(s);
            }
            return out_;
        }
    }
}
