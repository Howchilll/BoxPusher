# 推箱子

Unity 2022.3.51f1（URP 2D）做的推箱子小游戏，20 关，带关卡编辑器。框架用 HowFrame（我的个人开源框架处理ui 持久化 音效等系统）

## 怎么跑

- `Assets/Scenes/Main.unity`：游戏本体（标题 → 选关 → 游戏）
- `Assets/Scenes/LevelEditor.unity`：关卡编辑器，单独运行

方向键 / WASD 移动，Z 撤销，R 重来，H 提示。

## 玩法

墙、空地、玩家出生点、箱子、目标点，加三种机关：

- 传送门：走进或推进一端，另一端空着就过去
- 冰面：箱子推上去一直滑到被挡住（人不滑）
- 机关板 + 机关门 + 石头：板上压着东西时门开。石头和箱子一样推，专门用来压板

机关门、机关板、石头一关各一个且必须一起出现，传送门成对出现。

## 代码结构

```
Assets/Scripts/
├── Sim/      纯 C# 模拟层（二维数组规则 + 求解器 + 难度评估），不依赖引擎
└── Runtime/  游戏流程、渲染、输入、关卡读写、UI 面板（HowFrame）
Assets/GameRes/   运行时资源，走 Addressables：UI 面板预制体、音频、关卡配置（Levels）
Tools~/LevelLab/  dotnet 命令行工具：用同一份 Sim 代码生成、求解、导出关卡
```

游戏、编辑器试玩和求解器用同一套规则代码，求解器判定能解的关卡玩家一定能解。

## 关卡

- 正式关卡：`Assets/GameRes/Levels/level_NNN.json`，走 Addressables（标签 `Levels`，启动时预加载）
- 草稿：项目根目录 `LevelDrafts/`，每个关卡位可以存多个，只新增不覆盖

编辑器里保存正式关卡前会自动求解，无解的不能保存。

