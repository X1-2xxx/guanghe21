# UI Prefab 接入说明（Unity 6.6）

## V4 剧情演出与游玩气泡

三幕对白、选项、镜头、全局教学和游玩小台词的使用、修改与程序调用见 [README_剧情演出V4.md](README_剧情演出V4.md)。Main 已放入独立演出预制体与场景绑定；正式运行从黑幕开场开始。原主菜单、暂停和 HUD 重置确认继续保留。

## 和正式关卡一起使用

正式游戏入口是 `Assets/Scenes/Main.unity`，双击这份场景后运行。它已在构建场景列表第一位，包含队友制作的角色、物理机关和旅馆任务。`SampleScene.unity` 是之前的 UI 模板，`CameraBubbleTest.unity` 是相机与对白测试；打开不同场景看到的内容会不同。拉取 Git 提交不会替你切换当前打开的场景；队友也需要提交 `.unity`、对应 `.meta` 及其引用的预制体和资源。

正式 Main 中的 GameUI 在编辑时可以隐藏，GameFlowTemplate 会在运行时激活原有 Canvas，继续使用其下的预制体实例，不重建美术面板。Main 已补上一份共享 TooltipPanel，和主菜单、暂停面板、HUD 同级。

LevelGameFlow 保留队友的关卡冻结、输入屏蔽和场景重置逻辑；GameFlowTemplate 同时保留文案预制体、按钮重载后重新绑定和模态弹窗。LevelUiCompatibility 只读取角色现有的 JumpEnabled / BreakEnabled，把拾取后的问号图标、道具图片和提示同步到暂停 UI；它不改变角色能力。重置关卡后，UI 以新角色的能力为准重新同步。此接入层不需要修改拾取、角色、机关或任务脚本。

如果误用 Git 的 Revert 撤销“暂停界面”提交，可编辑文案、悬浮提示及道具 UI 等会一起被移除；应先备份，再整合需要保留的功能，不要用“全部采用一方”覆盖共享脚本或整份场景。

`SampleScene` 的 `GameFlowTemplate` 只使用当前场景 Canvas 下的预置体实例。点击开始时，直接关闭 Canvas/MainMenu、打开 Canvas/HudPanel；返回主菜单时切换回来。运行时不会从 Resources 加载或另外实例化 UI。

GameFlowTemplate 必须保持启用。编辑器会恢复 SampleScene 中误关闭的控制器；脚本重新启用或运行中编译后，会自动重新绑定按钮和音量事件，保留当前游戏/暂停状态，不叠加事件，也不清空美术手动配置的 OnClick。Tooltip 和场景遮罩的管理器也会接回已有对象。

检查开始流程：点击“开始游戏”后主菜单应消失，右上角显示 HudPanel 的“暂停”入口；按 Esc 打开美术暂停面板，点“返回主菜单”并确认后应回到原菜单。连续重复这套操作也应有效。游戏界面已按需求去掉血条和演示按钮，对话由玩法接口触发，因此不要用血条是否出现判断是否开始成功。

Inspector 中的三个 UI 字段对应 Hierarchy 中的场景对象，运行时会显示实际使用的实例。

- 场景中的主菜单 → 开始游戏、设置、说明、版本和退出入口。
- 场景中的暂停面板 → 暂停时显示的菜单。
- 场景中的游戏界面 → 存放暂停按钮、对话和结果弹窗。

字段可以留空，程序会根据 GameUIPanel 的“面板类型”接入 Canvas 中的实例。如果同一种面板重复拖入，优先使用放在 Canvas 子物体列表后面的实例，并关闭旧的一份。场景中缺少某一面板时，会提示把对应预置体拖入 Canvas。

当前做 UI 时，“游戏关卡场景名”保持空白即可。它用于加载已经制作好的独立关卡，需要填写实际场景名并将场景加入构建列表。误填不存在的名字时会给出提示，然后照常进入当前场景的游戏界面。当前项目的 SampleScene 是 UI 模板，实际游戏地图和角色由玩法场景提供。

美术可以直接打开 `Assets/Resources/UI` 下的对应预置体，修改并保存。替换整个面板时，停止运行，删除 Canvas 下的旧实例，再将修改后的预置体拖入 Canvas，保存场景后运行。三个预置体根节点均已配置 GameUIPanel，替换实例后可以自动重新接入，原来的 Inspector 引用可留空。每种面板保留一份实例，避免重复界面叠在一起。

GameUIPanel 的“面板类型”和“功能对象引用”已配置。换图、改文字、改对象名、移动布局和调整层级时，保留该组件及已有功能对象的引用即可。按钮 OnClick 由脚本绑定，无需重复填写。

如果删除旧按钮再新建按钮，在预置体根节点的 GameUIPanel / 功能对象引用里，找到对应功能，将新按钮对象拖入“界面对象”即可；新按钮需要保留 Button 组件。替换确认弹窗时，同样更新确认面板、确认按钮、取消按钮和确认提示文字的引用。增加全新玩法按钮时仍需程序提供对应行为。

已经删除的可选按钮会被跳过，不影响其他按钮初始化。主菜单已启用“退出游戏”，点击后弹出“是否退出游戏”，确认后退出。暂停面板保留继续、设置、说明、重置和返回主菜单，已移除退出入口。HUD 已移除血条和血量数字。

## 项目统一字体

所有 UGUI 文字统一使用 `Assets/Resources/UI/UI/ZhaiZaiJiaFenTiaoTian-2.ttf`。主菜单、暂停、HUD、设置、说明、道具名称、悬浮提示、结果弹窗、对白与旁白的预制体都已引用这份字体。

运行时统一由 UIFontUtility 读取同一个字体资产；原来的系统中文字体备用逻辑已移除，编译脚本、切换面板或动态显示对话都不会换回 Arial、微软雅黑等其他字体。美术仍可直接在预制体 Text 组件中调整字号、颜色、对齐、换行和布局。

已打开的正式场景会自动同步旧字体引用并保留其他美术修改，场景仍由使用者按 Ctrl+S 保存。新增 UI 时直接选择这份字体；程序动态创建其他 Text 时也可调用 `UIFontUtility.Apply(text)`。恢复用的备份场景文件保持原样。

## 音量设置与操作说明

主菜单和暂停面板里的 SettingsPanel、HelpPanel 均已配置为模态弹窗。打开时自动移到所属菜单最前方，全屏底板压暗书本、道具和背景按钮，并阻止鼠标穿透；此时不显示任何道具悬浮提示。关闭后自动恢复普通暂停菜单的交互和道具提示。

暂停时的返回流程：音量设置 / 操作说明 → 底部“返回暂停菜单” → “继续游戏”。按 Esc 会先关闭当前设置或说明，再按一次 Esc 继续游戏。关闭设置本身仍保持暂停，音量调整立即生效。

主菜单中的对应按钮显示“返回主菜单”，关闭后仍留在主菜单。

美术可在原来的 MainMenu.prefab、PausePanel.prefab 中编辑这两个弹窗。DialogBackground 是居中的内容底板，可替换图片；根节点 Image 是覆盖全屏的半透明遮罩。保留根节点的 UIModalPanel、可点击的 CloseButton，以及 GameUIPanel 内已有的功能引用。UIModalPanel 会在打开时重新置顶，因此后续拖动道具框或调整其他 UI 的顺序，也不会把弹窗返回按钮盖到后面。

操作说明的正文放在 HelpPanel / HelpBodyText，已填写 Esc、界面鼠标操作及三个道具的已确认规则。后续团队确定移动、跳跃、冲锋按键后，可直接编辑 Text 补充内容。

TooltipManager.SetSuppressed(owner, true/false) 提供通用的悬浮屏蔽接口；设置和说明弹窗由 UIModalPanel 自动调用。每个弹窗独立登记，避免关闭其中一个时错误放开仍在显示的其他弹窗。

重置关卡、返回主菜单的二次确认弹窗也使用同一套背景遮罩、居中 DialogBackground、暖色按钮和统一 UI 字体。保留“确认 / 取消”两步，默认选中取消；Esc 也可以取消。主菜单退出确认和结果面板返回确认同步使用这套样式。GameUIPanel 中的确认、取消、提示文字引用均保留，实际重置和返回接口不变。

UIModalPanel 的“默认选中的按钮”可在预制体中指定：确认弹窗已关联 CancelButton，设置和说明弹窗留空时自动选中现有返回按钮。美术可以继续在 ConfirmationPanel 内调整底板和两个按钮，不需要修改业务代码。

## 直接编辑确认弹窗的文字

HudPanel 左上角在暂停图标旁新增“重置关卡”回转箭头按钮。点击后先询问是否重置，取消或按 Esc 不改变进度；确认后调用现有 ResetLevel，正式 Main 的 LevelGameFlow 会重新载入本关卡并回到游戏。按钮事件已通过 GameUIPanel 关联，不用手动填写 OnClick。

美术修改入口：`HudPanel / ResetButton / Icon` 换箭头图，`Label` 改按钮文字；`HudPanel / ConfirmationPanel / 重置关卡文案 / Title、MessageText` 改确认标题和正文。重置和结果面板“返回主菜单”各用独立文案，不会互相覆盖。

确认提示已改为读取预制体中的 Text；GameFlowTemplate 只选择对应文案物体，不会用代码里的固定字符串覆盖标题或正文。重置、返回主菜单和退出各有独立的文案，不必共用一个正文来回改。

停止运行，双击 `Assets/Resources/UI/PausePanel.prefab`，展开下列对象：

```text
ConfirmationPanel（ConfirmationDialog / UIModalPanel）
├─ 重置关卡文案
│  ├─ Title           ← 重置弹窗标题
│  └─ MessageText     ← 重置弹窗正文
├─ 返回主菜单文案
│  ├─ Title           ← 返回弹窗标题
│  └─ MessageText     ← 返回弹窗正文
├─ DialogBackground
├─ ConfirmButton / Label
└─ CancelButton / Label
```

选中相应的 MessageText，在 Inspector 下方的 Text 组件中直接修改 Text 内容；Title 修改标题。选中文案子物体时，会自动显示所属的一组并隐藏另一组，灰色的返回文案也能直接点选预览。

更方便的入口：直接选中 ConfirmationPanel，在 ConfirmationDialog 选择“预览哪组文案”，其下方就有“标题”和“正文”输入框，可以直接填写。这些输入框修改的就是实际 Text，不是额外一份文案；选择预览类型后立即显示对应内容。仍保留手动预览和定位正文按钮。保存预制体后再运行。

建议先停止运行再编辑。运行期间双击进入 Prefab 模式时也可以预览资产中的文字，但不要把修改场景运行实例当成保存预制体。如果在运行中刚新增过文案子物体，旧的运行实例可能还没包含它；先停止并重新运行，就会接入更新后的预制体。已有文案物体但缺少新增组件或关联时，程序会接回当前弹窗自己的文案，不重建 UI，也不替换美术布局。

主菜单退出提示位于 `MainMenu.prefab / ConfirmationPanel / 退出游戏文案`，结果界面返回提示位于 `HudPanel.prefab / ConfirmationPanel / 返回主菜单文案`，同样直接改 Title 和 MessageText。确认/取消按钮的文字在各自的 Label 中修改。

请保留 ConfirmationDialog 组件里已关联的文案子物体，以及各面板 GameUIPanel 中的确认、取消按钮引用。修改文字、换字体大小、调位置或换图不需要重新填写 OnClick。编辑预览可以临时显示弹窗，开始运行时仍会自动关闭，等点击相应操作再打开。

## 通用悬浮 Tooltip

`Assets/Resources/UI/TooltipPanel.prefab` 已放入 SampleScene 的 Canvas 下，与主菜单、暂停面板和 HudPanel 同级。全场景只保留一份，根节点保持激活；运行时通过 CanvasGroup 自动隐藏，不会随着暂停面板一起被关掉。

如果 SampleScene 此刻已经在编辑器中打开，脚本编译完成或停止运行后会自动补齐缺失的 TooltipPanel，保留场景中尚未保存的美术修改。完成后按 Ctrl+S 保存场景即可。

```text
Canvas（Canvas / CanvasScaler / GraphicRaycaster）
├─ MainMenu
├─ PausePanel
├─ HudPanel
└─ TooltipPanel（RectTransform / Image / CanvasGroup / Canvas / TooltipManager）
   └─ TipText（Text，已关联管理器）
```

提示框的子 Canvas 只用于显示排序，不需要 GraphicRaycaster。TooltipManager 会取 Inspector 中“显示层级”与当前菜单层级加一的较大值；即使正式 Main 的 GameUI 已经是 1000，说明框也会显示在它上方，避免同层的嵌套 Canvas 把说明盖在书本后面。底板和文字均不拦截鼠标，避免悬浮闪烁或挡住按钮。美术可以直接打开预制体替换底板图片、文字颜色和字号；宽度、边距、鼠标偏移在 TooltipManager 中修改。预制体在 Prefab 模式中可见，场景里的实例默认透明；不要通过关闭 TooltipPanel 根节点来隐藏预览，编辑器会恢复被误关闭的共享实例。

暂停面板现有的 `item_Cardboard`、`Item_SpaceKey`、`Item_Crossbow` 三个物品底板已挂 TooltipTrigger。框内常驻名称依次为“硬纸板”“跳跃键”“驽辛难得”，鼠标悬浮时才展示用户提供的完整说明。第一个硬纸板是初始自带道具，默认已解锁；另外两个暂时保持未解锁，获取后显示各自的说明。装饰问号、物品图标和名称文字均不拦截鼠标，让整个底板接收悬浮，包括图标、名称和空白处。原有底板和图标的贴图、位置、尺寸均保留。

美术修改框内名称：在暂停预制体中展开对应物品底板，修改其子物体 `ItemNameText` 的 Text 内容；修改详细解释：选中物品底板，在 TooltipTrigger 的“提示内容”中填写。名称与详细解释分别存放，不会把整段说明挤进物品框。名称不随解锁状态隐藏，未解锁的悬浮提示仍为“【尚未解锁】”。第三个框沿用旧物体名 `Item_Crossbow` 以兼容现有引用，实际显示名称和说明均为“驽辛难得”跑鞋。

复用到其他 UI：

1. 在需要悬浮提示的物体上添加 TooltipTrigger。
2. 物体或其子物体需要有 Image / Text 等 Graphic，并勾选 Raycast Target；其 Canvas 需要 GraphicRaycaster，场景需要 EventSystem。背景图片即使透明也可作为悬浮区域，不必添加 Button。
3. 填写 `tipText`（提示内容）；`isUnlocked`（已经解锁）勾选时显示填写内容，未勾选时固定显示 `【尚未解锁】`。已解锁但内容为空时不显示。
4. 不需要填写 OnClick。鼠标移出、禁用物品、关闭父面板或销毁物品时自动隐藏；Canvas.enabled=false 和 CanvasGroup.alpha=0 的隐藏方式也会在当帧末清除提示。

普通 UI 的 TooltipTrigger 可直接修改 `isUnlocked` 切换提示内容。三个道具框现已由 ItemSlotUI 同步管理图标和说明：玩法程序统一调用下面的 ItemUnlockManager 接口，不要只修改 TooltipTrigger 字段，否则图标和状态可能不同步。悬浮系统仍不依赖 GameFlowTemplate 或玩家控制器。

屏幕边缘会自动翻转并限制位置，跟随位置经过 Canvas 坐标换算，支持 Screen Space Overlay / Screen Space Camera 和 CanvasScaler；Time.timeScale=0 时仍然正常工作。超出屏幕高度的极长提示会截断显示，建议提示使用简短说明。

新增场景时，只需把同一个 TooltipPanel 预制体拖到屏幕 Canvas 下，与各面板同级。不要把它放在暂停面板里面，也不要给主菜单和暂停面板各放一份。

## 道具解锁：问号、图片与程序对接

三个框的 ItemSlotUI 已在 PausePanel 预制体中配置完成。未解锁时只显示问号，解锁后隐藏问号、显示对应道具图片，悬浮提示同步从“【尚未解锁】”变为完整说明。名称始终显示。硬纸板初始自带，不能被重新锁定。

| 道具 | 程序使用的标识 | 预制体中的物品框 | 初始状态 |
| --- | --- | --- | --- |
| 硬纸板 | `GameItemId.Cardboard` | `item_Cardboard` | 道具图片，已解锁 |
| 跳跃键 | `GameItemId.JumpKey` | `Item_SpaceKey` | 问号，未解锁 |
| 驽辛难得 | `GameItemId.Rocinante` | `Item_Crossbow`（沿用旧对象名） | 问号，未解锁 |

美术替换图片的方法：双击 PausePanel 预制体，在现有 QuestionMark、Cardboard、SpaceKey、Crossbow 对象的 Image / Source Image 中换图。改图和移动布局不需要改代码。每个框的 ItemSlotUI 中，“未解锁：问号物体”“已解锁：道具图片物体”“该框的悬浮提示”都已拖好；如果删除旧图标再新建对象，把新对象重新拖到对应字段。不要把整个物品框拖成图标对象。

没有拾取物体也能自行测试：运行游戏并打开暂停面板，在 Hierarchy 选中 Item_SpaceKey 或 Item_Crossbow，点击 Inspector 中 ItemSlotUI 组件右上角的菜单，选择“运行时测试 → 解锁这个道具”。问号应立刻消失并显示道具图片，悬浮说明随之更新。这个菜单调用的正是正式解锁接口；停止运行后恢复初始状态，不会修改预制体中的默认图标。

交给拾取程序员：在已有的“确认拾取成功”函数里，按捡到的道具调用一次 Unlock。无需找到暂停界面、按钮或图片，也无需手动拖 UI 引用；暂停界面没打开时同样可以调用。

捡到跳跃键：

```csharp
ItemUnlockManager.Unlock(GameItemId.JumpKey);
```

捡到驽辛难得跑鞋：

```csharp
ItemUnlockManager.Unlock(GameItemId.Rocinante);
```

这两句放到各自道具的拾取成功事件中。返回值首次解锁为 true，已经拥有时为 false；重复调用不会重复广播。物体回收、拾取音效等继续使用拾取程序原有逻辑。

拾取成功后，打开着的面板会立刻换图；面板关闭时先记录状态，下次打开自动显示最新图标。当前悬浮说明也会更新，不必重新移开鼠标。

角色程序需要判断跳跃、冲墙等能力时，查询同一份状态：

```csharp
if (ItemUnlockManager.IsUnlocked(GameItemId.JumpKey))
{
    // 执行你们已有的跳跃代码。
}

if (ItemUnlockManager.IsUnlocked(GameItemId.Rocinante))
{
    // 允许冲锋撞碎脆弱墙体。
}
```

UI 模块只保存状态和刷新显示，实际跳跃、冲锋与墙体破坏由玩法程序实现。需要立即收到通知的模块可在 OnEnable 订阅 `ItemUnlockManager.UnlockStateChanged`（参数为 GameItemId 和 bool），在 OnDisable 取消订阅；初始化时先用 IsUnlocked 读取当前状态，避免错过之前已发生的拾取。

其他可用接口：

```csharp
// 恢复存档或设置指定状态。不会改变硬纸板始终可用的规则。
ItemUnlockManager.SetUnlocked(GameItemId.JumpKey, true);

// 真正开始全新的一局时调用，回到仅有硬纸板的状态。
ItemUnlockManager.ResetProgress();
```

状态由独立的数据管理器保存，首次调用自动创建，不需要手动挂到相机或 UI 上。本次运行内跨场景、开关面板和编译脚本都会保留；停止运行后重新开始恢复初始状态。没有写入永久存档，也没有自动更改“开始游戏 / 重置关卡 / 返回主菜单”的业务流程；是否新开一局清空道具由玩法程序明确调用 ResetProgress。

以后增加剪刀等其他道具时，在 GameItemId 增加新枚举值，复制并配置一个物品框的 ItemSlotUI，拾取时调用相同的 Unlock 接口即可；状态管理器和悬浮系统不需要改写。

## 游戏逻辑对接

HUD 已移除演示通关、演示失败、演示受伤和示例台词按钮。正式的通关、失败、台词弹窗通过以下接口触发。

在玩法脚本中添加 Inspector 引用，把场景中的 Main Camera 拖入该字段。

```csharp
[SerializeField] private GameFlowTemplate flow;
```

根据实际玩法结果调用对应接口，每个接口按需要单独调用。

```csharp
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
| `flow.ShowVictory()` | 关卡达成通关条件后显示通关弹窗 |
| `flow.ShowDefeat()` | 角色死亡或满足失败条件后显示失败弹窗 |
| `flow.ShowDialogue(speaker, line)` | 显示真实台词或任务提示 |
| `flow.ShowNarration(line)` | 显示带有“旁白提示”标题的旁白弹窗 |
| `flow.ResetLevel()` | 重载独立关卡，或发送当前场景的重置信号 |
| `flow.ReturnToMainMenu()` | 直接返回主菜单 |

通关和失败条件由正式玩法脚本判断，达成条件时分别调用 ShowVictory 或 ShowDefeat。UI 框架中的初始血量、血量接口和血量消息已移除。

对白和旁白共用 `Assets/Resources/UI/HudPanel.prefab` 内的 `DialoguePanel`。美术可以在预置体中展开它，临时激活后编辑外框、角色名、正文和“继续”按钮，保存前关闭面板。角色名和正文初始为空，游戏运行后由玩法脚本传入内容。

进入游戏后，调用 `flow.ShowDialogue("角色名", "对白内容")` 显示对白，调用 `flow.ShowNarration("旁白内容")` 显示旁白。角色名传空字符串时隐藏角色名区域。玩家点“继续”或按 Esc 可以关闭当前弹窗。

游戏内按 Esc 打开暂停菜单；退出入口在主菜单，点击后显示二次确认。暂停菜单中的重置、返回主菜单同样有确认步骤，玩法代码直接调用 ResetLevel 或 ReturnToMainMenu 会立即执行相应流程。

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
    // 当前场景模式下，在这里恢复角色出生点、机关和关卡进度。
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

美术打开 BottomDialogue.prefab，替换“对话框底板”的 Image / Source Image，调整字号、颜色和底部边距。支持九宫格的背景图可以设置 Sprite Border 并使用 Sliced。根 Canvas 使用 Screen Space Overlay，CanvasScaler 的参考分辨率是 1920×1080；不要把它拖到角色头顶。角色名和正文初始为空，由玩法传入。

对白框和旁白与其他界面一致，统一使用 UI 文件夹里的 ZhaiZaiJiaFenTiaoTian-2.ttf，运行时不会替换成系统字体。

原来的 `flow.ShowDialogue(...)` 和 `flow.ShowNarration(...)` 接口也仍然可用，它们对应的 HudPanel/DialoguePanel 同样已调整到屏幕底部。该面板保留“继续”按钮，沿用玩家点击关闭的流程；Sequence 对话使用独立 BottomDialogue 预置体，按填写的时长自动推进。

## 在 Inspector 填多次对话

给 NPC 或剧情触发区域添加 `BubbleDialogSequence`。

1. 把 NPC 拖入“默认说话角色”。
2. 在“对话分组”添加 `intro`、`quest_done`、`after_battle` 等不同分组，每个分组标识保持唯一。
3. 展开“台词列表”，逐句填写“显示的角色名称”“台词内容”“阅读时长”“句后间隔”和“打字机效果”。
4. 同一段可以指定不同的“说话角色”。“显示的角色名称”可直接填写“玩家”“向导”等中文名称。勾选“这句是旁白”后会显示“旁白”，无需对应角色物体。
5. “触发方式”可选手动调用、组件启用时或进入二维触发区。二维触发方式需要 Collider2D 勾选 Is Trigger，玩家或触发器至少一方有启用物理模拟的 Dynamic / Kinematic Rigidbody2D，两边都为 Static 无法触发。玩家碰撞体所在物体使用“玩家标签”指定的标签，默认 Player。
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

蓝色方块是玩家，黄色方块是对话角色，橙色方块是镜头聚焦目标。先点击 Game 窗口获取键盘焦点，用 WASD 或方向键移动蓝色玩家进入浅黄色范围，自动播放第一段对话。本段结束后，走出浅黄色范围再进入，会播放下一段；一直站在范围内不会重复播放。也可按 E 或点左上角按钮直接测试。

测试玩家已配置为 Kinematic Rigidbody2D，并在启动时修正旧场景中遗留的 Static 设置，确保 MovePosition 移动和接近触发均有效。正式玩家可按自己的玩法使用 Dynamic 或 Kinematic，不需要使用测试控制器。

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
