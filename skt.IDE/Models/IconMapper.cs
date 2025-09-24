using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace skt.IDE.Models;

public static class IconMapper
{
    // Defaults
    private const string DefaultFile = IconKeys.Document;
    private const string DefaultFolderClosed = IconKeys.FolderBase;
    private const string DefaultFolderOpen = IconKeys.FolderBaseOpen;

    // Compiled regex patterns for better performance
    private static readonly Regex SrcFolderPattern = new(@"^(src|source|sources)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TestFolderPattern = new(@"^(test|tests|__tests__|spec|specs)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ImagesFolderPattern = new(@"^(img|imgs|image|images|assets|res|resources|media|icons?)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SvgFolderPattern = new(@"^(svg|svgs)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LibFolderPattern = new(@"^(lib|libs|library|libraries|vendor|packages)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex IncludeFolderPattern = new(@"^(include|includes|inc)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LogFolderPattern = new(@"^(log|logs|logging)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ToolsFolderPattern = new(@"^(tools?|tooling)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ScriptsFolderPattern = new(@"^(script|scripts|bin)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex UtilsFolderPattern = new(@"^(util|utils|utility|utilities)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TempFolderPattern = new(@"^(temp|tmp|cache)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TargetFolderPattern = new(@"^(build|target|out|dist)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ThemeFolderPattern = new(@"^(theme|themes)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PluginFolderPattern = new(@"^(plugin|plugins|extensions)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PublicFolderPattern = new(@"^(public|wwwroot|static)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PrivateFolderPattern = new(@"^(private|secret|secrets)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DockerFolderPattern = new(@"^(docker|compose|containers?)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex GitFolderPattern = new(@"^\.git$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex GithubFolderPattern = new(@"^\.github$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex JsonFolderPattern = new(@"^(json|schemas?)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PdfFolderPattern = new(@"^(pdf|docs?|documents)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ImportFolderPattern = new(@"^(include|imports?)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TrashFolderPattern = new(@"^(trash|recycle)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TemplateFolderPattern = new(@"^(template|templates)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex OtherFolderPattern = new(@"^(other|misc|miscellaneous)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PowershellFolderPattern = new(@"^(powershell|ps)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // File name patterns
    private static readonly Regex DockerfilePattern = new(@"^dockerfile(\..+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DockerComposePattern = new(@"^docker[-_]?compose(\..+)?(\.ya?ml|\.yml)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DockerIgnorePattern = new(@"^\.dockerignore$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ReadmePattern = new(@"^readme(\..+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LicensePattern = new(@"^(license|licence)(\..+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex GitlabCiPattern = new(@"^gitlab-ci(\..+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex GitPattern = new(@"^\.git(ignore|attributes)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex GithubPattern = new(@"^\.github$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SettingsPattern = new(@"^settings?\.(json|ya?ml)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PackageJsonPattern = new(@"^package(-lock)?\.json$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex JsconfigPattern = new(@"^jsconfig\.json$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex FaviconPattern = new(@"^favicon\.(ico|png|svg)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AppSettingsPattern = new(@"^appsettings(\.\w+)?\.json$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HttpPattern = new(@"^http(\..+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex WorkflowPattern = new(@"^workflow(s)?(\..+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LockPattern = new(@"^lock(\..+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // File extension => icon key (using Dictionary for O(1) lookup)
    private static readonly Dictionary<string, string> ExtIcons = new(StringComparer.OrdinalIgnoreCase)
    {
        [".json"] = IconKeys.Json,
        [".yaml"] = IconKeys.Yaml, [".yml"] = IconKeys.Yaml,
        [".xml"] = IconKeys.Xml,
        [".md"] = IconKeys.Markdown, [".markdown"] = IconKeys.Markdown,
        [".txt"] = IconKeys.Document,
        [".log"] = IconKeys.Log,
        [".pdf"] = IconKeys.Pdf,
        [".ps1"] = IconKeys.Powershell, [".psm1"] = IconKeys.Powershell, [".psd1"] = IconKeys.Powershell,
        [".png"] = IconKeys.Image,
        [".jpg"] = IconKeys.Image,
        [".jpeg"] = IconKeys.Image,
        [".gif"] = IconKeys.Image,
        [".svg"] = IconKeys.SvgFile,
        [".ico"] = IconKeys.Favicon,
        [".htm"] = IconKeys.Html,
        [".html"] = IconKeys.Html,
        [".js"] = IconKeys.Javascript,
        [".mjs"] = IconKeys.Javascript,
        [".cjs"] = IconKeys.Javascript,
        [".csv"] = IconKeys.Table,
        [".tsv"] = IconKeys.Table,
        [".ttf"] = IconKeys.Font,
        [".otf"] = IconKeys.Font,
        [".woff"] = IconKeys.Font,
        [".woff2"] = IconKeys.Font,
        [".db"] = IconKeys.Database,
        [".sqlite"] = IconKeys.Database,
        [".zip"] = IconKeys.Zip,
        [".gz"] = IconKeys.Zip,
        [".tar"] = IconKeys.Zip,
        [".7z"] = IconKeys.Zip,
        [".mp3"] = IconKeys.Audio,
        [".wav"] = IconKeys.Audio,
        [".mp4"] = IconKeys.Video,
        [".mov"] = IconKeys.Video,
        [".skt"] = IconKeys.SktFile,
        [".dll"] = IconKeys.Assembly,
        [".exe"] = IconKeys.Assembly,
    };


    private static readonly (Regex Pattern, string Closed, string Open)[] FolderIconRules =
    [
        (GitFolderPattern, IconKeys.FolderGit, IconKeys.FolderGitOpen),
        (GithubFolderPattern, IconKeys.FolderGithub, IconKeys.FolderGithubOpen),
        (SrcFolderPattern, IconKeys.FolderSrc, IconKeys.FolderSrcOpen),
        (TestFolderPattern, IconKeys.FolderTest, IconKeys.FolderTestOpen),
        (ImagesFolderPattern, IconKeys.FolderImages, IconKeys.FolderImagesOpen),
        (SvgFolderPattern, IconKeys.FolderSvg, IconKeys.FolderSvgOpen),
        (LibFolderPattern, IconKeys.FolderLib, IconKeys.FolderLibOpen),
        (IncludeFolderPattern, IconKeys.FolderInclude, IconKeys.FolderIncludeOpen),
        (LogFolderPattern, IconKeys.FolderLog, IconKeys.FolderLogOpen),
        (ToolsFolderPattern, IconKeys.FolderTools, IconKeys.FolderToolsOpen),
        (ScriptsFolderPattern, IconKeys.FolderScripts, IconKeys.FolderScriptsOpen),
        (UtilsFolderPattern, IconKeys.FolderUtils, IconKeys.FolderUtilsOpen),
        (TempFolderPattern, IconKeys.FolderTemp, IconKeys.FolderTempOpen),
        (TargetFolderPattern, IconKeys.FolderTarget, IconKeys.FolderTargetOpen),
        (ThemeFolderPattern, IconKeys.FolderTheme, IconKeys.FolderThemeOpen),
        (PluginFolderPattern, IconKeys.FolderPlugin, IconKeys.FolderPluginOpen),
        (PublicFolderPattern, IconKeys.FolderPublic, IconKeys.FolderPublicOpen),
        (PrivateFolderPattern, IconKeys.FolderPrivate, IconKeys.FolderPrivateOpen),
        (DockerFolderPattern, IconKeys.FolderDocker, IconKeys.FolderDockerOpen),
        (JsonFolderPattern, IconKeys.FolderJson, IconKeys.FolderJsonOpen),
        (PdfFolderPattern, IconKeys.FolderPdf, IconKeys.FolderPdfOpen),
        (ImportFolderPattern, IconKeys.FolderImport, IconKeys.FolderImportOpen),
        (TrashFolderPattern, IconKeys.FolderTrash, IconKeys.FolderTrashOpen),
        (TemplateFolderPattern, IconKeys.FolderTemplate, IconKeys.FolderTemplateOpen),
        (OtherFolderPattern, IconKeys.FolderOther, IconKeys.FolderOtherOpen),
        (PowershellFolderPattern, IconKeys.FolderPowershell, IconKeys.FolderPowershellOpen),
    ];

    private static readonly (Regex Pattern, string Icon)[] FileNameIconRules =
    [
        (DockerfilePattern, IconKeys.Docker),
        (DockerComposePattern, IconKeys.Docker),
        (DockerIgnorePattern, IconKeys.Docker),
        (ReadmePattern, IconKeys.Readme),
        (LicensePattern, IconKeys.Document),
        (GitlabCiPattern, IconKeys.Gitlab),
        (GitPattern, IconKeys.Git),
        (GithubPattern, IconKeys.Git),
        (SettingsPattern, IconKeys.Settings),
        (PackageJsonPattern, IconKeys.Jsconfig),
        (JsconfigPattern, IconKeys.Jsconfig),
        (FaviconPattern, IconKeys.Favicon),
        (AppSettingsPattern, IconKeys.Settings),
        (HttpPattern, IconKeys.Http),
        (WorkflowPattern, IconKeys.GithubActionsWorkflow),
        (LockPattern, IconKeys.Lock),
    ];

    private static readonly (Regex Pattern, string Icon)[] ExtRegexIconRules =
    [
        (new Regex(@"^\.(png|jpe?g|gif|svg)$", RegexOptions.Compiled | RegexOptions.IgnoreCase), IconKeys.Image),
        (new Regex(@"^\.(mp3|wav|flac|ogg)$", RegexOptions.Compiled | RegexOptions.IgnoreCase), IconKeys.Audio),
        (new Regex(@"^\.(mp4|mov|avi|mkv|webm)$", RegexOptions.Compiled | RegexOptions.IgnoreCase), IconKeys.Video),
        (new Regex(@"^\.(ttf|otf|woff2?)$", RegexOptions.Compiled | RegexOptions.IgnoreCase), IconKeys.Font),
        (new Regex(@"^\.(zip|gz|tar|7z|rar)$", RegexOptions.Compiled | RegexOptions.IgnoreCase), IconKeys.Zip),
        (new Regex(@"^\.(db|sqlite)$", RegexOptions.Compiled | RegexOptions.IgnoreCase), IconKeys.Database),
        (new Regex(@"^\.(ps(d|m)?1)$", RegexOptions.Compiled | RegexOptions.IgnoreCase), IconKeys.Powershell),
    ];

    private static readonly ConcurrentDictionary<string, string> FileIconCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, string> FolderIconCache = new(StringComparer.OrdinalIgnoreCase);

    public static string GetFolderIconKey(string folderName, bool isOpen)
    {
        var name = folderName.Trim();

        if (name.Length == 0) return isOpen ? DefaultFolderOpen : DefaultFolderClosed;

        var cacheKey = (isOpen ? "1:" : "0:") + name.ToLowerInvariant();
        if (FolderIconCache.TryGetValue(cacheKey, out var cached)) return cached;

        var matched = FolderIconRules.FirstOrDefault(r => r.Pattern.IsMatch(name));
        if (matched.Pattern is not null)
        {
            var icon = isOpen ? matched.Open : matched.Closed;
            FolderIconCache[cacheKey] = icon;
            return icon;
        }

        var @default = isOpen ? DefaultFolderOpen : DefaultFolderClosed;
        FolderIconCache[cacheKey] = @default;
        return @default;
    }

    public static string GetFileIconKey(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return DefaultFile;
        var name = Path.GetFileName(fileName.Trim());

        if (FileIconCache.TryGetValue(name, out var cached)) return cached;

        var fileNameRule = FileNameIconRules.FirstOrDefault(r => r.Pattern.IsMatch(name));
        if (fileNameRule.Pattern is not null)
        {
            FileIconCache[name] = fileNameRule.Icon;
            return fileNameRule.Icon;
        }

        var ext = Path.GetExtension(name);
        if (!string.IsNullOrEmpty(ext) && ExtIcons.TryGetValue(ext, out var mapped))
        {
            FileIconCache[name] = mapped;
            return mapped;
        }

        if (!string.IsNullOrEmpty(ext))
        {
            var extRule = ExtRegexIconRules.FirstOrDefault(r => r.Pattern.IsMatch(ext));
            if (extRule.Pattern is not null)
            {
                FileIconCache[name] = extRule.Icon;
                return extRule.Icon;
            }
        }

        FileIconCache[name] = DefaultFile;
        return DefaultFile;
    }

}
