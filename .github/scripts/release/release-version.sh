#!/usr/bin/env bash
# Works out the next semantic version, Git tag and release increment for each changed component
# (api, web) and writes versions, git_tags, release_types and pull_request to $GITHUB_OUTPUT.
#
# Called by the release-version job in build-test-publish-images.yml. Release type comes from the
# component-specific release label on the merged pull request, or from the manual dispatch inputs.
#
# Environment:
#   GH_TOKEN                  token for `gh api`
#   COMPONENTS_JSON           JSON array of changed components, for example ["api","web"]
#   MANUAL_API_RELEASE_TYPE   workflow_dispatch input (none|patch|minor|major)
#   MANUAL_WEB_RELEASE_TYPE   workflow_dispatch input (none|patch|minor|major)
#   GITHUB_EVENT_NAME, GITHUB_SHA, GITHUB_REPOSITORY, GITHUB_OUTPUT   set by the runner
set -euo pipefail
 
git fetch --tags --force
 
PR_NUMBER=""
LABELS_JSON='[]'
 
if [[ "${GITHUB_EVENT_NAME}" != "workflow_dispatch" ]]; then
  PR_NUMBER="$(gh api \
    "repos/${GITHUB_REPOSITORY}/commits/${GITHUB_SHA}/pulls" \
    --jq '[.[] | select(.merged_at != null and .base.ref == "main")] | sort_by(.merged_at) | last | .number // empty')"
 
  if [[ -z "${PR_NUMBER}" ]]; then
    echo "::error::Production publication must originate from a pull request merged into main." >&2
    exit 1
  fi
 
  LABELS_JSON="$(gh api \
    "repos/${GITHUB_REPOSITORY}/pulls/${PR_NUMBER}" \
    --jq '[.labels[].name]')"
fi
 
mapfile -t GENERIC_LABELS < <(
  jq -r '.[]' <<< "${LABELS_JSON}" |
    grep -E '^release:(patch|minor|major)$' || true
)
 
if (( ${#GENERIC_LABELS[@]} > 0 )); then
  echo "::error::Generic release labels are not supported. Use component-specific labels." >&2
  exit 1
fi
 
VERSIONS='{}'
GIT_TAGS='{}'
RELEASE_TYPES='{}'
 
component_changed() {
  local component="$1"
  jq -e --arg component "${component}" 'index($component) != null' \
    <<< "${COMPONENTS_JSON}" >/dev/null
}
 
manual_release_type() {
  local component="$1"
  case "${component}" in
    api)
      printf '%s\n' "${MANUAL_API_RELEASE_TYPE}"
      ;;
    web)
      printf '%s\n' "${MANUAL_WEB_RELEASE_TYPE}"
      ;;
    *)
      return 1
      ;;
  esac
}
 
resolve_release_type() {
  local component="$1"
  local changed="$2"
  local release_type=""
  local labels=()
 
  if [[ "${GITHUB_EVENT_NAME}" == "workflow_dispatch" ]]; then
    release_type="$(manual_release_type "${component}")"
 
    if [[ "${changed}" == "true" && ! "${release_type}" =~ ^(patch|minor|major)$ ]]; then
      echo "::error::A release increment is required for changed component ${component}." >&2
      return 1
    fi
 
    if [[ "${changed}" == "false" && "${release_type}" != "none" ]]; then
      echo "::error::Component ${component} is not selected but has release type ${release_type}." >&2
      return 1
    fi
  else
    mapfile -t labels < <(
      jq -r '.[]' <<< "${LABELS_JSON}" |
        grep -E "^release:${component}:(patch|minor|major)$" || true
    )
 
    if [[ "${changed}" == "true" && "${#labels[@]}" -ne 1 ]]; then
      echo "::error::PR #${PR_NUMBER} must have exactly one release:${component}:* label because ${component} changed." >&2
      return 1
    fi
 
    if [[ "${changed}" == "false" && "${#labels[@]}" -ne 0 ]]; then
      echo "::warning::PR #${PR_NUMBER} has release label(s) for unchanged component ${component}; they will be ignored." >&2
    fi
 
    if [[ "${changed}" == "true" ]]; then
      release_type="${labels[0]#release:${component}:}"
    else
      release_type="none"
    fi
  fi
 
  printf '%s\n' "${release_type}"
}
 
bump_version() {
  local current_version="$1"
  local release_type="$2"
  local major
  local minor
  local patch
 
  if [[ ! "${current_version}" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo "::error::Invalid current semantic version '${current_version}'." >&2
    return 1
  fi
 
  IFS='.' read -r major minor patch <<< "${current_version}"
 
  case "${release_type}" in
    major)
      major=$((major + 1))
      minor=0
      patch=0
      ;;
    minor)
      minor=$((minor + 1))
      patch=0
      ;;
    patch)
      patch=$((patch + 1))
      ;;
    *)
      echo "::error::Unsupported release type '${release_type}'." >&2
      return 1
      ;;
  esac
 
  printf '%s.%s.%s\n' "${major}" "${minor}" "${patch}"
}
 
assert_version_sequence() {
  local current
  local sequence_number
  local expected
  local actual
 
  current="0.0.0"
  for sequence_number in $(seq 1 10); do
    expected="0.0.${sequence_number}"
    actual="$(bump_version "${current}" "patch")"
    [[ "${actual}" == "${expected}" ]] || {
      echo "::error::Patch assertion failed: ${current} must produce ${expected}, received ${actual}." >&2
      return 1
    }
    current="${actual}"
  done
 
  current="0.0.0"
  for sequence_number in $(seq 1 10); do
    expected="0.${sequence_number}.0"
    actual="$(bump_version "${current}" "minor")"
    [[ "${actual}" == "${expected}" ]] || {
      echo "::error::Minor assertion failed: ${current} must produce ${expected}, received ${actual}." >&2
      return 1
    }
    current="${actual}"
  done
 
  echo "Semantic-version patch and minor sequence validation passed."
}
 
calculate_version() {
  local component="$1"
  local release_type="$2"
  local latest_tag
  local current_version
 
  latest_tag="$(
    git tag --list "${component}-v*" |
      grep -E "^${component}-v[0-9]+\.[0-9]+\.[0-9]+$" |
      sort -V |
      tail -1 || true
  )"
 
  if [[ -z "${latest_tag}" ]]; then
    current_version="0.0.0"
    echo "::notice::No release tag found for ${component}; using the initial 0.0.0 baseline." >&2
  else
    current_version="${latest_tag#${component}-v}"
  fi
 
  bump_version "${current_version}" "${release_type}"
}
 
assert_version_sequence
 
for COMPONENT in api web; do
  CHANGED="false"
 
  if component_changed "${COMPONENT}"; then
    CHANGED="true"
  fi
 
  RELEASE_TYPE="$(resolve_release_type "${COMPONENT}" "${CHANGED}")"
 
  if [[ "${CHANGED}" == "false" ]]; then
    continue
  fi
 
  mapfile -t EXISTING_COMMIT_TAGS < <(
    git tag --points-at "${GITHUB_SHA}" |
      grep -E "^${COMPONENT}-v[0-9]+\.[0-9]+\.[0-9]+$" || true
  )
 
  if (( ${#EXISTING_COMMIT_TAGS[@]} > 1 )); then
    echo "::error::Commit ${GITHUB_SHA} has multiple ${COMPONENT} release tags; refusing an ambiguous rerun." >&2
    exit 1
  fi
 
  if (( ${#EXISTING_COMMIT_TAGS[@]} == 1 )); then
    NEXT_TAG="${EXISTING_COMMIT_TAGS[0]}"
    NEXT_VERSION="${NEXT_TAG#${COMPONENT}-v}"
    echo "Reusing ${NEXT_TAG} for an idempotent rerun."
  else
    NEXT_VERSION="$(calculate_version "${COMPONENT}" "${RELEASE_TYPE}")"
    NEXT_TAG="${COMPONENT}-v${NEXT_VERSION}"
 
    if git rev-parse --verify --quiet "refs/tags/${NEXT_TAG}" >/dev/null; then
      echo "::error::Calculated Git tag ${NEXT_TAG} already exists." >&2
      exit 1
    fi
  fi
 
  VERSIONS="$(jq -c \
    --arg component "${COMPONENT}" \
    --arg value "${NEXT_VERSION}" \
    '. + {($component): $value}' <<< "${VERSIONS}")"
 
  GIT_TAGS="$(jq -c \
    --arg component "${COMPONENT}" \
    --arg value "${NEXT_TAG}" \
    '. + {($component): $value}' <<< "${GIT_TAGS}")"
 
  RELEASE_TYPES="$(jq -c \
    --arg component "${COMPONENT}" \
    --arg value "${RELEASE_TYPE}" \
    '. + {($component): $value}' <<< "${RELEASE_TYPES}")"
 
  echo "${COMPONENT}: ${NEXT_TAG} (${RELEASE_TYPE})"
done
 
{
  echo "versions=${VERSIONS}"
  echo "git_tags=${GIT_TAGS}"
  echo "release_types=${RELEASE_TYPES}"
  echo "pull_request=${PR_NUMBER}"
} >> "${GITHUB_OUTPUT}"
