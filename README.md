# VibeWorking / InputStats

一个 Windows 桌面小工具，实时统计鼠标移动距离、点击次数、键盘按键次数，并显示基于月薪的实时赚钱进度。

## 功能

- **鼠标移动距离** —— 实时追踪鼠标轨迹，换算为厘米
- **鼠标点击次数** —— 统计左右中键及侧键点击
- **键盘按键次数** —— 统计所有键盘按下事件
- **今日已赚** —— 基于月薪，按秒计算今天已赚的钱
- **本月已赚** —— 从当月1号起累计已赚的钱
- **数据持久化** —— 退出时自动保存统计，下次启动自动恢复
- **托盘最小化** —— 关闭窗口时最小化到系统托盘，不打扰工作

## 运行环境

- Windows 10/11
- .NET 8.0 SDK

## 运行方式

```bash
dotnet run
```

或编译为独立可执行文件：

```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

## 薪资配置

默认月薪为 **¥23,500**，按当月天数均摊到每秒计算。如需修改，编辑 `StatsService.cs` 中的 `MonthlySalary` 常量。

计算公式：

```
秒薪 = 月薪 / 当月天数 / 24 / 60 / 60
今日已赚 = 从 0:00 到当前时间的秒数 x 秒薪
本月已赚 = 从当月1号 0:00 到当前时间的秒数 x 秒薪
```

## 数据存储

统计数据保存在本地：

```
%LocalAppData%\InputStats\stats.json
```

## 技术栈

- WPF (XAML + C#)
- 全局低级别鼠标/键盘钩子 (`WH_MOUSE_LL` / `WH_KEYBOARD_LL`)
- Windows Forms NotifyIcon（系统托盘）
- JSON 序列化持久化

## 项目结构

| 文件 | 说明 |
|------|------|
| `MainWindow.xaml` | 主界面布局 |
| `StatsService.cs` | 统计数据与薪资计算逻辑 |
| `StatsStorage.cs` | JSON 持久化读写 |
| `InputHook.cs` | 全局输入钩子 |
| `NativeMethods.cs` | Win32 API 声明 |
| `DistanceCalculator.cs` | 像素到厘米距离换算 |
