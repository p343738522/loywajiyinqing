# LyoMir2 续作任务计划

对照对象：战神 `Mir200` 脚本/数据（不是旧 EXE），白猪 G2.5 登录器/客户端。  
运行环境：`D:\lyom2Release` + `Launcher2`（8088 中央服+热更），本机测试 IP `192.168.0.107`。  
**禁止**：关掉正在跑的启动器/MySQL/四个游戏进程；改桌面白猪明文、BaiZhuClient、`D:\www`。

## 可持续循环（自动推进，停在本文件不在聊天）

每轮固定三件事，做完立刻开下一轮，不在聊天里停下来等「继续」：

1. **接线**：A–I 里下一个有原生证据、且 AuditTools 不强制关的条目  
2. **性能**：H 里不改语义的一项（分配/日志/池）  
3. **审计**：新接线必须带 AuditTools；接不了就在本文件写清缺哪条 hook

当前轮：接现有 C#。代理空了立刻补位，不空转。  
已完成：H2 多处 List 池；D1 Query+Sell；C5 洗灵/镶嵌查询；C4 `REQSEESHOP` 1046；GiveHumLevelBuffer 过程面确认无 RTTI。  
仍关：YB 买入 Ident-125、毒 48、定时 46/74、寄售写外链、商城 1054 `[[0x7D5D98]]`、装备锁 1084 剩余秒（缺 THumanRcd+0x48 与 SM 0x2733/0x2737）。  
补位中：施毒术重开；下一条 GM 命令；商城 1047；镶嵌 1316 查询剩余；ArmLightGuard 毒；冰塔剩余；Yanshen 其余 List；GM skill 下一条。  
刚完成：@HeroAbil 核心 deferred；Yanshen CollectArea 已归还；商城 **1047 RENEWSEESHOP** 已接。  
刚完成：怪物 VMT+0x198 非合击/无 dump，保持关；E2 `[self+0x180]` 生产者未证，抗性保持 0。  
刚完成：CM 4123 Tag0 仍关（0x747878 未映射）。  
刚完成：LookFor 核心仍 deferred，洗灵格式化无调用点保持关。  
刚完成：神佑 4125 查询已活；玩家合击蓝耗 type-44 已接；合击无新 hit 点；@OutSay/@ShifangSay 已对齐。  
刚完成：@OutSay 静默（禁言+209，无绿字）。  
刚完成：SM 4038 随 4125 已接；绿怪/红怪/毒气毒已活；英雄 type-44 已接。  
刚完成：英雄合击 type-44 VMT+0x198 已接；46/74 仍关。  
刚完成：FeatureChanged VMT+0x1CC 已是 SM 41 活路，无新发送点。  
刚完成：毒气怪 POIS-36 `m_wEffectResistance+20`；红怪 27 已活；魔法 48 仍关。  
刚完成：UserLogon 3009 之后无更多可发 SM（主查+备份一致）。  
刚完成：UserLogon 3009 之后无更多可发 SM。  
刚完成：红怪 POIS-27 `m_wEffectResistance+7`；气攻怪 POIS-36 `m_wEffectResistance+20`；魔法 48 仍关。  
刚完成：寄售 1252/1253/1256/1257 只读已活；`ReqGetFirstUsedGift` 查询已活（消耗仍关）。  
父进程同时在池化 YanshenApi 扫描 List。  
刚完成：YbGoldGift 无新面；`ReqItemByGoldAct` 已活，GoldID/FirstGift 仍关。  
刚完成（你点名那 8 路已全部结束）：Q1 只读无下一条；Camp 已活；Fame 无表；AuthByHelped 缺 0x193E；VMT+0x2C 三站点已齐；ServerSay 0–5 已够；1084 缺持久化；**SM 3009 已接**。

服务器默认关着。进游戏回归时再开 `lyom2Release`，不替换 exe 除非你确认。

## 原作者原则（必须遵守）

1. **字节级对齐原生**。有 Delphi 地址/反汇编证据才接线；没有证据就 fail-closed，不发明玩法。
2. **原生空实现保持空**。原生返回 -1/0 的真空过程不要“补功能”。
3. **0 命中名字不要实现**。`GetStorageItemCount` 这类 C# 自造别名继续拒绝。
4. **先模型、后接线**。`Native*Planner` / AuditTools 先当神谕，再接到 `PasApiBridge` / `Operate()`。
5. **日志节流**。未知 ident 每进程只打一次，禁止热路径刷屏。
6. **BaiZhu 票据 fail-closed**。空 TicketDb / 过期 / 库失败 = 不登录。
7. **性能不得改语义**。`ProcessHumans` 节拍、随机数、存档顺序与原生一致；只减分配/锁/日志。

---

## 总图

```
A 工程与发布
B Mir200 脚本 API（PAS）
C 客户端封包 / 白猪协议
D 经济与社交（元宝寄售、摊位、商城、行会）
E 战斗与英雄
F 眼神插件（C# 复刻）
G 登录链 / 白猪启动器
H 性能与人数
I 回归（AuditTools）
```

优先级：P0 进游戏后立刻坏脚本/经济/战斗 → P1 功能半套 → P2 卡顿/内存。

## 整引擎工期（「源码全部完整」分三档，不要混）

这不是从零写引擎。登录、走路、打怪、背包、NPC PAS 主路径、持久摊位 4418–4467、寄售只读、动态房传送，C# 里已经有了。剩下的是战神镜像里还 fail-closed 的子系统。

规模（约）：PAS `RejectUnsupportedNativeApi` ~150 处（很多是同一 API 的过程/函数两面，且不少是故意关）；CM Q1 1054–1260 整季扣留；CM 尾 4125–4651 整季扣留；SM 无 builder 的 1729/2850/2956/4441–4443/4626 等继续丢。每接一条要 IDA + 执行器 + AuditTools，不能猜。

| 档 | 含义 | 工期（持续做、不换 exe、1 人主攻） | 完结标准 |
|---|---|---|---|
| **L1 能开区打** | 本服 Envir 主路径脚本不拒、战斗不穿模、人数上来不卡死 | **3–6 周** | P0 清完：E2 麻痹抗性生产者、H2 PAS 视野池、本服会调且已有 mutator 的 PAS、H3 在线对比后再开 Server GC |
| **L2 本服功能齐** | 这套 Mir200 + 白猪实际用到的玩法齐，不追求镜像每一个 ident | **4–8 个月** | P1：寄售外链/YBDeal 窗口、摊位会话 1210 或确认本服不用、商城 1054、乾坤/洗灵/镶嵌、Auth/战队、定时 46/74、毒 48、眼神剩余公式 |
| **L3 镜像字节齐** | 战神 exe 里每个 CM/SM/PAS 都有等价实现 | **无确定完结日（按年）** | 部分**永远不接**：0 命中自造名、原生真空、SM 尾填充未初始化、没有外挂元宝 DB 进程、未裁定的 ident 冲突（`DoRelive`） |

**不会出现的日期：**「再写两个月全部源码就完整」。战神是整包 Delphi 商业服 + 寄售独立进程 + 眼神 DLL。C# 仓库已经是半成品移植，不是缺一个模块。

**卡死项（有证据才动，没证据就一直关）：**

- 元宝寄售**写入**依赖外部 `[0x7D5D98]` RPC，本进程没有这根套接字
- 摊位**会话** `[[0x7D7190]]` + `[self+0x128]` 整台状态机未建模（持久摊位是另一套，已活）
- `YBDealDialogShowMode` / `AuthByHelped` / `RndGetMedal` / 定时 46/74：AuditTools 锁到执行器齐
- 好友 SM 4441–4443：结构尾 padding 未初始化，发了就是臆造字节

节奏：每天按文件顶上的循环推 L1；L1 完再开 L2 大块（一个子系统一周级，不并行乱接）。替换 `lyom2Release` 四个 exe 必须你点头。

---

## A. 工程与发布

| ID | 任务 | 优先级 | 说明 |
|---|---|---|---|
| A1 | LoginGate 加入 `LyoMir2.sln` | P0 | 源码在，sln 没有；`LiveLoginChainCheck` 已引用 |
| A2 | 发布替换清单 | P1 | 只替换 `D:\lyom2Release\mud2.0\` 四个 exe，不替换启动器 |
| A3 | 热更/区列表 | P1 | 以 `lyom2Release\www` 为准；Launcher2 管 8088 |
| A4 | 不把 artifacts/地图/MySQL 推进 git | P0 | 已 ignore |

---

## B. Mir200 脚本 API（核心兼容）

目标：`Envir\*.pas` 能跑完原生已注册的函数，行为与战神一致。

### B1 允许名单闸门 P0 — DONE

- 原生 `sub_6B8CC4` 用 `Config\validScriptFunc.txt` 做 Find。
- 已接到 `ClientMerchantDlgSelect` 非 `@` 文本：`TryNativeScriptInteractionFunction` → `Find()`。名单外 no-op；名单内过门但不发明 vmt+0x8C CallFunc。
- Envir PAS / `CallPlayerFunc` / `@label` 不受这份名单过滤。

### B2 已有函数体、缺 procedure 形态 P0

脚本常写过程调用。当前拒绝、函数形态已通的：

- `TakeDiamond` / `AddGloryPoint` / `DecGloryPoint` 过程 — **DONE**（AddDiamond 仍关：原生给予器未映射）
- `ActiveAuthen` / `HelpOtherAuthen`（过程仍关；函数形态已接，onLogin 走函数）

做：对照原生 RTTI，过程转发到已接线的 mutator，AuditTools 锁签名。

### B3 模型已写、尚未接线 P0/P1

`NativeAuthAndCreateScriptApiLadders.cs` 已有纯决策梯：

- `AuthByHelped` / `ActiveDelAuthen`
- `ClientSellerCancelYBDeal`
- `CreateCampAnimal`（FieldHero 工厂仍关闭）
- `CreateSelfCorps` / `CreateSelfGild`（先协调行会持久化，禁止双写）

### B4 明确保持 fail-closed（不是缺口）

- `PsShopGetGoodsList` / `PsShopBuyGoods`（0 命中，自造名）
- `guildpoint` 属性（本 exe 无字段）
- `creditpoint` PAS 属性（原生 TPlayer 未发布）
- `DoRelive`：ident 10161 与 `RM_USERSAVEITEM=10160` 冲突，未裁定前不接
- 原生真空：`OpenStorage`/`VipCall` 等保持空

### B5 仍拒绝、Mir200 脚本会用到的 P1

货币：`DonateDiam`、`ReqBuildDiamond`、`MakeDiamondWithYb`、`AddGuanMoPoint`、`AddGuildPoint`  
跨服：`ReqStartTransferArea`、`QueryTAScore`、`DecTAScore`  
动态房：`FlyToDynRoom` / `FlyToDynEnvirWithIdx` / `GroupFlyToDynRoom` — **已通**（无组队 `m_GroupOwner==null` 静默 no-op）  
炼体/内功：`GetLianTiLv*`、`HaveStudySSKSkill`、`AddSSKSkillExp`  
英雄：`CreateGuildHero`、`CreateProtectHero`、`FinishCombineHeroTrain`  
物品：`SendItemsToOther`、`PresentItem`、`ComposeItem`、`OpenLuckBox`  
地图：`CreateMapEvent`/`RemoveMapEvent`、`LmCreateMon`  
任务：`GetMyTaskState`/`TaskDialog`/`RegDelayProc`

规则：每接一个 API = 地址注释 + 侧效执行器 + AuditTools，禁止猜 SQL 列。

---

## C. 客户端封包 / 白猪协议

白猪 G2.5 走 LoginGate TCP 7000 + GameGate 7100；`def.openNewTigerGate` 明文默认 false，首包 `0xFF44FF44`。

| ID | 任务 | 优先级 |
|---|---|---|
| C1 | 已接线只读元宝寄售 1252/1253/1256/1257，补写入 1350–1364 / 1251 | P0 |
| C2 | 摊位会话 `[[0x7D7190]]` CM 1210–1212；SM_1729 body 仍 BLOCKED | P0 |
| C3 | 持久摊位 buy/open/pause 执行器仍 STUB | P1 |
| C4 | 商城 CM 1054–1057 shopMgr 未建模 | P1 | **P1 查询片 DONE** CM 1046/sub_63A254 经 MallManager+180B codec；CM 1054 仍关（shopMgr `[[0x7D5D98]]`/`0x637A00` 未映射） |
| C5 | 乾坤/洗灵/镶嵌/藏宝图/鸿福袋 CM 尾段 | P1 |
| C6 | 英雄灵珠/生肖镶嵌 CM 1291、1316 | P1 |
| C7 | SM 554/2956/3412/4032… 无 builder 的继续丢，有 AutoGotoMap 例外的 2850 不要回退 | P1 |
| C8 | 白猪 `bzaction\|TIPSBAR` 等扩展：只接已有 SysMsg/Hint 通路，不造新 ident | P1 | **CLOSED 无新 ident**。Mir200 把 `bzaction\|TIPSBAR\|…` 当 `ServerSay`/`PlayerNotice` 正文；白猪 `SM_SYSMESSAGE`(100) 与 HEAR/CRY 一起 `addMsg`。C# 无 TIPSBAR builder。NpcSay 已走 SM_SYSMESSAGE。ServerSay 改为 packed-cx `RM_SYSMESSAGE`（0x728913 色表），不再把 packed word 转成 `MsgColor` 后被 Notice 开关丢掉。AuditTools `BaiZhuBzactionSysMsgCheck`。 |
| C9 | CM 1084 装备锁剩余秒 | P1 | **CLOSED** mode 0 silence（`TryHandleEquipLockCm` 已截获，原生 `jbe` 无包）。缺 hook：`THumanRcd+0x48` BYTE → `[player+0xB78]`（0x6B0A5F load/save）；SM `0x2733`/`0x2737` builder（sub_765E68/sub_6B3EAC）。`NativeHumanDataCodec` rec[0x48] 是 HP，禁止当锁模式。AuditTools `NativeEquipLockRemainingSecondsCheck`。 |

---

## D. 经济与社交

| ID | 任务 | 优先级 |
|---|---|---|
| D1 | SuperMerchant 买卖+荣耀点必须两半一起落地 | P1 | **DONE** Query+Sell 同批；YB 买入仍关（Ident-125 回包发货缺口） |
| D2 | 好友 SM 4441–4445：现为按需 MySQL，无上线推送 | P1 |
| D3 | YB 面对面交易状态机 `TYBDealSetInfo` BLOCKED-D6 | P1 |
| D4 | 行会/战队脚本创建：与 guild 域协调后接线 | P1 |
| D5 | 转区分数无原生 SELECT，保持关 | P2 |

---

## E. 战斗与英雄

| ID | 任务 | 优先级 |
|---|---|---|
| E1 | 定时属性 46/74（及 44 消费者）接入战斗，现 dump-only | P0 |
| E2 | 麻痹抗性 `+0xAC` 未建模，抗性恒 0 | P0 |
| E3 | 毒 48 `sub_76FBBC` 未映射，仍用旧公式 | P1 |
| E4 | FieldHero 工厂关闭，`CreateCampAnimal` 无战神替身 | P1 |
| E5 | `GiveHumLevelBuffer` | P1 |

---

## F. 眼神（C# 复刻，不要加载 ys\*.dll）

| ID | 任务 | 优先级 |
|---|---|---|
| F1 | 穿戴触发 `@HeroEquiepchange` / `@MyEquiepchange` 208 字节未证，保持关直到布局证明 | P1 |
| F2 | Config12 仅 3 项接线，其余 BLOCKED | P2 |
| F3 | 装备吸血 / 无极真气时长 / 施毒 31B 公式 | P1 |
| F4 | 测试目录不要出现劫持 `libmysql.dll`（`D:\Mud2.0\Gs1` 那套） | P0 运维 |

---

## G. 登录链 / 白猪启动器

当前能进游戏：Launcher2 `:8088`（`/account` + 热更 + `/downloadconfig`）→ LoginGate `:7000` → DBSvr → GameGate `:7100` → GameSvr `:5000`。

| ID | 任务 | 优先级 |
|---|---|---|
| G1 | 保持 Launcher2 管 8088，LoginGate HTTP 8088 不要抢 | P0 |
| G2 | 区名/Area=180/`group1DBS` GBK 一致 | P0 |
| G3 | TicketDb 指向 lyom2Release MySQL（`HYU112Edsds`），不要混 `D:\Mud2.0` 的 `AsZvzz` | P0 |
| G4 | 网吧位 `AuthByte56 bit4` 保持 0 直到 IP 表移植 | P2 |
| G5 | 白猪 3.1 热更壳 vs G2.5 明文：服务端按 G2.5 协议，不改客户端目录 | P1 |

---

## H. 性能与人数（不改战斗公式）

主循环：`UsrEngn.PrcocessData` 一把 `ProcessHumanCriticalSection` 罩住 人/英雄/怪/商人/NPC，再 `Sleep(20)`。这是原生单线程模型，**不能拆成无锁多线程改手感**。

| ID | 任务 | 优先级 | 做法 |
|---|---|---|---|
| H1 | 热路径日志 | P0 | `ScriptDestroyItem`/`SetPlayerLevel` 每次打日志；机器人 `MainOutMessage` 每次攻击；改为一次/节流 |
| H2 | 视野搜索分配 | P1 | `SearchViewRange` / PAS / 城堡 / `MagicManager` / 攻击扫描 List 池 — **DONE**；TimedService 节拍无新 List |
| H3 | 工作站 GC | P1 | `GameSvr.csproj` `ServerGarbageCollection=false`；人数上来后改为 Server GC **并做在线对比** |
| H4 | TimedService 10ms Delay | P2 | 对齐原生即可，勿空转更勤 |
| H5 | 控制台在线人数打印 | P2 | 拉长间隔 |
| H6 | 存档批处理 | P1 | 已有周期存盘；禁止关服漏 `DealCancelA`（已补） |
| H7 | 对象池 | P2 | 掉落物/消息块；不要动 Delphi RNG |

卡死主因：单锁 + 全图 `SearchViewRange` + 同步 MySQL + 日志。优化顺序：日志 → 分配 → 视野 → GC → 再考虑把怪物 tick 仍在锁内但减每圈工作。

---

## I. 回归

每个接线功能必须带 AuditTools 小控制台（作者既有模式），禁止只靠手测。

优先补：钻石过程、荣耀过程、validScriptFunc 门、寄售写入、摊位会话、定时属性 46/74。

---

## 执行波次（自动推进，不替换正在跑的 exe）

**Wave 1（本轮，源码 only）**

1. A1 LoginGate 进 sln — **DONE**（sln 已加 LoginGate GUID）
2. H1 热路径日志节流 — **DONE**（`LogHotPathOnce`：DestroyItem 按物品名、SetLevel 按角色；机器人攻击 catch 按站点）
3. B2 钻石/荣耀 procedure 转发到已有 mutator — **DONE**（TakeDiamond 过程 count-only；AddGloryPoint 过程 1 参；DecGloryPoint 过程 5 参。AddDiamond 仍关：原生给予器未映射）
4. Envir 扫拒绝 API — **DONE**（动态房 `This_Player.FlyToDynRoom` / `FlyToDynEnvirWithIdx` / `GroupFlyToDynRoom` 函数/过程面**已经接线**，不是缺口。YBDealDialogShowMode / AuthByHelped / MakeDiamondWithYB / ReqPopGift 等 AuditTools 强制 fail-closed，等执行器）

并行结果（源码 only，不换正在跑的 exe）：

- Wave1 审计：DiamondGlory / DecGloryPoint / DiamondTransaction / GloryMutation / DynRoomProduction **PASS**。HotPathLoggingCheck 已补 H1 销钉，LoginGate 生命周期 INFO 按现源码 4 行（启动/2001/票据/停止）。
- GroupFlyToDynRoom：`m_GroupOwner == null` 静默 no-op，对齐 native `[self+0xA80]`。组内 `m_GroupOwner` 恒为队长。
- E1 46/74：**继续关**。44 已活。46 缺 job-3 260/264/268 活点；74 缺 15 个 magic-hit owner。计算器保持 dormant。
- H2 SearchViewRange：`TVisibleBaseObject` / `VisibleMapItem` ThreadStatic 池，语义未改。
- Envir 剩余真缺口：RndGetMedal（要 IDA+RNG 闭合）、GivePositiveVValue/GetVitalityValue（无 vitality 库）、插件名 ChgMonItemPercent / ServerAay。YBDeal / AuthByHelped / MakeDiamondWithYB / ReqPopGift / HaveTimeNum 等仍是执行器桶，不是忘接线。
- B1 validScriptFunc：**DONE**。`ClientMerchantDlgSelect` 非 `@` 文本走 `TryNativeScriptInteractionFunction` → `Find()`。名单外 no-op；名单内过门但不发明 vmt+0x8C CallFunc。`@label` / Envir PAS / CallPlayerFunc 不受名单过滤。

**Wave 2（进行中，源码 only，不开服）**

- B1 validScriptFunc — DONE
- C1 寄售写入 — `TPlayObject.YbConsignWrite.cs` 已按 native busy 门复刻；外链 RPC 仍 fail-closed。子代理核 1251/1350 是否还能再接一片
- C2 摊位 — `TPlayObject.StallWrite.cs` 与 Q1 Drop 并存，子代理清哪条是活路
- E2 麻痹抗性 — `IsRefusedByNativeParalysisResist` 已挂钩，`NativeParalysisResistPercent => 0`；子代理查 item+0xAC 能否填入
- RndGetMedal — 仍关，等 RNG 闭合证据

**Wave 3**

- C2 摊位会话
- B3 Auth/Camp/Corps 按 dormant ladder 接线（AuthByHelped 仍 fail-closed：缺 pending 0x193E + ConsumeHelpOther 闭合）
- H2 视野列表复用 — **DONE**（ThreadStatic 池）  

**Wave 4**

- 商城/乾坤/镶嵌等有 manager 指针证据的再开  
- FieldHero 等大块继续保持关闭直到工厂可测  

替换 `lyom2Release` 二进制必须你确认，且先停对应进程再拷（我不会自行关服）。
