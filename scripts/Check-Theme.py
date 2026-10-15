#!/usr/bin/env python3
"""Offline source checks. This does not compile C# or render WPF. Python 3.10+; no packages."""
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
DESKTOP = ROOT / 'src' / 'WorkshopStudio.Desktop'
UI = 'http://schemas.microsoft.com/winfx/2006/xaml/presentation'
X = 'http://schemas.microsoft.com/winfx/2006/xaml'
NS = {'ui': UI, 'x': X}
results = []

def check(name, action):
    try:
        detail = action()
        results.append((name, True, detail or ''))
    except Exception as exc:
        results.append((name, False, str(exc)))

def require(condition, message):
    if not condition:
        raise AssertionError(message)

def source_files():
    return [p for p in ROOT.rglob('*') if p.is_file() and not {'obj', 'bin', 'artifacts', 'work', '.git'}.intersection(p.relative_to(ROOT).parts)]

files = source_files()
xamls = [p for p in files if p.suffix == '.xaml']

def parse_xml():
    items = [p for p in files if p.suffix in {'.xaml', '.csproj', '.props', '.manifest'}]
    for p in items:
        ET.parse(p)
    return f'{len(items)} XML/XAML/project files'
check('XML syntax', parse_xml)

theme = ET.parse(DESKTOP / 'Themes' / 'StudioTheme.xaml').getroot()

def control_style(target):
    return next(e for e in theme.findall('ui:Style', NS) if e.get('TargetType') == target and e.get(f'{{{X}}}Key') is None)

def explicit_roots():
    counts = {'Window': 0, 'UserControl': 0}
    for p in xamls:
        root = ET.parse(p).getroot()
        for kind, key in [('Window', 'StudioWindow'), ('UserControl', 'StudioView')]:
            if root.tag == f'{{{UI}}}{kind}':
                counts[kind] += 1
                require(root.get('Style') == '{StaticResource ' + key + '}', f'{p.name}: missing {key}')
    require(counts['Window'] >= 9 and counts['UserControl'] >= 10, str(counts))
    return str(counts)
check('Explicit styles on application windows and views', explicit_roots)

def resource_keys():
    keys = set()
    for e in theme:
        if not isinstance(e.tag, str):
            continue
        key = e.get(f'{{{X}}}Key')
        if key is None and e.tag == f'{{{UI}}}Style':
            key = '{x:Type ' + e.get('TargetType', '') + '}'
        if key:
            require(key not in keys, f'duplicate resource: {key}')
            keys.add(key)
    return f'{len(keys)} distinct root resources'
check('Unique theme resources', resource_keys)

def references():
    keys = set()
    for p in xamls:
        for element in ET.parse(p).iter():
            key = element.get(f'{{{X}}}Key')
            if key:
                keys.add(key)
    count = 0
    for p in xamls:
        for key in re.findall(r'\{(?:Static|Dynamic)Resource\s+([\w]+)\}', p.read_text(encoding='utf-8')):
            require(key in keys, f'{p.name}: unresolved resource {key}')
            count += 1
    return f'{count} named resource references'
check('Named StaticResource/DynamicResource references', references)

def static_resource_order():
    available = set()
    count = 0
    for top in theme:
        for node in top.iter():
            for value in node.attrib.values():
                match = re.fullmatch(r'\{StaticResource\s+(\w+|\{x:Type\s+\w+\})\}', value)
                if match:
                    require(match.group(1) in available, f'forward/missing resource: {match.group(1)}')
                    count += 1
        key = top.get(f'{{{X}}}Key')
        if key:
            available.add(key)
        elif top.tag == f'{{{UI}}}Style':
            available.add('{x:Type ' + top.get('TargetType', '') + '}')
    return f'{count} ordered StaticResource references'
check('No forward references within theme dictionary', static_resource_order)

def templates():
    targets = ['PasswordBox', 'ComboBox', 'ComboBoxItem', 'TabControl', 'ListBox', 'ListBoxItem', 'DataGridColumnHeader', 'ScrollViewer', 'ToolTip', 'ContextMenu', 'MenuItem']
    for target in targets:
        require(any(e.get('Property') == 'Template' for e in control_style(target).findall('ui:Setter', NS)), target)
    return f'{len(targets)} formerly uncovered controls'
check('Critical controls use application templates', templates)

def parts():
    expected = {
        'TextBox': ['PART_ContentHost'], 'PasswordBox': ['PART_ContentHost'],
        'ComboBox': ['PART_Popup', 'PART_EditableTextBox'], 'TabControl': ['PART_SelectedContentHost'],
        'ScrollViewer': ['PART_ScrollContentPresenter', 'PART_VerticalScrollBar', 'PART_HorizontalScrollBar'],
        'ScrollBar': ['PART_Track'], 'DataGridColumnHeader': ['PART_LeftHeaderGripper', 'PART_RightHeaderGripper'],
        'ProgressBar': ['PART_Track', 'PART_Indicator']}
    for target, names in expected.items():
        actual = {e.get(f'{{{X}}}Name') for e in control_style(target).iter()}
        for name in names:
            require(name in actual, f'{target}: {name}')
    return f'{sum(len(v) for v in expected.values())} named parts retained'
check('Named WPF parts retained', parts)

def trigger_targets():
    count = 0
    for template in theme.iter(f'{{{UI}}}ControlTemplate'):
        names = {e.get(f'{{{X}}}Name') for e in template.iter()}
        for e in template.iter():
            for key in ['TargetName', 'Storyboard.TargetName']:
                if key in e.attrib:
                    require(e.get(key) in names, f'{template.get("TargetType")}: unknown target {e.get(key)}')
                    count += 1
    return f'{count} trigger/animation targets'
check('Template trigger and animation targets', trigger_targets)

def palette():
    expected = {'WindowBrush':'#111416', 'SidebarBrush':'#14181B', 'PanelBrush':'#1A1F23', 'InputBrush':'#121619', 'AccentBrush':'#FF781F'}
    actual = {e.get(f'{{{X}}}Key'):e.get('Color') for e in theme.findall('ui:SolidColorBrush', NS)}
    for key, color in expected.items():
        require(actual.get(key) == color, key)
    return 'Reference charcoal/orange palette; custom settings remain available'
check('Reference charcoal palette', palette)

def literal_surfaces():
    white = {'white', 'whitesmoke', '#fff', '#ffffff', '#ffffffff'}
    for p in xamls:
        for e in ET.parse(p).iter():
            require(e.get('Background', '').lower() not in white, f'{p.name}: literal white background')
            if e.get('Property') == 'Background':
                require(e.get('Value', '').lower() not in white, f'{p.name}: literal white background setter')
    return 'No hard-coded white XAML surface backgrounds'
check('No hard-coded white surfaces', literal_surfaces)

def text_contrast():
    require(not any(e.get('Property') == 'Foreground' for e in control_style('TextBlock').findall('ui:Setter', NS)), 'TextBlock overrides inherited foreground')
    return 'Generated labels can inherit parent foreground'
check('Text contrast inheritance', text_contrast)

def text_menus():
    for kind, key in [('TextBox','StudioTextContextMenu'), ('PasswordBox','StudioPasswordContextMenu')]:
        require(any(e.get('Property') == 'ContextMenu' and e.get('Value') == '{StaticResource '+key+'}' for e in control_style(kind).findall('ui:Setter', NS)), kind)
    menu = next(e for e in theme if e.get(f'{{{X}}}Key') == 'StudioPasswordContextMenu')
    require(not any(e.get('Command') in {'ApplicationCommands.Copy', 'ApplicationCommands.Cut'} for e in menu.iter()), 'Password menu exposes copying')
    return 'Themed menus; no copy/cut command for passwords'
check('Explicit text and password menus', text_menus)

def startup():
    source = (DESKTOP / 'App.xaml.cs').read_text(encoding='utf-8')
    require(source.index('ThemeService.Apply(workspace.Settings)') < source.index('new MainWindow(vm)'), 'Theme applied too late')
    return 'Saved theme applied before main-window construction'
check('Theme applied before first window', startup)

def browser():
    p = DESKTOP / 'Views' / 'BrowserWindow.xaml'
    require('DefaultBackgroundColor="#0C0E13"' in p.read_text(encoding='utf-8'), 'Blank browser background')
    source = p.with_suffix('.xaml.cs').read_text(encoding='utf-8')
    require('PreferredColorScheme' in source and re.search(r'ThemeService\.ThemeChanged\s*-=\s*ThemeChanged', source), 'Missing theme application/unsubscribe')
    return 'Initial surface and preferred color scheme configured'
check('Browser theme lifecycle', browser)

def event_handlers():
    count = 0
    for p in xamls:
        cs = p.with_suffix('.xaml.cs')
        if not cs.exists():
            continue
        source = cs.read_text(encoding='utf-8')
        for e in ET.parse(p).iter():
            for event in ['Click', 'Loaded', 'Closing', 'Closed', 'SelectionChanged', 'TextChanged']:
                handler = e.get(event)
                if handler and not handler.startswith('{'):
                    require(re.search(r'\b' + re.escape(handler) + r'\s*\(', source), f'{p.name}: {handler}')
                    count += 1
    return f'{count} XAML event handlers resolve in code-behind'
check('XAML event-handler names', event_handlers)

failed = [result for result in results if not result[1]]
for name, passed, detail in results:
    print(('PASS' if passed else 'FAIL') + '  ' + name + (': ' + detail if detail else ''))
print(f'\n{len(results)-len(failed)}/{len(results)} source checks passed. No C# compilation or WPF rendering performed.')
sys.exit(bool(failed))
