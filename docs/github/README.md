# Pinch

轻量的 Windows 图片压缩悬浮窗。开发者：Han。当前版本：**V2.3.0**。

## 安装

在 [Releases](https://github.com/Ganinsea/Pinch/releases) 下载 `PinchSetup.exe`，双击安装。

仓库的 `releases/v2.3.0/` 也保留了安装器、免安装主程序、卸载器、使用说明和 SHA-256 清单。正常使用推荐安装器。

## 功能

- 拖入一张或多张图片，后台逐张压缩为 JPG。
- 点击浮窗后按 `Ctrl+V`，可处理剪贴板图片或首个图片文件。
- 默认限制 2 MB，可调整为 0.10–50 MB（1 MB = 1,000,000 字节）。
- 输出自动命名为 `原名ys.jpg`；重名自动编号，不覆盖原图和已有文件。
- 保存位置可选原图目录、桌面或自定义文件夹。
- 自动处理照片 EXIF 方向，包括旋转和镜像；透明区域转为白色。
- 批次中单个文件失败不影响后续文件。
- 浮窗颜色、标题栏颜色、不透明度、置顶、结果提示时长和通知可配置。
- 关闭浮窗后收进系统托盘，不占任务栏；重复启动会唤醒已有实例。
- 提供用户级安装、升级失败回滚和卸载入口。

支持 JPG/JPEG、PNG、BMP、GIF、TIF/TIFF。GIF 和多页 TIFF 只取第一帧/页。暂不支持 WebP、HEIC、AVIF。单图最多 8000 万像素。

图片在本机处理，不上传图片。

## 设置

右键系统托盘中的 Pinch 图标，选择“设置...”。完整使用说明见 [Pinch-User-Guide.md](Pinch-User-Guide.md)。

## 从源码构建

需要 Windows 10/11、Windows PowerShell 5.1 和 .NET Framework C# 编译器。没有第三方 NuGet 依赖。

```powershell
git clone https://github.com/Ganinsea/Pinch.git
cd Pinch
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build-Installer.ps1
```

输出为 `bin/Pinch.exe`、`bin/PinchUninstall.exe`、`dist/PinchSetup.exe`。安装器会强制先重建主程序，编译失败时停止打包。

## 测试

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Test.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\Test-BuildFailure.ps1
```

包含方向、批量队列、防覆盖、设置、真实 30 秒定时器、安装回滚和卸载边界测试。旧版回滚样例位于 `tests/fixtures/legacy/`，克隆仓库后不需要任何外部历史备份即可执行。

## 目录

| 目录 | 内容 |
| --- | --- |
| `src/` | 主程序、设置、图片处理、安装和卸载源码 |
| `assets/` | 原始 logo、旧图标、当前透明图标及恢复备份 |
| `tests/` | 自动回归、故障注入和所需样例 |
| `scripts/` | 构建与公开导出工具 |
| `docs/` | 升级记录和项目交接资料 |
| `releases/v2.3.0/` | 当前已发布程序和安装包 |

个人设置默认存放于 `%LOCALAPPDATA%\Pinch\data`，不作为公共默认配置提交。本机开发目录、运行日志、临时测试输出和重复历史备份不是构建依赖。公开仓库包含完整构建和测试所需内容。

继续开发请阅读 [HANDOFF.md](HANDOFF.md) 与 [AGENTS.md](AGENTS.md)。
