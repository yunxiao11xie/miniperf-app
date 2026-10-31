# CHARTER.md — 运行章程

## 什么算完成（v1.0.0）

1. PLAN 五段全部实现且 build.ps1 零错误零警告
2. PRD 第 7 节 7 项 P0 验收全过（证据：screenshots/ 截图 + STATUS.md 勾选）
3. run/STATUS.md、REPORT.md、ACCEPTANCE.md 更新完毕
4. git commit（含版本号）并 push

## 可以自己决定（不问）

- 色值 ±10 分量、间距 ±4px、动画时长、代码组织
- 采样周期的微调（0.5s-2s 之间）
- 亚克力效果降级方案（若 DWM API 效果差，用纯色 alpha 替代）

## 不许做

- **安装任何软件**（包括 .NET SDK、NuGet 包管理器；引外部 dll 前必须问操作者）
- 删除项目外文件、改注册表、开机自启项（首版不做自启）
- 使用真实个人数据测试

## 缺东西 / 矛盾时

模拟实现或写明假设继续跑，全部记入 REPORT.md：
- 温度 WMI 无数据 → 显示 "—"，标注"温度不可用"
- 亚克力 P/Invoke 失败 → 降级 alpha，标注视觉差异

## 测试规则

1. 写码时：每段编译 + 启动冒烟（无异常日志）
2. 段完成：PLAN.md 该段验收
3. 大测：PRD 验收清单 + PrintWindow 截图对比原型

## 进度与断点

run/STATUS.md 记录；中断后从第一条未完成项续跑。
REPORT.md 格式：做了什么 / 自测结果（证据文件）/ 假设 / 模拟部分 / 遗留。
