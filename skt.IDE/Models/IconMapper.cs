using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace skt.IDE.Models;

public static class IconMapper
{
    // Defaults
    private const string DefaultFile = "Icon.Document";
    private const string DefaultFolderClosed = "Icon.FolderBase";
    private const string DefaultFolderOpen = "Icon.FolderBaseOpen";

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
    private static readonly Regex JsonFolderPattern = new(@"^(json|schemas?)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PdfFolderPattern = new(@"^(pdf|docs?|documents)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ImportFolderPattern = new(@"^(include|imports?)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TrashFolderPattern = new(@"^(trash|recycle)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TemplateFolderPattern = new(@"^(template|templates)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex OtherFolderPattern = new(@"^(other|misc|miscellaneous)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PowershellFolderPattern = new(@"^(powershell|ps)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // File name patterns
    private static readonly Regex DockerfilePattern = new(@"^dockerfile(\..+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ComposePattern = new(@"^compose(\.ya?ml)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
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
        [".json"] = "Icon.Json",
        [".yaml"] = "Icon.Yaml", [".yml"] = "Icon.Yaml",
        [".xml"] = "Icon.Xml",
        [".md"] = "Icon.Markdown", [".markdown"] = "Icon.Markdown",
        [".txt"] = "Icon.Document",
        [".log"] = "Icon.Log",
        [".pdf"] = "Icon.Pdf",
        [".ps1"] = "Icon.Powershell", [".psm1"] = "Icon.Powershell", [".psd1"] = "Icon.Powershell",
        [".png"] = "Icon.Image",
        [".jpg"] = "Icon.Image",
        [".jpeg"] = "Icon.Image",
        [".gif"] = "Icon.Image",
        [".svg"] = "Icon.Image",
        [".ico"] = "Icon.Favicon",
        [".htm"] = "Icon.Html",
        [".html"] = "Icon.Html",
        [".js"] = "Icon.Javascript",
        [".mjs"] = "Icon.Javascript",
        [".cjs"] = "Icon.Javascript",
        [".csv"] = "Icon.Table",
        [".tsv"] = "Icon.Table",
        [".ttf"] = "Icon.Font",
        [".otf"] = "Icon.Font",
        [".woff"] = "Icon.Font",
        [".woff2"] = "Icon.Font",
        [".db"] = "Icon.Database",
        [".sqlite"] = "Icon.Database",
        [".zip"] = "Icon.Zip",
        [".gz"] = "Icon.Zip",
        [".tar"] = "Icon.Zip",
        [".7z"] = "Icon.Zip",
        [".mp3"] = "Icon.Audio",
        [".wav"] = "Icon.Audio",
        [".mp4"] = "Icon.Video",
        [".mov"] = "Icon.Video",
        [".skt"] = "Icon.SktFile",
        [".dll"] = "Icon.Assembly",
        [".exe"] = "Icon.Assembly",
    };

    public static string GetFolderIconKey(string folderName, bool isOpen)
    {
        var name = (folderName ?? string.Empty).Trim();

        // Use individual regex patterns for better performance
        if (SrcFolderPattern.IsMatch(name)) return isOpen ? "Icon.FolderSrcOpen" : "Icon.FolderSrc";
        if (TestFolderPattern.IsMatch(name)) return isOpen ? "Icon.FolderTestOpen" : "Icon.FolderTest";
        if (ImagesFolderPattern.IsMatch(name)) return isOpen ? "Icon.FolderImagesOpen" : "Icon.FolderImages";
        if (SvgFolderPattern.IsMatch(name)) return isOpen ? "Icon.FolderSvgOpen" : "Icon.FolderSvg";
        if (LibFolderPattern.IsMatch(name)) return isOpen ? "Icon.FolderLibOpen" : "Icon.FolderLib";
        if (IncludeFolderPattern.IsMatch(name)) return isOpen ? "Icon.FolderIncludeOpen" : "Icon.FolderInclude";
        if (LogFolderPattern.IsMatch(name)) return isOpen ? "Icon.FolderLogOpen" : "Icon.FolderLog";
        if (ToolsFolderPattern.IsMatch(name)) return isOpen ? "Icon.FolderToolsOpen" : "Icon.FolderTools";
        if (ScriptsFolderPattern.IsMatch(name)) return isOpen ? "Icon.FolderScriptsOpen" : "Icon.FolderScripts";
        if (UtilsFolderPattern.IsMatch(name)) return isOpen ? "Icon.FolderUtilsOpen" : "Icon.FolderUtils";
        if (TempFolderPattern.IsMatch(name)) return isOpen ? "Icon.FolderTempOpen" : "Icon.FolderTemp";
        if (TargetFolderPattern.IsMatch(name)) return isOpen ? "Icon.FolderTargetOpen" : "Icon.FolderTarget";
        if (ThemeFolderPattern.IsMatch(name)) return isOpen ? "Icon.FolderThemeOpen" : "Icon.FolderTheme";
        if (PluginFolderPattern.IsMatch(name)) return isOpen ? "Icon.FolderPluginOpen" : "Icon.FolderPlugin";
        if (PublicFolderPattern.IsMatch(name)) return isOpen ? "Icon.FolderPublicOpen" : "Icon.FolderPublic";
        if (PrivateFolderPattern.IsMatch(name)) return isOpen ? "Icon.FolderPrivateOpen" : "Icon.FolderPrivate";
        if (DockerFolderPattern.IsMatch(name)) return isOpen ? "Icon.FolderDockerOpen" : "Icon.FolderDocker";
        if (JsonFolderPattern.IsMatch(name)) return isOpen ? "Icon.FolderJsonOpen" : "Icon.FolderJson";
        if (PdfFolderPattern.IsMatch(name)) return isOpen ? "Icon.FolderPdfOpen" : "Icon.FolderPdf";
        if (ImportFolderPattern.IsMatch(name)) return isOpen ? "Icon.FolderImportOpen" : "Icon.FolderImport";
        if (TrashFolderPattern.IsMatch(name)) return isOpen ? "Icon.FolderTrashOpen" : "Icon.FolderTrash";
        if (TemplateFolderPattern.IsMatch(name)) return isOpen ? "Icon.FolderTemplateOpen" : "Icon.FolderTemplate";
        if (OtherFolderPattern.IsMatch(name)) return isOpen ? "Icon.FolderOtherOpen" : "Icon.FolderOther";
        if (PowershellFolderPattern.IsMatch(name)) return isOpen ? "Icon.FolderPowershellOpen" : "Icon.FolderPowershell";

        return isOpen ? DefaultFolderOpen : DefaultFolderClosed;
    }

    public static string GetFileIconKey(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return DefaultFile;
        var name = Path.GetFileName(fileName.Trim());

        // Name-based patterns first (most specific)
        if (DockerfilePattern.IsMatch(name)) return "Icon.Docker";
        if (ComposePattern.IsMatch(name)) return "Icon.Docker";
        if (ReadmePattern.IsMatch(name)) return "Icon.Readme";
        if (LicensePattern.IsMatch(name)) return "Icon.Document";
        if (GitlabCiPattern.IsMatch(name)) return "Icon.Gitlab";
        if (GitPattern.IsMatch(name) || GithubPattern.IsMatch(name) ) return "Icon.Git";
        if (SettingsPattern.IsMatch(name)) return "Icon.Settings";
        if (PackageJsonPattern.IsMatch(name)) return "Icon.Jsconfig";
        if (JsconfigPattern.IsMatch(name)) return "Icon.Jsconfig";
        if (FaviconPattern.IsMatch(name)) return "Icon.Favicon";
        if (AppSettingsPattern.IsMatch(name)) return "Icon.Settings";
        if (HttpPattern.IsMatch(name)) return "Icon.Http";
        if (WorkflowPattern.IsMatch(name)) return "Icon.GithubActionsWorkflow";
        if (LockPattern.IsMatch(name)) return "Icon.Lock";

        // Extension-based lookup (O(1) performance)
        var ext = Path.GetExtension(name);
        if (!string.IsNullOrEmpty(ext) && ExtIcons.TryGetValue(ext, out var mapped))
        {
            return mapped;
        }

        return DefaultFile;
    }
}
