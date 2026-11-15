# UI Prefab 接入说明（Unity 6.6）

`SampleScene` 的 `GameFlowTemplate` 已直接引用三个 prefab 资产。运行时会实例化 Inspector 指定的 prefab，不通过字符串路径搜索，也不依赖 Hierarchy 对象名。

Inspector 字段对应：

- Main Menu Prefab → `Assets/Resources/UI/MainMenu.prefab`
- Pause Panel Prefab → `Assets/Resources/UI/PausePanel.prefab`
- HUD Panel Prefab → `Assets/Resources/UI/HudPanel.prefab`

替换 UI 资产时，把新的 prefab 从 Project 窗口拖到对应 Inspector 字段。美术修改 prefab 并保存后，下次运行会实例化该版本。无需将运行时实例拖回场景，也不要为按钮重复添加 OnClick。

按钮和面板对象名需保留脚本识别的名称，例如 `StartButton`、`ReturnToMenuButton`、`HealthSlider`、`ResultPanel`、`ConfirmButton`。血条填充图在 `HudPanel/HealthSlider/Fill Area/Fill`。

功能接口：

```csharp
flow.SetHealth(currentHealth, maxHealth);
flow.ShowVictory();
flow.ShowDefeat();
flow.ShowDialogue("角色名", "台词内容");
```

实际关卡加入 Build Profiles 场景列表后，在 `Gameplay Scene Name` 填写场景名。
