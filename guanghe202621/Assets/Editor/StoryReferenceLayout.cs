#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using GamePresentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>参考图的初始布局，只在编辑器制作预制体时执行，运行时不创建控件。</summary>
public static class StoryReferenceLayout
{
    private const string Folder = "Assets/Resources/UI/";
    private static readonly Color Ink = new Color(.24f,.12f,.09f,1);
    private static Sprite Sprite(string relative) => AssetDatabase.LoadAllAssetsAtPath(Folder + "UI/" + relative).OfType<Sprite>().FirstOrDefault();
    internal static Sprite HeroPortrait(StoryExpression expression = StoryExpression.Default) =>
        Sprite(expression == StoryExpression.Speechless ? "bqcf/主角_无语_说话.png" : "bqcf/主角_通常_说话.png");
    private static void Area(RectTransform rect, Vector2 min, Vector2 max)
    { rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;rect.localScale=Vector3.one; }
    private static void Corner(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {rect.anchorMin=rect.anchorMax=anchor;rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;rect.localScale=Vector3.one;}

    public static void ApplyView(StoryPanelView view)
    {
        var frame = view.dialogueText.transform.parent.GetComponent<Image>();
        view.dialogueFrame = frame.gameObject;
        frame.name="BottomFrame_纸张对话框";
        Area(frame.rectTransform,new Vector2(.10f,.015f),new Vector2(.97f,.42f));
        frame.sprite=Sprite("uisc/IMG_1126.png");frame.color=Color.white;frame.type=Image.Type.Simple;frame.preserveAspect=false;
        view.dialoguePanel.GetComponent<Image>().raycastTarget=false;

        view.portrait.transform.SetParent(view.dialoguePanel.transform,false);
        view.portrait.transform.SetAsFirstSibling();
        Area(view.portrait.rectTransform,new Vector2(.065f,.19f),new Vector2(.45f,.81f));
        view.portrait.sprite=HeroPortrait();
        view.portrait.preserveAspect=true;view.portrait.raycastTarget=false;
        ApplyDialogueTextLayout(view, frame);

        view.dialogueAdvanceButton=frame.GetComponent<Button>();
        if(!view.dialogueAdvanceButton)view.dialogueAdvanceButton=frame.gameObject.AddComponent<Button>();
        view.dialogueAdvanceButton.targetGraphic=frame;view.dialogueAdvanceButton.transition=Selectable.Transition.None;
        var navigation=view.dialogueAdvanceButton.navigation;navigation.mode=Navigation.Mode.None;view.dialogueAdvanceButton.navigation=navigation;
        Area((RectTransform)view.continueButton.transform,new Vector2(.72f,.067f),new Vector2(.84f,.145f));
        view.continueButton.GetComponent<Image>().color=Color.clear;
        var continueText=view.continueButton.GetComponentInChildren<Text>();continueText.color=Ink;continueText.fontSize=26;continueText.text="继续 ›";

        view.choicesPanel.transform.SetParent(view.dialoguePanel.transform,false);view.choicesPanel.transform.SetAsLastSibling();
        Area((RectTransform)view.choicesPanel.transform,new Vector2(.54f,.40f),new Vector2(.96f,.81f));
        Area((RectTransform)view.choiceA.transform,new Vector2(0,.59f),new Vector2(1,.98f));
        Area((RectTransform)view.choiceB.transform,new Vector2(0,.04f),new Vector2(1,.43f));
        var images=new[]{view.choiceA.GetComponent<Image>(),view.choiceB.GetComponent<Image>()};
        images[0].sprite=Sprite("uisc/IMG_1180.png");images[1].sprite=Sprite("uisc/IMG_1183.png");
        foreach(var image in images){image.color=Color.white;image.type=Image.Type.Simple;image.preserveAspect=false;}
        foreach(var text in new[]{view.choiceAText,view.choiceBText})
        {
            Area(text.rectTransform,new Vector2(.09f,.19f),new Vector2(.87f,.79f));
            text.color=Ink;text.fontSize=40;text.alignment=TextAnchor.MiddleLeft;text.lineSpacing=1.14f;
            text.resizeTextForBestFit=true;text.resizeTextMinSize=24;text.resizeTextMaxSize=40;
        }
        view.choiceAPrefix="A. ";view.choiceBPrefix="B. ";
        view.blackText.lineSpacing=1.15f;
        foreach (var text in new[] { view.blackText, view.narratorText, view.dialogueText })
            if (!text.GetComponent<StoryTextReveal>()) text.gameObject.AddComponent<StoryTextReveal>();
        view.resetHint.color = Ink;
        view.resetHint.fontSize = 34;
        if (view.resetHint.TryGetComponent<Outline>(out var hintOutline))
            hintOutline.effectColor = new Color(1f, .94f, .78f, .7f);
        ApplyNarrationLayout(view);
        UIFontUtility.ApplyToHierarchy(view.gameObject);
    }

    private static void ApplyNarrationLayout(StoryPanelView view)
    {
        var text = view.narratorText;
        var frame = text.transform.parent.GetComponent<Image>();
        frame.name = "NarrationFrame";
        Area(frame.rectTransform, new Vector2(.23f, .34f), new Vector2(.77f, .66f));
        // IMG_1124 还切出了一小片边角，旁白使用完整的纸框。
        frame.sprite = AssetDatabase.LoadAllAssetsAtPath(Folder + "UI/uisc/IMG_1124.png")
            .OfType<Sprite>().First(sprite => sprite.name == "IMG_1124_0");
        frame.color = Color.white;

        Area(text.rectTransform, new Vector2(.14f, .23f), new Vector2(.89f, .79f));
        text.color = Ink;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 42;
        text.lineSpacing = 1.18f;
        text.resizeTextForBestFit = false;

        if (view.narratorContinueButton) view.narratorContinueButton.gameObject.SetActive(false);
    }

    private static void ApplyDialogueTextLayout(StoryPanelView view, Image frame)
    {
        // 小纸牌和大纸卷是独立的 UI 部件，可以分别在预制体里调整。
        var nameplateTransform = frame.transform.Find("SpeakerNameplate");
        var nameplate = nameplateTransform
            ? nameplateTransform.GetComponent<Image>()
            : new GameObject("SpeakerNameplate", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        nameplate.transform.SetParent(frame.transform, false);
        Area(nameplate.rectTransform, new Vector2(.07f, .52f), new Vector2(.24f, .81f));
        nameplate.sprite = Sprite("uisc/IMG_1079.png");
        nameplate.raycastTarget = false;

        var speaker = view.speakerText;
        speaker.transform.SetParent(nameplate.transform, false);
        Area(speaker.rectTransform, new Vector2(.215f, .23f), new Vector2(.94f, .76f));
        speaker.alignment = TextAnchor.MiddleCenter;
        speaker.fontSize = 48;
        speaker.resizeTextForBestFit = true;
        speaker.resizeTextMinSize = 44;
        speaker.resizeTextMaxSize = 48;
        speaker.color = Ink;

        var body = view.dialogueText;
        Area(body.rectTransform, new Vector2(.112f, .21f), new Vector2(.80f, .475f));
        body.alignment = TextAnchor.UpperLeft;
        body.fontSize = 40;
        body.resizeTextForBestFit = false;
        body.lineSpacing = 1.24f;
        if (!body.GetComponent<StoryDialogueLetterSpacing>()) body.gameObject.AddComponent<StoryDialogueLetterSpacing>();
        body.color = Ink;
    }

    public static void ApplyTray(GameObject root)
    {
        var slots=root.GetComponentsInChildren<ItemSlotUI>(true).OrderBy(s=>(int)s.ItemId).ToArray();
        for(int i=0;i<slots.Length;i++)
        {
            var item=slots[i];Corner((RectTransform)item.transform,new Vector2(0,1),new Vector2(255+i*104,-96),new Vector2(88,88));
            var background=item.GetComponent<Image>();background.sprite=null;background.color=Color.clear;background.raycastTarget=true;
            var data=new SerializedObject(item);
            foreach(string field in new[]{"lockedIcon","unlockedIcon"})
            {
                var icon=(GameObject)data.FindProperty(field).objectReferenceValue;
                if(icon){Area((RectTransform)icon.transform,Vector2.zero,Vector2.one);icon.GetComponent<Image>().preserveAspect=true;}
            }
            foreach(var label in item.GetComponentsInChildren<Text>(true))label.gameObject.SetActive(false);
        }
    }
    private static void ApplyHud(GameObject root)
    {
        var pause=root.GetComponentsInChildren<Button>(true).First(b=>b.name=="PauseButton");
        var reset=root.GetComponentsInChildren<Button>(true).First(b=>b.name=="ResetButton");
        Corner((RectTransform)pause.transform,new Vector2(0,1),new Vector2(118,-96),new Vector2(128,138));
        Corner((RectTransform)reset.transform,new Vector2(1,1),new Vector2(-118,-96),new Vector2(128,138));
        foreach(var label in pause.GetComponentsInChildren<Text>(true))label.gameObject.SetActive(false);
        foreach(var label in reset.GetComponentsInChildren<Text>(true))label.gameObject.SetActive(false);
        var icon=reset.transform.Find("Icon")?.GetComponent<Image>();
        if(icon){Area(icon.rectTransform,Vector2.zero,Vector2.one);icon.preserveAspect=true;}
    }
    public static void ApplyInValidation()
    {
        try
        {
            Edit("StoryPanel.prefab",root=>{var view=root.GetComponent<StoryPanelView>();ApplyView(view);view.Hide();view.dialoguePanel.SetActive(true);view.resetHint.gameObject.SetActive(false);});
            Edit("GameplayItemTray.prefab",ApplyTray);Edit("HudPanel.prefab",ApplyHud);
            AssetDatabase.SaveAssets();File.WriteAllText("reference-layout-result.txt","OK：纸张对话框、右侧 A/B 选项、左上圆图标和右上重置按钮已配置。");
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);File.WriteAllText("reference-layout-result.txt",e.ToString());EditorApplication.Exit(1);}
    }
    private static void Edit(string file,Action<GameObject> edit)
    {var root=PrefabUtility.LoadPrefabContents(Folder+file);try{edit(root);PrefabUtility.SaveAsPrefabAsset(root,Folder+file);}finally{PrefabUtility.UnloadPrefabContents(root);}}
}
#endif
