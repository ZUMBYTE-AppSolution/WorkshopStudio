"""Validate local wiki destinations and repository image/example links without network access."""
from pathlib import Path
import re
import sys

root = Path(__file__).resolve().parents[1]
wiki = root / 'docs/wiki'
errors = []
links = 0
for page in sorted(wiki.glob('*.md')):
    text = page.read_text(encoding='utf-8')
    for target in re.findall(r'\]\(([^)]+)\)', text):
        links += 1
        target = target.split('#', 1)[0]
        if not target:
            continue
        if target.startswith('https://raw.githubusercontent.com/ZUMBYTE-AppSolution/WorkshopStudio/main/'):
            local = root / target.split('/main/', 1)[1]
        elif target.startswith('https://github.com/ZUMBYTE-AppSolution/WorkshopStudio/blob/main/'):
            local = root / target.split('/main/', 1)[1]
        elif target.startswith('https://github.com/ZUMBYTE-AppSolution/WorkshopStudio/tree/main/'):
            local = root / target.split('/main/', 1)[1]
        elif target.startswith(('https://', 'http://')):
            continue
        else:
            local = wiki / (target if target.endswith('.md') else target + '.md')
        if not local.exists():
            errors.append(f'{page.name}: missing {target}')
    if 'C:\\Users\\' in text:
        errors.append(f'{page.name}: personal absolute path')
if len(list(wiki.glob('*.md'))) < 18:
    errors.append('Expected at least 16 wiki topics plus sidebar/footer')
if errors:
    print('\n'.join(errors))
    sys.exit(1)
print(f'PASS: {len(list(wiki.glob("*.md")))} wiki files; {links} links and image destinations resolved locally.')
