# STACK.md — MiniPerf App 技术栈

## 白话摘要

- **花多少钱**：0（全开源/系统自带）
- **要装什么**：**什么都不装**。使用 Windows 11 自带的 .NET Framework 4.8 运行时
  和自带 C# 编译器（csc.exe）命令行构建，尊重"未经确认不安装软件"约定
- **换起来难不难**：UI 与数据采集分离；换 UI 框架不动采集层
- **部署在哪**：本机单 exe（D:\redmi电脑\miniperf-app\build\），桌面建快捷方式

## 选型

| 层 | 选择 | 理由 |
|----|------|------|
| 运行时 | .NET Framework 4.8（系统自带） | 零安装；Win11 100% 预装 |
| 编译 | csc.exe（系统自带）+ build.ps1 | 零 SDK 依赖 |
| UI | WPF（无边框窗 + DWM 亚克力 P/Invoke） | Fluent 视觉、顶层窗口控制 |
| 托盘 | WinForms NotifyIcon + GDI+ 动态绘制 | 成熟稳定，16×16 数字+底条（方案C） |
| 数据 | System.Diagnostics.PerformanceCounter（PDH） | 任务管理器同通道；GPU Engine/NPU Neural 同旧项目验证过的口径 |
| 进程 | System.Diagnostics.Process | Top 进程（CPU 差分 / WorkingSet） |
| 配置 | XML 序列化 → %APPDATA%\MiniPerfApp\settings.xml | 无 JSON 依赖需求 |
| 曲线 | WPF Canvas 自绘 Polyline | 60s 环形缓冲 |

## 版本与许可

| 组件 | 许可 |
|------|------|
| .NET Fx 4.8 / csc | 微软系统组件 |
| 本项目代码 | 个人使用（若开源再定，倾向 MIT） |

## 待实测项

1. DWM 亚克力 P/Invoke 在 Win11 26200 的效果（失败则回退半纯色 alpha 模拟）
2. 温度：WMI MSAcpi_ThermalZoneTemperature 是否有数（无则显示 —，LibreHardwareMonitor
   集成列为 P2，其 dll 需下载引入——届时征求操作者同意）
3. csc 多文件编译 + WinForms/WPF 混合引用的构建脚本稳定性

## 风险

| 风险 | 缓解 |
|------|------|
| PerformanceCounter 首采样返回 0 | 采集线程启动时预热一次采样丢弃 |
| GPU Engine 实例名随进程增减变化 | 实例缓存 + 每秒增删同步；类型分组求 max |
| csc 无 NuGet | 首版零外部依赖设计；LHM 等库延后 |
