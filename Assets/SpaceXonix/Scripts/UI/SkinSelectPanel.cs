using System;
using SpaceXonix.Presentation;
using SpaceXonix.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SpaceXonix.UI
{
    /// <summary>
    /// The hangar, shown after the difficulty is picked: one ship at a time, big, with its name,
    /// perk and drawback, browsed left and right as an endless carousel by the arrows, a swipe or
    /// the keyboard. The ship on show is the one equipped and saved; Launch starts the run in it.
    /// A grid of seven small tiles was tried first and was unreadable on both a phone and a PC.
    /// </summary>
    public sealed class SkinSelectPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private ShipSkinLibrary library;

        [Header("Card")]
        [SerializeField] private RectTransform card;
        [SerializeField] private CanvasGroup cardGroup;
        [SerializeField] private UiSpriteAnimator preview;
        [SerializeField] private Text nameLabel;
        [SerializeField] private Text perkLabel;
        [SerializeField] private Text drawbackLabel;
        [SerializeField] private Text counterLabel;

        [Header("Browsing")]
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private HorizontalSwipe swipe;
        [SerializeField] private Image[] dots = new Image[0];
        [SerializeField] private Sprite dotOn;
        [SerializeField] private Sprite dotOff;

        [Header("Dressing")]
        [Tooltip("The neighbouring ships, shown dimly either side so the carousel reads as one.")]
        [SerializeField] private UiSpriteAnimator previousPeek;
        [SerializeField] private UiSpriteAnimator nextPeek;
        [Tooltip("The ship on show, which floats gently above its pedestal.")]
        [SerializeField] private RectTransform floatingShip;
        [SerializeField] private Image glow;
        [SerializeField] private RectTransform previousArrow;
        [SerializeField] private RectTransform nextArrow;
        [Tooltip("The primary button, which pulses so the next step is obvious.")]
        [SerializeField] private RectTransform launchPulse;

        [Header("Leaving")]
        [SerializeField] private Button launchButton;
        [SerializeField] private Button backButton;

        [Header("Motion")]
        [SerializeField, Min(.01f)] private float slideSeconds = .18f;
        [SerializeField] private float slideDistance = 260f;

        private GameSettingsModel settings;
        private Action launch;
        private Vector2 cardRest;
        private bool cardRestCaptured;
        private float slideTime = float.MaxValue;
        private int slideFrom;
        private Vector2 shipRest, previousArrowRest, nextArrowRest;
        private bool dressingCaptured;
        private float idleTime;

        public bool IsShown => root != null && root.activeSelf;
        public int Count => library != null && library.skins != null ? library.skins.Length : 0;
        public int CurrentIndex { get; private set; }
        public ShipSkinDefinition Current => SkinAt(CurrentIndex);
        public string EquippedId => library != null ? library.Find(settings?.ShipSkin)?.id : null;

        private void Awake()
        {
            if (launchButton != null) launchButton.onClick.AddListener(Launch);
            if (backButton != null) backButton.onClick.AddListener(Hide);
            if (previousButton != null) previousButton.onClick.AddListener(Previous);
            if (nextButton != null) nextButton.onClick.AddListener(Next);
            if (swipe != null) swipe.Swiped += OnSwiped;
        }

        private void OnDestroy()
        {
            if (launchButton != null) launchButton.onClick.RemoveListener(Launch);
            if (backButton != null) backButton.onClick.RemoveListener(Hide);
            if (previousButton != null) previousButton.onClick.RemoveListener(Previous);
            if (nextButton != null) nextButton.onClick.RemoveListener(Next);
            if (swipe != null) swipe.Swiped -= OnSwiped;
        }

        /// <summary>Opens the hangar on the equipped ship. The settings model is passed in so tests need no live service.</summary>
        public void Show(GameSettingsModel model = null)
        {
            settings = model ?? GameSettings.Current;
            CurrentIndex = Mathf.Max(0, IndexOf(library != null ? library.Find(settings?.ShipSkin) : null));
            Display(0);
            if (root != null) root.SetActive(true);
        }

        /// <summary>Opens the hangar as the last step before a run; Launch then calls <paramref name="onLaunch"/>.</summary>
        public void OpenForLaunch(Action onLaunch, GameSettingsModel model = null)
        {
            launch = onLaunch;
            Show(model);
        }

        /// <summary>Starts the run in the ship on show. Public so tests can launch without a click.</summary>
        public void Launch() => launch?.Invoke();

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        /// <summary>The next ship, wrapping from the last back to the first.</summary>
        public void Next() => Step(1);

        /// <summary>The previous ship, wrapping from the first round to the last.</summary>
        public void Previous() => Step(-1);

        /// <summary>Shows and equips the ship at a slot. Public so tests can pick one directly.</summary>
        public void Equip(int index)
        {
            if (Count == 0) return;
            CurrentIndex = ((index % Count) + Count) % Count;
            Display(0);
        }

        private void Step(int direction)
        {
            if (Count == 0) return;
            CurrentIndex = (CurrentIndex + direction + Count) % Count;
            Display(direction);
        }

        // Dragging the card left brings the next ship in from the right, like turning a page.
        private void OnSwiped(int direction) => Step(-direction);

        private void Update()
        {
            if (!IsShown) return;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) Previous();
                if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) Next();
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame) Launch();
                if (keyboard.escapeKey.wasPressedThisFrame) Hide();
            }
            Animate(Time.unscaledDeltaTime);
            AnimateIdle(Time.unscaledDeltaTime);
        }

        /// <summary>The idle motion: the ship floats, its glow breathes, the arrows nudge outward, Launch pulses.</summary>
        public void AnimateIdle(float deltaTime)
        {
            CaptureDressing();
            idleTime += Mathf.Max(0f, deltaTime);
            var wave = Mathf.Sin(idleTime * Mathf.PI * 2f * .5f);
            if (floatingShip != null) floatingShip.anchoredPosition = shipRest + new Vector2(0f, wave * 10f);
            if (glow != null) { var c = glow.color; c.a = Mathf.Lerp(.08f, .16f, .5f + .5f * wave); glow.color = c; }
            var nudge = Mathf.Max(0f, Mathf.Sin(idleTime * Mathf.PI * 2f * .8f)) * 8f;
            if (previousArrow != null) previousArrow.anchoredPosition = previousArrowRest + new Vector2(-nudge, 0f);
            if (nextArrow != null) nextArrow.anchoredPosition = nextArrowRest + new Vector2(nudge, 0f);
            if (launchPulse != null) launchPulse.localScale = Vector3.one * (1f + .035f * (.5f + .5f * Mathf.Sin(idleTime * Mathf.PI * 2f * .9f)));
        }

        private void CaptureDressing()
        {
            if (dressingCaptured) return;
            if (floatingShip != null) shipRest = floatingShip.anchoredPosition;
            if (previousArrow != null) previousArrowRest = previousArrow.anchoredPosition;
            if (nextArrow != null) nextArrowRest = nextArrow.anchoredPosition;
            dressingCaptured = true;
        }

        /// <summary>Advances the slide between ships. Public so tests can finish it.</summary>
        public void Animate(float deltaTime)
        {
            if (card == null || slideTime >= slideSeconds) return;
            slideTime = Mathf.Min(slideSeconds, slideTime + Mathf.Max(0f, deltaTime));
            var t = slideTime / slideSeconds;
            var eased = 1f - (1f - t) * (1f - t);
            card.anchoredPosition = cardRest + new Vector2(slideFrom * slideDistance * (1f - eased), 0f);
            if (cardGroup != null) cardGroup.alpha = Mathf.Lerp(.2f, 1f, eased);
        }

        /// <summary>Fills the card with the current ship, equips it, and slides it in from <paramref name="direction"/>.</summary>
        private void Display(int direction)
        {
            var skin = Current;
            if (skin == null) return;
            if (settings != null) settings.ShipSkin = skin.id;
            if (preview != null) preview.SetFrames(skin.frames, skin.framesPerSecond);
            if (nameLabel != null) nameLabel.text = skin.displayName.ToUpperInvariant();
            var stats = skin.stats ?? ShipStats.Neutral;
            if (perkLabel != null) perkLabel.text = stats.perk;
            if (drawbackLabel != null) drawbackLabel.text = stats.drawback;
            if (counterLabel != null) counterLabel.text = $"{CurrentIndex + 1} / {Count}";
            var before = SkinAt((CurrentIndex - 1 + Count) % Count);
            var after = SkinAt((CurrentIndex + 1) % Count);
            if (previousPeek != null && before != null) previousPeek.SetFrames(before.frames, before.framesPerSecond);
            if (nextPeek != null && after != null) nextPeek.SetFrames(after.frames, after.framesPerSecond);
            for (var i = 0; i < dots.Length; i++)
            {
                if (dots[i] == null) continue;
                dots[i].gameObject.SetActive(i < Count);
                dots[i].sprite = i == CurrentIndex ? dotOn : dotOff;
            }
            if (card == null) return;
            if (!cardRestCaptured) { cardRest = card.anchoredPosition; cardRestCaptured = true; }
            slideFrom = direction;
            slideTime = direction == 0 ? slideSeconds : 0f;
            card.anchoredPosition = cardRest + new Vector2(direction * slideDistance, 0f);
            if (cardGroup != null) cardGroup.alpha = direction == 0 ? 1f : .2f;
            if (direction == 0) card.anchoredPosition = cardRest;
        }

        private int IndexOf(ShipSkinDefinition skin)
        {
            if (skin == null || library?.skins == null) return 0;
            return Array.IndexOf(library.skins, skin);
        }

        private ShipSkinDefinition SkinAt(int index) =>
            library != null && library.skins != null && index >= 0 && index < library.skins.Length ? library.skins[index] : null;
    }
}
