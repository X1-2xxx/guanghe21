using System;
using System.Collections.Generic;
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
        public Image portrait, endingImage;
        public Button continueButton, blackContinueButton, narratorContinueButton;
        public Button choiceA, choiceB;
        [InspectorName("点击纸张对话框继续")] public Button dialogueAdvanceButton;
        [InspectorName("A 选项前缀")] public string choiceAPrefix = "A. ";
        [InspectorName("B 选项前缀")] public string choiceBPrefix = "B. ";
        public Text choiceAText, choiceBText, resetHint;
        private Action onContinue;
        private Action<int> onChoice;
        private string lastLine, lastSpeaker;
        private Sprite lastPortrait;
        private Text pagedText;
        private List<string> textPages;
        private int pageIndex;
        private StoryChoiceLayout choiceLayout;

        public int CurrentPageLength => pagedText && pagedText.text != null ? pagedText.text.Length : 0;

        public void Bind(Action advance, Action<int> choose)
        {
            onContinue = advance; onChoice = choose;
            continueButton.onClick.AddListener(Continue);
            blackContinueButton.onClick.AddListener(Continue);
            narratorContinueButton.onClick.AddListener(Continue);
            if (dialogueAdvanceButton) dialogueAdvanceButton.onClick.AddListener(Continue);
            choiceA.onClick.AddListener(() => onChoice?.Invoke(0));
            choiceB.onClick.AddListener(() => onChoice?.Invoke(1));
            UIFontUtility.ApplyToHierarchy(gameObject);
            Hide(); SetResetHint(false);
        }
        public void RememberDialogue(string text, string speaker, Sprite image)
        { lastLine = text; lastSpeaker = speaker; lastPortrait = image; }
        public void ResetDialogueMemory() { lastLine = lastSpeaker = null; lastPortrait = null; }
        private void Continue() => onContinue?.Invoke();
        private void LateUpdate() => choiceLayout?.RefreshIfNeeded();
        public void Hide()
        {
            blackPanel.SetActive(false); dialoguePanel.SetActive(false);
            choicesPanel.SetActive(false); narratorPanel.SetActive(false);
            if (endingImage) endingImage.gameObject.SetActive(false);
            group.blocksRaycasts = false;
            pagedText = null;
            textPages = null;
            pageIndex = 0;
        }
        public void Show(StoryStep step, Sprite actorPortrait)
        {
            Hide(); group.alpha = 1f; group.blocksRaycasts = true;
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
                narratorContinueButton.gameObject.SetActive(!step.autoAdvance);
                narratorPanel.SetActive(true);
                SetTextPages(narratorText, step.text);
                return;
            }
            dialoguePanel.SetActive(true);
            bool choices = step.kind == StoryStepKind.Choice;
            // 选项放到右侧后，底部保留角色上一句台词，避免对话框突然变空。
            bool retain = choices && string.IsNullOrWhiteSpace(step.text) && !string.IsNullOrEmpty(lastLine);
            bool showDialogue = !choices || retain || !string.IsNullOrWhiteSpace(step.text);
            if (dialogueFrame) dialogueFrame.SetActive(showDialogue);
            speakerText.text = retain ? lastSpeaker : step.speaker;
            Sprite image = !showDialogue ? null : (retain ? lastPortrait : actorPortrait);
            portrait.sprite = image; portrait.gameObject.SetActive(image != null);
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
            choicesPanel.SetActive(choices); continueButton.gameObject.SetActive(!choices);
            if (dialogueAdvanceButton) dialogueAdvanceButton.interactable = !choices;
            if (choices)
            {
                choiceA.gameObject.SetActive(step.choices.Count > 0); choiceB.gameObject.SetActive(step.choices.Count > 1);
                choiceLayout ??= new StoryChoiceLayout((RectTransform)choicesPanel.transform, choiceAText, choiceBText,
                    (RectTransform)choiceA.transform, (RectTransform)choiceB.transform);
                choiceLayout.Apply(step.choices.Count > 0 ? choiceAPrefix + step.choices[0].text : string.Empty,
                    step.choices.Count > 1 ? choiceBPrefix + step.choices[1].text : string.Empty);
            }
        }

        public bool TryAdvanceTextPage()
        {
            if (!pagedText || textPages == null || pageIndex + 1 >= textPages.Count) return false;
            pagedText.text = textPages[++pageIndex];
            if (pagedText == dialogueText) lastLine = pagedText.text;
            return true;
        }

        private void SetTextPages(Text target, string text)
        {
            pagedText = target;
            target.horizontalOverflow = HorizontalWrapMode.Overflow;
            textPages = LayoutFor(target).Paginate(text, target.rectTransform.rect.height, target == dialogueText ? 3 : 4);
            pageIndex = 0;
            target.text = textPages.Count > 0 ? textPages[0] : string.Empty;
        }

        private static StoryTextLayout LayoutFor(Text label)
        {
            var spacing = label.GetComponent<StoryDialogueLetterSpacing>();
            int fontSize = label.resizeTextForBestFit ? label.resizeTextMaxSize : label.fontSize;
            return new StoryTextLayout(label, fontSize, spacing ? spacing.Spacing : 0f);
        }
        public void SetPaused(bool paused) { group.alpha = paused ? 0f : 1f; group.blocksRaycasts = !paused && (blackPanel.activeSelf || dialoguePanel.activeSelf || narratorPanel.activeSelf); }
        public void SetResetHint(bool visible) => resetHint.gameObject.SetActive(visible);
        public void FadeBlack(float alpha)
        { blackPanel.SetActive(true); blackText.text = string.Empty; blackGroup.alpha = alpha; }
        public void ShowEnding(Sprite image)
        {
            Hide(); blackPanel.SetActive(true); blackText.text = "未完待续";
            blackContinueButton.gameObject.SetActive(false);
            blackGroup.alpha = 1f;
            group.blocksRaycasts = true;
            if (image != null) { endingImage.sprite = image; endingImage.gameObject.SetActive(true); }
        }
    }
}
