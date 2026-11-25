#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using GamePresentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>只在编辑器绑定素材并保存双人立绘预制体，保留已制作好的对白、按钮和纸框布局。</summary>
public static class StoryPortraitSetup
{
    private const string Folder = "Assets/Resources/UI/";
    [MenuItem("堂吉诃德/剧情/接入双人立绘与帕洛梅克表情")]
    public static void Apply()
    {
        var library = AssetDatabase.LoadAssetAtPath<PresentationLibrary>(Folder + "PresentationLibrary.asset");
        ConfigureLibrary(library);
        EditorUtility.SetDirty(library);
        var root = PrefabUtility.LoadPrefabContents(Folder + "StoryPanel.prefab");
        try
        {
            var view = root.GetComponent<StoryPanelView>();
            ConfigureView(view);
            // 停止播放后打开预制体也能看到双人排版，便于美术直接调整。
            view.dialoguePanel.SetActive(true);
            view.portraitStage.BeginEpisode(true, library.heroPortrait, library.innkeeperPortrait);
            view.portraitStage.Show(false, library.heroPortrait, false, true);
            PrefabUtility.SaveAsPrefabAsset(root, Folder + "StoryPanel.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("双人立绘和四种老板表情已保存到预制体与剧情配置。未发言角色亮度可在 StoryPortraitStage 调整。");
    }

    public static void ConfigureLibrary(PresentationLibrary library)
    {
        library.innkeeperPortrait = Portrait("通常");
        library.innkeeperExpressions = new List<InnkeeperPortraitVariant>
        {
            new InnkeeperPortraitVariant { expression = InnkeeperExpression.Chuckle, portrait = Portrait("嘻嘻") },
            new InnkeeperPortraitVariant { expression = InnkeeperExpression.SelfConscious, portrait = Portrait("心虚") },
            new InnkeeperPortraitVariant { expression = InnkeeperExpression.Troubled, portrait = Portrait("苦恼") }
        };
        foreach (var episode in library.episodes)
        {
            if (episode.id != "inn.intro" && episode.id != "inn.quest_done" && episode.id != "inn.shoes") continue;
            episode.showInnkeeper = true;
            foreach (var step in episode.steps.Where(s => PresentationLibrary.IsInnkeeper(s.speaker)))
            {
                step.innkeeperExpression = InnkeeperExpression.Normal;
                if (episode.id == "inn.intro")
                {
                    if (step.id == "s005") step.innkeeperExpression = InnkeeperExpression.Chuckle;
                    if (step.id == "s006" || step.id == "s008") step.innkeeperExpression = InnkeeperExpression.SelfConscious;
                    if (step.id == "s007") step.innkeeperExpression = InnkeeperExpression.Troubled;
                }
                else step.innkeeperExpression = InnkeeperExpression.Chuckle;
            }
        }
    }

    public static void ConfigureView(StoryPanelView view)
    {
        var stage = view.GetComponent<StoryPortraitStage>();
        if (!stage) stage = view.gameObject.AddComponent<StoryPortraitStage>();
        stage.heroImage = view.portrait;
        if (!stage.innkeeperImage)
        {
            var image = new GameObject("InnkeeperPortrait_帕洛梅克立绘", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(view.dialoguePanel.transform, false);
            image.transform.SetAsFirstSibling();
            image.rectTransform.anchorMin = new Vector2(.535f, .19f);
            image.rectTransform.anchorMax = new Vector2(.985f, .82f);
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
            image.preserveAspect = true; image.raycastTarget = false;
            image.sprite = Portrait("通常");
            stage.innkeeperImage = image;
        }
        view.portraitStage = stage;
    }

    private static Sprite Portrait(string name)
    {
        var sprite = AssetDatabase.LoadAllAssetsAtPath(Folder + "UI/bqcf/YDLB/" + name + ".png").OfType<Sprite>().FirstOrDefault();
        if (!sprite) throw new InvalidOperationException("未找到帕洛梅克表情 Sprite：" + name);
        return sprite;
    }
}
#endif
