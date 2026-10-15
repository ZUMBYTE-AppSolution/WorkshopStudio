#!/usr/bin/env python3
"""Offline source/asset contracts only. NOT a C# compiler, runtime test or Steam login test."""
from pathlib import Path
import re
import struct
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
CORE = ROOT / 'src/WorkshopStudio.Core'
DESKTOP = ROOT / 'src/WorkshopStudio.Desktop'
read = lambda p: p.read_text(encoding='utf-8')
runner = read(CORE/'SteamCmdRunner.cs')
transport = read(CORE/'SteamCmdTransport.cs')
protocol = read(CORE/'SteamCmdProtocol.cs')
vm = read(DESKTOP/'ViewModels/MainViewModel.cs')
browser = read(DESKTOP/'Views/BrowserWindow.xaml.cs')
results = []
def check(name, fn):
    try:
        detail = fn()
        results.append((name, True, str(detail or '')))
    except Exception as exc:
        results.append((name, False, str(exc)))
def need(value, message):
    if not value: raise AssertionError(message)

def version():
    props = ET.parse(ROOT/'Directory.Build.props').getroot()
    need(props.find('PropertyGroup/Version').text == '1.2.0', 'Wrong assembly version')
    need('Version 1.2.0' in read(DESKTOP/'MainWindow.xaml'), 'Wrong sidebar version')
    need('typeof(MainViewModel).Assembly.GetName().Version' in vm, 'Updater must read installed assembly version')
    return 'Assembly, sidebar and updater version source agree'
check('Version wiring', version)

def icon():
    data = (DESKTOP/'Assets/WorkshopStudio.ico').read_bytes()
    reserved, kind, count = struct.unpack_from('<HHH', data)
    need((reserved, kind, count) == (0, 1, 9), 'Invalid ICO directory')
    sizes=[]
    for i in range(count):
        width,height,_,_,planes,bits,size,offset = struct.unpack_from('<BBBBHHII',data,6+i*16)
        width=width or 256; height=height or 256
        need(width==height and bits==32 and offset+size<=len(data), 'Invalid image frame')
        need(data[offset:offset+8] == b'\x89PNG\r\n\x1a\n', 'Frame does not contain PNG data')
        sizes.append(width)
    need(sorted(sizes)==[16,20,24,32,40,48,64,128,256], 'Missing icon size')
    return f'{len(sizes)} valid 32-bit PNG frames: {sorted(sizes)}'
check('Windows icon binary',icon)

def icon_wiring():
    project=ET.parse(DESKTOP/'WorkshopStudio.Desktop.csproj').getroot()
    need(project.find('PropertyGroup/ApplicationIcon').text.replace('\\','/')=='Assets/WorkshopStudio.ico', 'Missing Win32 icon')
    resources={e.get('Include').replace('\\','/') for e in project.findall('ItemGroup/Resource')}
    need({'Assets/WorkshopStudio.ico','Assets/WorkshopStudio.png'}<=resources,'Missing WPF resources')
    need('Property="Icon"' in read(DESKTOP/'Themes/StudioTheme.xaml'), 'Missing shared window icon')
    need(read(DESKTOP/'MainWindow.xaml').count('Assets/WorkshopStudio.png')==2,'Header/sidebar asset not wired')
    return 'EXE, WPF window style, title bar, sidebar'
check('Application icon wiring',icon_wiring)

check('Console transport selected on Windows',lambda:need('OperatingSystem.IsWindows()' in transport and 'return StartWindows(exe, arguments)' in transport,'Missing Windows ConPTY path'))
check('Native ConPTY creation',lambda:need(all(s in transport for s in ['CreatePseudoConsole','UpdateProcThreadAttribute','CreateProcessW','0x00020016','0x00080000']),'Missing pseudoconsole launch code'))
check('UTF-8 terminal input',lambda:need('new UTF8Encoding(false)' in transport and 'NewLine = "\\r"' in transport,'Wrong terminal input format'))
check('Output draining during UI prompts',lambda:need('Channel.CreateUnbounded' in runner and 'reader.BaseStream.ReadAsync(buffer.AsMemory()' in runner and 'channel.Writer.TryWrite' in runner,'Missing independent reader loop'))
check('Console close on worker',lambda:need('Task.Run(() => Native.ClosePseudoConsole(handle))' in transport and 'await active.CloseConsoleAsync()' in runner,'Missing teardown path'))
check('Cached login and no argv secrets',lambda:need('"+login", username' in runner and '"+@NoPromptForPassword", "0"' in runner and 'ArgumentList.Add(secret)' not in runner+transport and 'redactor.Add(secret)' in runner,'Missing credential boundary'))
check('No one-second prompt suppression',lambda:need('lastPrompt' not in runner and 'TimeSpan.FromSeconds(1)' not in runner,'Old prompt suppression survives'))
check('Per-stream prompt detectors',lambda:need('active.Readers.Select(_ => new SteamCmdPromptDetector())' in runner and 'detectors[part.Reader]' in runner,'stderr still ignored'))
check('Abort distinct from timeout',lambda:need('deadline.IsCancellationRequested && !ct.IsCancellationRequested' in runner,'Timeout filter masks user abort'))
check('Release exclusive state in finally',lambda:need('finally { IsBusy = false; _exclusive.Release(); }' in runner,'Exclusive state can remain stuck'))

patterns={}
for name in ['Password','Guard','Mobile','Ready']:
    found=re.search(r'private static readonly Regex '+name+r' = new\(\s*@"(.*?)",',protocol,re.S)
    if found: patterns[name]=re.compile(found.group(1),re.I)
def prompt_contract():
    # These particular expressions use the shared Python/.NET regex subset.
    # This checks examples with Python, not .NET execution.
    cases={
        'Password':['Password: ', 'password for test:'],
        'Guard':['Steam Guard code:','Two-factor code:','Auth code:','Please enter the current code from your Steam Guard mobile authenticator app:','Enter your code:'],
        'Mobile':['Please confirm this login in the Steam Mobile app.'],
        'Ready':['Waiting for user info...OK','Waiting for user info...\r\nOK',"Logging in user 'test' to Steam Public...OK"]
    }
    count=0
    for kind,examples in cases.items():
        for text in examples:
            need(patterns[kind].search(text),f'{kind}: {text}');count+=1
    need(not patterns['Ready'].search('Loading Steam API...OK'), 'False login success')
    return f'{count} representative patterns + API-OK negative example (Python regex, not C#)'
check('Prompt expression examples',prompt_contract)

check('Temporary password retained',lambda:need('_sessionPasswords[model.Id]=d.Password' in vm and '_sessionPasswords.Clear()' in vm,'One-time editor password discarded'))
check('Login setup assistance',lambda:need('EnsureLoginReadyAsync' in vm and 'await InstallSteamCmdAsync()' in vm,'Missing first-login setup'))
check('Visible and exportable auth log',lambda:need(all(s in vm for s in ['public string LoginLog','copy-login-log','save-login-log','File.WriteAllTextAsync(_lastLoginLogFile,LoginLog)']),'Missing login diagnostics'))
check('Web sessions isolated by profile',lambda:need('ProfileName = accountId' in browser and 'IsInPrivateModeEnabled = !remember' in browser,'Shared or non-private browser profile'))
check('Web login status uses visible menu only',lambda:need("document.getElementById('account_pulldown')" in browser and 'GetCookiesAsync' not in browser and 'document.cookie' not in browser and 'querySelector(\'input' not in browser,'Missing status or unsafe token/password scraping'))
check('Web runtime and navigation diagnostics',lambda:need(all(s in browser for s in ['WebView2RuntimeNotFoundException','NavigationCompleted','ProcessFailed','ShowBrowserError','Reload_Click','External_Click']),'Missing browser diagnostics'))
check('Browser security retained',lambda:need('AreHostObjectsAllowed = false' in browser and 'IsWebMessageEnabled = false' in browser and 'IsSteamBrowserUrl' in browser and not any(s in browser for s in ['--disable-web-security','--ignore-certificate-errors','--no-sandbox']),'Browser protections weakened'))
check('Local process regression tests present',lambda:need('FakeSteamCmd(args)' in read(ROOT/'tests/WorkshopStudio.SelfTest/Program.cs') and 'ws_guard' in read(ROOT/'tests/WorkshopStudio.SelfTest/SteamLoginTests.cs') and '<UseAppHost>true</UseAppHost>' in read(ROOT/'tests/WorkshopStudio.SelfTest/WorkshopStudio.SelfTest.csproj'),'Process tests not connected'))

for name,ok,detail in results:
    print(('PASS' if ok else 'FAIL')+'  '+name+(': '+detail if detail else ''))
failed=sum(not ok for _,ok,_ in results)
print(f'\n{len(results)-failed}/{len(results)} offline source/asset checks passed.')
print('No C# compilation, native Windows execution, WPF rendering or real Steam login performed.')
sys.exit(bool(failed))
