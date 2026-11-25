using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A store's planned goods and their pictures</b> (store sections, 2026-11-25: every item the store sells
    /// gets its own picture). One entry per good the store plans to stock but does not shelve yet: its id (the
    /// catalog listing id where the good is already authored, or the planned id named in
    /// <c>docs/design/st-peters-store-sections.md</c>) and the picture its slot will show. Nothing reads it at
    /// runtime; it is the art ready to bind, so the content test can hold every planned good to a picture of its
    /// own before its slot exists. When a good is shelved, its slot's <see cref="ShopShelfSlot.Goods"/> takes
    /// the picture and its entry leaves this list.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Store Item Pictures", fileName = "StoreItemPictures")]
    public class StoreItemPicturesDef : ScriptableObject
    {
        [Tooltip("Stable id, storepictures.<shop>.")]
        public string Id;

        [Tooltip("The shop these goods are planned for (the ShopShelfSlotsDef's ShopId).")]
        public string ShopId;

        public List<StoreItemPicture> Items = new List<StoreItemPicture>();
    }

    [Serializable]
    public class StoreItemPicture
    {
        [Tooltip("The good's catalog listing id where it exists (gear.shovel), else its planned id.")]
        public string ItemId;

        [Tooltip("Its own picture, drawn for this good alone.")]
        public Sprite Picture;

        [Tooltip("Where it is planned to stand, and what binds it.")]
        public string Note;
    }
}
