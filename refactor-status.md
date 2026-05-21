# MajdataViewX Note 池化重构 — 状态报告

> 基于 `refactor.md` 的实施记录。计划详见 `C:\Users\BUCYU\.claude\plans\rippling-yawning-bachman.md`。

## ✅ 已完成

### 1. 池化基础设施（Phase 0-3）

新增文件夹 `Assets/Scripts/Notes/Pool/`：

| 文件 | 作用 |
|---|---|
| `NotePool.cs` | 通用 Note 池：`Get / Release / Prewarm / ClearAll`，含 EDITOR-only 双重释放保护 |
| `ArrowPool.cs` | Slide 箭头独立池（`GetMany / ReleaseMany`），尚未在 SlideDrop 启用 |
| `SlideArrowTable.cs` | **静态生成**的 42 个 slide shape × N 个 ArrowPose，由 Python 脚本一次性抽取 |
| `PoolingInfo/Tap·Hold·Star·Touch·TouchHold·Slide·Wifi·EachLine PoolingInfo.cs` | 8 个数据载体 POCO，DataLoader → Note 之间的契约 |

新增工具：
- `tools/extract_slide_arrows.py` — 解析 `Assets/SlidePrefab/*.prefab` YAML，输出 `SlideArrowTable.cs`。
  ```
  python tools/extract_slide_arrows.py             # 写入文件
  python tools/extract_slide_arrows.py --dry-run   # 预览
  ```
  改动 prefab 后**重跑**即可同步。

### 2. Note 重构（Phase 1-4：start-init-end-destroy + Update/FixedUpdate 分工）

下列 note 已按 `refactor.md` 的统一规范重写：

| Note | 状态 | 关键改动 |
|---|---|---|
| **NoteBase** | ✅ | 增加 `prefabRef` + `virtual End()` |
| **TapBase** | ✅ | 拆 `PreLoad / ApplyTapInfoCommon / ResetTapState / Render / Update / FixedUpdate / End`；`Render` 移到 `FixedUpdate` |
| **TapDrop** | ✅ | `Awake → PreLoad`，`Init(TapPoolingInfo)`，`End() override` |
| **HoldDrop** | ✅ | 重写为 `Awake → Init → Update(running) → FixedUpdate(Render) → End`；`tapLine`、`holdEffect` 子对象池化 |
| **StarDrop** | ✅ | `Init(StarPoolingInfo) override`；保留 `Start` 兼容旧 slide 路径（slide-headed star 仍走 InstantiateSlide） |
| **TouchDrop** | ✅ | `Awake → Init → Update → FixedUpdate(Render) → End`；fan SortingOrder 缓存基线避免累积 |
| **TouchHoldDrop** | ✅ | 同上，含 `holdEffect` 池化与 mask 重置 |
| **EachLineDrop** | ✅ | `Awake → Init → FixedUpdate(Render) → End`；纯渲染无判定 |

每个 note 现在都有清晰的 `#region` 分区（依赖 / 池化数据 / 运行时状态），`Update` 处理 `running(autoplay) + check`，`FixedUpdate` 处理 `Render`。

### 3. Manager 集成

- **`DataLoader.LoadTiming`**：`Tap / Hold / TouchHold / Touch / EachLine` 全部 `Instantiate` 替换为 `NotePool.Instance.Get(prefab) + compo.Init(info)`；force-star 也走池化（普通 slide 头星暂保留旧 Instantiate 路径）。
- **`NoteManager.ResetState`**：先遍历 `LoadedNotes` 调用 `note.End()` 把活跃实例归还到池，再 fallback 销毁残留子对象。
- **`NoteManager.AddNote / AddTouch`**：从 `Dictionary.Add`（重复键抛错）改为 indexer（容许 GameObject 跨 timing 复用）。

---

## 🔶 未完成（已为下一阶段铺垫好）

### SlideDrop / WifiDrop 池化（refactor.md "对于 slide note" 部分）

**为什么这次没做**：两个文件合计 ~1400 行，包含连接星星 (ConnSlide) 链、Animator 状态机、smoothSlideAnime、HideBar、JudgeQueue、areaStep、BreakShineController 动态附加等深度耦合的逻辑，强行重构容易引入难以察觉的判定偏移。

**已为下一阶段就绪的资产**：
- `SlideArrowTable.cs` 已含 42 个 shape 全部 ArrowPose 数据（已校验位置非零）。
- `ArrowPool.cs` API 完成。
- `SlidePoolingInfo.cs` / `WifiPoolingInfo.cs` POCO 已定义。

**下一阶段建议步骤**（对应 plan 文件 Phase 5-7）：

1. **建一个 `Slide_Arrow.prefab`**（Unity 编辑器手动）—— 单 SpriteRenderer + GUID `3030b339c8cedc34bbbaf6fd2c4500e6` 的 sprite，size `0.7×0.94`。在 `DataLoader.Awake` 注册到 `ArrowPool.Instance.RegisterPrefab(slideArrowPrefab)`。
2. **改 `SlideDrop.Initialize`**：删除 `for (var i = 0; i < transform.childCount - 1; i++) slideBars.Add(transform.GetChild(i).gameObject);` 这一段，改为：
   ```csharp
   var poses = SlideArrowTable.Get(slideType);
   foreach (var p in poses) {
       var x = isMirror ? -p.X : p.X;
       var rot = isMirror ? -p.RotZ : p.RotZ;
       var arrow = ArrowPool.Instance.Get(transform);
       arrow.transform.localPosition = new Vector3(x, p.Y, 0);
       arrow.transform.localRotation = Quaternion.Euler(0, 0, rot);
       arrow.SetActive(true);
       slideBars.Add(arrow);
   }
   ```
   `slidePositions` / `slideRotations` 也直接从 ArrowPose 数组算出，不再读 `transform.position`。
3. **加 `SlideDrop.Init(SlidePoolingInfo) / End() override`**：把 DataLoader 直接赋值的字段改为通过 `info` 设置；`End` 中 `ArrowPool.ReleaseMany(slideBars); NotePool.Release(prefabRef, gameObject);`。
4. **slidePrefab[index] 不再多种**：改为单一 `SlideRoot.prefab`（只有 SlideDrop + Animator + slideOK 子对象，不含 arrow children）。`DataLoader.SLIDE_PREFAB_MAP` 仍保留作为 shape→key 索引。
5. **WifiDrop**：同样套路，但 `SlideArrowTable["wifi"]` 是扁平化的 12 个 arrow（3 × 4），需要在 SlideArrowTable 用法上分 3 段处理（见 WifiDrop 内 `slideBars` 的三组使用），或单独维护 `WifiArrowTable` 三个数组的版本。
6. **`DataLoader.InstantiateSlide / InstantiateWifi`**：把 `Instantiate(slidePrefab[idx])` 替换为 `NotePool.Get(slideRootPrefab) + slideDrop.Init(info)`；star_slidePrefab 也走 `NotePool` / 单独 starPool。
7. **`NoteManager.ResetState`** 已经是 pool-aware（会调 `End`），届时 SlideDrop.End override 后会自动加入回收链路，无需再改。

### 其他已知边界

- **EachLineDrop 不在 `LoadedNotes` 中**（不继承 NoteBase），ResetState 时仍走 fallback Destroy，不能复用池实例。如要彻底池化，把它改为继承 NoteBase 或单独跟踪。
- **TapBase.tapLinePrefab / HoldDrop.tapLinePrefab** 的 prefab 来源策略：优先 SerializeField → 实例 `tapLine` 字段（兼容旧 inspector）→ `Majdata<DataLoader>.Instance.tapLine`。第一次重玩前最好在 Unity 编辑器把这两个字段在各 note prefab 上 SerializeField 设好，避免运行时 fallback 到 `GameObject.Find`。
- **sortingOrder 基线缓存**（`_baseSpriteOrder` 等）依赖 prefab 上的初始 sortingOrder。复用时是 `base + offset`（绝对值），不会累积。

---

## 🧪 验证清单

| 项 | 怎么测 |
|---|---|
| 构建是否过 | Unity 编辑器 reload；`Assembly-CSharp.csproj` 重新编译，无错误 |
| 普通谱面通关 | Easy/Master 两张谱面，AutoPlay = `Enable` / `Disable` / `Random` / `DJAuto` 各一遍，对比基线无判定差异 |
| 重玩稳定 | 同一谱面连续重玩 5 次，无视觉残留、Animator/Particle 正常 |
| GC 改善 | Unity Profiler 加载 + 重玩阶段 `GC.Alloc` 应明显下降 |
| Slide 仍可用 | 含 slide / wifi 的谱面正常通关（slide 路径未改，应当无差异） |
| 镜像 slide | 含 `<` `>` `^` `qq` `pp` 的谱面通关 |

---

## 📂 文件改动一览

```
新增：
  Assets/Scripts/Notes/Pool/NotePool.cs
  Assets/Scripts/Notes/Pool/ArrowPool.cs
  Assets/Scripts/Notes/Pool/SlideArrowTable.cs        (生成)
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
  Assets/Scripts/Managers/DataLoader.cs               (LoadTiming + each-line 路径改池化)
  Assets/Scripts/Managers/NoteManager.cs              (ResetState 走 End()，AddNote 用 indexer)

未改 (待 Phase 5-7)：
  Assets/Scripts/Notes/SlideDrop.cs
  Assets/Scripts/Notes/WifiDrop.cs
  Assets/Scripts/Managers/DataLoader.cs#InstantiateSlide / InstantiateWifi / InstantiateStarGroup
```

---

## 🔁 重新生成 SlideArrowTable

如果以后修改了 `Assets/SlidePrefab/*.prefab`：
```bash
python tools/extract_slide_arrows.py
```
脚本会重新解析所有 prefab，覆盖 `Assets/Scripts/Notes/Pool/SlideArrowTable.cs`。
