# VibeWorking / InputStats

[![Release](https://img.shields.io/github/v/release/develiang/VibeWorking)](https://github.com/develiang/VibeWorking/releases)

一个 Windows 桌面小工具，实时统计鼠标移动距离、点击次数、键盘按键次数，并显示基于月薪的实时赚钱进度。

## 下载

前往 [Releases](https://github.com/develiang/VibeWorking/releases) 页面下载最新版本：

| 版本 | 体积 | 说明 |
|------|------|------|
| **框架依赖版** | ~1 MB | 需先安装 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |
| **自包含版** | ~60 MB | 开箱即用，无需安装任何运行时 |

> 推荐普通用户下载**自包含版**，双击即可运行。

## 功能

- **鼠标移动距离** —— 实时追踪鼠标轨迹，按显示器 DPI 换算为厘米
- **鼠标点击次数** —— 统计左右中键及侧键点击
- **键盘按键次数** —— 统计所有键盘按下事件
- **点击热区** —— 可视化鼠标点击热力分布
- **今日/本月已赚** —— 基于月薪和工作时间，按秒计算已赚的钱
- **操作趋势** —— 按 10分钟/小时/天 查看点击/按键历史趋势
- **主题切换** —— 深色/浅色双主题
- **数据持久化** —— 退出时自动保存统计与历史数据，下次启动自动恢复
- **托盘最小化** —— 关闭窗口时最小化到系统托盘，不打扰工作

## 运行环境

- Windows 10/11 x64

## 开发运行

```bash
dotnet run
```

或编译为独立可执行文件：

```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

## 薪资配置

在设置面板中可配置：

- **月薪资**（默认 ¥23,500）
- **每日工作时间**（默认 09:00 - 18:00）
- **薪资更新间隔**

计算公式：

```
秒薪 = 月薪 / 当月天数 / 每日工作秒数
今日已赚 = 今日已工作秒数 × 秒薪
本月已赚 = (已过去完整工作天数 × 每日工作秒数 + 今日已工作秒数) × 秒薪
```

## 数据存储

所有本地数据保存在：

```
%LocalAppData%\InputStats\
├── stats.json    — 统计数据、热力图、历史趋势
├── settings.json — 薪资、工作时间、主题等设置
├── theme.json    — 主题偏好
└── logs\         — 运行日志
```

## 技术栈

- WPF (XAML + C#)
- 全局低级别鼠标/键盘钩子 (`WH_MOUSE_LL` / `WH_KEYBOARD_LL`)
- Windows Forms NotifyIcon（系统托盘）
- JSON 序列化持久化

## 项目结构

```
InputStats/
├── App.xaml / App.xaml.cs          — 程序入口
├── Views/                           — 界面层（主窗口、对话框、控件）
│   ├── MainWindow                   — 无边框主界面、托盘图标、热区渲染
│   ├── SettingsDialog               — 设置编辑器
│   ├── ExitConfirmDialog            — 退出确认
│   ├── StatsChartDialog             — 趋势图表
│   └── LineChartControl             — 自定义折线图控件
├── ViewModels/
│   └── StatsService                 — 统计计数、收入计算、热力图、历史数据
├── Models/
│   └── TimeSeriesData               — 时间序列数据结构
├── Services/
│   ├── StatsStorage                 — JSON 持久化读写
│   └── Logger                       — 文件日志
├── Input/
│   ├── InputHook                    — Win32 全局输入钩子
│   ├── DistanceCalculator           — DPI 感知像素到厘米换算
│   └── NativeMethods                — Win32 API 声明
├── Themes/
│   ├── Theme.cs / ThemeManager      — 主题枚举与运行时切换
│   ├── DarkTheme.xaml               — 深色主题资源字典
│   └── LightTheme.xaml              — 浅色主题资源字典
```
