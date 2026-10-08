"""Small cloud-safe structural audit; --compile also checks pure runtime C#."""
import argparse
import json
import re
import subprocess
from pathlib import Path

def run():
    parser = argparse.ArgumentParser()
    parser.add_argument('--compile', action='store_true')
    parser.add_argument('--core-root', type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    cfg = json.loads((root / 'Tools/bootstrap.json').read_text())
    module = cfg['module']
    package = root / 'Packages' / ('com.terraloom.' + module.lower())
    manifest = json.loads((root / 'Packages/manifest.json').read_text())
    descriptor = json.loads((package / 'package.json').read_text())
    assert descriptor['name'] == 'com.terraloom.' + module.lower()
    assert manifest['dependencies']['com.unity.render-pipelines.universal'] == '17.3.0'
    assert '6000.3.9f1' in (root / 'ProjectSettings/ProjectVersion.txt').read_text()
    assert not (root / 'Assets/TutorialInfo').exists()
    assert not (root / 'Assets/Readme.asset').exists()
    guids = {}
    for area in [root / 'Assets', package]:
        for path in area.rglob('*.meta'):
            match = re.search(r'^guid: ([a-f0-9]{32})$', path.read_text(), re.M)
            assert match, 'Missing GUID: ' + str(path)
            assert match[1] not in guids, 'Duplicate GUID: ' + str(path)
            guids[match[1]] = path
        for path in area.rglob('*'):
            if path.name.endswith('~') or path.suffix == '.meta':
                continue
            assert Path(str(path) + '.meta').exists(), 'Missing .meta: ' + str(path)
    assembly = json.loads((package / 'Runtime' / ('TerraLoom.' + module + '.Runtime.asmdef')).read_text())
    assert assembly['noEngineReferences'] is True
    if module != 'Core':
        core_dep = manifest['dependencies']['com.terraloom.core']
        assert core_dep == cfg['corePackageUrl'], 'Core pin mismatch'
        assert re.search(r'#[a-f0-9]{40}$', core_dep), 'Core must be pinned to a commit'
        assert 'TerraLoom.Core.Runtime' in assembly['references']
        assert 'com.terraloom.paths' not in manifest['dependencies']
        assert 'com.terraloom.rivers' not in manifest['dependencies']
    settings = (root / 'ProjectSettings/GraphicsSettings.asset').read_text()
    assert re.search(r'm_CustomRenderPipeline: \{fileID: 11400000, guid: [a-f0-9]{32}', settings)
    assert 'm_ActiveColorSpace: 1' in (root / 'ProjectSettings/ProjectSettings.asset').read_text()
    if args.compile:
        command = ['dotnet', 'build', str(root / 'Tools/RuntimeCompile.csproj'), '--nologo', '-v', 'minimal']
        if module != 'Core':
            if args.core_root:
                core = args.core_root.resolve()
                assert subprocess.check_output(['git', '-C', str(core), 'rev-parse', 'HEAD'], text=True).strip() == cfg['coreCommit'], 'Core checkout must match pin'
            else:
                core = root / '.artifacts/dependencies/core'
                if not core.exists():
                    core.parent.mkdir(parents=True, exist_ok=True)
                    subprocess.run(['git', 'clone', '--quiet', '--no-checkout', cfg['coreRepository'], str(core)], check=True)
                subprocess.run(['git', '-C', str(core), 'checkout', '--quiet', '--detach', cfg['coreCommit']], check=True)
            output = root / '.artifacts/core-build'
            core_obj = root / '.artifacts/core-obj'
            subprocess.run(['dotnet', 'build', str(core / 'Tools/RuntimeCompile.csproj'), '--nologo', '-v', 'minimal', '-o', str(output), '-p:BaseIntermediateOutputPath=' + str(core_obj) + '/'], check=True)
            command.append('-p:CoreRuntimeAssembly=' + str(output / 'TerraLoom.Core.Runtime.dll'))
        subprocess.run(command, check=True)
    print(module + ': skeleton audit passed' + ('; pure runtime compiled' if args.compile else '; no Unity compile performed'))

if __name__ == '__main__':
    run()
