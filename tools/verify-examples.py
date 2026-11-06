#!/usr/bin/env python3
"""
Compile every C# snippet that the MCP server hands to agents.

`get_code_example` and `get_pipeline_template` return code as verbatim string
literals inside Tools/ExampleTools.cs. That means the compiler never sees them: a
wrong type name, a property that does not exist, a private constructor — all build
clean and ship, and the first person to find out is the developer who pasted the
snippet. Three consecutive review rounds each found a different non-compiling
example in that file, including two introduced by the fix for the previous one.

This extracts each ```csharp block, wraps it in a method, and builds the lot against
the real published VisioForge packages. It is not part of `dotnet test` — it restores
from nuget.org and takes tens of seconds — so run it whenever ExampleTools.cs changes:

    python3 MCP/tools/verify-examples.py

Exit codes: 0 all snippets compile · 1 at least one does not · 2 could not run.

Known limit: it builds for net8.0-windows, so only the WINDOWS arm of a snippet's
`#if` is checked. The other arms are read but not compiled.
"""
import os
import re
import shutil
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
EXAMPLES = os.path.join(HERE, '..', 'VisioForge.MCP.Server', 'Tools', 'ExampleTools.cs')

# Identifiers the snippets deliberately leave to the reader — a UI control, a window
# handle. Declared here so the harness does not report them as undefined; everything
# else that does not resolve is a real defect in the snippet.
PLACEHOLDERS = """
        VisioForge.Core.Types.IVideoView videoView = null;
        VisioForge.Core.Types.IVideoView VideoView1 = null;
"""

CSPROJ = """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>disable</Nullable>
    <!-- On by default in every modern project template, so a snippet that relies on
         System being implicitly imported is not thereby broken. -->
    <ImplicitUsings>enable</ImplicitUsings>
    <NoWarn>CS0219;CS0162;CS8321</NoWarn>
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
    <UseWPF>true</UseWPF>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="VisioForge.DotNet.VideoCapture" Version="{v}" />
    <PackageReference Include="VisioForge.DotNet.MediaPlayer" Version="{v}" />
    <PackageReference Include="VisioForge.DotNet.VideoEdit" Version="{v}" />
    <PackageReference Include="VisioForge.DotNet.MediaBlocks" Version="{v}" />
  </ItemGroup>
</Project>
"""


def extract(src):
    """[(example_id, ordinal, code)] for every ```csharp block, in file order."""
    names = [(m.start(), m.group(1)) for m in re.finditer(r'"([a-z0-9-]+)" => @"', src)]
    out, seen = [], {}
    for m in re.finditer(r'```csharp\n(.*?)```', src, re.S):
        owner = [n for p, n in names if p < m.start()]
        owner = owner[-1] if owner else 'unknown'
        seen[owner] = seen.get(owner, 0) + 1
        # `""` is an escaped quote inside a C# verbatim string literal.
        out.append((owner, seen[owner], m.group(1).replace('""', '"')))
    return out


def to_unit(example_id, ordinal, code, index):
    """Wrap one snippet as a compilable file: usings hoisted, body in an async method."""
    # A using DIRECTIVE only — `using System.Text;`. Not a using DECLARATION
    # (`using var editor = new VideoEditCoreX();`), which is a statement and must stay
    # in the body; hoisting it made the compiler treat the file as top-level
    # statements and report CS8801 on every later reference to the variable.
    directive = re.compile(r'using\s+(static\s+)?[A-Za-z_][\w.]*\s*;\s*$')
    usings, body = [], []
    for line in code.splitlines():
        (usings if directive.match(line) else body).append(line)
    ident = re.sub(r'[^A-Za-z0-9]', '_', f'{example_id}_{ordinal}')
    return (
        '\n'.join(usings)
        + f'\n\nnamespace SnippetCheck {{\n    // {example_id} (block {ordinal})\n'
        + f'    internal static class Snippet_{index}_{ident} {{\n'
        + '        internal static async System.Threading.Tasks.Task RunAsync() {\n'
        + PLACEHOLDERS
        + '\n'.join('            ' + b for b in body)
        + '\n        }\n    }\n}\n'
    )


def main():
    version = os.environ.get('VF_VERSION', '2026.8.16')
    if not os.path.isfile(EXAMPLES):
        print(f'ERROR: {EXAMPLES} not found', file=sys.stderr)
        return 2

    source = open(EXAMPLES, encoding='utf-8').read()
    snippets = extract(source)
    # Guarding only the zero case is not enough: a regex that drifted to matching 3 of
    # 13 blocks would print "OK, all 3 snippets compile" and exit 0, waving through ten
    # unchecked ones. Nothing else compiles these, so the count has to be exact.
    expected = source.count('```csharp')
    if len(snippets) != expected:
        print(f'ERROR: found {len(snippets)} snippets but the file contains {expected} '
              '```csharp blocks — the extraction regex no longer matches the file shape.',
              file=sys.stderr)
        return 2

    work = tempfile.mkdtemp(prefix='vf-snippet-check-')
    try:
        with open(os.path.join(work, 'check.csproj'), 'w') as f:
            f.write(CSPROJ.replace('{v}', version))
        for i, (name, ordinal, code) in enumerate(snippets):
            with open(os.path.join(work, f'S{i:02d}_{re.sub(r"[^A-Za-z0-9]", "_", name)}.cs'), 'w') as f:
                f.write(to_unit(name, ordinal, code, i))

        print(f'Compiling {len(snippets)} snippets against VisioForge {version}...')
        try:
            proc = subprocess.run(
                ['dotnet', 'build', '-c', 'Release', '--nologo', '-v', 'q'],
                cwd=work, capture_output=True, text=True, timeout=900,
            )
        except FileNotFoundError:
            print('ERROR: `dotnet` not found on PATH.', file=sys.stderr)
            return 2
        except subprocess.TimeoutExpired:
            print('ERROR: the build did not finish within 900s.', file=sys.stderr)
            return 2

        errors = [l for l in (proc.stdout + proc.stderr).splitlines() if ': error ' in l]

        # A non-zero exit with nothing that looks like a compile error is a broken run,
        # not a clean one: an MSBuild crash, an OOM kill, an SDK resolution failure. This
        # is the only gate over the snippets, so a false green here is worse than useless.
        if proc.returncode != 0 and not errors:
            print(f'ERROR: build failed (exit {proc.returncode}) with no compiler diagnostics '
                  'to attribute — this is a harness or toolchain problem, not a snippet one.',
                  file=sys.stderr)
            print((proc.stdout + proc.stderr)[-2000:], file=sys.stderr)
            return 2

        if not errors:
            print(f'OK — all {len(snippets)} snippets compile.')
            return 0

        # Map each error back to the example it came from.
        print(f'\n{len(errors)} compile error(s):\n')
        for line in errors:
            m = re.search(r'S(\d+)_[A-Za-z0-9_]*\.cs', line)
            owner = snippets[int(m.group(1))][0] if m else '?'
            print(f'  [{owner}] {line.strip()}')
        return 1
    finally:
        shutil.rmtree(work, ignore_errors=True)


if __name__ == '__main__':
    sys.exit(main())
