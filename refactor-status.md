# MajdataViewX Note 池化重构 — 状态报告

> 基于 `refactor.md` 的实施记录。计划详见 `C:\Users\BUCYU\.claude\plans\rippling-yawning-bachman.md`。

## ✅ 已完成

### 1. 池化基础设施

新增文件夹 `Assets/Scripts/Notes/Pool/`：

| 文件 | 作用 |
|---|---|
| `NotePool.cs` | 通用 Note 池：`Get / Release / Prewarm / ClearAll`，含 EDITOR-only 双重释放保护 |
| `ArrowPool.cs` | Slide 箭头独立池（`GetMany / ReleaseMany`）。基础设施已就位，SlideDrop 当前仍走 prefab 子对象，下一阶段可切换到 ArrowPool 路径 |
| `SlideArrowTable.cs` | **静态生成**的 42 个 slide shape × N 个 ArrowPose（共 1418 行），由 Python 脚本一次性抽取 |
| `PoolingInfo/Tap·Hold·Star·Touch·TouchHold·Slide·Wifi·EachLine PoolingInfo.cs` | 8 个数据载体 POCO，DataLoader → Note 之间的契约 |

新增工具：
- `tools/extract_slide_arrows.py` — 解析 `Assets/SlidePrefab/*.prefab` YAML，输出 `SlideArrowTable.cs`。
  ```
  python tools/extract_slide_arrows.py             # 写入文件
  python tools/extract_slide_arrows.py --dry-run   # 预览
  ```
  改动 prefab 后**重跑**即可同步。

### 2. Note 重构 — start-init-end-destroy + Update/FixedUpdate 分工

下列 note **全部**已按 `refactor.md` 的统一规范重写：

| Note | 状态 | 关键改动 |
|---|---|---|
| **NoteBase** | ✅ | 增加 `prefabRef` + `virtual End()` |
| **TapBase** | ✅ | 拆 `PreLoad / ApplyTapInfoCommon / ResetTapState / Render / Update / FixedUpdate / End`；`Render` 移到 `FixedUpdate` |
| **TapDrop** | ✅ | `Awake → PreLoad`，`Init(TapPoolingInfo)`，`End() override` |
| **HoldDrop** | ✅ | 重写为 `Awake → Init → Update(running) → FixedUpdate(Render) → End`；`tapLine`、`holdEffect` 子对象池化 |
| **StarDrop** | ✅ | `Init(StarPoolingInfo) override`；保留 `Start` 兜底兼容（`_initApplied` 标志） |
| **TouchDrop** | ✅ | `Awake → Init → Update → FixedUpdate(Render) → End`；fan SortingOrder 缓存基线避免累积 |
| **TouchHoldDrop** | ✅ | 同上，含 `holdEffect` 池化与 mask 重置 |
| **EachLineDrop** | ✅ | `Awake → Init → FixedUpdate(Render) → End`；纯渲染无判定 |
| **SlideDrop** | ✅ | `Awake (依赖注入) → Init(info) → Initialize (重载已支持池复用) → Update(running+check) → FixedUpdate(Render) → End`；BreakShineController 动态记账与清理；slideOK 自动 reparent 回 child；conn slide 链式 End |
| **WifiDrop** | ✅ | 同 SlideDrop 套路；3 个 `star_slide` 通过 `NotePool` 共享同一 `star_slidePrefab` 池化 |

每个 note 现在都有清晰的 `#region` 分区（依赖 / 池化数据 / 运行时状态），`Update` 处理 `running(autoplay) + check`，`FixedUpdate` 处理 `Render`。

### 3. Manager 集成

- **`DataLoader.LoadTiming`**：`Tap / Hold / TouchHold / Touch / EachLine / force-star` 全部 `Instantiate` 替换为 `NotePool.Instance.Get(prefab) + compo.Init(info)`。
- **`DataLoader.InstantiateSlide / InstantiateWifi`**：head-star、slide body、star_slide 三个 GameObject **全部走 NotePool**；通过 `SlidePoolingInfo / WifiPoolingInfo / StarPoolingInfo` 把数据交给各自 `Init`。`SlideDrop` 首次 `AddComponent`、复用时直接 `GetComponent` 复用既有组件。
- **`NoteManager.ResetState`**：先遍历 `LoadedNotes` 调用 `note.End()` 把活跃实例归还到池，再 fallback 销毁残留子对象。
- **`NoteManager.AddNote / AddTouch`**：从 `Dictionary.Add`（重复键抛错）改为 indexer（容许 GameObject 跨 timing 复用）。

---

## 🔶 后续可选优化

### Slide arrow 真正使用 ArrowPool（非阻塞）

当前 `SlideDrop` / `WifiDrop` 仍**通过 prefab 上的 arrow 子对象**渲染（`transform.GetChild`），这与原版行为一致。基础设施 `ArrowPool` + `SlideArrowTable` 已就位，但激活它需要 Unity 编辑器一侧的工作（建一个空白 `Slide_Arrow.prefab`，然后修改 SlideDrop.Initialize 改为从 ArrowPool 摆放）。

### 步骤（按需）：

1. **Unity 编辑器**：建 `Assets/Prefab/Pool/Slide_Arrow.prefab` —— 单 SpriteRenderer + GUID `3030b339c8cedc34bbbaf6fd2c4500e6` 的 sprite，size `0.7×0.94`。
2. **`DataLoader.Awake`**：`ArrowPool.Instance.RegisterPrefab(slideArrowPrefab);`
3. **`SlideDrop.Initialize`** 中删除：
   ```csharp
   for (var i = 0; i < transform.childCount - 1; i++)
       slideBars.Add(transform.GetChild(i).gameObject);
   ```
   改为：
   ```csharp
   var poses = SlideArrowTable.Get(slideType);
   foreach (var p in poses) {
       var arrow = ArrowPool.Instance.Get(transform);
       arrow.transform.localPosition = new Vector3(isMirror ? -p.X : p.X, p.Y, 0);
       arrow.transform.localRotation = Quaternion.Euler(0, 0, isMirror ? -p.RotZ : p.RotZ);
       arrow.SetActive(true);
       slideBars.Add(arrow);
   }
   ```
   `slidePositions` / `slideRotations` 直接从 ArrowPose 数组算出，不再读 `transform.position`。
4. **`SlideDrop.End`** 中加：`ArrowPool.Instance.ReleaseMany(slideBars);`
5. **WifiDrop**：`SlideArrowTable["wifi"]` 是扁平化的 12 个 arrow（3 × 4），需要在用法上分 3 段处理（参考 `WifiDrop` 内 `slideBars` 的三组使用），或者单独为 wifi 维护"3 数组"版本的表。
6. **slide prefabs**：在 Unity 编辑器移除每个 `Star_*.prefab` 的 arrow 子对象（仅保留 slideOK），减少首次实例化成本。

### 其他已知边界

- **EachLineDrop 不在 `LoadedNotes` 中**（不继承 NoteBase），ResetState 时仍走 fallback Destroy。如要彻底池化，把它改为继承 NoteBase 或单独跟踪。
- **TapBase.tapLinePrefab / HoldDrop.tapLinePrefab** 的 prefab 来源策略：优先 SerializeField → 实例 `tapLine` 字段（兼容旧 inspector）→ `Majdata<DataLoader>.Instance.tapLine`。第一次重玩前最好在 Unity 编辑器把这两个字段在各 note prefab 上 SerializeField 设好，避免运行时 fallback。
- **sortingOrder 基线缓存**（`_baseSpriteOrder` 等）依赖 prefab 上的初始 sortingOrder。复用时是 `base + offset`（绝对值），不会累积。
- **SlideDrop.Initialize 双阶段**：`Init(info)` 设数据 + 重置状态，`Initialize()` 由 `DataLoader.InstantiateStarGroup` 在所有 conn-subSlides 都完成 `Init` 后统一调用，以便正确建立 ConnSlide 链与 totalSlideLen。两阶段不可颠倒。

---

## 🧪 验证清单

| 项 | 怎么测 |
|---|---|
| 构建是否过 | Unity 编辑器 reload；`Assembly-CSharp.csproj` 重新编译，无错误 |
| 普通谱面通关 | Easy/Master 两张谱面，AutoPlay = `Enable` / `Disable` / `Random` / `DJAuto` 各一遍，对比基线无判定差异 |
| 重玩稳定 | 同一谱面连续重玩 5 次，无视觉残留、Animator/Particle 正常 |
| GC 改善 | Unity Profiler 加载 + 重玩阶段 `GC.Alloc` 应明显下降 |
| **Slide / Wifi** | 含 slide / wifi 的谱面正常通关，特别注意 conn slide / 镜像 / break slide |
| 镜像 slide | 含 `<` `>` `^` `qq` `pp` 的谱面通关 |

---

## 📂 文件改动一览

```
新增：
  Assets/Scripts/Notes/Pool/NotePool.cs
  Assets/Scripts/Notes/Pool/ArrowPool.cs
  Assets/Scripts/Notes/Pool/SlideArrowTable.cs        (脚本生成)
  Assets/Scripts/Notes/Pool/PoolingInfo/*.cs           (8 个)
  tools/extract_slide_arrows.py
  refactor-status.md                                  (本文档)

修改：
  Assets/Scripts/Notes/NoteBase.cs                    (加 prefabRef, virtual End)
  Assets/Scripts/Notes/TapBase.cs                     (大改)
  Assets/Scripts/Notes/TapDrop.cs                     (大改)
  Assets/Scripts/Notes/StarDrop.cs                    (大改，保留旧路径兼容)
  Assets/Scripts/Notes/HoldDrop.cs                    (大改)
  Assets/Scripts/Notes/TouchDrop.cs                   (大改)
  Assets/Scripts/Notes/TouchHoldDrop.cs               (大改)
  Assets/Scripts/Notes/EachLineDrop.cs                (大改)
  Assets/Scripts/Notes/SlideDrop.cs                   (重写：池化生命周期 + 状态重置)
  Assets/Scripts/Notes/WifiDrop.cs                    (重写：池化生命周期 + 3 star pool)
  Assets/Scripts/Managers/DataLoader.cs               (LoadTiming/InstantiateSlide/InstantiateWifi 全部走池化)
  Assets/Scripts/Managers/NoteManager.cs              (ResetState 走 End()，AddNote 用 indexer)
```

---

## 🔁 重新生成 SlideArrowTable

如果以后修改了 `Assets/SlidePrefab/*.prefab`：
```bash
python tools/extract_slide_arrows.py
```
脚本会重新解析所有 prefab，覆盖 `Assets/Scripts/Notes/Pool/SlideArrowTable.cs`。
