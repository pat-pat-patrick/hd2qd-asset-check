# HD2QD Asset Check

A plugin of [HD2 Mod Quick Deploy](https://github.com/pat-pat-patrick/hd2-mod-cli). It compares
each enabled mod's models and textures with the game resources they replace:

- detail levels, and triangle counts at close and long range;
- shadows, culling boxes, and added lights;
- mipmaps, compression, and textures resident in video memory;
- the video memory each mod takes against the game's own copies.

The results come from the files, not from measurements in game.

## Use

1. In HD2 Mod Quick Deploy, open the Plugins page (the jigsaw piece beside the gear).
2. Choose **Add plugin…** and select `HD2QD.AssetCheck.dll`, or drop the file on the page.
3. Choose **Open** on the plugin's card.

On the command line: `hd2qd asset-check`.

## Build

`build.bat` runs the tests, then writes `release\HD2QD.AssetCheck.dll`. It needs the .NET SDK
8 or later (`dotnet` on PATH, or `DOTNET` set to its `dotnet.exe`). The plugin itself runs on
the .NET Framework 4.8 that Windows 10 (1903 and later) and Windows 11 include.

---

# HD2QD 资源检查

[HD2 Mod Quick Deploy](https://github.com/pat-pat-patrick/hd2-mod-cli) 的插件。将各已启用 MOD 的
模型与贴图与其所替换的原版资源对比：

- 细节层级，以及近距离与远距离的三角面数；
- 阴影、剔除盒与新增灯光；
- mipmap、压缩，以及常驻显存的贴图；
- 各 MOD 的显存占用与原版资源的对比。

结果基于文件分析，并非游戏内实测。

## 使用

1. 在 HD2 Mod Quick Deploy 中打开“插件”页（齿轮旁的拼图按钮）。
2. 点击“添加插件…”，选择 `HD2QD.AssetCheck.dll`，或将该文件拖到插件页上。
3. 在插件卡片上点击“打开”。

命令行：`hd2qd asset-check`。

## 构建

`build.bat` 先运行测试，再生成 `release\HD2QD.AssetCheck.dll`。构建需要 .NET SDK 8 或更高版本
（`dotnet` 在 PATH 中，或将 `DOTNET` 设为其 `dotnet.exe`）。插件本身运行于 Windows 10（1903
及以上）和 Windows 11 自带的 .NET Framework 4.8。
