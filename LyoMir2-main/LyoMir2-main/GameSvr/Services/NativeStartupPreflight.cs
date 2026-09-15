using SystemModule;

namespace GameSvr.Services
{
    /// <summary>
    /// 启动期数据目录解析与缺文件提示。不伪造 Envir/Map 内容；只把相对路径落到
    /// LYOMIR_ROOT（或可执行目录的上一级，即原版 Mir200）并打出可读错误。
    /// 本机已核实的 1.85 数据根见 <see cref="ExampleMir200Root"/>，不要拷进仓库。
    /// </summary>
    internal static class NativeStartupPreflight
    {
        internal const string RootEnvironmentVariable = "LYOMIR_ROOT";

        /// <summary>
        /// 战神原包 传奇1.85纯净版 Mir200（含 Envir/Map/Share/Gs1）。仅作启动提示，
        /// 不会在未设置环境变量时自动改路径。
        /// </summary>
        internal const string ExampleMir200Root =
            @"F:\BaiduNetdiskDownload\战神引擎包(1)\战神引擎包\4.版本库\传奇1.85纯净版\Mir200";

        internal static void ApplyEnvironmentRoot()
        {
            var envRoot = Environment.GetEnvironmentVariable(RootEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(envRoot))
                return;
            envRoot = envRoot.Trim();
            if (!Directory.Exists(envRoot))
            {
                M2Share.ErrorMessage(
                    $"[启动检查] 环境变量 {RootEnvironmentVariable}={envRoot} 不是有效目录，已忽略。");
                return;
            }

            M2Share.sRootPath = Path.GetFullPath(envRoot);
        }

        /// <summary>
        /// LoadConfig 可能把 [Share] 相对路径写回 g_Config。相对路径一律相对 sRootPath
        ///（战神 GS1 的父目录 / LYOMIR_ROOT），已是绝对路径则规范化。
        /// </summary>
        internal static void ApplySharePaths()
        {
            ApplyEnvironmentRoot();
            var cfg = M2Share.g_Config;
            if (cfg == null)
                return;
            cfg.sBaseDir = ResolveUnderRoot(cfg.sBaseDir);
            cfg.sEnvirDir = ResolveUnderRoot(cfg.sEnvirDir);
            cfg.sMapDir = ResolveUnderRoot(cfg.sMapDir);
            cfg.sNoticeDir = ResolveUnderRoot(cfg.sNoticeDir);
            cfg.sLogDir = ResolveUnderRoot(cfg.sLogDir);
        }

        internal static void ReportMissingRuntimeFiles()
        {
            var setupPath = Path.Combine(M2Share.sConfigPath, M2Share.sConfigFileName);
            M2Share.MainOutMessage(
                $"[启动检查] GS1(sConfigPath)={M2Share.sConfigPath}");
            M2Share.MainOutMessage(
                $"[启动检查] Mir200(sRootPath)={M2Share.sRootPath} " +
                $"(可用环境变量 {RootEnvironmentVariable} 覆盖，例如 {ExampleMir200Root})");

            if (!File.Exists(setupPath) || new FileInfo(setupPath).Length == 0)
            {
                M2Share.ErrorMessage(
                    $"[启动检查] 缺少 {M2Share.sConfigFileName}: {setupPath}。" +
                    "可参考仓库 GameSvr/!Setup.example.txt，复制为进程目录下的 !Setup.txt。" +
                    "无此文件时使用内置默认值，不自动生成空配置。");
            }
            else
            {
                M2Share.MainOutMessage($"[启动检查] 已读取 {setupPath}");
            }

            var envirDir = M2Share.g_Config?.sEnvirDir ?? string.Empty;
            var mapDir = M2Share.g_Config?.sMapDir ?? string.Empty;
            var shareDir = M2Share.g_Config?.sBaseDir ?? string.Empty;
            var mapInfo = CombineConfig(envirDir, "MapInfo.txt");
            var miniMap = CombineConfig(envirDir, "MiniMap.txt");

            ReportDir("Envir", envirDir);
            ReportFile("MapInfo.txt", mapInfo, required: true);
            ReportFile("MiniMap.txt", miniMap, required: false);
            ReportDir("Map", mapDir);
            ReportDir("Share", shareDir);

            var conn = M2Share.g_Config?.sConnctionString;
            if (string.IsNullOrWhiteSpace(conn))
            {
                M2Share.ErrorMessage(
                    "[启动检查] 数据库连接串为空。请在 !Setup.txt [DataBase] ConnString " +
                    "或 ConnctionString 填写 MySQL（database=mir3）。");
            }
        }

        internal static void ReportMissingMapForEnter(string charName, string mapName)
        {
            var mapCount = M2Share.MapManager?.Maps?.Count ?? 0;
            M2Share.ErrorMessage(
                $"[进图失败] 角色={charName ?? "<unknown>"} 地图={mapName ?? ""} " +
                $"已加载地图数={mapCount} Envir={M2Share.g_Config?.sEnvirDir} " +
                $"Map={M2Share.g_Config?.sMapDir}。" +
                "缺 Envir/MapInfo.txt 或地图块时不会发 SM_LOGON/SM_NEWMAP。");
        }

        internal static string CombineConfig(string directory, string fileName)
        {
            if (string.IsNullOrWhiteSpace(directory))
                return Path.Combine(M2Share.sConfigPath, fileName);
            if (Path.IsPathRooted(directory))
                return Path.Combine(directory, fileName);
            return Path.Combine(M2Share.sConfigPath, directory, fileName);
        }

        private static string ResolveUnderRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path ?? string.Empty;
            if (Path.IsPathRooted(path))
                return Path.GetFullPath(path);
            return Path.GetFullPath(Path.Combine(M2Share.sRootPath, path));
        }

        private static void ReportDir(string label, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                M2Share.ErrorMessage($"[启动检查] {label} 路径为空。");
                return;
            }

            if (Directory.Exists(path))
                M2Share.MainOutMessage($"[启动检查] {label} 目录存在: {path}");
            else
                M2Share.ErrorMessage(
                    $"[启动检查] 缺少 {label} 目录: {path}。" +
                    $"请设置 {RootEnvironmentVariable} 指向完整 Mir200（不要拷进本仓库），例如 {ExampleMir200Root}。");
        }

        private static void ReportFile(string label, string path, bool required)
        {
            if (File.Exists(path))
            {
                M2Share.MainOutMessage($"[启动检查] {label} 存在: {path}");
                return;
            }

            var msg = $"[启动检查] 缺少 {label}: {path}";
            if (required)
                M2Share.ErrorMessage(msg + "。引擎初始化将失败。");
            else
                M2Share.ErrorMessage(msg + "。将继续启动，对应表为空。");
        }
    }
}
