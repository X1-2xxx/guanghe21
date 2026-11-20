# UI Prefab 接入说明（Unity 6.6）

`SampleScene` 的 `GameFlowTemplate` 已绑定 Canvas 下的三个预置体实例。进入游戏时隐藏主菜单并显示 HUD，返回主菜单时切换回来。

Inspector 字段对应：

- Main Menu Prefab → `Canvas/MainMenu`
- Pause Panel Prefab → `Canvas/PausePanel`
- Hud Panel Prefab → `Canvas/HudPanel`

美术可以直接打开 `Assets/Resources/UI` 下的对应预置体，修改并保存。替换整个面板时，在 Canvas 下换上新预置体，再从 Hierarchy 拖入对应字段。按钮 OnClick 由脚本绑定。

按钮和面板对象名需保留脚本识别的名称，例如 `StartButton`、`ReturnToMenuButton`、`ResultPanel`、`ConfirmButton`。HUD 已移除血条和血量数字。

## 游戏逻辑对接

HUD 已移除演示通关、演示失败、演示受伤和示例台词按钮。正式的通关、失败、台词弹窗通过以下接口触发。

在玩法脚本中添加 Inspector 引用，把场景中的 Main Camera 拖入该字段。

```csharp
[SerializeField] private GameFlowTemplate flow;
```

根据实际玩法结果调用对应接口，每个接口按需要单独调用。

```csharp
flow.SetHealth(currentHealth, maxHealth);
flow.ShowVictory();
flow.ShowDefeat();
flow.ShowDialogue("角色名", "台词内容");
flow.ShowNarration("旁白或任务提示内容");
```

| 接口 | 用途 |
| --- | --- |
| `flow.StartGame()` | 开始游戏，按 Gameplay Scene Name 配置加载关卡或切换当前场景 UI |
| `flow.PauseGame()` | 暂停游戏并打开暂停菜单 |
| `flow.ResumeGame()` | 继续游戏并关闭暂停菜单 |
| `flow.SetHealth(current, maximum)` | 发布血量消息，供玩法系统订阅 HealthChanged |
| `flow.ShowVictory()` | 关卡达成通关条件后显示通关弹窗 |
| `flow.ShowDefeat()` | 角色死亡或满足失败条件后显示失败弹窗 |
| `flow.ShowDialogue(speaker, line)` | 显示真实台词或任务提示 |
| `flow.ShowNarration(line)` | 显示带有“旁白提示”标题的旁白弹窗 |
| `flow.ResetLevel()` | 重置关卡并发布默认血量消息 |
| `flow.ReturnToMainMenu()` | 直接返回主菜单 |

血量、伤害、死亡判定和通关条件由玩法脚本计算。SetHealth 接口继续发布血量消息，当前 HUD 不显示血条。失败条件达成时调用 ShowDefeat。Starting Health 是开始游戏和重置时发布的默认血量，对接时应与角色初始血量一致。

对白和旁白共用 `Assets/Resources/UI/HudPanel.prefab` 内的 `DialoguePanel`。美术可以在预置体中展开它，临时激活后编辑外框、角色名、正文和“继续”按钮，保存前关闭面板。角色名和正文初始为空，游戏运行后由玩法脚本传入内容。

进入游戏后，调用 `flow.ShowDialogue("角色名", "对白内容")` 显示对白，调用 `flow.ShowNarration("旁白内容")` 显示旁白。角色名传空字符串时隐藏角色名区域。玩家点“继续”或按 Esc 可以关闭当前弹窗。

游戏内按 Esc 打开暂停菜单，点击退出游戏后显示二次确认。UI 中的重置、返回主菜单同样有确认步骤，玩法代码直接调用 ResetLevel 或 ReturnToMainMenu 会立即执行相应流程。

玩法脚本可以订阅现有消息，处理游戏状态和关卡重置。

```csharp
private void OnEnable()
{
    GameEventCenter.StateChanged += OnGameStateChanged;
    GameEventCenter.LevelReset += OnLevelReset;
}

private void OnDisable()
{
    GameEventCenter.StateChanged -= OnGameStateChanged;
    GameEventCenter.LevelReset -= OnLevelReset;
}

private void OnGameStateChanged(GameState state)
{
    // 仅 Playing 状态允许移动、攻击或其他玩法输入。
    // 这里接入角色控制器的输入开关。
}

private void OnLevelReset()
{
    // 当前场景模式下，在这里恢复角色出生点、血量和机关。
    // 恢复完成后，用 flow.SetHealth 发布真实血量。
}
```

新启用的玩法脚本可以读取 `GameStateManager.Current` 获取当前状态。暂停时游戏时间停止，返回菜单和通关、失败时也应按状态关闭玩法输入。

实际关卡加入 Build Profiles 场景列表后，在 `Gameplay Scene Name` 填写场景名。

## 音频接入

在 `SampleScene` 里选中 `Main Camera → Audio Manager`，把交付的音频拖进对应槽位。空槽位静音，音乐和音效分别使用现有的音量设置。

新增的三种声音放在 Audio Manager 的“物理块音效”分组，按下面的对应关系拖入音频，完成后保存场景。

| Inspector 槽位 | 拖入的音频 |
| --- | --- |
| Physics Block Impact | 物理块碰撞与落地音效 |
| Physics Block Attach | 粘贴物理块音效，纸张掀动后啪的一声 |
| Physics Block Detach | 分离物理块音效，剪刀剪开连接纸片的声音 |

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
| `physicsBlockImpact` | 物理块碰撞或落地时，共用一段音效 | `PhysicsBlockImpact` |
| `physicsBlockAttach` | 物理块粘贴成功时，纸张掀动后啪的一声 | `PhysicsBlockAttach` |
| `physicsBlockDetach` | 物理块分离成功时，剪刀剪开连接纸片的声音 | `PhysicsBlockDetach` |

当前项目还没有角色、弹簧和薄板脚本。实现这些动作时，在动作成功的位置发送对应事件。例如冲刺开始时调用：

```csharp
GameEventCenter.RequestSfx(GameplaySfx.Dash);
```

物理块动作的播放入口已预留，当前项目还没有对应的物理块玩法脚本。接入时，在实际碰撞或落地、连接成功、断开连接成功的位置，各调用下面对应的一行。碰撞与落地如果走同一次回调，只发送一次事件。粘贴和分离在动作成功时发送一次事件。

```csharp
GameEventCenter.RequestSfx(GameplaySfx.PhysicsBlockImpact);
GameEventCenter.RequestSfx(GameplaySfx.PhysicsBlockAttach);
GameEventCenter.RequestSfx(GameplaySfx.PhysicsBlockDetach);
```

## 底部对话框与美术替换

正式场景 SampleScene 和测试场景 CameraBubbleTest 均已启用“使用底部对话框”，并引用 `Assets/Resources/UI/BottomDialogue.prefab`。对话固定在屏幕下方，镜头移动和角色走出画面都不会改变对话框位置。多句同时请求时会排队显示，每句文字完整出现后开始计算阅读时长。

```text
底部对话                     Canvas、CanvasScaler、BubbleDialogView
└── 对话框底板               Image，可替换透明 PNG
    ├── 角色名称             Text
    └── 台词正文             Text
```

美术打开 BottomDialogue.prefab，替换“对话框底板”的 Image / Source Image，调整字体、字号、颜色和底部边距。支持九宫格的背景图可以设置 Sprite Border 并使用 Sliced。根 Canvas 使用 Screen Space Overlay，CanvasScaler 的参考分辨率是 1920×1080；不要把它拖到角色头顶。角色名和正文初始为空，由玩法传入。

对白框会优先使用已指定的美术字体。使用内置占位字体时，会尝试使用本机的微软雅黑、苹方或思源黑体来显示中文。正式交付时请在预置体上指定项目内包含中文的字体，保证不同电脑显示一致。

原来的 `flow.ShowDialogue(...)` 和 `flow.ShowNarration(...)` 接口也仍然可用，它们对应的 HudPanel/DialoguePanel 同样已调整到屏幕底部。该面板保留“继续”按钮，沿用玩家点击关闭的流程；Sequence 对话使用独立 BottomDialogue 预置体，按填写的时长自动推进。

## 在 Inspector 填多次对话

给 NPC 或剧情触发区域添加 `BubbleDialogSequence`。

1. 把 NPC 拖入“默认说话角色”。
2. 在“对话分组”添加 `intro`、`quest_done`、`after_battle` 等不同分组，每个分组标识保持唯一。
3. 展开“台词列表”，逐句填写“显示的角色名称”“台词内容”“阅读时长”“句后间隔”和“打字机效果”。
4. 同一段可以指定不同的“说话角色”。“显示的角色名称”可直接填写“玩家”“向导”等中文名称。勾选“这句是旁白”后会显示“旁白”，无需对应角色物体。
5. “触发方式”可选手动调用、组件启用时或进入二维触发区。二维触发方式需要 Collider2D 勾选 Is Trigger，玩家或触发器至少一方有 Rigidbody2D，玩家碰撞体所在物体使用“玩家标签”指定的标签，默认 Player。
6. “允许重复播放”控制某一分组能否再次播放。“循环播放所有分组”开启后，播放到末尾会回到第一段，跳过已完成且禁止重复的分组。

```csharp
[SerializeField] private BubbleDialogSequence npcDialog;

// 初次见面时调用。
npcDialog.PlayConversation("intro");
// 后续任务完成时调用。
npcDialog.PlayConversation("quest_done");
// 普通交互依次播放下一段。
npcDialog.PlayNextConversation();
// 重置同场景剧情进度。
npcDialog.ResetProgress();
```

以上各行分别放在对应玩法事件中。同一组件正在播放时再次触发会保持当前段；强制切换时先调用 `StopConversation()`。分组进度存在组件内存中，重新加载场景后重新计数。

临时对白或旁白可以直接使用管理器，返回句柄用于提前关闭。

```csharp
int id = BubbleDialogManager.Instance.ShowDialogue("旁白", "远处传来了一阵脚步声。", 3f, true);
// 需要提前关闭时调用。
BubbleDialogManager.Instance.HideBubble(id);
```

`ShowBubble(character, text, ...)` 旧接口仍可调用，当前配置下同样显示为底部对话框。角色名可通过最后一个参数传入。头顶气泡作为兼容模式保留，正式场景和测试场景均默认关闭该模式。

## 相机演出与自动返回玩家

`CameraPerformanceManager` 已挂在 SampleScene 的 Main Camera。把实际游戏相机拖入“演出相机”，将原有跟随脚本组件拖进“需要暂停的跟随组件”，并将实际玩家拖入“玩家返回目标”。玩家动态生成时也可调用 `SetPlayerTarget(player, offset)`。目标留空时，播放演出会尝试查找 Player 标签的角色。

“镜头列表”里可以填写多个目标或世界坐标、变焦尺寸、移动时长和停留时长。“返回玩家时长”控制结束时回到玩家的速度，默认 0.6 秒。

镜头按“移向目标 → 停留 → 返回玩家当前的位置 → 恢复跟随”的顺序自动运行，玩家站着不动也会返回。演出期间玩家移动，返回位置也会更新；变焦恢复为演出前的尺寸。

返回位置优先由实现 `ICameraFollowPositionProvider` 的跟随组件提供，以兼容正式跟随脚本的边界和偏移规则；否则使用“玩家返回目标”加“返回玩家时的偏移”。只有场景里既没有返回目标、也找不到 Player 标签时，才回到演出前的位置。SampleScene 当前尚未放入正式玩家，添加角色时需要完成上述引用。

```csharp
CameraPerformanceManager.Instance.FocusOn(target, 3f, 0.8f, 2f);
CameraPerformanceManager.Instance.FocusAt(new Vector2(10f, 2f), 4f, 1f, 2f);
CameraPerformanceManager.Instance.ZoomTo(3f, 0.8f, 1f);
CameraPerformanceManager.Instance.PlayConfiguredPerformance();
CameraPerformanceManager.Instance.StopPerformance();
```

每行是独立用法。提前中断同样返回玩家并恢复跟随；新演出替换当前演出。两个场景使用同一份管理器脚本和返回逻辑。

“忽略游戏暂停”默认关闭，游戏暂停时演出和对话计时一起暂停。玩法可以订阅 `PerformanceStarted`、`PerformanceEnded(bool completed)` 来控制角色输入，在角色禁用时取消订阅。正常结束的 completed 为 true，中断为 false。

需要对话开始时播放镜头时，在 Sequence 的“对话开始时”事件里拖入相机，再选择 CameraPerformanceManager / PlayConfiguredPerformance。

## 无美术资源测试

停止 Play，打开 `Assets/Scenes/CameraBubbleTest.unity`，再点击 Play。场景已配置好玩家、NPC、两段中文台词、底部对话框、触发器和镜头目标。进入测试后会自动播放一次镜头演出，再自动返回蓝色玩家。左上角显示等待触发、移向目标、聚焦停留、返回玩家及演出结束等状态；按 4 或点击按钮可以再次播放。

蓝色方块是玩家，黄色方块是对话角色，橙色方块是镜头聚焦目标。用 WASD 或方向键移动，靠近黄色角色会播放下一段对话。先点击 Game 窗口获取键盘焦点，也可直接点左上角按钮。

| 按键 | 测试内容 | 预期结果 |
| --- | --- | --- |
| 1 | 初次对话 | 台词和角色名称显示在屏幕底部，部分句子使用打字机 |
| 2 | 后续对话 | 同一角色播放另一个分组 |
| 3 | 连续三句旁白 | 底部对话框依次显示三句话 |
| 4 | 相机演出 | 聚焦橙色目标、变焦，演出结束后自动回到玩家 |
| 5 | 中断演出 | 立即返回玩家并恢复跟随 |
| 6 | 重置测试 | 清空对话，恢复出生点和剧情进度 |
| 7 | 清空对话 | 提前关闭当前及排队中的对话 |
| E | 下一段 | 依次播放分组，到末尾循环 |
| P | 暂停或继续 | 移动、镜头、对话计时一起暂停或继续 |

验证返回时先按 4，全程不按移动键，镜头应在演出结束后自动回到蓝色玩家。然后再按一次 4，演出中移动玩家，结束后应回到玩家最新的位置。左上角“玩家相机跟随”应从“已暂时关闭”变回“已开启”。

改台词时停止运行，选中 `对话角色_黄色`，在“对话分组 / 台词列表”中直接填写中文内容并保存场景。测试专用脚本和方块占位图放在 Assets/Testing，测试场景单独打开运行。

实现使用 Unity 的 [ScreenToWorldPoint](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/camera/screentoworldpoint) 和 [uGUI Text 布局接口](https://github.com/Unity-Technologies/uGUI/blob/main/com.unity.ugui/Runtime/UGUI/UI/Core/Text.cs)。
