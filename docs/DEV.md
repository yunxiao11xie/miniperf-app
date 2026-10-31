# DEV.md — 开发文档

> 只写实现时用得上的。视觉参数以 docs/design/DESIGN.md 为唯一来源。

## 架构（单 exe，模块边界 = 文件边界）

```
src/
├── Program.cs          入口：Mutex 单实例、设置加载、窗口/托盘启动
├── MetricsCollector.cs 后台线程 1s 采样 → immutable Snapshot（线程安全）
├── MetricsSnapshot.cs  一次采样的全部数值（record 语义）
├── MiniBarWindow.xaml(.cs)   迷你条：无边框、亚克力、拖动、滚轮透明度、点击展开面板
├── PanelWindow.xaml(.cs)     展开面板：八格网格 + 曲线 + Top进程，失焦自动收起
├── ChartControl.cs     60s 环形缓冲 + Canvas 重绘（可绑定任意指标）
├── TrayService.cs      NotifyIcon + GDI+ 绘制方案C图标 + 右键菜单(显示/退出)
├── TopProcesses.cs     进程 CPU 差分与内存排序（2s 周期，独立线程）
└── SettingsStore.cs    位置/透明度/置顶 持久化（%APPDATA%\MiniPerfApp\settings.xml）
build.ps1               csc 编译脚本 → build/MiniPerfApp.exe
```

## 数据流

```
MetricsCollector (1s线程)
  ├─ CPU: "Processor Information" "% Processor Utility" _Total
  ├─ RAM: GlobalMemoryStatusEx（可用/总量）
  ├─ GPU: "GPU Engine" "*engtype_{3D,Compute,Copy,VideoDecode,VideoProcessing}"
  │       实例缓存同步 → 各类型求和取 max
  ├─ NPU: 同上 engtype_Neural 求和
  ├─ NET: "Network Interface" "Bytes Received/sec|Bytes Sent/sec"（全实例求和）
  └─ DSK: "PhysicalDisk" "_Total" "% Idle Time"/"Disk Read Bytes/sec"
        ↓ Snapshot 发布（lock-free：volatile 引用替换）
UI (DispatcherTimer 1s) 读最新 Snapshot → 迷你条六段 / 面板八格 / 曲线推进 / 托盘重绘
```

## 接口约定

- `MetricsSnapshot`：Cpu/Gpu/Npu/RamPct (0-100 double)、NetDown/NetUp/ReadBps (B/s)、
  DiskPct、CpuTemp(double? null=不可用)、TopCpu/TopRam (List<ProcInfo> 前5)
- 数值格式化统一入口 `Fmt.Percent/Rate/Bytes`（K/M/G 自适应，面板与迷你条共用）
- 阈值变色：`Brushes.Warn(v)` 返回黄/红/主题色 Brush，三处 UI 复用

## 工程规范

1. **只改 src/ 与 build.ps1**，构建产物 build/ 不进 git
2. 每段完成：`powershell -File build.ps1` 零警告零报错 → 跑 exe → 按段验收
3. UI 数值全部走 DispatcherTimer 单一入口，采集线程禁止碰 UI
4. 所有 P/Invoke 集中 NativeMethods.cs 单文件
5. 中文注释文件保存为 UTF-8（BOM），csc /codepage:65001 编译

## 测试分档映射（本项目具体化）

| 档 | 内容 |
|----|------|
| 写码时 | build.ps1 编译通过；exe 启动无未捕获异常（日志 %APPDATA%\MiniPerfApp\log.txt） |
| 段完成 | PLAN.md 对应段验收命令/动作 |
| 大测 | 全验收清单 + PrintWindow 截图存档 screenshots/ |
