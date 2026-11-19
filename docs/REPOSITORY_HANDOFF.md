# GitHub and operator handoff

Repository target: https://github.com/ostaddehdari/hokmnameh

The generated bundle contains a local **main branch Git commit** and a portable `.bundle` file. The commit is NOT in remote GitHub yet; direct network egress to GitHub was blocked in the creating environment. The host SSH connection also failed.

On a networked machine with permission to push to the owner repo:

```bash
# If using ZIP of source files:
cd hokmnameh
# Use git bundle for full commit history instead of rebuilding from ZIP, if desired.
# git clone /path/to/hokmnameh-bootstrap.bundle hokmnameh
# cd hokmnameh
git remote add origin https://github.com/ostaddehdari/hokmnameh.git
git push -u origin main
```

If remote already has commits by the time you push, **fetch and reconcile** (`git fetch`, `git log --graph`) rather than `--force` pushing.

For least-privilege use a GitHub fine-grained token with repository contents write or, preferably, SSH keys protected by a credential helper. Avoid pasting credentials into the terminal command string.
