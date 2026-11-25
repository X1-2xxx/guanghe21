using System;
using System.Collections.Generic;
using UnityEngine;

namespace GamePresentation
{
    public enum StoryStepKind
    {
        [InspectorName("底部立绘对白")] Dialogue,
        [InspectorName("屏幕上方旁白")] Narration,
        [InspectorName("主线头顶气泡")] HeadBubble,
        [InspectorName("玩家选项")] Choice,
        [InspectorName("镜头聚焦并返回")] Camera,
        [InspectorName("等待玩法信号")] WaitSignal,
        [InspectorName("演出指令")] Command,
        [InspectorName("是否学会半路叫停")] BranchMidStop,
        [InspectorName("黑幕开场文字")] BlackTitle,
        [InspectorName("原型结束图")] End,
        [InspectorName("黑幕淡出")] FadeOut,
        [InspectorName("是否已完成指定剧情")] BranchEpisodeComplete
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
        [InspectorName("台词"), TextArea(2, 10)] public string text;
        [InspectorName("下一节点")] public string next;
        [InspectorName("条件成立时节点")] public string alternate;
        [InspectorName("镜头目标 / 等待信号 / 指令")] public string argument;
        [InspectorName("阅读或停留秒数")] public float seconds = 3f;
        [InspectorName("选项")] public List<StoryChoice> choices = new List<StoryChoice>();
    }
    [Serializable] public sealed class StoryEpisode
    {
        [InspectorName("剧情标识")] public string id;
        [InspectorName("章节名称")] public string title;
        [InspectorName("入口节点")] public string entry;
        [InspectorName("只播放一次")] public bool once = true;
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
        [InspectorName("堂吉诃德立绘")] public Sprite heroPortrait;
        [InspectorName("旅馆老板立绘")] public Sprite innkeeperPortrait;
        [InspectorName("未能拖动物块的音效（可留空）")] public AudioClip deniedClip;
        [InspectorName("开场坠坑音效（可留空）")] public AudioClip pitImpactClip;
        [InspectorName("原型结束图片（可留空）")] public Sprite endingSprite;
        [InspectorName("小气泡台词")] public List<BarkRule> barks = new List<BarkRule>();
        [InspectorName("三幕主线剧情")] public List<StoryEpisode> episodes = new List<StoryEpisode>();
        public BarkRule FindBark(string id) => barks.Find(b => b.id == id);
        public StoryEpisode FindEpisode(string id) => episodes.Find(e => e.id == id);
    }
}
