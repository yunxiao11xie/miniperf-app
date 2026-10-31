# MiniPerf App

**轻巧的 Windows 桌面硬件监控**：悬浮条常驻 + 一键展开详情面板 + 动态托盘图标。
支持 Intel Core Ultra 的 **NPU** 监控，数据与任务管理器同口径，约 20KB 单文件、零依赖、免安装。

```
┌────────────────────────────────────────┐
│ CPU 23%  GPU 8%  NPU 0%  内存 70% 网速 3.2M 频率 3.4G │  ← 悬浮条（可拖动）
└────────────────────────────────────────┘
        │ 单击展开
        ▼
┌────────────────────────────────────────┐
│ MiniPerf                          ● ─  │
│ [CPU] [GPU] [NPU] [内存]                │
│ [网↓]  [网↑]  [频率]  [磁盘]            │
│ ── 实时曲线（CPU·内存 / GPU·NPU / 网速）── │
│ ── Top 5 进程（按 CPU / 按内存）───────── │
└────────────────────────────────────────┘
```

## 为什么做这个

市面上多数监控工具要么笨重、要么不带 NPU、要么托盘和右键菜单塞满杂项。
MiniPerf 只做一件事：**让你随时瞄一眼硬件状态，不添乱**。

- 数据源与 Windows 任务管理器**完全同口径**（PDH 性能计数器：`% Processor Utility`、GPU Engine 引擎分组）
- NPU 通过 `GPU Engine / engtype_Neural` 直读——**无需任何外部库或驱动**（Core Ultra 笔记本亲测）
- 全部交互三件套：拖动 / 滚轮 / 单击，没有多余按钮

## 功能

| 区域 | 能做什么 |
|------|---------|
| 悬浮条 | CPU / GPU / NPU / 内存 / 网速 / CPU 频率 六指标实时显示；数值 ≥55% 变黄、≥85% 变红 |
| 交互 | 按住拖动移动位置 · 滚轮调透明度（40–100%）· 单击展开/收起面板 · 右键退出 |
| 详情面板 | 八指标大数字 · 三种曲线模式（CPU·内存 / GPU·NPU / 网速，点击切换）· Top 5 进程（CPU/内存双排序）· 顶栏可拖动 |
| 托盘 | 动态图标实时显示 CPU 百分比 + 底部进度条（高负载变红）· 双击开面板 · 右键仅 3 项 |
| 记忆 | 位置 / 透明度自动保存重启恢复；重复启动自动唤起已有实例 |

## 安装使用

1. 从 [Releases](../../releases) 下载 `MiniPerfApp.exe`（单文件，约 20KB）
2. 双击运行——首次启动会自动在桌面创建快捷方式
3. 完事。卸载 = 删文件（配置在 `%APPDATA%\MiniPerfApp\`，一并删除即彻底干净）

> 需要 .NET Framework 4.8 —— Windows 10/11 全部自带，无需安装任何运行时。

## 从源码构建

不装 SDK、不装 Visual Studio，Windows 自带的编译器就够：

```powershell
git clone https://github.com/yunxiao11xie/miniperf-app.git
cd miniperf-app
powershell -File build.ps1     # 产出 build\MiniPerfApp.exe
```

构建脚本用的是系统自带 `csc.exe`（`C:\Windows\Microsoft.NET\Framework64\v4.0.30319`），
代码为 C# 5 语法（无 string 插值等新特性），WPF 纯代码构建（无 XAML）。

## 项目结构

```
├── src/                    源码（每文件一个模块）
│   ├── Program.cs          入口：单实例、异常钩子、桌面快捷方式
│   ├── MetricsCollector.cs 1s 节拍采集线程（CPU/GPU/NPU/网/盘/频率）
│   ├── GpuEngineSampler    GPU 引擎动态实例管理（5s 同步 + 1s 读值）
│   ├── MiniBarWindow.cs    悬浮条（拖动/滚轮/单击/阈值变色）
│   ├── PanelWindow.cs      详情面板（八格/曲线/Top 进程）
│   ├── ChartControl.cs     60 点滚动曲线（纯代码 WPF 绘制）
│   ├── TrayService.cs      托盘图标（GDI+ 每秒重绘）
│   └── TopProcesses.cs     进程 CPU 差分与内存排序
├── build.ps1               零依赖构建脚本
├── makeicon.ps1            程序图标生成（BMP-in-ICO）
└── docs/                   SoPro 四步工作流全套文档
    ├── PRD.md              需求（P0/P1/P2 分级 + 验收场景）
    ├── design/             设计规范 / 状态清单 / UI 决策 / HTML 原型
    ├── STACK.md DEV.md PLAN.md CHARTER.md
    └── run/                运行报告 / 验收清单 / 断点状态
```

## 已知限制与说明

| 项 | 说明 |
|----|------|
| CPU 温度 | WMI 方案需要管理员权限；普通启动不显示（v1.2 起已从界面移除，采集代码保留）。后续计划接入 LibreHardwareMonitor（同样需要管理员） |
| 数值偶差 | 两个采样器的窗口不可能完全同秒，波动剧烈时与任务管理器差 ±3~5 个百分点属正常（实测稳态 ≤3pp） |
| 深色模式 | 未实现，当前仅浅色（Fluent 随系统明暗在路线图中） |
| 开机自启 | 路线图中（设置开关形式） |

## 路线图

- [ ] 深色模式（随系统自动）
- [ ] 开机自启开关
- [ ] LibreHardwareMonitor 集成（温度全家桶，需管理员）
- [ ] 电池状态（电量/充电/续航）
- [ ] 曲线指标可自选

## 开发方法论

本项目按 [SoPro《AI 协作开发工作流指南》](docs/reference/)（王栋，2026-10）的四步流程开发：
PRD 对齐 → 设计稿（HTML 原型两轮确认）→ 开发文档组 → 一键运行 + 验收。
全部文档在 `docs/` 下，版本演化见 git history。

前身是同作者的 [Rainmeter 皮肤版 miniperf](https://github.com/yunxiao11xie/miniperf)（已封存），
其中沉淀了 17 条 Rainmeter 平台实测踩坑记录。

## License

[MIT](LICENSE)
