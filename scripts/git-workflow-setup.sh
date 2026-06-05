#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# LegalSynq Git Workflow Setup
# Run once from the project root to bootstrap the four-branch workflow.
#
# What this does:
#   1. Ensures you are on main (fetches latest)
#   2. Creates xenia, dev, qa off main (skips if they already exist)
#   3. Pushes all three to origin
#   4. Switches the working branch to xenia
#
# Usage:
#   bash scripts/git-workflow-setup.sh
# ─────────────────────────────────────────────────────────────────────────────
set -e

REMOTE="origin"
BASE="main"
REQUIRED_BRANCHES=("xenia" "dev" "qa")

echo ""
echo "=== LegalSynq Git Workflow Setup ==="
echo ""

# ── 1. Fetch latest state from remote ────────────────────────────────────────
echo "→ Fetching latest from $REMOTE..."
git fetch "$REMOTE" --prune

# ── 2. Switch to main and pull latest ────────────────────────────────────────
echo "→ Switching to $BASE and pulling latest..."
git checkout "$BASE"
git pull "$REMOTE" "$BASE"

# ── 3. Create required branches off main (skip if already exists) ─────────────
for branch in "${REQUIRED_BRANCHES[@]}"; do
  if git show-ref --verify --quiet "refs/heads/$branch"; then
    echo "✓ Branch '$branch' already exists locally — skipping create"
  elif git show-ref --verify --quiet "refs/remotes/$REMOTE/$branch"; then
    echo "→ Branch '$branch' exists on remote — checking out..."
    git checkout --track "$REMOTE/$branch"
    git checkout "$BASE"
  else
    echo "→ Creating branch '$branch' off $BASE..."
    git branch "$branch" "$BASE"
  fi

  # Push to remote if not already there
  if git show-ref --verify --quiet "refs/remotes/$REMOTE/$branch"; then
    echo "✓ '$branch' already on remote"
  else
    echo "→ Pushing '$branch' to $REMOTE..."
    git push "$REMOTE" "$branch"
  fi
done

# ── 4. Switch working branch to xenia ────────────────────────────────────────
echo ""
echo "→ Switching working branch to xenia..."
git checkout xenia

# ── 5. Summary ────────────────────────────────────────────────────────────────
echo ""
echo "=== Done ==="
echo ""
echo "Active branch : $(git branch --show-current)"
echo ""
echo "Branches on $REMOTE:"
git branch -r | grep -v "subrepl\|gitsafe\|HEAD" | sed 's|remotes/origin/||' | sort | sed 's/^/  origin\//'
echo ""
echo "Next steps:"
echo "  • In Replit's Git pane, confirm the active branch shows 'xenia'"
echo "  • All future Replit checkpoints will now commit to xenia"
echo ""
echo "Branch protection rules to add on GitHub"
echo "  (Settings → Branches → Add rule for each):"
echo "  dev    → only accept merges from xenia"
echo "  qa     → only accept merges from dev"
echo "  main   → only accept merges from qa"
echo "  xenia  → only accept merges from main (release) or qa (QA-only)"
