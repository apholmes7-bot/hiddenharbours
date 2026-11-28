using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>THE PLAYER'S CAMERA</b> (owner 2026-11-26: "build the camera now with photo record added to save").
    /// Once he has it (its def's owned flag), P (the pad's Y) lifts it on foot or on a deck, the move keys
    /// slide the frame, Space (the right trigger) takes the picture, and P again lowers it. At the helm and
    /// behind a wheel it does not answer: P there is the autopilot's.
    ///
    /// <para><b>Presentation until the shutter.</b> Up, it holds the move axis (<see cref="MoveActionClaim"/>)
    /// so he stands still and aims, and it tells the viewfinder (<see cref="PhotoViewfinder"/>) where the
    /// frame is. Only the shutter writes anything: one album row (save v28) and a <see cref="PhotoTaken"/>
    /// on the bus. Nothing is saved to disk here; the row goes with the next save, like every other row.</para>
    ///
    /// <para><b>Both hands.</b> It will not come up while he holds something, which also keeps the rod's
    /// cast off the shutter's Space (an owner question: see the ADR).</para>
    ///
    /// <para><b>Installs itself</b> after the first scene loads (the <c>InteractPopup</c> pattern), so no
    /// scene needs re-baking. One per session.</para>
    /// </summary>
    public sealed class PlayerPhotoCamera : MonoBehaviour
    {
        private static PlayerPhotoCamera _instance;

        private PhotoCameraModel _model;
        private IControlIntentSource<CameraIntents> _source;
        private bool _claimedMove;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Install()
        {
            if (_instance != null) return;
            PhotoCameraDef def = PhotoCameraDef.Load();
            if (def == null) return;   // a build with no camera: nothing to install
            var go = new GameObject("PlayerPhotoCamera");
            DontDestroyOnLoad(go);
            go.AddComponent<PlayerPhotoCamera>().Configure(def, null);
        }

        /// <summary>The installed camera, or null.</summary>
        public static PlayerPhotoCamera Instance => _instance;

        /// <summary>The camera's logic (what a test reads).</summary>
        public PhotoCameraModel Model => _model;

        /// <summary>Use <paramref name="def"/>, read <paramref name="source"/> (null: the bindings asset).</summary>
        public void Configure(PhotoCameraDef def, IControlIntentSource<CameraIntents> source)
        {
            _model = new PhotoCameraModel(def);
            _source = source;
        }

        /// <summary>Does he have the camera? Its def's flag, set by whoever gives it to him.</summary>
        public static bool Owned(PhotoCameraDef def, ISaveService save)
        {
            if (def == null) return false;
            if (string.IsNullOrEmpty(def.OwnedWhileFlag)) return true;
            return save != null && save.GetFlag(def.OwnedWhileFlag);
        }

        /// <summary>Is there anything in his hands?</summary>
        public static bool HandsFull(ICarrier hands)
        {
            CarriedItem.Both(hands, out ICarriable first, out ICarriable second);
            return first != null || second != null;
        }

        /// <summary>Can the camera answer in this place? On foot and on a deck only.</summary>
        public static bool Answers(InteractContext context)
            => context == InteractContext.OnFoot || context == InteractContext.OnDeck;

        private void Awake()
        {
            if (_instance == null) _instance = this;
        }

        private void OnDisable() => LowerNow();

        private void OnDestroy()
        {
            LowerNow();
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_model == null || _model.Def == null) return;
            PhotoCameraDef def = _model.Def;

            bool present = InteractActorProbe.Has;
            InteractActor actor = present ? InteractActorProbe.Current : default;
            if (!present || !Answers(actor.Context) || InteractionGate.IsBlocked)
            {
                LowerNow();
                return;
            }
            if (actor.Context == InteractContext.OnFoot) _model.NoteFacing(actor.Facing);

            IControlIntentSource<CameraIntents> source = _source ?? CameraInput.Device;
            CameraIntents intents = source != null ? source.Read() : CameraIntents.None;
            float now = Time.unscaledTime;

            if (!_model.IsUp)
            {
                if (!intents.Raise) return;
                if (!Owned(def, GameServices.Save)) return;   // before she gives it, P here is nothing
                if (HandsFull(GameServices.Hands))
                {
                    PhotoViewfinder.Say(def.HandsFullLine, now + def.ResultShowSeconds);
                    return;
                }
                _model.Raise();
                if (!MoveActionClaim.IsClaimed) { MoveActionClaim.IsClaimed = true; _claimedMove = true; }
                Show(def, actor.Position);
                return;
            }

            if (intents.Raise || HandsFull(GameServices.Hands))
            {
                LowerNow();
                return;
            }

            _model.Steer(intents.Aim, Time.unscaledDeltaTime);
            Show(def, actor.Position);

            if (!intents.Shutter) return;
            SaveData save = GameServices.Save?.Current;
            string key = actor.PlayerKey;
            double gameSeconds = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0d;
            PhotoShotOutcome outcome = _model.Shoot(save, key, actor.Position, gameSeconds,
                                                    GameServices.CurrentRegionId, now);
            if (outcome == PhotoShotOutcome.Cooling) return;

            if (outcome == PhotoShotOutcome.Kept && _model.LastKept != null)
            {
                // The album's little Polaroid: rendered before the viewfinder flash is drawn, once per shutter.
                string thumbnail = PhotoThumbnailCapture.Take(def, _model.FrameFor(actor.Position));
                PhotoAlbumRows.SetThumbnail(save, _model.LastKept.Number, thumbnail);
            }
            PhotoViewfinder.Shot(_model.LineFor(outcome), now + def.FlashSeconds, now + def.ResultShowSeconds);
            if (outcome == PhotoShotOutcome.Kept && _model.LastKept != null)
                EventBus.Publish(new PhotoTaken(_model.LastKept.PlayerKey, _model.LastKept.Number));
        }

        private void Show(PhotoCameraDef def, Vector2 standingAt)
        {
            string hint = ActiveControlDevice.Current == ControlDevice.Gamepad ? def.GamepadHint : def.KeyboardHint;
            PhotoViewfinder.Show(_model.FrameFor(standingAt), hint);
        }

        /// <summary>Lower the camera and give the move axis back (only if it took it).</summary>
        private void LowerNow()
        {
            // Only the frame this camera raised is taken down: an idle camera touches nothing.
            if (_model != null && _model.IsUp)
            {
                _model.Lower();
                PhotoViewfinder.Hide();
            }
            if (_claimedMove)
            {
                MoveActionClaim.IsClaimed = false;
                _claimedMove = false;
            }
        }
    }
}
