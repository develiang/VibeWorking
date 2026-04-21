# CLAUDE.md

此文件为 Claude Code（claude.ai/code）在操作本仓库代码时提供指引。
开发该项目需要有结构化，层次化的思维，如果可以分类，即把文件分成几个模块，最好是按照功能来划分，这样在开发和维护时会更清晰。

## 项目概览

VibeWorking 是一款 Windows 桌面 WPF 应用，用于追踪鼠标移动距离、点击次数、键盘按键次数，并根据月薪显示实时赚钱进度。界面文本为中文。

## 常用命令

- **开发运行：** `dotnet run`
- **生成独立可执行文件：** `dotnet publish -c Release -r win-x64 --self-contained true`

## 架构

### 窗口样式
`MainWindow` 是一个无边框窗口（`WindowStyle=None`、`AllowsTransparency=True`、`Background=Transparent`）。外层 `Border` 提供圆角和阴影效果。用户几乎可以通过点击任意位置来拖拽窗口（`MouseLeftButtonDown` → `DragMove`）。唯一的界面控件是标题栏中的导航按钮（视图、设置）和关闭按钮。

### 全局输入钩子
`InputHook` 通过 `SetWindowsHookEx` 安装系统级的低级别钩子（`WH_MOUSE_LL` 和 `WH_KEYBOARD_LL`）。回调函数在 UI 线程上运行，因此不能阻塞。它向 `MainWindow` 抛出 `MouseMoved`、`MouseClicked` 和 `KeyPressed` 事件。

### 统计与收入计算
`StatsService` 是 `MainWindow` 的 `DataContext`，并实现了 `INotifyPropertyChanged` 接口。它包含：
- 线程安全的计数器（点击/按键使用 `Interlocked`，厘米和收入使用 `lock`）。
- 一个 140×78 的点击热力图网格（`HeatMapData`）。点击会在附近单元格产生类似高斯分布的径向衰减效果。
- 根据 `MonthlySalary`、`WorkStartTime` 和 `WorkEndTime` 计算收入。薪资按实际工作时间秒数按比例计算，而非 24 小时。`Timer` 按可配置的时间间隔刷新收入。

`MainWindow` 使用对数缩放的 黑→蓝→青→绿→黄→红 渐变（`HeatColor`）将热力图渲染到 `WriteableBitmap`。

### 主题系统
主题文件是 `Themes/` 目录下的 `ResourceDictionary` XAML 文件（深色、浅色）。`ThemeManager.ApplyTheme` 使用 `pack://application:,,,/` URI 将选中的字典合并到 `Application.Current.Resources.MergedDictionaries` 中。设置对话框会提示用户主题更改需要重启才能完全生效。

### 关闭 / 退出行为
`OnClosing` 始终被取消；窗口会最小化到系统托盘。实际退出路径是 `CloseApp()`，该方法在关闭前释放托盘图标和钩子。关闭按钮可以通过 `ExitConfirmDialog` 配置为最小化或退出，并可以选择记住该偏好。

### 数据持久化
所有本地数据以 JSON 格式存储在 `%LocalAppData%\InputStats\` 目录下：
- `stats.json` — `StatsStorage` 持久化点击次数、按键次数、移动厘米数、热门按键计数和扁平化的热力图数据。
- `settings.json` — `AppSettingsStorage` 持久化薪资、更新间隔、工作时间和关闭操作。
- `theme.json` — `ThemeStorage` 持久化选中的主题枚举值。

### DPI 感知距离计算
`DistanceCalculator` 通过 `shcore.dll` 的 `GetDpiForMonitor` 获取光标当前所在显示器的 DPI，将像素距离转换为厘米。

## 关键文件

| 文件 | 作用 |
|------|------|
| `MainWindow.xaml` / `.cs` | 无边框界面、热力图渲染、托盘图标、输入事件接线 |
| `StatsService.cs` | 计数器、收入计算、热力图数据、属性变更通知 |
| `InputHook.cs` | Win32 低级别鼠标/键盘钩子生命周期管理 |
| `NativeMethods.cs` | Win32 API 声明（`user32`、`shcore`、`kernel32`） |
| `StatsStorage.cs` | 统计、设置和主题的 JSON 读写 |
| `DistanceCalculator.cs` | 按显示器 DPI 查询和像素到厘米转换 |
| `ThemeManager.cs` / `Theme.cs` | 运行时主题字典合并 |
| `SettingsDialog.xaml` / `.cs` | 模态设置编辑器（薪资、间隔、工作时间、主题） |
| `ExitConfirmDialog.xaml` / `.cs` | 关闭操作确认 |
