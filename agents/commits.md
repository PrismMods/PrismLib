# Commit & Push Agent

How commits get made in this repo. Read this before staging anything.

This file is shared by every PrismMods project (Quartz, Topaz, Sapphire, PrismLib, the websites, the bot). Everything above **This repo** at the bottom is identical in all of them — change it in all of them or not at all. **This repo** holds what differs, including how finely to split.

## Shorthands

- **`pcommit`** — commit **and** push. Run the full commit workflow below, then push (see Push). Plain "commit" means commit only, no push.

## Format: Conventional Commits

Every commit subject is `type: summary` (optionally `type(scope): summary`).

```
<type>: <short summary>
```

- `type` is one of the list below, lowercase.
- `summary` is imperative mood ("add", "fix", "reset" — not "added"/"fixes"), lowercase first word, **no trailing period**.
- Keep the subject ≤ ~72 chars. Fold the area/feature into the summary if it isn't obvious (e.g. `fix: reset combo count on built-in level start`).
- Scope is optional. Use it when one type spans an obvious module: `feat(keyviewer): ...`. The house default is **no scope** — the area lives in the summary instead.

### Types

| type | use for |
|------|---------|
| `feat` | a new feature |
| `fix` | a bug fix |
| `chore` | maintenance work, no production code change (deps, config, tooling, regenerated files) |
| `docs` | documentation only |
| `refactor` | code change that's neither a fix nor a feature |
| `style` | formatting, whitespace, semicolons (no logic change) |
| `test` | adding or fixing tests |
| `perf` | performance improvement |
| `build` | build system or dependency changes (Quartz: `build.json` alpha bumps) |
| `release` | a version cut made by the repo's release script (`release: 1.0.0-a10`) |
| `ci` | CI config changes |

Pick by intent, not by file: a localization-key add is `chore`, a parser test is `test`, a version bump is `build` or `release` as **This repo** says.

### Body (optional)

Subject-only is the norm. Add a blank line + body only when the **why** isn't obvious from the diff — explain the reason, not the mechanics. Don't restate the subject.

## Including yourself
I get you want to include yourself in the commits, but can u just not? do not put @claude as one of the collaborators or co-authors while commiting.

## Splitting

One logical change per commit, at the granularity **This repo** sets. These hold at every granularity:

- Group by **concern**, not by file. A feature that touches a settings class, a patch, and a page is **one** commit.
- Keep cross-file references together so each commit compiles on its own: if file A's hunk calls a method added in file B, they land in the same commit.
- Never mix a fix for one feature into another feature's commit just because it sits in the same file.
- A version bump or release cut is always its own commit.

## Mixed-concern files: hunk-split

When one file holds hunks belonging to different commits (and **This repo** says to split that finely):

1. Count hunks: `git diff <file> | grep -c '^@@'`. Find the target hunk's position by grepping for a unique line near it.
2. Stage only the wanted hunks with a fed `git add -p` (no TTY needed):
   ```
   printf 'n\nn\ny\nn\n' | git add -p <file>   # y = stage this hunk, n = skip, in file order
   ```
3. **Verify before committing** — never trust the stage blind:
   ```
   git diff --cached <file> | grep -E '^\+' | grep -i '<marker for the hunk you wanted>'
   ```
   Confirm the wanted lines are staged and the others are not.
4. Commit. The remaining hunks stay unstaged for their own commit (a later `git add <file>` picks them all up).

If a hunk boundary won't split the way you need, fall back to a hand-built patch: `git diff <file> > /tmp/p`, trim to the wanted hunks, `git apply --cached /tmp/p`.

## Workflow

1. `git status --short` and `git diff --stat` — see the whole surface.
2. Read the diffs. Build a mental map of concern → files/hunks.
3. For each concern, in an order that keeps every commit building:
   - stage its files (and hunk-split any shared files, if this repo splits that finely),
   - verify the stage with `git diff --cached`,
   - commit with a Conventional subject.
4. `git status --short` → must be clean of your work (nothing stranded). Another session's uncommitted files are not yours: leave them unstaged.
5. Watch for **strays**: edits that appear mid-session and weren't in your plan (e.g. a font-size tweak). Don't fold them into an unrelated commit — give them their own.

## Push

- Push **only when asked**. Committing ≠ pushing.
- The default branch is `main`; work commits straight onto it.
- Check direction before pushing — the remote-tracking ref can be stale, so read the server:
  ```
  git ls-remote origin -h refs/heads/main   # compare to git rev-parse HEAD
  ```
- Normal fast-forward push: `git push origin main`. Then verify `ls-remote` == local HEAD.
- **Force-push only on already-pushed history you rewrote.** If you reword/rebase commits that were never pushed, a plain push still fast-forwards — no force needed.

## Rewriting messages (not yet pushed)

To restyle subjects after the fact without interactive rebase (no TTY): map old→new with `git filter-branch --msg-filter` over `origin/main..HEAD`. The tree must be clean first — `git stash` any stray edit, rewrite, then `git stash pop`.

## Notes

- If tooling appends a `Co-Authored-By: Claude ...` trailer to a message template, **strip it before committing** — see [Including yourself](#including-yourself). Never add one by hand.
- Don't commit or push unless the task calls for it.

## This repo

**Split finely**, like Quartz: one logical change per commit, hunk-split shared files. Older history used bare subjects ("notes", "update version"); new commits use the format above.

- Releases: `./release.sh <ver>` stamps both AssemblyInfos and `Prism.Version` and regenerates `prismlib.json`; commit that as `release: <ver>`. Steps are 0.0.1 unless the user says otherwise.
- A change to a public API the mods' bridges call says so in the body, naming the bridge methods that must follow (`Topaz`/`Sapphire` `PrismBridge`).
- Check a commit with `./release.sh` (no version) or `./dev.sh`: both build with xbuild and run `tools/PrismLibTest.cs`.
