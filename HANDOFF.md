# Pinch 开发交接

当前源码版本：2.3.0，开发者 Han。仓库可放在任意 Windows 本地目录，构建脚本以自身所在目录为根。

1. 阅读 `README.md`、`Pinch-User-Guide.md` 和 `docs/upgrade-2.3.0.md`。
2. 执行 `Build-Installer.ps1` 构建，再执行 `Test.ps1` 回归。
3. 图片处理修改必须覆盖 EXIF 1–8、两种 TIFF 字节序、批量混合方向和不覆盖文件。
4. 设置修改必须检查保存/取消、重启恢复与队列配置快照。
5. 安装修改必须保留协作退出、原子替换、故障回滚和卸载用户数据保留。

## 入口

- `src/ImageCompressorFloat.cs`：进程入口、命令行和单实例。
- `src/ImageCompressor.cs`：方向、编码与安全输出。
- `src/FloatForm.cs`：浮窗、托盘、队列和完成提示。
- `src/PinchSettings.cs`、`src/SettingsForm.cs`：设置模型和窗口。
- `src/InstallerCore.cs`、`src/PinchSetup.cs`、`src/PinchUninstall.cs`：安装/卸载。
- `src/AppInfo.cs`：版本来源；保持 `src/app.manifest` 版本一致。

原始图片不要加入版本库。测试生成的素材放在 `tests/output`，设置/日志留在用户数据目录。

`tests/fixtures/legacy/Pinch.exe` 是回滚测试数据，不应作为当前应用启动。测试只对它进行复制和哈希比较。

## 验收边界

源码含完整本地回归。原生 WinForms 设置窗口的三页布局已经渲染检查。维护者仍需在其目标 Windows、高 DPI 和多显示器环境中检查实际鼠标拖放、系统剪贴板和托盘操作；不要将代码测试当作所有桌面环境的验收。

发布包位于 `releases/v2.3.0/`，属于已发布版本快照。新的构建输出位于 `bin/`、`dist/`。后续发布应使用新版本目录和新 GitHub Release，不能静默覆盖旧版本的验收记录。
