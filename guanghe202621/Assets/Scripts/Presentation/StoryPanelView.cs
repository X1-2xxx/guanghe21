using System;
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
        public void Hide()
        {
            blackPanel.SetActive(false); dialoguePanel.SetActive(false);
            choicesPanel.SetActive(false); narratorPanel.SetActive(false);
            if (endingImage) endingImage.gameObject.SetActive(false);
            group.blocksRaycasts = false;
        }
        public void Show(StoryStep step, Sprite actorPortrait)
        {
            Hide(); group.alpha = 1f; group.blocksRaycasts = true;
            if (step.kind == StoryStepKind.BlackTitle)
            {
                blackGroup.alpha = 1f;
                blackContinueButton.gameObject.SetActive(true);
                blackPanel.SetActive(true); blackText.text = step.text; return;
            }
            if (step.kind == StoryStepKind.Narration)
            {
                narratorPanel.SetActive(true); narratorText.text = step.text; return;
            }
            dialoguePanel.SetActive(true);
            bool choices = step.kind == StoryStepKind.Choice;
            // 选项放到右侧后，底部保留角色上一句台词，避免对话框突然变空。
            bool retain = choices && string.IsNullOrWhiteSpace(step.text) && !string.IsNullOrEmpty(lastLine);
            speakerText.text = retain ? lastSpeaker : step.speaker;
            dialogueText.text = retain ? lastLine : step.text;
            Sprite image = retain ? lastPortrait : actorPortrait;
            portrait.sprite = image; portrait.gameObject.SetActive(image != null);
            if (!choices) RememberDialogue(step.text, step.speaker, image);
            choicesPanel.SetActive(choices); continueButton.gameObject.SetActive(!choices);
            if (dialogueAdvanceButton) dialogueAdvanceButton.interactable = !choices;
            if (choices)
            {
                choiceA.gameObject.SetActive(step.choices.Count > 0); choiceB.gameObject.SetActive(step.choices.Count > 1);
                if (step.choices.Count > 0) choiceAText.text = choiceAPrefix + step.choices[0].text;
                if (step.choices.Count > 1) choiceBText.text = choiceBPrefix + step.choices[1].text;
            }
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
