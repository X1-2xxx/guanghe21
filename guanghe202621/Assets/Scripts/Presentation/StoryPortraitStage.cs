using UnityEngine;
using UnityEngine.UI;

namespace GamePresentation
{
    /// <summary>只控制预制体中的两张立绘、表情记忆与说话亮度，不参与剧情推进和玩家控制。</summary>
    public sealed class StoryPortraitStage : MonoBehaviour
    {
        [InspectorName("左侧堂吉诃德立绘")] public Image heroImage;
        [InspectorName("右侧帕洛梅克立绘")] public Image innkeeperImage;
        [SerializeField, Range(.1f, 1f), InspectorName("未说话角色亮度")] private float inactiveBrightness = .7f;
        [SerializeField, InspectorName("说话角色颜色")] private Color speakingColor = Color.white;
        [SerializeField, InspectorName("右侧显示选项时隐藏右侧立绘")] private bool hideInnkeeperForChoices = true;
        private Sprite heroExpression, innkeeperExpression;
        private bool dual, innkeeperSpeaking;

        public bool HeroVisible => heroImage && heroImage.gameObject.activeInHierarchy && (dual || !innkeeperSpeaking);

        public void BeginEpisode(bool twoCharacters, Sprite hero, Sprite innkeeper)
        {
            dual = twoCharacters && hero && innkeeper;
            heroExpression = hero; innkeeperExpression = innkeeper;
            innkeeperSpeaking = false;
            Hide();
        }

        /// <summary>台词只更新说话者的表情，另一位保留上一张差分，靠亮度区分发言状态。</summary>
        public void Show(bool isInnkeeper, Sprite expression, bool choices, bool showDialogue)
        {
            if (!showDialogue) { Hide(); return; }
            if (!choices) innkeeperSpeaking = isInnkeeper;
            if (dual)
            {
                if (!choices && expression)
                {
                    if (isInnkeeper) innkeeperExpression = expression;
                    else heroExpression = expression;
                }
                SetImage(heroImage, heroExpression, true);
                SetImage(innkeeperImage, innkeeperExpression, !choices || !hideInnkeeperForChoices);
            }
            else
            {
                // 单人章节沿用原来的左侧槽位，兼容已有预制体与角色台词。
                SetImage(heroImage, expression, true);
                if (innkeeperImage) innkeeperImage.gameObject.SetActive(false);
            }
            RefreshColors();
        }

        public void Hide()
        {
            if (heroImage) heroImage.gameObject.SetActive(false);
            if (innkeeperImage) innkeeperImage.gameObject.SetActive(false);
        }

        private void LateUpdate() => RefreshColors();
        private void RefreshColors()
        {
            Color dim = new Color(speakingColor.r * inactiveBrightness, speakingColor.g * inactiveBrightness,
                speakingColor.b * inactiveBrightness, speakingColor.a);
            if (heroImage) heroImage.color = dual && innkeeperSpeaking ? dim : speakingColor;
            if (innkeeperImage) innkeeperImage.color = innkeeperSpeaking ? speakingColor : dim;
        }

        private static void SetImage(Image image, Sprite sprite, bool visible)
        {
            if (!image) return;
            image.sprite = sprite;
            image.gameObject.SetActive(visible && sprite);
        }
    }
}
