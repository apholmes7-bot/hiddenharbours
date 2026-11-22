using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>How a piece of gear is counted on the page.</summary>
    public enum NotebookGearCount
    {
        /// <summary>Kept or not, no number (the rod, the shovel, the watch).</summary>
        Presence,

        /// <summary>Counted stock: pots, tubs, bait. <see cref="NotebookGearHolding.InWater"/> says how
        /// many of them are set.</summary>
        Stock,

        /// <summary>Bolted into boats: <see cref="NotebookGearHolding.Count"/> is how many boats carry it.</summary>
        Fitted,
    }

    /// <summary>One piece of gear the player holds, as the book sees it. A view shape, never saved.</summary>
    public readonly struct NotebookGearHolding
    {
        public readonly string Id;
        public readonly NotebookGearCount Kind;
        public readonly int Count;
        public readonly int InWater;

        public NotebookGearHolding(string id, NotebookGearCount kind, int count = 0, int inWater = 0)
        {
            Id = id ?? "";
            Kind = kind;
            Count = count;
            InWater = inWater;
        }
    }

    /// <summary>
    /// <b>Where the Gear page's rows come from</b> — the one seam between the page and ownership. The
    /// page asks for what the player holds and nothing else; which Defs exist, their names and headings
    /// are the page Def's business (<see cref="NotebookGearPageDef"/>), so listing unowned gear later is
    /// that Def's flag and this stays as it is.
    /// </summary>
    public interface INotebookGearSource
    {
        /// <summary>Fill <paramref name="into"/> (cleared first) with what the player holds.</summary>
        void Collect(List<NotebookGearHolding> into);
    }

    /// <summary>
    /// The running game's gear: read from the save through Core's own lockers (the owned-gear wallet,
    /// pot and tub stock with what is set, bait stock, the instruments fitted per hull) and the worn
    /// watch. Read-only: nothing here writes the save.
    /// </summary>
    public sealed class SaveGearSource : INotebookGearSource
    {
        private const string WatchPrefix = "watch.";
        private const string WatchesFolder = "Watches";

        private WatchDef[] _watches;

        public void Collect(List<NotebookGearHolding> into)
        {
            _watches ??= Resources.LoadAll<WatchDef>(WatchesFolder);
            Collect(GameServices.Save?.Current, _watches, GameConfig.CarriedFromStart(GameServices.Config), into);
        }

        /// <summary>The pure read, for tests: a null save holds nothing. No carried-from-start gear.</summary>
        public static void Collect(SaveData save, IReadOnlyList<WatchDef> watches, List<NotebookGearHolding> into)
            => Collect(save, watches, null, into);

        /// <summary>The pure read with the gear every game carries from the start
        /// (<see cref="GameConfig.CarriedFromStartGearIds"/>: the headlamp), which is owned without an id
        /// in the wallet, just as the starting watch is.</summary>
        public static void Collect(SaveData save, IReadOnlyList<WatchDef> watches, IReadOnlyList<string> carriedFromStart,
                                   List<NotebookGearHolding> into)
        {
            if (into == null) return;
            into.Clear();
            if (save == null) return;

            // The watch first: it is worn from the start without an id in the wallet (WatchEquipment).
            WatchDef worn = WatchEquipment.SelectOwned(save, watches);
            if (worn != null) into.Add(new NotebookGearHolding(worn.Id, NotebookGearCount.Presence));

            // Then what he has carried since the start (the headlamp): owned, never written to the save.
            if (carriedFromStart != null)
            {
                for (int i = 0; i < carriedFromStart.Count; i++)
                {
                    string id = carriedFromStart[i];
                    if (string.IsNullOrEmpty(id) || Contains(into, id)) continue;
                    into.Add(new NotebookGearHolding(id, NotebookGearCount.Presence));
                }
            }

            if (save.OwnedGear != null)
            {
                for (int i = 0; i < save.OwnedGear.Count; i++)
                {
                    string id = save.OwnedGear[i];
                    if (string.IsNullOrEmpty(id) || IsWatchId(id, watches) || Contains(into, id)) continue;
                    into.Add(new NotebookGearHolding(id, NotebookGearCount.Presence));
                }
            }

            if (save.PotStock != null)
            {
                for (int i = 0; i < save.PotStock.Count; i++)
                {
                    PotStock row = save.PotStock[i];
                    if (string.IsNullOrEmpty(row.TrapDefId) || row.Count <= 0 || Contains(into, row.TrapDefId)) continue;
                    into.Add(new NotebookGearHolding(row.TrapDefId, NotebookGearCount.Stock, row.Count,
                                                     PotLocker.DeployedCount(save, row.TrapDefId)));
                }
            }

            if (save.TubStock != null)
            {
                for (int i = 0; i < save.TubStock.Count; i++)
                {
                    TubStock row = save.TubStock[i];
                    if (string.IsNullOrEmpty(row.LonglineDefId) || row.Count <= 0 || Contains(into, row.LonglineDefId)) continue;
                    into.Add(new NotebookGearHolding(row.LonglineDefId, NotebookGearCount.Stock, row.Count,
                                                     TubLocker.DeployedCount(save, row.LonglineDefId)));
                }
            }

            if (save.BaitStock != null)
            {
                for (int i = 0; i < save.BaitStock.Count; i++)
                {
                    BaitStock row = save.BaitStock[i];
                    if (string.IsNullOrEmpty(row.BaitId) || row.Count <= 0 || Contains(into, row.BaitId)) continue;
                    into.Add(new NotebookGearHolding(row.BaitId, NotebookGearCount.Stock, row.Count));
                }
            }

            if (save.HullInstruments != null)
            {
                for (int i = 0; i < save.HullInstruments.Count; i++)
                {
                    string id = save.HullInstruments[i].InstrumentId;
                    if (string.IsNullOrEmpty(id)) continue;
                    int at = IndexOf(into, id);
                    if (at < 0) into.Add(new NotebookGearHolding(id, NotebookGearCount.Fitted, 1));
                    else into[at] = new NotebookGearHolding(id, NotebookGearCount.Fitted, into[at].Count + 1);
                }
            }
        }

        private static bool IsWatchId(string id, IReadOnlyList<WatchDef> watches)
        {
            if (id.StartsWith(WatchPrefix, System.StringComparison.Ordinal)) return true;
            if (watches == null) return false;
            for (int i = 0; i < watches.Count; i++)
                if (watches[i] != null && watches[i].LegacyGearId == id) return true;
            return false;
        }

        private static bool Contains(List<NotebookGearHolding> list, string id) => IndexOf(list, id) >= 0;

        private static int IndexOf(List<NotebookGearHolding> list, string id)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i].Id == id) return i;
            return -1;
        }
    }

    /// <summary>
    /// <b>THE GEAR PAGE</b> — pure, engine-free: holdings and the page Def in, blocks out.
    ///
    /// <para>One block per piece of gear: its picture (drawn by the presenter from the icon library, or
    /// a paper placeholder when the id has none), its name, and a count or state where one exists. Each
    /// group's heading rides on its first item's block, so a heading is never left alone at the foot of
    /// a leaf. A block is at least <see cref="NotebookLayout.IconRows"/> lines so the picture never
    /// overhangs the next one. Order is the Def's; gear no group lists follows under the Def's
    /// <see cref="NotebookGearPageDef.OtherHeading"/>, in the order he came by it.</para>
    /// </summary>
    public static class NotebookGearPage
    {
        /// <summary>Source-id prefix for a gear block, so the presenter's selection can name it.</summary>
        public const string SourcePrefix = "gear_page.";

        public const string StockFormat = "Have {0}";
        public const string StockSetFormat = "Have {0}, {1} set";
        public const string FittedOneFormat = "On {0} boat";
        public const string FittedManyFormat = "On {0} boats";
        public const string UnownedLine = "Not yet";
        public const string FallbackHeading = "Gear";

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public static List<NotebookBlock> Blocks(IReadOnlyList<NotebookGearHolding> held, NotebookGearPageDef page,
                                                 int cols)
        {
            var blocks = new List<NotebookBlock>();
            var placed = new HashSet<string>();

            if (page != null && page.Groups != null)
            {
                for (int g = 0; g < page.Groups.Length; g++)
                {
                    NotebookGearPageDef.Group group = page.Groups[g];
                    if (group.Entries == null) continue;
                    string heading = group.Heading;
                    bool headed = false;

                    for (int e = 0; e < group.Entries.Length; e++)
                    {
                        NotebookGearPageDef.Entry entry = group.Entries[e];
                        if (string.IsNullOrEmpty(entry.Id) || placed.Contains(entry.Id)) continue;

                        bool owned = TryFind(held, entry.Id, out NotebookGearHolding holding);
                        if (!owned && !page.ListUnowned) continue;

                        placed.Add(entry.Id);
                        string name = string.IsNullOrEmpty(entry.Name) ? NameFromId(entry.Id) : entry.Name;
                        blocks.Add(Item(headed ? null : heading, entry.Id, name,
                                        owned ? StateLine(holding) : UnownedLine, owned, cols));
                        headed = true;
                    }
                }
            }

            // Owned gear no group lists: never dropped, never blank.
            bool otherHeaded = false;
            string other = page != null && !string.IsNullOrEmpty(page.OtherHeading) ? page.OtherHeading : FallbackHeading;
            if (held != null)
            {
                for (int i = 0; i < held.Count; i++)
                {
                    NotebookGearHolding h = held[i];
                    if (string.IsNullOrEmpty(h.Id) || placed.Contains(h.Id)) continue;
                    placed.Add(h.Id);
                    blocks.Add(Item(otherHeaded ? null : other, h.Id, NameFromId(h.Id), StateLine(h), true, cols));
                    otherHeaded = true;
                }
            }

            return blocks;
        }

        /// <summary>The count or state line, or null when the gear is kept without a number.</summary>
        public static string StateLine(in NotebookGearHolding h)
        {
            switch (h.Kind)
            {
                case NotebookGearCount.Stock:
                    return h.InWater > 0
                        ? string.Format(Invariant, StockSetFormat, h.Count, h.InWater)
                        : string.Format(Invariant, StockFormat, h.Count);
                case NotebookGearCount.Fitted:
                    return string.Format(Invariant, h.Count == 1 ? FittedOneFormat : FittedManyFormat, h.Count);
                default:
                    return null;
            }
        }

        /// <summary>A readable name for an id no Def entry names: "gear.rod_rack" reads "Rod rack".</summary>
        public static string NameFromId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            int dot = id.IndexOf('.');
            string leaf = dot >= 0 && dot < id.Length - 1 ? id.Substring(dot + 1) : id;
            var sb = new StringBuilder(leaf.Length);
            for (int i = 0; i < leaf.Length; i++)
            {
                char c = leaf[i] == '_' ? ' ' : leaf[i];
                sb.Append(i == 0 ? char.ToUpperInvariant(c) : c);
            }
            return sb.ToString();
        }

        private static NotebookBlock Item(string heading, string id, string name, string state, bool owned, int cols)
        {
            var rows = new List<NotebookRow>();
            NotebookStepState ink = owned ? NotebookStepState.Todo : NotebookStepState.Done;   // Done letters faint

            if (!string.IsNullOrEmpty(heading))
                foreach (string line in NotebookLayout.Wrap(heading.ToUpperInvariant(), NotebookKit.TitleColsFor(cols)))
                    rows.Add(new NotebookRow(line, wide: true, hasBox: false, NotebookStepState.Todo, continuation: false));

            int textCols = NotebookLayout.IconTextColsFor(cols);
            int first = rows.Count;
            foreach (string line in NotebookLayout.Wrap(name, textCols))
                rows.Add(new NotebookRow(line, wide: true, hasBox: false, ink, rows.Count > first, id));

            if (!string.IsNullOrEmpty(state))
                foreach (string line in NotebookLayout.Wrap(state, textCols))
                    rows.Add(new NotebookRow(line, wide: true, hasBox: false, ink, continuation: true, id));

            while (rows.Count - first < NotebookLayout.IconRows)
                rows.Add(new NotebookRow("", wide: true, hasBox: false, ink, continuation: true, id));

            return new NotebookBlock(NotebookBlockKind.Section, rows, SourcePrefix + id);
        }

        private static bool TryFind(IReadOnlyList<NotebookGearHolding> held, string id, out NotebookGearHolding found)
        {
            if (held != null)
                for (int i = 0; i < held.Count; i++)
                    if (held[i].Id == id) { found = held[i]; return true; }
            found = default;
            return false;
        }
    }
}
