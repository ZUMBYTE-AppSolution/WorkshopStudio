#!/usr/bin/env python3
"""Offline source-contract checks only. Does not compile or execute C# or WPF."""
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
CORE = ROOT / 'src' / 'WorkshopStudio.Core'
DESKTOP = ROOT / 'src' / 'WorkshopStudio.Desktop'
resolver = (CORE / 'ModSourceResolver.cs').read_text(encoding='utf-8')
builder = (CORE / 'PackageBuilder.cs').read_text(encoding='utf-8')
models = (CORE / 'Models.cs').read_text(encoding='utf-8')
vm = (DESKTOP / 'ViewModels/MainViewModel.cs').read_text(encoding='utf-8')
editor = (DESKTOP / 'Views/ModEditorWindow.xaml.cs').read_text(encoding='utf-8')
preview = (DESKTOP / 'Views/PackagePreviewWindow.xaml.cs').read_text(encoding='utf-8')
results = []

def check(name, condition):
    ok = bool(condition)
    results.append(ok)
    print(('PASS' if ok else 'FAIL') + ': ' + name)

check('Safe default is SingleMod with persisted explicit selection',
      'SourceMode { get; set; } = ModSourceMode.SingleMod' in models and 'List<string> SelectedModFolders' in models)
check('Zomboid AppID is recognized even when Generic was selected', 'app.AppId == 108600 ||' in resolver)
check('Mandatory scope validation precedes creation of the staging directory',
      builder.index('Validate(app, mod, settings);') < builder.index('Directory.CreateDirectory(root);')
      and builder.index('var plan = ModSourceResolver.Resolve(app, mod);') < builder.index('if(settings.ValidateZomboidLayout'))
check('Game and Steam markers are rejected and guard is repeated during traversal',
      all(t in resolver for t in ['ProjectZomboid64.exe', 'ProjectZomboid32.exe', 'steamapps', 'vanillaLayout'])
      and 'ModSourceResolver.EnsureNotGameDirectory(directory);' in builder)
check('Multiple candidates do not silently become one upload',
      'discovery.Candidates.Count != 1' in resolver and 'FirstOrDefault(c =>' in resolver and 'SelectedModFolders' in resolver)
check('Selection rejects duplicate folder names and path traversal',
      'names.Distinct(StringComparer.OrdinalIgnoreCase)' in resolver and 's is "." or ".."' in resolver and 's.IndexOfAny' in resolver)
check('B42 version sources include the mod root and preserve common',
      'parentCandidate is { Versioned: true }' in resolver and 'candidate.Versioned ? ["common"]' in resolver)
check('Numeric mod name directly under mods does not expand to siblings',
      '!parent.Name.Equals("mods", StringComparison.OrdinalIgnoreCase)' in resolver and 'var namedContainer' in resolver)
check('Copy loop uses resolved mounts, not the entire unfiltered ContentFolder',
      'SafeEntries(mount.SourceRoot, mount.TargetPrefix' in builder and 'SafeFiles(mod.ContentFolder)' not in builder
      and 'var prefix = "mods/" + candidate.FolderName;' in resolver)
check('Required metadata is checked against actual staged files after filtering',
      'foreach (var info in mount.RequiredInfoFiles)' in builder and '!files.Any(f => f.RelativePath == Join(mount.TargetPrefix, info))' in builder)
check('Byte limit is enforced during copy, not after whole-file allocation',
      'total + length + read > settings.MaxPackageMiB * 1048576L' in builder)
check('Empty directories are retained and form part of deterministic identity',
      'mount.RequiredDirectories' in builder and 'directories.OrderBy' in builder and 'directories = sortedDirectories' in builder)
check('Display formatting is not serialized into the content fingerprint',
      '[System.Text.Json.Serialization.JsonIgnore]\n    public string SizeLabel' in builder)
check('Template produces a direct mod root without a Contents wrapper',
      '"Contents"' not in (CORE / 'TemplateCreator.cs').read_text(encoding='utf-8'))
check('Editor exposes single selection, explicit bundle selection, and source mapping',
      'ChooseSource(false)' in editor and 'ChooseSource(true)' in editor and 'new ModPickerWindow' in editor and 'SourceMapping.Text=plan.Summary' in editor)
check('Preview shows all files and source mapping without invoking upload',
      'FileGrid.ItemsSource=snapshot.Files' in preview and 'MappingBox.Text=snapshot.Plan.Summary' in preview
      and 'Take(500)' not in preview and 'SteamCmd' not in preview and 'KEIN UPLOAD' in preview)
watchers = vm[vm.index('private void ConfigureAutomation()'):vm.index('private void ConfigureAutomation()')+4500]
check('File watchers subscribe to resolved source mounts',
      'foreach(var mount in plan.Sources)' in watchers and 'new FileSystemWatcher(sourceFolder)' in watchers)
dashboard = (DESKTOP / 'Views/DashboardView.xaml').read_text(encoding='utf-8')
check('Dashboard uses live bindings for games, publications, and activities',
      all(t in dashboard for t in ['DashboardApps', 'PublishedCount', 'RecentJobs', 'ActivityPanel', 'QuickActions']))
tests = (ROOT / 'tests/WorkshopStudio.SelfTest/ModSourceTests.cs').read_text(encoding='utf-8')
test_count = len(re.findall(r'^\s*(?:AsyncTest|Test)\("', tests, re.M))
program = (ROOT / 'tests/WorkshopStudio.SelfTest/Program.cs').read_text(encoding='utf-8')
check('44 packaging and local-folder matching cases are registered for local C# execution',
      test_count == 44 and re.search(r'RegisterModSourceTests\(root,\s*builder,\s*store\);', program) is not None)
readme = (ROOT / 'README_DE.md').read_text(encoding='utf-8')
check('Setup docs no longer require a manually prepared Contents folder',
      'muss der gewählte Inhaltsordner direkt `mods/` enthalten' not in readme and 'Upload-Inhalt prüfen' in readme
      and 'Du musst keinen `Content/`-' in readme)
print(f'\n{sum(results)}/{len(results)} offline source-contract checks passed.')
print('C# was not compiled or executed. No WPF rendering or Steam upload is verified by this script.')
sys.exit(0 if all(results) else 1)
