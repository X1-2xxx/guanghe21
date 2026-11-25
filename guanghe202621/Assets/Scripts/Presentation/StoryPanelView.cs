using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
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
            if (step.kind == StoryStepKind.Narration)
            {
                ShowNarration(step.text, !step.autoAdvance);
                return;
            }
            Hide(); group.alpha = 1f; group.blocksRaycasts = true;
            if (step.kind == StoryStepKind.BlackTitle)
            {
                blackGroup.alpha = 1f;
                blackContinueButton.gameObject.SetActive(!step.autoAdvance);
                blackPanel.SetActive(true);
                SetTextPages(blackText, step.text);
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
            if (choices) dialogueText.text = retain ? lastLine : step.text;
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
                if (step.choices.Count > 0) choiceAText.text = choiceAPrefix + step.choices[0].text;
                if (step.choices.Count > 1) choiceBText.text = choiceBPrefix + step.choices[1].text;
                choiceLayout ??= new StoryChoiceLayout((RectTransform)choicesPanel.transform, choiceAText, choiceBText,
                    (RectTransform)choiceA.transform, (RectTransform)choiceB.transform);
                choiceLayout.Apply();
            }
        }

        public void ShowNarration(string text, bool showContinue)
        {
            Hide();
            group.alpha = 1f;
            group.blocksRaycasts = showContinue;
            narratorContinueButton.gameObject.SetActive(showContinue);
            narratorPanel.SetActive(true);
            SetTextPages(narratorText, text);
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
            textPages = BuildPages(text);
            pageIndex = 0;
            target.text = textPages.Count > 0 ? textPages[0] : string.Empty;
        }

        private static List<string> BuildPages(string text)
        {
            const int pageLength = 64;
            const string sentenceEnds = "。！？!?…」』”\n";
            const string clauseEnds = "，、；：,. ;：\t";
            var pages = new List<string>();
            // 空行指定一页；超长时优先在句号或分句处拆开，保留短对白原样。
            foreach (string rawParagraph in Regex.Split(text ?? string.Empty, @"\r?\n[ \t]*\r?\n"))
            {
                string paragraph = rawParagraph.Trim();
                int[] characters = StringInfo.ParseCombiningCharacters(paragraph);
                for (int start = 0; start < characters.Length;)
                {
                    int end = Math.Min(start + pageLength, characters.Length);
                    if (end < characters.Length)
                    {
                        int clauseEnd = -1;
                        for (int i = end; i > start + pageLength / 2; i--)
                        {
                            char previous = paragraph[characters[i - 1]];
                            if (sentenceEnds.IndexOf(previous) >= 0) { end = i; clauseEnd = -1; break; }
                            if (clauseEnd < 0 && clauseEnds.IndexOf(previous) >= 0) clauseEnd = i;
                        }
                        if (clauseEnd > 0) end = clauseEnd;
                        // 收尾引号、连续省略号与标点留在当前页。
                        while (end < characters.Length && "。！？!?…」』”）".IndexOf(paragraph[characters[end]]) >= 0) end++;
                    }
                    int endOffset = end == characters.Length ? paragraph.Length : characters[end];
                    string page = paragraph.Substring(characters[start], endOffset - characters[start]).Trim();
                    if (page.Length > 0) pages.Add(page);
                    start = end;
                }
            }
            return pages;
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
