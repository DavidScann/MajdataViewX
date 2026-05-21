请先对项目有一个基础的理解

# 重构方式

首先，在Notes文件夹中可以看到很多Note类型，它们使用dataloader加载。你要做的是把它们都池化以提高性能

## 对于大多数note

大多数note的逻辑都不难，start-destroy的生命周期，你要把它们池化，变成start-init-end-destroy的逻辑。类似这样：Init(TapPoolingInfo info)

## 对于slide note

这个比较复杂，首先star和tap一样池化，而你需要把几十个slide prefab中的位置角度等信息提取并变成一个字典，使用slide shape来改变slide的箭头摆放位置，也就是说，我们把箭头池化了。这让slide也可以遵循start-init-end-destroy的逻辑。

# 一些提升

最后，我希望你能解析每个note的逻辑，并把它们的代码稍微整理一下。每个note都是这样update/fixedupdate中running(autoplay part)->check->Render，很明显render部分应在fixedupdate中而有些note并不是这样做的，而且它们的代码都极其混乱，压根不按顺序，你给它们整理好

保留我留下的注释，你可以适当添加注释
