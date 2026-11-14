# Unity 6.6 游戏基础框架

## 已包含

- `GameFlowTemplate`：运行时生成的主菜单、HUD、暂停、设置、操作说明、版本、确认、台词、通关和失败界面。
- `GameStateManager`：集中管理 MainMenu、Playing、Paused、Victory、Defeat，暂停状态自动设置 `Time.timeScale`。
- `GameEventCenter`：血量、状态、台词和关卡重置事件。
- `AudioManager`：音乐/音效音量持久化、音频播放接口和无资源 UI 点击提示音。
- `SceneTransitionManager`：黑幕淡入淡出、叠加加载/重载/卸载关卡场景。

## 血量接口

从场景中的 `GameFlowTemplate` 调用：

```csharp
flow.SetHealth(currentHealth, maxHealth);
```

UI 血条在进入游戏时启用并初始化为满血。Slider 的 Fill Image 和 RectTransform 已由框架创建并绑定，锚点落在屏幕内。其他脚本也可以订阅 `GameEventCenter.HealthChanged`。

## 配置实际关卡

默认 `gameplaySceneName` 留空时，点击开始会显示内置占位游戏画面。要加载自己的关卡：

1. 将关卡场景加入 Unity 6.6 的 Build Profiles 场景列表。
2. 在 `SampleScene` 的 `GameFlowTemplate` 组件中填写完全匹配的 `Gameplay Scene Name`。
3. 开始游戏会淡入黑幕后以 Additive 模式加载关卡；返回菜单会卸载它。重置会卸载并重新加载该关卡。

关卡场景可以包含自己的 Camera 和 AudioListener；框架会在进入关卡时停用菜单场景上的 Camera/AudioListener，回菜单时恢复。

## 从游戏逻辑触发界面

```csharp
flow.ShowVictory();
flow.ShowDefeat();
flow.ShowDialogue("角色名", "这里是台词内容");
```

`AudioManager` 提供 `PlayMusic(AudioClip)`、`PlaySfx(AudioClip)`、`SetMusicVolume(float)` 和 `SetSfxVolume(float)`。
