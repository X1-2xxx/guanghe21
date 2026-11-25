#if UNITY_EDITOR
using System.Collections.Generic;
using GamePresentation;
using UnityEditor;
using UnityEngine;

/// <summary>按章节和节点填写中文台词，无需进入代码或展开整份剧本。</summary>
[CustomEditor(typeof(PresentationLibrary))]
public sealed class PresentationLibraryEditor : Editor
{
    private int chapter, node;
    private bool showBarks, showExpressions;
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox("对白与选项在这里修改；气泡/对话框的布局和图片在预制体里修改。运行进度不会写回本资产。", MessageType.Info);
        foreach (string key in new[]{"heroPortrait","heroSpeechlessPortrait","innkeeperPortrait","endingSprite","deniedClip","pitImpactClip"})
            EditorGUILayout.PropertyField(serializedObject.FindProperty(key));
        showExpressions = EditorGUILayout.Foldout(showExpressions, "堂吉诃德其他表情差分", true);
        if (showExpressions)
        {
            EditorGUILayout.HelpBox("在下方的对白节点选择「立绘表情」。更换同尺寸的差分图片可保持立绘位置。", MessageType.None);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("heroExpressions"), true);
        }
        EditorGUILayout.PropertyField(serializedObject.FindProperty("innkeeperExpressions"), true);
        var library=(PresentationLibrary)target;
        if(library.episodes.Count>0)
        {
            var names=library.episodes.ConvertAll(e=>e.title).ToArray();
            int selected=EditorGUILayout.Popup("选择章节",Mathf.Clamp(chapter,0,names.Length-1),names);
            if(selected!=chapter){chapter=selected;node=0;}
            var episode=serializedObject.FindProperty("episodes").GetArrayElementAtIndex(chapter);
            EditorGUILayout.PropertyField(episode.FindPropertyRelative("id"));
            EditorGUILayout.PropertyField(episode.FindPropertyRelative("title"));
            EditorGUILayout.PropertyField(episode.FindPropertyRelative("entry"));
            EditorGUILayout.PropertyField(episode.FindPropertyRelative("showInnkeeper"));
            var steps=episode.FindPropertyRelative("steps");
            var labels=library.episodes[chapter].steps.ConvertAll(s=>s.id+" · "+s.kind+" · "+(s.text??"").Replace("\n"," ")).ToArray();
            if(labels.Length>0)
            {
                node=EditorGUILayout.Popup("选择台词/镜头节点",Mathf.Clamp(node,0,labels.Length-1),labels);
                EditorGUILayout.PropertyField(steps.GetArrayElementAtIndex(node),new GUIContent("当前节点"),true);
            }
        }
        showBarks=EditorGUILayout.Foldout(showBarks,"教学和游玩小台词（含冷却/降频设置）",true);
        if(showBarks)EditorGUILayout.PropertyField(serializedObject.FindProperty("barks"),true);
        serializedObject.ApplyModifiedProperties();
        if(GUILayout.Button("检查所有分支连接"))
        {
            var errors=Validate(library);
            EditorUtility.DisplayDialog("剧情配置检查",errors.Count==0?"所有章节入口、选项和下一节点均有效。":string.Join("\n",errors),"确定");
        }
    }
    public static List<string> Validate(PresentationLibrary library)
    {
        var errors=new List<string>(); var episodes=new HashSet<string>();
        foreach(var episode in library.episodes)
        {
            if(!episodes.Add(episode.id))errors.Add("章节标识重复："+episode.id);
            var ids=new HashSet<string>(); foreach(var step in episode.steps)if(!ids.Add(step.id))errors.Add(episode.id+" 节点重复："+step.id);
            if(!ids.Contains(episode.entry))errors.Add(episode.id+" 入口不存在");
            foreach(var step in episode.steps)
            {
                Check(step.next);Check(step.alternate);
                foreach(var choice in step.choices)Check(choice.next);
                if(step.kind==StoryStepKind.Choice&&(step.choices.Count<1||step.choices.Count>2))errors.Add(episode.id+"/"+step.id+" 选项数量须为 1–2");
                void Check(string id){if(!string.IsNullOrEmpty(id)&&!ids.Contains(id))errors.Add(episode.id+"/"+step.id+" 指向不存在的节点："+id);}
            }
        }
        return errors;
    }
}

/// <summary>只在编辑器中逐段验收演出；不会给正式游戏添加演示按钮。</summary>
public sealed class StoryPreviewWindow : EditorWindow
{
    private int episode;
    [MenuItem("堂吉诃德/剧情/打开剧情预览窗口")]
    private static void Open()=>GetWindow<StoryPreviewWindow>("剧情预览");
    private void OnGUI()
    {
        EditorGUILayout.HelpBox("打开 Main 场景，运行并点开始游戏。然后可以在这里单独预览某段剧情；停止运行不会改变关卡资产。",MessageType.Info);
        var library=AssetDatabase.LoadAssetAtPath<PresentationLibrary>("Assets/Resources/UI/PresentationLibrary.asset");
        if(!library)return;
        episode=EditorGUILayout.Popup("预览章节",Mathf.Clamp(episode,0,library.episodes.Count-1),library.episodes.ConvertAll(e=>e.title).ToArray());
        var runtime=PresentationRuntime.Instance;
        using(new EditorGUI.DisabledScope(!EditorApplication.isPlaying||!runtime||!runtime.CanPresent))
        {
            if(GUILayout.Button("播放选中章节")){runtime.Director.Clear();runtime.Director.Enqueue(library.episodes[episode].id);}
            if(GUILayout.Button("结束预览并恢复操作"))runtime.CancelCurrentStory();
            if(GUILayout.Button("重置本次运行的全局教学标记"))runtime.ResetTutorialProgress();
        }
        if(runtime&&runtime.Director!=null)EditorGUILayout.LabelField("当前节点",(runtime.Director.EpisodeId??"游玩中")+" / "+runtime.Director.StepId);
        Repaint();
    }
}
#endif
