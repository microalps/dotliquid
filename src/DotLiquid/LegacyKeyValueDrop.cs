using DotLiquid.Util;
using System.Collections.Generic;

namespace DotLiquid
{
    class LegacyKeyValueDrop : Drop
    {
        private readonly string key;
        private readonly IDictionary<string, object> value;

        public LegacyKeyValueDrop(string key, IDictionary<string, object> value)
        {
            this.key = key;
            this.value = value;
        }

        public override object BeforeMethod(string method)
        {
            if (method.SafeTypeInsensitiveEqual(0L) || method.Equals("Key") || method.Equals("itemName"))
                return key;
            else if (method.SafeTypeInsensitiveEqual(1L) || method.Equals("Value"))
                return value;
            else if (value.ContainsKey(method))
                return value[method];
            return null;
        }

        public override bool ContainsKey(object name)
        {
            string method = name.ToString();
            return method.SafeTypeInsensitiveEqual(0L) || method.Equals("Key") || method.Equals("itemName")
                || method.SafeTypeInsensitiveEqual(1L) || method.Equals("Value") || value.ContainsKey(method);
        }
    }
}
