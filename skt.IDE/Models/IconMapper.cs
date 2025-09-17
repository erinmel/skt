using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace skt.IDE.Models;

public static class IconMapper
{
    private const string Prefix = "avares://skt.IDE/Assets/icons";

    // Defaults
    private const string DefaultFile = $"{Prefix}/files/document.svg";
    private const string DefaultFolderClosed = $"{Prefix}/folders/folder-base.svg";
    private const string DefaultFolderOpen = $"{Prefix}/folders/folder-base-open.svg";

    // Folder name => icon base (without -open)
    private static readonly (Regex Pattern, string BaseName)[] FolderRules =
    {
        (Rx(@"^(src|source|sources)$"), "folder-src"),
        (Rx(@"^(test|tests|__tests__|spec|specs)$"), "folder-test"),
        (Rx(@"^(img|imgs|image|images|assets|res|resources|media|icons?)$"), "folder-images"),
        (Rx(@"^(svg|svgs)$"), "folder-svg"),
        (Rx(@"^(lib|libs|library|libraries|vendor|packages)$"), "folder-lib"),
        (Rx(@"^(include|includes|inc)$"), "folder-include"),
        (Rx(@"^(log|logs|logging)$"), "folder-log"),
        (Rx(@"^(tools?|tooling)$"), "folder-tools"),
        (Rx(@"^(script|scripts|bin)$"), "folder-scripts"),
        (Rx(@"^(util|utils|utility|utilities)$"), "folder-utils"),
        (Rx(@"^(temp|tmp|cache)$"), "folder-temp"),
        (Rx(@"^(build|target|out|dist)$"), "folder-target"),
        (Rx(@"^(theme|themes)$"), "folder-theme"),
        (Rx(@"^(plugin|plugins|extensions)$"), "folder-plugin"),
        (Rx(@"^(public|wwwroot|static)$"), "folder-public"),
        (Rx(@"^(private|secret|secrets)$"), "folder-private"),
        (Rx(@"^(docker|compose|containers?)$"), "folder-docker"),
        (Rx(@"^(json|schemas?)$"), "folder-json"),
        (Rx(@"^(pdf|docs?|documents)$"), "folder-pdf"),
        (Rx(@"^(include|imports?)$"), "folder-import"),
        (Rx(@"^(trash|recycle)$"), "folder-trash"),
        (Rx(@"^(template|templates)$"), "folder-template"),
        (Rx(@"^(other|misc|miscellaneous)$"), "folder-other"),
    };

    // File extension => icon name
    private static readonly Dictionary<string, string> ExtIcons = new(StringComparer.OrdinalIgnoreCase)
    {
        [".json"] = "json",
        [".yaml"] = "yaml",
        [".yml"] = "yaml",
        [".xml"] = "xml",
        [".md"] = "markdown",
        [".markdown"] = "markdown",
        [".txt"] = "document",
        [".log"] = "log",
        [".pdf"] = "pdf",
        [".ps1"] = "powershell",
        [".psm1"] = "powershell",
        [".psd1"] = "powershell",
        [".png"] = "image",
        [".jpg"] = "image",
        [".jpeg"] = "image",
        [".gif"] = "image",
        [".svg"] = "image",
        [".ico"] = "favicon",
        [".htm"] = "html",
        [".html"] = "html",
        [".js"] = "javascript",
        [".mjs"] = "javascript",
        [".cjs"] = "javascript",
        [".csv"] = "table",
        [".tsv"] = "table",
        [".ttf"] = "font",
        [".otf"] = "font",
        [".woff"] = "font",
        [".woff2"] = "font",
        [".db"] = "database",
        [".sqlite"] = "database",
        [".zip"] = "zip",
        [".gz"] = "zip",
        [".tar"] = "zip",
        [".7z"] = "zip",
        [".mp3"] = "audio",
        [".wav"] = "audio",
        [".mp4"] = "video",
        [".mov"] = "video",
    };

    // File name regex => icon name
    private static readonly (Regex Pattern, string Icon)[] FileNameRules =
    {
        (Rx(@"^dockerfile(\..+)?$"), "docker"),
        (Rx(@"^compose(\.ya?ml)?$"), "docker"),
        (Rx(@"^readme(\..+)?$"), "readme"),
        (Rx(@"^(license|licence)(\..+)?$"), "document"),
        (Rx(@"^gitlab-ci(\..+)?$"), "gitlab"),
        (Rx(@"^\.git(ignore|attributes)?$"), "git"),
        (Rx(@"^\.github$"), "git"),
        (Rx(@"^settings?\.(json|ya?ml)$"), "settings"),
        (Rx(@"^package(-lock)?\.json$"), "jsconfig"),
        (Rx(@"^jsconfig\.json$"), "jsconfig"),
        (Rx(@"^favicon\.(ico|png|svg)$"), "favicon"),
        (Rx(@"^appsettings(\.\w+)?\.json$"), "settings"),
        (Rx(@"^http(\..+)?$"), "http"),
        (Rx(@"^workflow(s)?(\..+)?$"), "github-actions-workflow"),
        (Rx(@"^lock(\..+)?$"), "lock"),
    };

    public static string GetFolderIconPath(string folderName, bool isOpen)
    {
        var name = (folderName ?? string.Empty).Trim();
        foreach (var (pattern, baseName) in FolderRules)
        {
            if (pattern.IsMatch(name))
            {
                return $"{Prefix}/folders/{baseName}{(isOpen ? "-open" : string.Empty)}.svg";
            }
        }
        return isOpen ? DefaultFolderOpen : DefaultFolderClosed;
    }

    public static string GetFileIconPath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return DefaultFile;
        var name = Path.GetFileName(fileName.Trim());

        // Name-based first
        foreach (var (pattern, icon) in FileNameRules)
        {
            if (pattern.IsMatch(name))
            {
                return $"{Prefix}/files/{icon}.svg";
            }
        }

        // Extension-based
        var ext = Path.GetExtension(name);
        if (!string.IsNullOrEmpty(ext) && ExtIcons.TryGetValue(ext, out var mapped))
        {
            return $"{Prefix}/files/{mapped}.svg";
        }

        return DefaultFile;
    }

    private static Regex Rx(string pattern) => new(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
}
