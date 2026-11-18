# UI Prefab 接入说明（Unity 6.6）

`SampleScene` 的 `GameFlowTemplate` 已绑定 Canvas 下的三个预置体实例。进入游戏时隐藏主菜单并显示 HUD，返回主菜单时切换回来。

Inspector 字段对应：

- Main Menu Prefab → `Canvas/MainMenu`
- Pause Panel Prefab → `Canvas/PausePanel`
- Hud Panel Prefab → `Canvas/HudPanel`

美术可以直接打开 `Assets/Resources/UI` 下的对应预置体，修改并保存。替换整个面板时，在 Canvas 下换上新预置体，再从 Hierarchy 拖入对应字段。按钮 OnClick 由脚本绑定。

按钮和面板对象名需保留脚本识别的名称，例如 `StartButton`、`ReturnToMenuButton`、`HealthSlider`、`ResultPanel`、`ConfirmButton`。血条填充图在 `HudPanel/HealthSlider/Fill Area/Fill`。

功能接口：

```csharp
flow.SetHealth(currentHealth, maxHealth);
flow.ShowVictory();
flow.ShowDefeat();
flow.ShowDialogue("角色名", "台词内容");
```

实际关卡加入 Build Profiles 场景列表后，在 `Gameplay Scene Name` 填写场景名。

## 音频接入

在 `SampleScene` 里选中 `Main Camera → Audio Manager`，把交付的音频拖进对应槽位。空槽位静音，音乐和音效分别使用现有的音量设置。

| 程序变量 | 播放时机 | 玩法事件 |
| --- | --- | --- |
| `menuMusic` | 打开或返回主菜单时循环播放 | 由游戏流程调用 |
| `gameplayMusic` | 进入关卡时循环播放，暂停和继续时保持播放 | 由游戏流程调用 |
| `turnCharacterSwitch` | TURN 切换角色成功时 | `TurnCharacterSwitch` |
| `footstep` | 走路动画每次脚触地时 | `Footstep` |
| `dash` | 冲刺成功开始时 | `Dash` |
| `jumpTakeoff` | 跳跃成功起跳时 | `JumpTakeoff` |
| `jumpLanding` | 从空中落地时 | `JumpLanding` |
| `springBounce` | 弹簧成功弹起角色时 | `SpringBounce` |
| `thinBoardBreak` | 薄板被冲破时 | `ThinBoardBreak` |

当前项目还没有角色、弹簧和薄板脚本。实现这些动作时，在动作成功的位置发送对应事件。例如冲刺开始时调用：

```csharp
GameEventCenter.RequestSfx(GameplaySfx.Dash);
```
