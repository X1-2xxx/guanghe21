using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace GamePresentation
{
    /// <summary>只操作预制体里已经存在的控件，所有布局、贴图与字体可由美术直接编辑。</summary>
    public sealed class StoryPanelView : MonoBehaviour
    {
        public CanvasGroup group;
        public CanvasGroup blackGroup;
        public GameObject blackPanel, dialoguePanel, choicesPanel, narratorPanel;
        [InspectorName("底部对白大框")] public GameObject dialogueFrame;
        public Text blackText, speakerText, dialogueText, narratorText;
        [Header("天空字幕与装备提示（均由预制体提供）")]
        public GameObject skySubtitlePanel, equipmentNotice;
        public Text skySubtitleText;
        [SerializeField, InspectorName("获得提示文字")] private Text equipmentNoticeText;
        [SerializeField, InspectorName("获得提示图标")] private Image equipmentNoticeIcon;
        [SerializeField, InspectorName("硬纸板获得图标")] private Sprite cardboardNoticeSprite;
        [SerializeField, InspectorName("跳跃键获得图标")] private Sprite jumpKeyNoticeSprite;
        [SerializeField, InspectorName("驽骍难得获得图标")] private Sprite rocinanteNoticeSprite;
        [SerializeField, Min(1f), InspectorName("装备提示显示秒数")] private float equipmentNoticeSeconds = 4f;
        public Image portrait, endingImage;
        [Header("结尾黑幕与字幕")]
        public Text endingTitleText;
        [SerializeField, Min(0f)] private float endingFadeSeconds = 1f;
        [SerializeField, Min(0f)] private float endingTitleFadeSeconds = .8f;
        private bool endingActive;
        private float endingElapsed;
        // Ignore the key that advanced the preceding scene and wait for the title to finish fading.
        public bool EndingReadyForRestart => endingActive &&
            endingElapsed >= Mathf.Max(0f, endingFadeSeconds) + Mathf.Max(0f, endingTitleFadeSeconds) + .25f;
        [InspectorName("双人立绘显示与亮度")] public StoryPortraitStage portraitStage;
        public Button continueButton, blackContinueButton, narratorContinueButton;
        public Button choiceA, choiceB;
        [InspectorName("点击纸张对话框继续")] public Button dialogueAdvanceButton;
        [InspectorName("A 选项前缀")] public string choiceAPrefix = "A. ";
        [InspectorName("B 选项前缀")] public string choiceBPrefix = "B. ";
        public Text choiceAText, choiceBText, resetHint;
        [SerializeField, Min(1f), InspectorName("字幕每秒字数")] private float subtitleCharactersPerSecond = 32f;
        [SerializeField, Min(0f), InspectorName("对话框滑入秒数")] private float panelSlideSeconds = .32f;
        [SerializeField, Min(0f), InspectorName("对话框滑入距离（画布像素）")] private float panelSlideDistance = 220f;
        private readonly StoryPanelTransition entrance = new StoryPanelTransition();
        [SerializeField, InspectorName("每组对白首次出场动画")] private StoryDialogueEntrance dialogueEntrance;
        private bool dialogueEntrancePlayed;
        public bool IsEntering => entrance.IsEntering || (dialogueEntrance && dialogueEntrance.IsEntering);
        internal void TickEntrance(float delta) { entrance.Tick(delta); if (dialogueEntrance) dialogueEntrance.Tick(delta); }
        internal void CompleteEntrance() { entrance.Complete(); if (dialogueEntrance) dialogueEntrance.Complete(); }
        private void OnDisable() { entrance.Reset(); if (dialogueEntrance) dialogueEntrance.Complete(); }
        private void BeginEntrance(GameObject panel, Vector2 offset) => entrance.Begin(panel, panelSlideSeconds, offset);
        private Action onContinue;
        private Action<int> onChoice;
        private string lastLine, lastSpeaker;
        private Sprite lastPortrait;
        private Text pagedText;
        private List<string> textPages;
        private int pageIndex;
        private StoryChoiceLayout choiceLayout;
        private StoryTextReveal pageReveal;
        private int[] subtitleCharacters = Array.Empty<int>();
        private float revealedCharacters;
        private float equipmentRemaining;

        public int CurrentPageLength => pagedText && pagedText.text != null ? pagedText.text.Length : 0;
        public bool IsPageRevealed => !pageReveal || revealedCharacters >= subtitleCharacters.Length;
        public bool HeroPortraitVisible => portraitStage ? portraitStage.HeroVisible : portrait && portrait.gameObject.activeInHierarchy;

        public void Bind(Action advance, Action<int> choose)
        {
            onContinue = advance; onChoice = choose;
            continueButton.onClick.AddListener(Continue);
            blackContinueButton.onClick.AddListener(Continue);
            if (dialogueAdvanceButton) dialogueAdvanceButton.onClick.AddListener(Continue);
            choiceA.onClick.AddListener(() => onChoice?.Invoke(0));
            choiceB.onClick.AddListener(() => onChoice?.Invoke(1));
            UIFontUtility.ApplyToHierarchy(gameObject);
            Hide(); ClearEquipmentNotice(); SetResetHint(false);
        }
        public void RememberDialogue(string text, string speaker, Sprite image)
        { lastLine = text; lastSpeaker = speaker; lastPortrait = image; }
        public void ResetDialogueMemory() { lastLine = lastSpeaker = null; lastPortrait = null; }
        public void BeginEpisode(bool twoCharacters, Sprite hero, Sprite innkeeper)
        {
            ResetDialogueMemory(); dialogueEntrancePlayed = false;
            if (dialogueEntrance) dialogueEntrance.Complete();
            if (portraitStage) portraitStage.BeginEpisode(twoCharacters, hero, innkeeper);
        }
        private void Continue() => onContinue?.Invoke();
        private void LateUpdate()
        {
            choiceLayout?.RefreshIfNeeded();
            TickEnding();
            if (equipmentRemaining <= 0f || group.alpha <= 0f) return;
            equipmentRemaining -= Time.unscaledDeltaTime;
            if (equipmentRemaining <= 0f) ClearEquipmentNotice();
        }
        public void Hide() => Hide(false);
        private void Hide(bool keepEntrance)
        {
            endingActive = false;
            if (endingTitleText) endingTitleText.gameObject.SetActive(false);
            if (!keepEntrance) { entrance.Reset(); if (dialogueEntrance) dialogueEntrance.Complete(); }
            blackPanel.SetActive(false); dialoguePanel.SetActive(false);
            choicesPanel.SetActive(false); narratorPanel.SetActive(false);
            if (skySubtitlePanel) skySubtitlePanel.SetActive(false);
            if (portraitStage) portraitStage.Hide();
            if (endingImage) endingImage.gameObject.SetActive(false);
            group.blocksRaycasts = false;
            pagedText = null;
            textPages = null;
            pageIndex = 0;
            StopSubtitle();
        }
        public void Show(StoryStep step, Sprite actorPortrait)
        {
            bool keepDialogue = dialoguePanel.activeSelf && (step.kind == StoryStepKind.Dialogue || step.kind == StoryStepKind.Choice) &&
                (step.kind == StoryStepKind.Choice || !dialogueFrame || dialogueFrame.activeSelf);
            Hide(keepDialogue); group.alpha = 1f; group.blocksRaycasts = true;
            if (step.kind == StoryStepKind.BlackTitle)
            {
                blackGroup.alpha = 1f;
                blackContinueButton.gameObject.SetActive(!step.autoAdvance);
                blackPanel.SetActive(true);
                SetTextPages(blackText, step.text);
                return;
            }
            if (step.kind == StoryStepKind.Narration)
            {
                if (narratorContinueButton) narratorContinueButton.gameObject.SetActive(false);
                narratorPanel.SetActive(true);
                SetTextPages(narratorText, step.text);
                BeginEntrance(narratorPanel, Vector2.up * panelSlideDistance * .5f);
                return;
            }
            if (step.kind == StoryStepKind.SkySubtitle)
            {
                skySubtitlePanel.SetActive(true);
                SetTextPages(skySubtitleText, step.text);
                BeginEntrance(skySubtitlePanel, Vector2.zero);
                return;
            }
            dialoguePanel.SetActive(true);
            bool choices = step.kind == StoryStepKind.Choice;
            // 选项放到右侧后，底部保留角色上一句台词，避免对话框突然变空。
            bool retain = choices && !step.hidePreviousDialogue && string.IsNullOrWhiteSpace(step.text) && !string.IsNullOrEmpty(lastLine);
            bool showDialogue = retain || !string.IsNullOrWhiteSpace(step.text);
            dialoguePanel.SetActive(showDialogue || choices);
            if (dialogueFrame) dialogueFrame.SetActive(showDialogue);
            speakerText.text = retain ? lastSpeaker : step.speaker;
            Sprite image = !showDialogue ? null : (retain ? lastPortrait : actorPortrait);
            if (portraitStage) portraitStage.Show(PresentationLibrary.IsInnkeeper(retain ? lastSpeaker : step.speaker), image, choices, showDialogue);
            else { portrait.sprite = image; portrait.gameObject.SetActive(image != null); }
            if (choices)
            {
                dialogueText.horizontalOverflow = HorizontalWrapMode.Overflow;
                dialogueText.text = retain ? lastLine : LayoutFor(dialogueText).Wrap(step.text);
            }
            else
            {
                SetTextPages(dialogueText, step.text);
                RememberDialogue(dialogueText.text, step.speaker, image);
            }
            choicesPanel.SetActive(choices); continueButton.gameObject.SetActive(!choices && showDialogue);
            if (dialogueAdvanceButton) dialogueAdvanceButton.interactable = !choices;
            if (choices)
            {
                choiceA.gameObject.SetActive(step.choices.Count > 0); choiceB.gameObject.SetActive(step.choices.Count > 1);
                choiceLayout ??= new StoryChoiceLayout((RectTransform)choicesPanel.transform, choiceAText, choiceBText,
                    (RectTransform)choiceA.transform, (RectTransform)choiceB.transform);
                choiceLayout.Apply(step.choices.Count > 0 ? choiceAPrefix + step.choices[0].text : string.Empty,
                    step.choices.Count > 1 ? choiceBPrefix + step.choices[1].text : string.Empty);
            }
            if (!dialogueEntrancePlayed && showDialogue)
            {
                dialogueEntrancePlayed = true;
                if (dialogueEntrance && dialogueEntrance.IsConfigured) dialogueEntrance.Begin();
                else BeginEntrance(dialoguePanel, Vector2.down * panelSlideDistance);
            }
        }

        public bool TryAdvanceTextPage()
        {
            if (!pagedText || textPages == null || pageIndex + 1 >= textPages.Count) return false;
            ShowPage(textPages[++pageIndex]);
            if (pagedText == dialogueText) lastLine = pagedText.text;
            return true;
        }

        internal void TickSubtitle(float delta)
        {
            if (IsPageRevealed) return;
            int before = Mathf.Min(Mathf.FloorToInt(revealedCharacters), subtitleCharacters.Length);
            revealedCharacters += delta * Mathf.Max(1f, subtitleCharactersPerSecond);
            int count = Mathf.FloorToInt(revealedCharacters);
            int after = Mathf.Min(count, subtitleCharacters.Length);
            if (after > before && pagedText)
                AudioManager.TypeCharacters(pagedText.text, subtitleCharacters[before],
                    after == subtitleCharacters.Length ? pagedText.text.Length : subtitleCharacters[after], false);
            pageReveal.SetVisibleCharacters(count >= subtitleCharacters.Length ? int.MaxValue : subtitleCharacters[count]);
        }

        internal bool CompleteSubtitle()
        {
            if (IsPageRevealed) return false;
            revealedCharacters = subtitleCharacters.Length;
            pageReveal.SetVisibleCharacters(int.MaxValue);
            return true;
        }

        private void StopSubtitle()
        {
            if (pageReveal) pageReveal.SetVisibleCharacters(int.MaxValue);
            pageReveal = null;
        }

        private void ShowPage(string text)
        {
            pagedText.text = text;
            pageReveal = pagedText.GetComponent<StoryTextReveal>();
            if (!pageReveal) return;
            subtitleCharacters = StringInfo.ParseCombiningCharacters(text);
            revealedCharacters = 0f;
            pageReveal.SetVisibleCharacters(0);
        }

        private void SetTextPages(Text target, string text)
        {
            pagedText = target;
            target.horizontalOverflow = HorizontalWrapMode.Overflow;
            textPages = LayoutFor(target).Paginate(text, Mathf.Max(1f, target.rectTransform.rect.height - 4f), 2);
            pageIndex = 0;
            ShowPage(textPages.Count > 0 ? textPages[0] : string.Empty);
        }

        private static StoryTextLayout LayoutFor(Text label)
        {
            var spacing = label.GetComponent<StoryDialogueLetterSpacing>();
            int fontSize = label.resizeTextForBestFit ? label.resizeTextMaxSize : label.fontSize;
            return new StoryTextLayout(label, fontSize, spacing ? spacing.Spacing : 0f);
        }
        public void SetPaused(bool paused) { group.alpha = paused ? 0f : 1f; group.blocksRaycasts = !paused && (blackPanel.activeSelf || dialoguePanel.activeSelf || narratorPanel.activeSelf || (skySubtitlePanel && skySubtitlePanel.activeSelf)); }
        /// <summary>装备提示与主线纸框独立，不暂停或拦截操作，内容和图标直接在预制体中修改。</summary>
        public void ShowEquipmentNotice() => ShowEquipmentNotice(GameItemId.Rocinante);
        public void ShowEquipmentNotice(GameItemId item)
        {
            if (!equipmentNotice) return;
            Sprite icon;
            string text;
            switch (item)
            {
                case GameItemId.Cardboard: icon = cardboardNoticeSprite; text = "获得能力：【硬纸板】"; break;
                case GameItemId.JumpKey: icon = jumpKeyNoticeSprite; text = "获得能力：【跳跃键】"; break;
                case GameItemId.Rocinante: icon = rocinanteNoticeSprite; text = "获得装备：【驽骍难得】"; break;
                default: return;
            }
            if (equipmentNoticeText) equipmentNoticeText.text = text;
            if (equipmentNoticeIcon)
            {
                equipmentNoticeIcon.sprite = icon;
                equipmentNoticeIcon.preserveAspect = true;
                equipmentNoticeIcon.enabled = icon;
            }
            equipmentRemaining = equipmentNoticeSeconds;
            equipmentNotice.SetActive(true);
        }
        public void ClearEquipmentNotice()
        { equipmentRemaining = 0f; if (equipmentNotice) equipmentNotice.SetActive(false); }
        public void SetResetHint(bool visible)
        {
            if (!resetHint) return;
            resetHint.gameObject.SetActive(visible);
            if (visible) resetHint.transform.SetAsLastSibling();
        }
        public void FadeBlack(float alpha)
        { blackPanel.SetActive(true); blackText.text = string.Empty; blackGroup.alpha = alpha; }
        private void TickEnding()
        {
            if (!endingActive || group.alpha <= 0f) return;
            endingElapsed += Time.unscaledDeltaTime;
            float fade = endingFadeSeconds <= 0f ? 1f : Mathf.Clamp01(endingElapsed / endingFadeSeconds);
            blackGroup.alpha = Mathf.SmoothStep(0f, 1f, fade);
            var title = endingTitleText ? endingTitleText : blackText;
            float titleTime = Mathf.Max(0f, endingElapsed - endingFadeSeconds);
            float titleFade = endingTitleFadeSeconds <= 0f ? (endingElapsed >= endingFadeSeconds ? 1f : 0f) : Mathf.Clamp01(titleTime / endingTitleFadeSeconds);
            var color = title.color; color.a = Mathf.SmoothStep(0f, 1f, titleFade); title.color = color;
        }
        public void ShowEnding(Sprite image, string title = "To Be Continued")
        {
            Hide(); blackPanel.SetActive(true); blackText.text = string.Empty;
            var label = endingTitleText ? endingTitleText : blackText;
            label.text = title; label.gameObject.SetActive(true);
            label.color = new Color(1f, 1f, 1f, 0f);
            endingElapsed = 0f; endingActive = true; group.alpha = 1f;
            blackContinueButton.gameObject.SetActive(false);
            blackGroup.alpha = 0f;
            group.blocksRaycasts = true;
            if (image != null) { endingImage.sprite = image; endingImage.gameObject.SetActive(true); }
        }
    }
}
