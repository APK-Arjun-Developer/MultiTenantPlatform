#!/usr/bin/env bash
set -euo pipefail

types='feat|fix|ci|chore|docs|refactor|test|perf|hotfix'

case "$1" in
  branch)
    if [[ "$2" != master && ! "$2" =~ ^($types)/[a-z0-9][a-z0-9-]*$ ]]; then
      echo "Invalid branch name: $2. Use <type>/<lowercase-kebab-case>." >&2
      exit 1
    fi
    ;;
  commit)
    pattern="^($types)(\([^)]+\))?!?: ([^[:space:]].*)$"
    if [[ ! "$2" =~ $pattern || ${#2} -gt 72 || "${BASH_REMATCH[3]}" =~ [A-Z] ]]; then
      echo "Invalid commit subject: $2. Use a lowercase Conventional Commit subject, up to 72 characters." >&2
      exit 1
    fi
    ;;
  *) exit 2 ;;
esac
