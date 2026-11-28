using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>Where one photo request stands, read from flags and the album.</summary>
    public enum PhotoRequestStage
    {
        /// <summary>Not hers, or not yet hers to ask (its <see cref="PhotoRequestDef.RequiresFlag"/>).</summary>
        None = 0,

        /// <summary>She has not asked: she asks now.</summary>
        Ask = 1,

        /// <summary>She has asked; nothing in his album will do yet.</summary>
        Waiting = 2,

        /// <summary>His album holds a picture that will do: she thanks him now.</summary>
        Thank = 3,

        /// <summary>She has thanked him.</summary>
        Done = 4,
    }

    /// <summary>
    /// <b>What a photo request adds to a conversation</b> (the <see cref="SupplyRunTalk"/> shape):
    /// <see cref="WorldInteractor"/> asks this as a talk opens and gets the lines to append and what to do
    /// when the talk is heard to its end. Outside a request it hands back exactly what went in.
    ///
    /// <para><b>Heard to its end.</b> Asking sets the asked flag (which may hand him the camera); thanking
    /// sets the done flag and pays the reward once. Walking away mid-sentence leaves both for next time.</para>
    /// </summary>
    public static class PhotoRequestTalk
    {
        /// <summary>Where the requests come from. Null (the default) loads them from Resources; a test hands
        /// in its own.</summary>
        public static Func<IReadOnlyList<PhotoRequestDef>> RequestsSource { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => RequestsSource = null;

        /// <summary>Every shipped request (or the test's).</summary>
        public static IReadOnlyList<PhotoRequestDef> Requests()
            => RequestsSource != null ? RequestsSource() : Resources.LoadAll<PhotoRequestDef>(PhotoRequestDef.ResourcesFolder);

        /// <summary>Does <paramref name="save"/>'s album (the local player's) hold a picture that will do?</summary>
        public static bool AlbumHasOne(PhotoRequestDef req, SaveData save)
            => req != null && PhotoAlbumRows.HasQualifying(save, SavePlayerKeys.Local, req.SubjectId, req.MinCount,
                                                           req.WorstPose, req.MinScore);

        /// <summary>Where <paramref name="req"/> stands.</summary>
        public static PhotoRequestStage StageOf(PhotoRequestDef req, SaveData save, IFlagStore flags)
        {
            if (req == null || flags == null) return PhotoRequestStage.None;
            if (flags.Get(req.DoneFlag)) return PhotoRequestStage.Done;
            if (!flags.Get(req.AskedFlag))
                return string.IsNullOrEmpty(req.RequiresFlag) || flags.Get(req.RequiresFlag)
                    ? PhotoRequestStage.Ask : PhotoRequestStage.None;
            return flags.Get(req.PicturedFlag) || AlbumHasOne(req, save)
                ? PhotoRequestStage.Thank : PhotoRequestStage.Waiting;
        }

        /// <summary>
        /// Mark every asked request whose picture the album now holds as pictured (what the camera's
        /// <see cref="PhotoTaken"/> sets off, and a talk re-checks). Returns how many it marked.
        /// </summary>
        public static int MarkPictured(SaveData save, IFlagStore flags)
        {
            if (flags == null) return 0;
            IReadOnlyList<PhotoRequestDef> reqs = Requests();
            if (reqs == null) return 0;
            int marked = 0;
            for (int i = 0; i < reqs.Count; i++)
            {
                PhotoRequestDef r = reqs[i];
                if (r == null || string.IsNullOrEmpty(r.PicturedFlag) || flags.Get(r.PicturedFlag)) continue;
                if (!flags.Get(r.AskedFlag) || !AlbumHasOne(r, save)) continue;
                flags.Set(r.PicturedFlag, true);
                marked++;
            }
            return marked;
        }

        /// <summary>
        /// <paramref name="lines"/> with <paramref name="npcId"/>'s request lines appended, and in
        /// <paramref name="whenHeard"/> what to do once the talk is heard to its end (null: nothing).
        /// </summary>
        public static string[] WithLines(string npcId, string[] lines, ISaveService save, IFlagStore flags,
                                         out Action whenHeard)
        {
            whenHeard = null;
            if (string.IsNullOrEmpty(npcId) || flags == null) return lines;
            IReadOnlyList<PhotoRequestDef> reqs = Requests();
            if (reqs == null) return lines;
            SaveData data = save?.Current;

            for (int i = 0; i < reqs.Count; i++)
            {
                PhotoRequestDef r = reqs[i];
                if (r == null || r.GiverNpcId != npcId) continue;
                switch (StageOf(r, data, flags))
                {
                    case PhotoRequestStage.Ask:
                        whenHeard = () => flags.Set(r.AskedFlag, true);
                        return Append(lines, r.AskLines);
                    case PhotoRequestStage.Waiting:
                        return Append(lines, r.ReminderLines);
                    case PhotoRequestStage.Thank:
                        whenHeard = () => Thank(r, flags, GameServices.Wallet);
                        return Append(lines, r.ThanksLines);
                    case PhotoRequestStage.Done:
                        return r.AfterLines != null && r.AfterLines.Length > 0 ? Append(lines, r.AfterLines) : lines;
                }
            }
            return lines;
        }

        /// <summary>Her thanks, heard to its end: pictured and done are set, and the reward paid once.</summary>
        public static void Thank(PhotoRequestDef req, IFlagStore flags, IWallet wallet)
        {
            if (req == null || flags == null || flags.Get(req.DoneFlag)) return;
            flags.Set(req.PicturedFlag, true);
            flags.Set(req.DoneFlag, true);
            if (req.RewardMoney > 0) wallet?.Add(req.RewardMoney);
        }

        private static string[] Append(string[] lines, string[] extra)
        {
            if (extra == null || extra.Length == 0) return lines;
            int n = lines?.Length ?? 0;
            var all = new string[n + extra.Length];
            if (n > 0) Array.Copy(lines, all, n);
            Array.Copy(extra, 0, all, n, extra.Length);
            return all;
        }
    }

    /// <summary>
    /// Listens for the camera's <see cref="PhotoTaken"/> and marks a request pictured the moment his album
    /// holds what she asked for, so the notebook's step ticks without a talk. Installs itself; the talk
    /// checks the album again, so a missed message costs nothing.
    /// </summary>
    public sealed class PhotoRequestWatcher : MonoBehaviour
    {
        private static PhotoRequestWatcher _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Install()
        {
            if (_instance != null) return;
            var go = new GameObject("PhotoRequestWatcher");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<PhotoRequestWatcher>();
        }

        private void OnEnable() => EventBus.Subscribe<PhotoTaken>(OnTaken);
        private void OnDisable() => EventBus.Unsubscribe<PhotoTaken>(OnTaken);

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private static void OnTaken(PhotoTaken e)
            => PhotoRequestTalk.MarkPictured(GameServices.Save?.Current, NotebookContentSource.Flags());
    }
}
