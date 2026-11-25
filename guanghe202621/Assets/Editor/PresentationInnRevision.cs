#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using GamePresentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>第三幕预制体的编辑器制作工具。生成的控件会保存为资产，运行时不创建 UI。</summary>
public static class PresentationInnRevision
{
    private const string Folder = "Assets/Resources/UI/";
    [Serializable] private sealed class RevisionData
    {
        public StoryEpisode[] episodes;
        public BarkRule[] barks;
    }

    [MenuItem("堂吉诃德/剧情/应用帕洛梅克第三幕剧本与字幕预制体")]
    public static void Apply()
    {
        var library = AssetDatabase.LoadAssetAtPath<PresentationLibrary>(Folder + "PresentationLibrary.asset");
        var data = JsonUtility.FromJson<RevisionData>(File.ReadAllText(Folder + "PresentationV4.json"));
        foreach (var episode in data.episodes.Where(e => e.id.StartsWith("inn.", StringComparison.Ordinal)))
        {
            int index = library.episodes.FindIndex(e => e.id == episode.id);
            if (index >= 0) library.episodes[index] = episode;
            else library.episodes.Add(episode);
        }
        foreach (var bark in data.barks.Where(b => b.id.StartsWith("inn_", StringComparison.Ordinal) || b.id == "wall_no_shoes"))
        {
            int index = library.barks.FindIndex(b => b.id == bark.id);
            if (index >= 0) library.barks[index] = bark;
            else library.barks.Add(bark);
        }
        EditorUtility.SetDirty(library);
        var root = PrefabUtility.LoadPrefabContents(Folder + "StoryPanel.prefab");
        try
        {
            ConfigureView(root.GetComponent<StoryPanelView>());
            PrefabUtility.SaveAsPrefabAsset(root, Folder + "StoryPanel.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("第三幕已更新：进门前天空字幕、帕洛梅克委托、失物气泡和交差后领鞋。");
    }

    public static void ConfigureView(StoryPanelView view)
    {
        // 只删除字幕自身的描边/阴影，纸框和美术图片的边线继续由贴图负责。
        foreach (var label in new[] { view.resetHint, view.blackText, view.narratorText })
            foreach (var effect in label.GetComponents<Shadow>()) Object.DestroyImmediate(effect);

        var sky = view.transform.Find("SkySubtitle");
        if (!sky)
        {
            sky = Panel("SkySubtitle", view.transform, new Vector2(.26f, .62f), new Vector2(.74f, .88f));
            var label = Label("SkyText", sky, new Vector2(.04f, .08f), new Vector2(.96f, .92f), 40);
            label.text = "天空旁白在这里显示";
            label.gameObject.AddComponent<StoryTextReveal>();
        }
        view.skySubtitlePanel = sky.gameObject;
        view.skySubtitleText = sky.GetComponentInChildren<Text>(true);
        sky.gameObject.SetActive(false);

        var notice = view.transform.Find("EquipmentNotice");
        if (!notice)
        {
            notice = Panel("EquipmentNotice", view.transform, new Vector2(.33f, .82f), new Vector2(.70f, .94f));
            var background = notice.gameObject.AddComponent<Image>();
            background.sprite = Sprite("UI/uisc/IMG_1079.png");
            background.raycastTarget = false;
            var icon = Panel("ShoeIcon", notice, new Vector2(.04f, .15f), new Vector2(.22f, .85f)).gameObject.AddComponent<Image>();
            icon.sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/驽骍难得.png").OfType<Sprite>().FirstOrDefault();
            icon.preserveAspect = true; icon.raycastTarget = false;
            var label = Label("NoticeText", notice, new Vector2(.24f, .20f), new Vector2(.91f, .80f), 32);
            label.text = "获得装备：【驽辛难得】";
        }
        view.equipmentNotice = notice.gameObject;
        notice.gameObject.SetActive(false);
        StoryPortraitSetup.ConfigureView(view);
    }

    private static Sprite Sprite(string path) => AssetDatabase.LoadAllAssetsAtPath(Folder + path).OfType<Sprite>().FirstOrDefault();
    private static RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
    private static Text Label(string name, Transform parent, Vector2 min, Vector2 max, int size)
    {
        var text = Panel(name, parent, min, max).gameObject.AddComponent<Text>();
        text.font = AssetDatabase.LoadAssetAtPath<Font>(UIFontUtility.FontAssetPath);
        text.color = new Color(.24f, .12f, .09f, 1f);
        text.fontSize = size; text.lineSpacing = 1.2f; text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false; text.resizeTextForBestFit = false;
        return text;
    }
}
#endif
