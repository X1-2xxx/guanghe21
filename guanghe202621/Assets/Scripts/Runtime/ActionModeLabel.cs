using UnityEngine;
using UnityEngine.UI;

namespace LevelKit
{
    [DisallowMultipleComponent, RequireComponent(typeof(Text))]
    [AddComponentMenu("Level Kit/Action Mode Label")]
    public sealed class ActionModeLabel : MonoBehaviour
    {
        [SerializeField] private PlayerActionController player;
        [SerializeField] private Font chineseFont;

        private Text label;
        private Font systemFont;

        private void Awake()
        {
            label = GetComponent<Text>();
            label.raycastTarget = false;
            if (chineseFont)
            {
                label.font = chineseFont;
                return;
            }

            const string sample = "主角玩家在行动";
            if (label.font && SupportsText(label.font, sample)) return;

            systemFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei", "SimHei", "Noto Sans CJK SC", "PingFang SC" }, label.fontSize);
            if (systemFont) label.font = systemFont;
        }

        private void OnEnable()
        {
            if (player) player.ActionChanged += Refresh;
            Refresh(player && player.IsActing);
        }

        private void OnDisable()
        {
            if (player) player.ActionChanged -= Refresh;
        }

        private void OnDestroy()
        {
            if (systemFont) Destroy(systemFont);
        }

        private void Refresh(bool acting) => label.text = acting ? "主角在行动" : "玩家在行动";

        private static bool SupportsText(Font font, string text)
        {
            foreach (char character in text)
                if (!font.HasCharacter(character)) return false;
            return true;
        }
    }
}
