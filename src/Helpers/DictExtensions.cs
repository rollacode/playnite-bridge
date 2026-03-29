namespace PlayniteBridge.Helpers
{
    using System.Collections.Generic;

    internal static class DictExtensions
    {
        public static TValue GetValueOrNull<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key) where TValue : class
        {
            return dict.ContainsKey(key) ? dict[key] : null;
        }

        public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue defaultValue)
        {
            return dict.ContainsKey(key) ? dict[key] : defaultValue;
        }
    }
}
