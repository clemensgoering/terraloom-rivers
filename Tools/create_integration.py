"""Create/update the sibling Integration Unity test project from versioned consumer fixtures."""
import json
import shutil
from pathlib import Path

root = Path(__file__).resolve().parents[1]
workspace = root.parent
destination = workspace / 'Integration'
for name in ('Core', 'Paths', 'Rivers'):
    if not (workspace / name / 'Packages' / ('com.terraloom.' + name.lower())).is_dir():
        raise SystemExit('Place Core, Paths and Rivers beside each other before creating Integration.')
destination.mkdir(exist_ok=True)
for folder in ('ProjectSettings', 'Assets/Settings'):
    if not (destination / folder).exists():
        shutil.copytree(root / folder, destination / folder)
if not (destination / 'Assets/Settings.meta').exists():
    shutil.copy2(root / 'Assets/Settings.meta', destination / 'Assets/Settings.meta')
shutil.copytree(root / 'IntegrationProject/Assets', destination / 'Assets', dirs_exist_ok=True)
(destination / 'Tools').mkdir(exist_ok=True)
for name in ('UnityTestRunner.ps1', 'ValidateUnity.ps1'):
    shutil.copy2(root / 'Tools' / name, destination / 'Tools' / name)
manifest = json.loads((root / 'Packages/manifest.json').read_text(encoding='utf-8-sig'))
for name in ('Core', 'Paths', 'Rivers'):
    manifest['dependencies']['com.terraloom.' + name.lower()] = 'file:../../' + name + '/Packages/com.terraloom.' + name.lower()
(destination / 'Packages').mkdir(exist_ok=True)
(destination / 'Packages/manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
(destination / 'README.md').write_text('''# TerraLoom integration test

Open this project in Unity 6000.3.9f1. Tools > TerraLoom > Integration > Build Test Scene.
Open Assets/TerraLoom/Integration/Generated/TerraLoomDynamic.unity and enter Play Mode.
TerraLoomLandscape.unity adds stronger valley relief, connected road bends/branch and habitat profiles.
The composition/seed inspectors expose ordered stages and input/output freshness checks.
Same seed / Next seed rebuilds terrain, seed targets, regions, rivers, paths and regional assets at runtime.
Tab switches to walking; WASD moves, mouse looks, Space jumps, Esc returns to the overview.
The baked example works without generating on Start. Rebuild through the composition inspector.

This project reads all three local packages. Fixture sources live in ../Rivers/IntegrationProject.
Generated content is reproducible through IntegrationSampleBuilder.BuildBatch.
Run Tools/ValidateUnity.ps1 -Platform EditMode and -Platform PlayMode AFTER building the scene.
See ../Rivers/Docs/INTEGRATION.md for scope and limitations.
''', encoding='utf-8')
print('Integration project ready:', destination)
