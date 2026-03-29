namespace PlayniteBridge.Handlers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Playnite.SDK;
    using Playnite.SDK.Models;
    using PlayniteBridge.Helpers;
    using PlayniteBridge.Server;

    internal class CollectionHandler
    {
        private readonly IPlayniteAPI _api;
        private readonly CollectionResolver _resolver;

        public CollectionHandler(IPlayniteAPI api, CollectionResolver resolver)
        {
            _api = api;
            _resolver = resolver;
        }

        public object GetCollection<T>(IItemCollection<T> collection) where T : DatabaseObject
        {
            if (collection == null) return new List<object>();
            return collection.OrderBy(x => x.Name).Select(x => new { id = x.Id.ToString(), name = x.Name }).ToList();
        }

        public object CreateItem<T>(IItemCollection<T> collection, RequestContext ctx) where T : DatabaseObject
        {
            var data = ctx.Body;
            var name = data["name"].ToString();
            var item = _resolver.GetOrCreate(collection, name);
            return item != null
                ? (object)new { ok = true, id = item.Id.ToString(), name = item.Name }
                : new { error = "Failed to create item" };
        }

        public object DeleteCollectionItem<T>(IItemCollection<T> collection, string idStr) where T : DatabaseObject
        {
            if (!Guid.TryParse(idStr, out var id))
                return new { error = "Invalid ID", code = 400 };
            var item = collection.Get(id);
            if (item == null) return new { error = "Item not found", code = 404 };
            var name = item.Name;
            collection.Remove(id);
            return new { ok = true, deleted = name };
        }

        public object GetEmulators()
        {
            var emulators = _api.Database.Emulators;
            if (emulators == null) return new List<object>();
            return emulators.OrderBy(e => e.Name).Select(e => new
            {
                id = e.Id.ToString(),
                name = e.Name,
                installDir = e.InstallDir
            }).ToList();
        }
    }
}
