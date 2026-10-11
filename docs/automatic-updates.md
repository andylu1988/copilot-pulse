# 自动更新 / Automatic updates

从 v1.11.0 起，应用运行时每小时检查所选仓库的最新正式版本。关闭应用时不会运行额外后台服务；重新打开会按上次检查时间继续检查。版本提醒与首次发现时间保存在数据目录的 `update-state.json`，不会因重启而重置。

发现新版本后，主界面“迷你”左侧出现 **有新版本** 按钮，鼠标悬停可查看版本号和按系统时区显示的自动安装时间。后台预先下载 Windows x64 安装包并校验 GitHub 提供的 SHA256。如果用户一直不打开更新窗口，首次发现满 **48 小时** 后自动升级；打开更新窗口会取消该版本的无人值守安装，用户可选择手动安装。新版本发布后重新开始完整的 48 小时提醒期。

安装需要短暂关闭客户端。更新助手运行在临时目录，先验证安装包并等待当前客户端退出、释放文件与锁，再静默安装到原目录。安装完成后自动重新打开客户端，无需管理员权限或重启 Windows。历史记录、窗口位置、皮肤、字体与本机 WebView2 登录资料保留。检查/下载/校验失败时保留当前版本并等待下一小时重试。安装失败会尝试重新打开原客户端；这不是完整的原子回滚系统。

实现沿用现有每用户 Inno Setup 安装包，采用常见的“检查、暂存、校验、退出、安装、重启”流程。不引入 Windows 服务或定时任务，也不在自动检查时打开浏览器登录。GitHub 公共仓库无需 GitHub 账号；EMU 私有仓库仍需要已授权的 GitHub CLI。

Applications running v1.11.0+ check for stable releases hourly while running. A new-version button appears directly before Mini. Downloads are staged and SHA256 verified. Unopened notifications automatically install after 48 hours from first discovery, persisted in UTC across restarts. Opening Updates cancels unattended installation for that release. The helper waits for the client to exit, runs the existing per-user installer silently, and restarts the client. Windows does not need to reboot. Failed checks/downloads retry hourly. Installer failures attempt to relaunch the installed client; full atomic rollback is not provided.

## 参考 / References

- [Inno Setup command-line parameters](https://jrsoftware.org/ishelp/topic_setupcmdline.htm): silent setup, restart suppression, install directory and logging.
- [Electron autoUpdater](https://www.electronjs.org/docs/latest/api/auto-updater): established download-ready and quit-and-install lifecycle; this WPF app uses its existing Inno package rather than Electron.
