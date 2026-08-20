# Installation and Version Selection

Use this reference when the inspection state is `AppUINotInstalled`, when the
installed source is mutable or unclear, or when version sources disagree. This
skill understands the public AppUI `0.4.x` contract first. Inspect the installed
version and its migration documents before editing a project on another line.

## Resolve the source before editing

Use this source-of-truth order exactly:

```text
immutable GitHub Tag/Release
→ supported-unity-versions.md
→ package.json and migration guide at that Tag
→ installed package and Samples
→ tutorials
```

The higher source wins only when its identity is complete. Confirm that the Tag
is immutable, the GitHub Release points at the same Commit, and the support table
lists that exact AppUI Tag and Unity Editor pair. Then inspect `package.json` and
the applicable migration guide from that Tag. Finally compare the consumer's
manifest, lock file, installed package, and imported Samples. Tutorials are
orientation, not release authority.

If any two sources disagree, stop before changing `Packages/manifest.json`.
Report each observed value and the unresolved mismatch. Do not select an older
Tag merely because a tutorial names it.

For the current officially supported `0.4.x` release evidence:

```powershell
$officialTag = 'v0.4.0-pre.1'
$officialUrl = "https://github.com/TechJoiH/JoiH-AppUI.git#$officialTag"
```

Re-resolve `$officialTag` from repository evidence for a future release; never
derive it from a mutable branch or from memory.

## Install the resolved Tag

In Unity Package Manager, choose **Add package from git URL...** and enter:

```text
https://github.com/TechJoiH/JoiH-AppUI.git#v0.4.0-pre.1
```

The equivalent `Packages/manifest.json` entry is:

```json
{
  "dependencies": {
    "com.joih.appui": "https://github.com/TechJoiH/JoiH-AppUI.git#v0.4.0-pre.1"
  }
}
```

Never use mutable `main`, another branch, or an unversioned Git URL as a
production dependency. A 40-character Commit can be useful for an explicitly
owned experiment, but it is not an Officially Supported Release unless the
support table and Release identity say so.

After Unity resolves the package, verify all of these facts before proceeding:

- `Packages/manifest.json` and `packages-lock.json` resolve the intended Tag.
- Installed `package.json` reports `com.joih.appui` version `0.4.0-pre.1` and
  Unity line `6000.0` for this release.
- The exact Editor is covered by the support table. For `v0.4.0-pre.1`, the
  published Officially Supported pair is Unity `6000.0.25f1`.
- Base Runtime and Editor resolve with UGUI only. Do not add an async library,
  asset backend, Resources policy, or TextMeshPro merely to complete install.
- Samples remain optional consumer-owned code. Import **Basic Integration** or
  **Custom Host Integration** only after choosing the appropriate learning or
  production-host path.

## Apply the five support statuses

Treat every exact Unity version plus exact AppUI Commit/Tag combination as one
of these five statuses:

| Status | Meaning | Installation decision |
|---|---|---|
| `Officially Supported` | AppUI completed its official gates for the exact pair. | The immutable listed Tag may be used for the listed Editor. |
| `Community Verified` | External evidence covers an exact community combination. | Follow that evidence only with the user's approval; it is not official maintenance. |
| `Community Port` | A porting path exists without complete evidence. | Treat it as consumer-owned experimental work, pinned to an exact fork/Commit. |
| `Unsupported` | The pair is outside the supported set and has no compatibility guarantee. | Explain the boundary; do not describe it as proven broken. |
| `Known Incompatible` | Reproducible evidence shows that the exact pair cannot work. | Reject the combination and select a different evidenced pair before integration. |

`Official Target` is not a sixth status. A package minimum Unity field also does
not make every later Editor Officially Supported. Do not proceed with a Known
Incompatible combination.

## Existing installations and upgrades

Preserve a valid installed version while evidence is unresolved. If the
inspection reports a branch, an unversioned reference, or a different version,
show the current manifest and lock facts and ask before replacing the dependency.
For a `0.3.x` source, read `migration-0.4.md` from the target Tag. For a `0.2.x`
source, read `migration-0.3.md` and then `migration-0.4.md`. Do not copy current
API assumptions backward across versions.

Installation is complete only when Unity resolves the immutable source and the
Base package compiles. It does not prove that the three host ports or Runtime
root exist; continue with the host-boundary reference next.
