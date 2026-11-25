#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using GamePresentation;
using LevelKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>仅在编辑器生成一次初始资产；运行时不会 new UI，也不会覆盖美术已经修改的预制体。</summary>
public static class PresentationV4Setup
{
    private const string Folder = "Assets/Resources/UI/";
    private static readonly Color Paper = new Color(1f, .94f, .78f);
    private static Font Font => AssetDatabase.LoadAssetAtPath<Font>(UIFontUtility.FontAssetPath);

    [MenuItem("堂吉诃德/剧情/补齐演出预制体（保留现有美术修改）")]
    public static void BuildAssets()
    {
        var library = AssetDatabase.LoadAssetAtPath<PresentationLibrary>(Folder + "PresentationLibrary.asset");
        if (!library)
        {
            library = ScriptableObject.CreateInstance<PresentationLibrary>();
            JsonUtility.FromJsonOverwrite(File.ReadAllText(Folder + "PresentationV4.json"), library);
            library.heroPortrait = StoryReferenceLayout.HeroPortrait();
            library.heroSpeechlessPortrait = StoryReferenceLayout.HeroPortrait(StoryExpression.Speechless);
            AddDefaultHeroExpressions(library);
            AssetDatabase.CreateAsset(library, Folder + "PresentationLibrary.asset");
        }
        var story = AssetDatabase.LoadAssetAtPath<StoryPanelView>(Folder + "StoryPanel.prefab");
        if (!story) story = BuildStoryPanel();
        var head = AssetDatabase.LoadAssetAtPath<BubbleDialogView>(Folder + "HeadBubble.prefab");
        if (!head)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "BubbleDialog.prefab"));
            instance.name = "HeadBubble";
            instance.GetComponent<Canvas>().sortingOrder = 2000;
            foreach (var graphic in instance.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            instance.transform.Find("Tail").gameObject.SetActive(false);
            var paper = instance.transform.Find("Background").GetComponent<Image>();
            paper.sprite = AssetDatabase.LoadAllAssetsAtPath(Folder + "UI/uisc/IMG_1197.png")
                .OfType<Sprite>().First(sprite => sprite.name == "IMG_1197_0");
            paper.type = UnityEngine.UI.Image.Type.Sliced;
            paper.pixelsPerUnitMultiplier = 1.65f;
            paper.rectTransform.offsetMin = paper.rectTransform.offsetMax = Vector2.zero;
            var serialized = new SerializedObject(instance.GetComponent<BubbleDialogView>());
            serialized.FindProperty("minWidth").floatValue = 128;
            serialized.FindProperty("maxWidth").floatValue = 440;
            serialized.FindProperty("padding").vector2Value = new Vector2(52f, 42f);
            serialized.FindProperty("contentLift").floatValue = 6f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            head = PrefabUtility.SaveAsPrefabAsset(instance, Folder + "HeadBubble.prefab").GetComponent<BubbleDialogView>();
            Object.DestroyImmediate(instance);
        }
        var tray = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "GameplayItemTray.prefab");
        if (!tray) tray = BuildTray();
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "PresentationRuntime.prefab"))
        {
            var root = new GameObject("剧情演出系统");
            var runtime = root.AddComponent<PresentationRuntime>();
            runtime.library = library;
            runtime.storyView = ((GameObject)PrefabUtility.InstantiatePrefab(story.gameObject, root.transform)).GetComponent<StoryPanelView>();
            runtime.headView = ((GameObject)PrefabUtility.InstantiatePrefab(head.gameObject, root.transform)).GetComponent<BubbleDialogView>();
            runtime.inventoryTray = (GameObject)PrefabUtility.InstantiatePrefab(tray, root.transform);
            runtime.cameraPerformance = root.AddComponent<CameraPerformanceManager>();
            runtime.cameraAnchor = root.AddComponent<PresentationCameraAnchor>();
            runtime.storyView.GetComponent<Canvas>().enabled = false;
            runtime.headView.SetVisible(false); runtime.inventoryTray.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, Folder + "PresentationRuntime.prefab");
            Object.DestroyImmediate(root);
        }
        else
        {
            var existing = AssetDatabase.LoadAssetAtPath<PresentationRuntime>(Folder + "PresentationRuntime.prefab");
            if (!existing.library)
            {
                var config = new SerializedObject(existing); config.FindProperty("library").objectReferenceValue = library;
                config.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
    }

    private static void AddDefaultHeroExpressions(PresentationLibrary library)
    {
        var presets = new (StoryExpression expression, string artName)[]
        {
            (StoryExpression.Angry, "生气_怒气图标"),
            (StoryExpression.Puzzled, "疑惑"),
            (StoryExpression.PuzzledQuestionMark, "疑惑_问号"),
            (StoryExpression.ShockedTalking, "震惊_说话"),
            (StoryExpression.ShockedMouth, "震惊_震惊嘴"),
            (StoryExpression.Excited, "兴奋_兴奋嘴_兴奋特效"),
            (StoryExpression.Disgusted, "嫌弃"),
            (StoryExpression.NeutralClosedMouth, "通常_闭嘴"),
            (StoryExpression.NeutralShockedMouth, "通常_震惊嘴"),
            (StoryExpression.NeutralPainedMouth, "通常_难受嘴"),
            (StoryExpression.NeutralSpeechlessMouth, "通常_无语嘴"),
            (StoryExpression.NeutralExcitedMouth, "通常_兴奋嘴"),
            (StoryExpression.ClosedEyesPained, "紧闭眼_难受嘴"),
            (StoryExpression.ClosedEyesExcitedEffect, "紧闭眼_兴奋嘴_兴奋特效"),
            (StoryExpression.ClosedEyesExcited, "紧闭眼_兴奋嘴"),
            (StoryExpression.ClosedEyesShocked, "紧闭眼_震惊嘴"),
            (StoryExpression.SpeechlessClosedMouthEllipsis, "无语_闭嘴_省略号"),
            (StoryExpression.SpeechlessMouthEllipsis, "无语_无语嘴_省略号"),
            (StoryExpression.SpeechlessMouth, "无语_无语嘴")
        };
        foreach (var (expression, artName) in presets)
        {
            var portrait = AssetDatabase.LoadAllAssetsAtPath(Folder + "UI/bqcf/主角_" + artName + ".png")
                .OfType<Sprite>().FirstOrDefault();
            if (portrait) library.heroExpressions.Add(new StoryPortraitVariant { expression = expression, portrait = portrait });
        }
    }

    private static StoryPanelView BuildStoryPanel()
    {
        var root = CanvasRoot("StoryPanel", 1150);
        var view = root.AddComponent<StoryPanelView>(); view.group = root.AddComponent<CanvasGroup>();
        var black = Image("BlackTitle", root.transform, Vector2.zero, Vector2.one, Color.black);
        view.blackPanel = black.gameObject; view.blackGroup = black.gameObject.AddComponent<CanvasGroup>();
        view.endingImage = Image("EndingImage_替换结束图", black.transform, Vector2.zero, Vector2.one, Color.white);
        view.endingImage.preserveAspect = true; view.endingImage.raycastTarget = false;
        view.blackText = Label("BlackText", black.transform, new Vector2(.12f,.35f), new Vector2(.88f,.65f), "很久以前，荒原边上有座安静的小镇。", 38, TextAnchor.MiddleCenter);
        view.blackContinueButton = Button("BlackContinueButton", black.transform, new Vector2(.77f,.08f), new Vector2(.94f,.16f), "点击继续");
        var dialogue = Image("DialogueLayer", root.transform, Vector2.zero, Vector2.one, new Color(0,0,0,0));
        view.dialoguePanel = dialogue.gameObject;
        var frame = Image("BottomFrame_替换对话框PNG", dialogue.transform, Vector2.zero, new Vector2(1,.43f), new Color(.12f,.08f,.065f,.96f));
        view.portrait = Image("Portrait_替换透明立绘PNG", frame.transform, new Vector2(.02f,.04f), new Vector2(.22f,.98f), Color.white);
        view.portrait.preserveAspect = true; view.portrait.raycastTarget = false;
        view.speakerText = Label("Speaker", frame.transform, new Vector2(.25f,.8f), new Vector2(.95f,.98f), "堂吉诃德", 34);
        view.dialogueText = Label("Content_由剧情配置赋值", frame.transform, new Vector2(.25f,.2f), new Vector2(.95f,.79f), "在剧情与台词配置中编辑对白。", 28, TextAnchor.UpperLeft);
        view.continueButton = Button("ContinueButton", frame.transform, new Vector2(.81f,.03f), new Vector2(.96f,.17f), "继续 ›");
        var choices = Rect("Choices", frame.transform, new Vector2(.25f,.04f), new Vector2(.95f,.76f)); view.choicesPanel = choices.gameObject;
        view.choiceA = Button("ChoiceA", choices, new Vector2(0,.51f), Vector2.one, "选项 A");
        view.choiceB = Button("ChoiceB", choices, Vector2.zero, new Vector2(1,.48f), "选项 B");
        view.choiceAText = view.choiceA.GetComponentInChildren<Text>(); view.choiceBText = view.choiceB.GetComponentInChildren<Text>();
        view.choiceAText.fontSize = view.choiceBText.fontSize = 25;
        var narrator = Image("NarratorLayer", root.transform, Vector2.zero, Vector2.one, new Color(0,0,0,0));
        view.narratorPanel = narrator.gameObject;
        var narrationFrame = Image("NarrationTop", narrator.transform, new Vector2(.08f,.74f), new Vector2(.92f,.96f), new Color(.05f,.05f,.05f,.92f));
        view.narratorText = Label("NarratorText", narrationFrame.transform, new Vector2(.035f,.22f), new Vector2(.965f,.97f), "旁白文字", 32, TextAnchor.MiddleLeft);
        view.narratorContinueButton = Button("NarratorContinueButton", narrationFrame.transform, new Vector2(.79f,.01f), new Vector2(.98f,.23f), "继续 ›");
        view.resetHint = Label("ResetHint", root.transform, new Vector2(.22f,.87f), new Vector2(.78f,.98f), "按 R 键 / 双击主角重置回存档点", 30, TextAnchor.MiddleCenter);
        view.resetHint.gameObject.AddComponent<Outline>().effectColor = Color.black;
        StoryReferenceLayout.ApplyView(view);
        view.Hide(); view.dialoguePanel.SetActive(true); view.resetHint.gameObject.SetActive(false);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "StoryPanel.prefab"); Object.DestroyImmediate(root);
        return prefab.GetComponent<StoryPanelView>();
    }
    private static GameObject BuildTray()
    {
        var root = CanvasRoot("GameplayItemTray", 1050);
        var pause = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "PausePanel.prefab");
        var slots = pause.GetComponentsInChildren<ItemSlotUI>(true).OrderBy(s => (int)s.ItemId).ToArray();
        for (int i=0; i<slots.Length; i++)
        {
            var holder = Rect("道具框_" + slots[i].ItemId, root.transform, Vector2.one, Vector2.one);
            holder.pivot = Vector2.one; holder.sizeDelta = new Vector2(340,92); holder.anchoredPosition = new Vector2(-28,-25-i*102);
            var original=slots[i].GetComponent<Image>();var background=holder.gameObject.AddComponent<Image>();
            if(original){background.sprite=original.sprite;background.color=original.color;}
            var source=new SerializedObject(slots[i]);
            var lockedSource=(GameObject)source.FindProperty("lockedIcon").objectReferenceValue;
            var unlockedSource=(GameObject)source.FindProperty("unlockedIcon").objectReferenceValue;
            var locked=Image("QuestionMark",holder,new Vector2(.025f,.08f),new Vector2(.255f,.92f),Color.white);
            var unlocked=Image("ItemIcon",holder,new Vector2(.025f,.08f),new Vector2(.255f,.92f),Color.white);
            if(lockedSource)locked.sprite=lockedSource.GetComponent<Image>()?.sprite;
            if(unlockedSource)unlocked.sprite=unlockedSource.GetComponent<Image>()?.sprite;
            locked.preserveAspect=unlocked.preserveAspect=true;locked.raycastTarget=unlocked.raycastTarget=false;
            Label("ItemName",holder,new Vector2(.28f,.1f),new Vector2(.96f,.9f),i==0?"硬纸板":i==1?"跳跃键":"驽辛难得",28,TextAnchor.MiddleCenter);
            var item=holder.gameObject.AddComponent<ItemSlotUI>();var tooltip=holder.GetComponent<TooltipTrigger>();
            tooltip.tipText=slots[i].GetComponent<TooltipTrigger>().tipText;tooltip.isUnlocked=i==0;
            var data=new SerializedObject(item);data.FindProperty("itemId").enumValueIndex=(int)slots[i].ItemId;
            data.FindProperty("lockedIcon").objectReferenceValue=locked.gameObject;data.FindProperty("unlockedIcon").objectReferenceValue=unlocked.gameObject;
            data.FindProperty("tooltip").objectReferenceValue=tooltip;data.ApplyModifiedPropertiesWithoutUndo();
            locked.gameObject.SetActive(i!=0);unlocked.gameObject.SetActive(i==0);
        }
        StoryReferenceLayout.ApplyTray(root);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "GameplayItemTray.prefab"); Object.DestroyImmediate(root); return prefab;
    }
    private static GameObject CanvasRoot(string name, int sort)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = sort;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f; return root;
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent,false); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
    }
    private static Image Image(string name, Transform parent, Vector2 min, Vector2 max, Color color)
    { var image = Rect(name,parent,min,max).gameObject.AddComponent<Image>(); image.color=color; return image; }
    private static Text Label(string name, Transform parent, Vector2 min, Vector2 max, string text, int size, TextAnchor align=TextAnchor.MiddleLeft)
    {
        var label = Rect(name,parent,min,max).gameObject.AddComponent<Text>(); label.font=Font; label.fontSize=size;
        label.color=Paper; label.text=text; label.alignment=align; label.raycastTarget=false;
        label.horizontalOverflow=HorizontalWrapMode.Wrap; label.verticalOverflow=VerticalWrapMode.Truncate; return label;
    }
    private static Button Button(string name, Transform parent, Vector2 min, Vector2 max, string text)
    {
        var image = Image(name,parent,min,max,new Color(.31f,.18f,.12f,.97f));
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic=image;
        var navigation = button.navigation; navigation.mode=Navigation.Mode.None; button.navigation=navigation;
        Label("Label",image.transform,new Vector2(.03f,.03f),new Vector2(.97f,.97f),text,28,TextAnchor.MiddleCenter); return button;
    }

    // 在独立验证副本里执行，生成场景增量。主项目只合入新物体，不整体重存旧场景。
    public static void BuildValidationScene()
    {
        try
        {
            BuildAssets();
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            if (Object.FindAnyObjectByType<PresentationSceneBinding>()) throw new Exception("场景已存在演出绑定，避免重复生成。");
            var root = new GameObject("V4剧情场景绑定"); var binding = root.AddComponent<PresentationSceneBinding>();
            binding.runtimePrefab = AssetDatabase.LoadAssetAtPath<PresentationRuntime>(Folder + "PresentationRuntime.prefab");
            binding.player = Object.FindAnyObjectByType<PlayerActionController>(); binding.levelCamera=Camera.main;
            binding.entranceCamera = GameObject.Find("相机切换3")?.GetComponent<CameraTarget>();
            binding.villageStone = GameObject.Find("石块1").GetComponent<Rigidbody2D>();
            binding.checkpoints = Object.FindObjectsByType<Campfire>(FindObjectsSortMode.None).OrderBy(c=>c.transform.position.x).ToArray();
            binding.legacyInnQuest = Object.FindAnyObjectByType<InnQuest>();
            binding.legacyQuestItems=Object.FindObjectsByType<InnQuestItem>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(x=>x.gameObject).ToArray();
            binding.shoes = Object.FindAnyObjectByType<BreakPickup>(FindObjectsInactive.Include).gameObject;
            var wall=Object.FindAnyObjectByType<BreakableBlock>();
            var stone=binding.villageStone.transform;
            binding.targets=new[] {
                Target("village_ground",new Vector2(-17.65f,-1.4f),horizontalOnly:true),
                Target("village_pit",new Vector2(-15,-1.4f),horizontalOnly:true,orthographicSize:3.8f),
                new PresentationSceneBinding.Target{key="village_stone",value=stone,horizontalOnly=true},
                Target("lower_platform",new Vector2(41.41f,-2.64f)), Target("jump_key",new Vector2(46,-1.5f)),
                new PresentationSceneBinding.Target{key="inn_wall",value=wall.transform}, Target("inn_stairs",new Vector2(81.47f,9.26f)) };
            foreach (var target in binding.targets) if(target.value!=stone && target.value!=wall.transform) target.value.SetParent(root.transform,true);
            binding.shoesPoint=RectMarker("V4跑鞋位置_美术关卡可拖动",root.transform,new Vector2(77,23));
            SignalZone(root.transform,"剧情触发区_进入树林","forest.enter",new Vector2(10,-1.5f),new Vector2(.4f,3));
            SignalZone(root.transform,"剧情触发区_进入跳跃小关","jump.enter",new Vector2(36.5f,-1.5f),new Vector2(.4f,3));
            SignalZone(root.transform,"剧情触发区_发现跳跃键","jump.lower",new Vector2(40,-1.5f),new Vector2(1,3));
            SignalZone(root.transform,"剧情触发区_旅馆前平台","inn.approach",new Vector2(62.2f,7.35f),new Vector2(1.1f,2.5f));
            SignalZone(root.transform,"剧情触发区_破墙后结束","inn.exit",new Vector2(93,7),new Vector2(1,4));
            foreach(var name in new[]{"左侧木板","右侧木板","树木1","树木2","树木3"})
            {
                var target=GameObject.Find(name);
                if(!target)continue;
                var observer=new GameObject("小台词观察_"+name).AddComponent<BarkWorldObserver>();
                observer.transform.SetParent(root.transform);
                observer.kind=name.Contains("树木")?BarkWorldObserver.Kind.RotatingTree:BarkWorldObserver.Kind.Bridge;
                observer.target=target.GetComponent<Rigidbody2D>();
            }
            PrefabUtility.InstantiatePrefab(binding.runtimePrefab.gameObject,scene);
            EditorSceneManager.SaveScene(scene);
            File.WriteAllText("presentation-build-result.txt","OK：已生成 V4 配置、预制体与场景增量。"); EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);File.WriteAllText("presentation-build-result.txt",e.ToString());EditorApplication.Exit(1);}
    }
    private static PresentationSceneBinding.Target Target(string key, Vector2 position, bool horizontalOnly = false, float orthographicSize = 0f) =>
        new PresentationSceneBinding.Target{key=key,value=RectMarker(key,null,position),horizontalOnly=horizontalOnly,orthographicSize=orthographicSize};
    private static Transform RectMarker(string name, Transform parent, Vector2 position)
    {var marker=new GameObject(name).transform;marker.SetParent(parent);marker.position=position;return marker;}
    private static void SignalZone(Transform parent,string name,string signal,Vector2 position,Vector2 size)
    {
        var go=new GameObject(name);go.transform.SetParent(parent);go.transform.position=position;
        var trigger=go.AddComponent<StorySignalTrigger>();trigger.signal=signal;go.GetComponent<BoxCollider2D>().size=size;
    }
}
#endif
