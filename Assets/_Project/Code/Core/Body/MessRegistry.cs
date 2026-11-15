using System;
using System.Collections.Generic;
using System.Globalization;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b><see cref="IMessRegistry"/> over the save</b> (body slice 1): <see cref="SaveData.Messes"/> and
    /// the id serial <see cref="SaveData.NextMessSerial"/>. Writes the save's live data only; a mess is
    /// kept on disk by the next save point, like everything else eating changes.
    /// </summary>
    public sealed class MessRegistry : IMessRegistry
    {
        /// <summary>The id prefix: <c>mess.1</c>, <c>mess.2</c>...</summary>
        public const string IdPrefix = "mess.";

        private static readonly List<MessRecordDto> None = new List<MessRecordDto>();
        private readonly Func<ISaveService> _save;

        public MessRegistry(Func<ISaveService> save = null)
        {
            _save = save ?? (() => GameServices.Save);
        }

        public IReadOnlyList<MessRecordDto> All => List() ?? None;

        public string Add(string regionId, UnityEngine.Vector2 position, double gameSeconds)
        {
            SaveData data = _save()?.Current;
            if (data == null) return null;
            List<MessRecordDto> list = data.Messes ??= new List<MessRecordDto>();
            int serial = (data.NextMessSerial > 0 ? data.NextMessSerial : 0) + 1;
            data.NextMessSerial = serial;
            var mess = new MessRecordDto(IdPrefix + serial.ToString(CultureInfo.InvariantCulture),
                                         regionId ?? "", position, gameSeconds);
            list.Add(mess);
            EventBus.Publish(new MessLeft(mess));
            return mess.Id;
        }

        public bool TryGet(string messId, out MessRecordDto mess)
        {
            List<MessRecordDto> list = List();
            int i = IndexOf(list, messId);
            mess = i >= 0 ? list[i] : default;
            return i >= 0;
        }

        public int CopyIn(string regionId, List<MessRecordDto> into)
        {
            List<MessRecordDto> list = List();
            if (list == null || into == null) return 0;
            int n = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (!string.Equals(list[i].RegionId, regionId ?? "", StringComparison.Ordinal)) continue;
                into.Add(list[i]);
                n++;
            }
            return n;
        }

        public bool CleanUp(string messId)
        {
            List<MessRecordDto> list = List();
            int i = IndexOf(list, messId);
            if (i < 0) return false;
            MessRecordDto mess = list[i];
            list.RemoveAt(i);
            EventBus.Publish(new MessCleanedUp(mess));
            return true;
        }

        private List<MessRecordDto> List() => _save()?.Current?.Messes;

        private static int IndexOf(List<MessRecordDto> list, string messId)
        {
            if (list == null || string.IsNullOrEmpty(messId)) return -1;
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i].Id, messId, StringComparison.Ordinal)) return i;
            return -1;
        }
    }
}
