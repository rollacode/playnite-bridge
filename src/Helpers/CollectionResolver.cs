namespace PlayniteBridge.Helpers
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Playnite.SDK;
    using Playnite.SDK.Models;

    internal class CollectionResolver
    {
        public T GetOrCreate<T>(IItemCollection<T> collection, string name) where T : DatabaseObject
        {
            if (collection == null || string.IsNullOrEmpty(name)) return null;
            var existing = collection.FirstOrDefault(x => x.Name != null && x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing != null) return existing;

            try
            {
                var item = (T)Activator.CreateInstance(typeof(T), new object[] { name });
                collection.Add(item);
                return item;
            }
            catch
            {
                return null;
            }
        }

        public List<Guid> ResolveIds<T>(IItemCollection<T> collection, object names, bool create = true) where T : DatabaseObject
        {
            List<string> nameList = null;
            if (names is ArrayList arrayList)
                nameList = arrayList.Cast<string>().ToList();
            else if (names is List<string> stringList)
                nameList = stringList;
            else if (names is IEnumerable<string> enumerable)
                nameList = enumerable.ToList();
            if (nameList == null || nameList.Count == 0) return new List<Guid>();

            var ids = new List<Guid>();
            foreach (var name in nameList)
            {
                if (string.IsNullOrEmpty(name)) continue;

                if (create)
                {
                    var item = GetOrCreate(collection, name);
                    if (item != null) ids.Add(item.Id);
                }
                else
                {
                    var item = collection.FirstOrDefault(x => x.Name != null && x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (item != null) ids.Add(item.Id);
                }
            }
            return ids;
        }

        public List<string> GetNameList(Game game, string field)
        {
            switch (field)
            {
                case "categories": return game.Categories?.Select(x => x.Name).ToList() ?? new List<string>();
                case "tags": return game.Tags?.Select(x => x.Name).ToList() ?? new List<string>();
                case "features": return game.Features?.Select(x => x.Name).ToList() ?? new List<string>();
                case "genres": return game.Genres?.Select(x => x.Name).ToList() ?? new List<string>();
                default: return new List<string>();
            }
        }
    }
}
