#!/usr/bin/env bash
# Creates and pushes one annotated Git tag per released component, reusing a tag that already
# points at this commit so a rerun is idempotent. Writes created_tags to $GITHUB_OUTPUT.
#
# Called by the finalise-release job in build-test-publish-images.yml.
#
# Environment:
#   COMPONENTS_JSON      JSON array of released components
#   GIT_TAGS_JSON        JSON object, component to tag name
#   RELEASE_TYPES_JSON   JSON object, component to release increment
#   PR_NUMBER            merged pull request number (may be empty)
#   GITHUB_SHA, GITHUB_OUTPUT   set by the runner
set -euo pipefail
 
git fetch --tags --force
git config user.name "github-actions"
git config user.email "github-actions@github.com"
 
TAG_REFS_TO_PUSH=()
CREATED_TAGS='[]'
 
while IFS= read -r COMPONENT; do
  RELEASE_TAG="$(jq -er --arg component "${COMPONENT}" \
    '.[$component]' <<< "${GIT_TAGS_JSON}")"
  RELEASE_TYPE="$(jq -er --arg component "${COMPONENT}" \
    '.[$component]' <<< "${RELEASE_TYPES_JSON}")"
 
  EXISTING_COMMIT="$(git rev-list -n 1 "${RELEASE_TAG}" 2>/dev/null || true)"
 
  if [[ -n "${EXISTING_COMMIT}" ]]; then
    if [[ "${EXISTING_COMMIT}" != "${GITHUB_SHA}" ]]; then
      echo "::error::${RELEASE_TAG} already points to ${EXISTING_COMMIT}." >&2
      exit 1
    fi
    echo "${RELEASE_TAG} already points to this release commit."
  else
    MESSAGE="CDC ${COMPONENT} production release ${RELEASE_TAG}; increment=${RELEASE_TYPE}"
    if [[ -n "${PR_NUMBER}" ]]; then
      MESSAGE="${MESSAGE}; PR #${PR_NUMBER}"
    fi
 
    git tag -a "${RELEASE_TAG}" "${GITHUB_SHA}" -m "${MESSAGE}"
    TAG_REFS_TO_PUSH+=("refs/tags/${RELEASE_TAG}")
  fi
 
  CREATED_TAGS="$(jq -c --arg tag "${RELEASE_TAG}" '. + [$tag]' \
    <<< "${CREATED_TAGS}")"
done < <(jq -r '.[]' <<< "${COMPONENTS_JSON}")
 
if (( ${#TAG_REFS_TO_PUSH[@]} > 0 )); then
  git push --atomic origin "${TAG_REFS_TO_PUSH[@]}"
fi
 
echo "created_tags=${CREATED_TAGS}" >> "${GITHUB_OUTPUT}"
