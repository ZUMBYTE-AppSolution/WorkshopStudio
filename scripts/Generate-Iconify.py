"""Refresh the bundled Iconify Material Design Icons geometry dictionary.

Run only when intentionally updating the icons. The application uses the checked-in
XAML geometries offline and never contacts Iconify at runtime.
"""

import json
from pathlib import Path
from urllib.parse import quote
from urllib.request import Request, urlopen
from xml.etree import ElementTree
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "src/WorkshopStudio.Desktop/Themes/IconifyIcons.xaml"

# Resource key -> Iconify mdi icon name. All shapes have the same 24x24 viewBox.
ICONS = {
    "IconDashboard": "view-dashboard-outline",
    "IconAccount": "account-circle-outline",
    "IconWorkshop": "cloud-search-outline",
    "IconGame": "gamepad-variant-outline",
    "IconMod": "puzzle-outline",
    "IconCollection": "folder-multiple-outline",
    "IconUpload": "cloud-upload-outline",
    "IconAutomation": "robot-outline",
    "IconHistory": "history",
    "IconSettings": "cog-outline",
    "IconHelp": "help-circle-outline",
    "IconSearch": "magnify",
    "IconMinimize": "window-minimize",
    "IconMaximize": "window-maximize",
    "IconRestore": "window-restore",
    "IconClose": "close",
    "IconAdd": "plus",
    "IconSave": "content-save-outline",
    "IconRefresh": "refresh",
    "IconDelete": "delete-outline",
    "IconEdit": "pencil-outline",
    "IconCopy": "content-copy",
    "IconImport": "tray-arrow-down",
    "IconExport": "tray-arrow-up",
    "IconFolder": "folder-open-outline",
    "IconDocument": "file-document-outline",
    "IconCode": "code-tags",
    "IconInfo": "information-outline",
    "IconClipboard": "clipboard-text-outline",
    "IconBack": "arrow-left",
    "IconExternal": "open-in-new",
    "IconStop": "stop-circle-outline",
    "IconPlay": "play-circle-outline",
    "IconCheck": "check-circle-outline",
    "IconAlert": "alert-circle-outline",
    "IconPending": "clock-outline",
    "IconImage": "image-outline",
    "IconLogin": "login",
    "IconLogout": "logout",
    "IconKey": "key-outline",
    "IconPackage": "package-variant-closed",
    "IconList": "format-list-bulleted",
    "IconFilter": "filter-outline",
    "IconScan": "radar",
    "IconView": "eye-outline",
    "IconWeb": "web",
    "IconDownload": "download-outline",
    "IconFolderPlus": "folder-plus-outline",
}


names = ",".join(ICONS.values())
url = "https://api.iconify.design/mdi.json?icons=" + quote(names, safe=",")
with urlopen(Request(url, headers={"User-Agent": "Mozilla/5.0"}), timeout=30) as response:
    payload = json.load(response)
if payload.get("prefix") != "mdi":
    raise ValueError("Unexpected icon set")


def extract(item):
    key, name = item
    icon = payload["icons"][name]
    if icon.get("width", 24) != 24 or icon.get("height", 24) != 24:
        raise ValueError(f"Unexpected dimensions for {name}")
    root = ElementTree.fromstring(f'<svg>{icon["body"]}</svg>')
    paths = root.findall("path")
    if not paths or any(child.tag != "path" for child in root):
        raise ValueError(f"Unexpected SVG structure for {name}")
    data = " ".join(path.attrib["d"] for path in paths)
    if any("transform" in path.attrib for path in paths):
        raise ValueError(f"Unexpected transform for {name}")
    return key, name, data


results = dict((key, (name, data)) for key, name, data in map(extract, ICONS.items()))

lines = [
    '<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" '
    'xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">',
    '  <!-- Iconify mdi, Pictogrammers, Apache-2.0. Each Geometry uses a 24x24 coordinate space. -->',
]
for key in ICONS:
    name, data = results[key]
    lines.append(f'  <!-- mdi:{name} -->')
    lines.append(f'  <Geometry x:Key="{key}">{escape(data)}</Geometry>')
lines.append('</ResourceDictionary>')
OUTPUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
print(f"Wrote {len(ICONS)} Iconify icons to {OUTPUT}")
