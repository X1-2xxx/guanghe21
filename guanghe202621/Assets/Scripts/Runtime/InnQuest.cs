using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LevelKit
{
    [DefaultExecutionOrder(-40), DisallowMultipleComponent]
    [AddComponentMenu("Level Kit/Inn Quest")]
    public sealed class InnQuest : MonoBehaviour
    {
        private enum Stage { WaitingForIntro, IntroDialogue, Collecting, ReadyToReturn, FinalDialogue, Finished }

        [SerializeField] private PlayerActionController player;
        [Header("老板剧情对话")]
        [SerializeField] private BubbleDialogSequence bossDialogue;
        [SerializeField] private string introConversation = "intro";
        [SerializeField] private string finalConversation = "quest_done";
        [SerializeField, HideInInspector] private GameObject dialoguePanel;
        [SerializeField, HideInInspector] private Text dialogueText;
        [SerializeField, FormerlySerializedAs("stableReward")] private GameObject reward;
        [SerializeField] private Transform rewardThrowOrigin;
        [SerializeField] private Transform rewardLandingPoint;
        [SerializeField, Min(0f)] private float rewardThrowSeconds = 0.8f;
        [SerializeField, Min(0f)] private float rewardArcHeight = 1.2f;
        [SerializeField] private GameObject exitPassageCover;
        [SerializeField, HideInInspector] private string[] introLines =
            { "老板：欢迎来到旅馆。请帮我按顺序找来三件物品。", "老板：找齐之后，再回来找我吧。" };
        [SerializeField, HideInInspector] private string[] finalLines =
            { "老板：三件物品都找到了，谢谢你！这件驽骍难得送给你。", "老板：拿好它，右侧的门板就拦不住你了。" };

        private GamePresentation.PresentationRuntime presentation;
        private string presentationEpisode;
        private Stage stage;
        private int collectedCount;
        private bool rewardReleased;
        private bool rewardFlying;
        private float rewardElapsed;
        private Vector3 throwStart, throwEnd;
        private Collider2D[] rewardColliders;
        private bool[] colliderStates;
        private Rigidbody2D rewardBody;
        private bool bodyWasSimulated;
        private bool OwnsDialogue => stage == Stage.IntroDialogue || stage == Stage.FinalDialogue;

        private void Awake()
        {
            if (dialoguePanel) dialoguePanel.SetActive(false);
            if (reward) reward.SetActive(false);
            if (exitPassageCover) exitPassageCover.SetActive(true);
        }

        private void Update()
        {
            if (LevelGameFlow.BlocksGameplayInput(this)) return;
            AdvanceRewardThrow(Time.deltaTime);
            if (!OwnsDialogue) return;
            if (presentationEpisode != null)
            {
                if (presentation && presentation.Director.HasCompleted(presentationEpisode)) FinishDialogue();
                else if (!presentation || !presentation.Director.HasScheduled(presentationEpisode)) CancelDialogue();
                return;
            }
            if (!bossDialogue || !bossDialogue.IsPlaying)
            {
                if (bossDialogue && bossDialogue.LastPlaybackCompleted) FinishDialogue();
                else CancelDialogue();
                return;
            }
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
                ContinueDialogue();
        }

        internal void TryTalk(PlayerActionController visitor)
        {
            if (!isActiveAndEnabled || !player || visitor != player ||
                (stage != Stage.WaitingForIntro && stage != Stage.ReadyToReturn)) return;
            bool introduction = stage == Stage.WaitingForIntro;
            string conversation = introduction ? introConversation : finalConversation;
            var runtime = GamePresentation.PresentationRuntime.Instance;
            if (runtime && runtime.UsesCollectionQuest && runtime.Scene.legacyInnQuest == this)
            {
                string episode = introduction ? "inn.intro" : "inn.quest_done";
                if (runtime.library.FindEpisode(episode) == null)
                { Debug.LogError("旅馆剧情缺少章节：" + episode, this); return; }
                presentation = runtime; presentationEpisode = episode;
                stage = introduction ? Stage.IntroDialogue : Stage.FinalDialogue;
                runtime.Director.Enqueue(episode);
                return;
            }
            if (!bossDialogue || !bossDialogue.isActiveAndEnabled || !bossDialogue.HasConversation(conversation))
            {
                Debug.LogError("请给旅馆任务绑定老板的 BubbleDialogSequence，并配置 intro 与 quest_done 对话分组。", this);
                return;
            }
            if (!player.BeginConversation()) return;

            stage = introduction ? Stage.IntroDialogue : Stage.FinalDialogue;
            bossDialogue.PlayConversation(conversation);
            if (!bossDialogue.IsPlaying && !bossDialogue.LastPlaybackCompleted) CancelDialogue();
        }

        internal bool TryCollect(PlayerActionController visitor, int itemNumber)
        {
            if (!isActiveAndEnabled || !player || visitor != player || stage != Stage.Collecting ||
                itemNumber != collectedCount + 1) return false;
            collectedCount++;
            if (collectedCount == 3) stage = Stage.ReadyToReturn;
            return true;
        }

        public void ContinueDialogue()
        {
            if (LevelGameFlow.BlocksGameplayInput(this)) return;
            if (!OwnsDialogue) return;
            if (presentationEpisode != null) { if (presentation) presentation.Director.Advance(); return; }
            if (bossDialogue) bossDialogue.ContinueDialogue();
            if (bossDialogue && !bossDialogue.IsPlaying && bossDialogue.LastPlaybackCompleted) FinishDialogue();
        }

        private void FinishDialogue()
        {
            if (!OwnsDialogue) return;
            presentationEpisode = null; presentation = null;
            bool finished = stage == Stage.FinalDialogue;
            stage = finished ? Stage.Finished : Stage.Collecting;
            if (dialoguePanel) dialoguePanel.SetActive(false);
            player.EndConversation();
            if (finished)
            {
                if (exitPassageCover) exitPassageCover.SetActive(false);
                ThrowReward();
            }
        }

        private void CancelDialogue()
        {
            if (!OwnsDialogue) return;
            presentationEpisode = null; presentation = null;
            stage = stage == Stage.IntroDialogue ? Stage.WaitingForIntro : Stage.ReadyToReturn;
            if (dialoguePanel) dialoguePanel.SetActive(false);
            if (player) player.EndConversation();
        }

        private void ThrowReward()
        {
            if (rewardReleased || !reward) return;
            rewardReleased = true;
            throwStart = rewardThrowOrigin ? rewardThrowOrigin.position : transform.position + Vector3.up;
            throwEnd = rewardLandingPoint ? rewardLandingPoint.position : reward.transform.position;
            rewardColliders = reward.GetComponentsInChildren<Collider2D>(true);
            colliderStates = new bool[rewardColliders.Length];
            for (int i = 0; i < rewardColliders.Length; i++)
            {
                colliderStates[i] = rewardColliders[i].enabled;
                rewardColliders[i].enabled = false;
            }
            rewardBody = reward.GetComponent<Rigidbody2D>();
            if (rewardBody)
            {
                bodyWasSimulated = rewardBody.simulated;
                rewardBody.simulated = false;
            }
            reward.transform.position = throwStart;
            reward.SetActive(true);
            rewardElapsed = 0f;
            rewardFlying = true;
            if (rewardThrowSeconds <= 0f) LandReward();
        }

        private void AdvanceRewardThrow(float deltaTime)
        {
            if (!rewardFlying) return;
            if (!reward) { rewardFlying = false; return; }
            rewardElapsed += Mathf.Max(0f, deltaTime);
            float progress = rewardThrowSeconds <= 0f ? 1f : Mathf.Clamp01(rewardElapsed / rewardThrowSeconds);
            reward.transform.position = Vector3.Lerp(throwStart, throwEnd, progress)
                + Vector3.up * (4f * rewardArcHeight * progress * (1f - progress));
            if (progress >= 1f) LandReward();
        }

        private void LandReward()
        {
            rewardFlying = false;
            if (!reward) return;
            reward.transform.position = throwEnd;
            if (rewardBody)
            {
                rewardBody.position = throwEnd;
                rewardBody.linearVelocity = Vector2.zero;
                rewardBody.angularVelocity = 0f;
                rewardBody.simulated = bodyWasSimulated;
            }
            for (int i = 0; i < rewardColliders.Length; i++)
                if (rewardColliders[i]) rewardColliders[i].enabled = colliderStates[i];
        }

        private void OnDisable()
        {
            if (rewardFlying) LandReward();
            if (!OwnsDialogue) return;
            if (presentationEpisode != null && presentation) presentation.CancelCurrentStory();
            presentationEpisode = null; presentation = null;
            if (bossDialogue && bossDialogue.IsPlaying) bossDialogue.StopConversation();
            bool intro = stage == Stage.IntroDialogue;
            stage = intro ? Stage.WaitingForIntro : Stage.ReadyToReturn;
            if (!intro && reward && !rewardReleased) reward.SetActive(false);
            if (!intro && exitPassageCover) exitPassageCover.SetActive(true);
            if (dialoguePanel) dialoguePanel.SetActive(false);
            if (player) player.EndConversation();
        }

        private void OnDrawGizmosSelected()
        {
            if (!rewardThrowOrigin || !rewardLandingPoint) return;
            Gizmos.color = Color.yellow;
            Vector3 start = rewardThrowOrigin.position, end = rewardLandingPoint.position, previous = start;
            for (int i = 1; i <= 24; i++)
            {
                float t = i / 24f;
                Vector3 point = Vector3.Lerp(start, end, t) + Vector3.up * (4f * rewardArcHeight * t * (1f - t));
                Gizmos.DrawLine(previous, point); previous = point;
            }
            Gizmos.DrawWireSphere(start, 0.1f); Gizmos.DrawWireSphere(end, 0.1f);
        }
    }
}
