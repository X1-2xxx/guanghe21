using System;
using System.Collections.Generic;
using UnityEngine;

namespace GamePresentation
{
    public enum StoryStepKind
    {
        [InspectorName("底部立绘对白")] Dialogue,
        [InspectorName("居中旁白")] Narration,
        [InspectorName("主线头顶气泡")] HeadBubble,
        [InspectorName("玩家选项")] Choice,
        [InspectorName("镜头聚焦并返回")] Camera,
        [InspectorName("等待玩法信号")] WaitSignal,
        [InspectorName("演出指令")] Command,
        [InspectorName("是否学会半路叫停")] BranchMidStop,
        [InspectorName("黑幕开场文字")] BlackTitle,
        [InspectorName("原型结束图")] End,
        [InspectorName("黑幕淡出")] FadeOut,
        [InspectorName("是否已完成指定剧情")] BranchEpisodeComplete,
        [InspectorName("等待入口镜头就位")] RoomEntranceCamera,
        [InspectorName("天空字幕（无纸框）")] SkySubtitle
    }

    public enum StoryExpression
    {
        [InspectorName("默认·说话")] Default = 0,
        [InspectorName("无语·说话")] Speechless = 1,
        [InspectorName("生气·怒气图标")] Angry = 2,
        [InspectorName("疑惑")] Puzzled = 3,
        [InspectorName("疑惑·问号")] PuzzledQuestionMark = 4,
        [InspectorName("震惊·说话")] ShockedTalking = 5,
        [InspectorName("震惊·震惊嘴")] ShockedMouth = 6,
        [InspectorName("兴奋·兴奋特效")] Excited = 7,
        [InspectorName("嫌弃")] Disgusted = 8,
        [InspectorName("通常·闭嘴")] NeutralClosedMouth = 9,
        [InspectorName("通常·震惊嘴")] NeutralShockedMouth = 10,
        [InspectorName("通常·难受嘴")] NeutralPainedMouth = 11,
        [InspectorName("通常·无语嘴")] NeutralSpeechlessMouth = 12,
        [InspectorName("通常·兴奋嘴")] NeutralExcitedMouth = 13,
        [InspectorName("紧闭眼·难受嘴")] ClosedEyesPained = 14,
        [InspectorName("紧闭眼·兴奋特效")] ClosedEyesExcitedEffect = 15,
        [InspectorName("紧闭眼·兴奋嘴")] ClosedEyesExcited = 16,
        [InspectorName("紧闭眼·震惊嘴")] ClosedEyesShocked = 17,
        [InspectorName("无语·闭嘴·省略号")] SpeechlessClosedMouthEllipsis = 18,
        [InspectorName("无语·无语嘴·省略号")] SpeechlessMouthEllipsis = 19,
        [InspectorName("无语·无语嘴")] SpeechlessMouth = 20
    }

    [Serializable] public sealed class StoryPortraitVariant
    {
        [InspectorName("表情")] public StoryExpression expression;
        [InspectorName("立绘 Sprite")] public Sprite portrait;
    }

    public enum InnkeeperExpression
    {
        [InspectorName("通常")] Normal,
        [InspectorName("嘻嘻")] Chuckle,
        [InspectorName("心虚")] SelfConscious,
        [InspectorName("苦恼")] Troubled
    }

    [Serializable] public sealed class InnkeeperPortraitVariant
    {
        [InspectorName("表情")] public InnkeeperExpression expression;
        [InspectorName("立绘 Sprite")] public Sprite portrait;
    }

    [Serializable] public sealed class StoryChoice
    {
        [InspectorName("选项文字"), TextArea(2, 5)] public string text;
        [InspectorName("选择后节点")] public string next;
    }
    [Serializable] public sealed class StoryStep
    {
        [InspectorName("节点标识")] public string id;
        [InspectorName("演出类型")] public StoryStepKind kind;
        [InspectorName("说话人")] public string speaker = "堂吉诃德";
        [InspectorName("堂吉诃德表情")] public StoryExpression expression;
        [InspectorName("帕洛梅克表情")] public InnkeeperExpression innkeeperExpression;
        [InspectorName("台词"), TextArea(2, 10), Tooltip("对白和报幕遇到空行会换页，较长的段落会自动分段。")]
        public string text;
        [InspectorName("下一节点")] public string next;
        [InspectorName("条件成立时节点")] public string alternate;
        [InspectorName("镜头目标 / 等待信号 / 指令")] public string argument;
        [InspectorName("等待期间显示头顶小气泡")] public bool showHeadWhileWaiting;
        [InspectorName("黑幕报幕自动播放")] public bool autoAdvance;
        [InspectorName("选项出现时隐藏上一句与立绘")] public bool hidePreviousDialogue;
        [InspectorName("阅读或停留秒数")] public float seconds = 3f;
        [InspectorName("选项")] public List<StoryChoice> choices = new List<StoryChoice>();
    }
    [Serializable] public sealed class StoryEpisode
    {
        [InspectorName("剧情标识")] public string id;
        [InspectorName("章节名称")] public string title;
        [InspectorName("入口节点")] public string entry;
        [InspectorName("只播放一次")] public bool once = true;
        [InspectorName("堂吉诃德与帕洛梅克双人同屏")] public bool showInnkeeper;
        [InspectorName("剧情节点")] public List<StoryStep> steps = new List<StoryStep>();
    }
    [Serializable] public sealed class BarkRule
    {
        [InspectorName("触发标识")] public string id;
        [InspectorName("首次台词"), TextArea(1, 5)] public string[] lines;
        [InspectorName("第二次短句"), TextArea(1, 4)] public string[] repeat;
        [InspectorName("第三次起短句"), TextArea(1, 3)] public string[] brief;
        [InspectorName("显示秒数")] public float duration = 3f;
        [InspectorName("同类冷却秒数")] public float cooldown = 8f;
        [InspectorName("优先级")] public int priority = 10;
        [InspectorName("一次性")] public bool once;
    }

    /// <summary>策划在 Inspector 修改台词和分支；运行进度不写回这份资产。</summary>
    [CreateAssetMenu(menuName = "堂吉诃德/剧情与游玩台词配置")]
    public sealed class PresentationLibrary : ScriptableObject
    {
        [InspectorName("堂吉诃德半身立绘")] public Sprite heroPortrait;
        [InspectorName("堂吉诃德无语差分")] public Sprite heroSpeechlessPortrait;
        [InspectorName("堂吉诃德其他表情差分")] public List<StoryPortraitVariant> heroExpressions = new List<StoryPortraitVariant>();
        [InspectorName("旅馆老板立绘")] public Sprite innkeeperPortrait;
        [InspectorName("帕洛梅克表情差分")] public List<InnkeeperPortraitVariant> innkeeperExpressions = new List<InnkeeperPortraitVariant>();
        [InspectorName("未能拖动物块的音效（可留空）")] public AudioClip deniedClip;
        [InspectorName("开场坠坑音效（可留空）")] public AudioClip pitImpactClip;
        [InspectorName("原型结束图片（可留空）")] public Sprite endingSprite;
        [InspectorName("小气泡台词")] public List<BarkRule> barks = new List<BarkRule>();
        [InspectorName("三幕主线剧情")] public List<StoryEpisode> episodes = new List<StoryEpisode>();
        public BarkRule FindBark(string id) => barks.Find(b => b.id == id);
        public StoryEpisode FindEpisode(string id) => episodes.Find(e => e.id == id);

        /// <summary>表情随对白节点切换。未提供差分时继续使用角色的默认立绘。</summary>
        public Sprite ResolvePortrait(StoryStep step)
        {
            return IsInnkeeper(step.speaker) ? ResolveInnkeeperPortrait(step.innkeeperExpression) : ResolveHeroPortrait(step.expression);
        }

        public static bool IsInnkeeper(string speaker) => speaker == "旅馆老板" || speaker == "帕洛梅克";

        public Sprite ResolveInnkeeperPortrait(InnkeeperExpression expression)
        {
            foreach (var variant in innkeeperExpressions)
                if (variant != null && variant.expression == expression && variant.portrait) return variant.portrait;
            return innkeeperPortrait;
        }

        public Sprite ResolveHeroPortrait(StoryExpression expression)
        {
            if (expression == StoryExpression.Default) return heroPortrait;
            if (expression == StoryExpression.Speechless) return heroSpeechlessPortrait ? heroSpeechlessPortrait : heroPortrait;
            foreach (var variant in heroExpressions)
            {
                if (variant != null && variant.expression == expression && variant.portrait) return variant.portrait;
            }
            return heroPortrait;
        }
    }
}
