#!/usr/bin/env bash
# Decides which components (api, web) a run affects and writes result=<JSON array> to $GITHUB_OUTPUT.
# Manual dispatches choose components from the release-type inputs (and must run from main);
# every other event uses the path-filter result.
#
# Called by the changes job in build-test-publish-images.yml.
#
# Environment:
#   API_RELEASE_TYPE, WEB_RELEASE_TYPE   workflow_dispatch inputs (none|patch|minor|major)
#   FILTER_CHANGES                       JSON array from dorny/paths-filter (not used for dispatch)
#   GITHUB_EVENT_NAME, GITHUB_REF, GITHUB_OUTPUT   set by the runner
set -euo pipefail
 
if [[ "${GITHUB_EVENT_NAME}" == "workflow_dispatch" ]]; then
  if [[ "${GITHUB_REF}" != "refs/heads/main" ]]; then
    echo "::error::Production workflow dispatches must run from the main branch."
    exit 1
  fi
 
  COMPONENTS='[]'
 
  if [[ "${API_RELEASE_TYPE}" != "none" ]]; then
    COMPONENTS="$(jq -c '. + ["api"]' <<< "${COMPONENTS}")"
  fi
 
  if [[ "${WEB_RELEASE_TYPE}" != "none" ]]; then
    COMPONENTS="$(jq -c '. + ["web"]' <<< "${COMPONENTS}")"
  fi
 
  if [[ "${COMPONENTS}" == "[]" ]]; then
    echo "::error::Select a release increment for at least one component."
    exit 1
  fi
 
  echo "result=${COMPONENTS}" >> "${GITHUB_OUTPUT}"
else
  echo "result=${FILTER_CHANGES}" >> "${GITHUB_OUTPUT}"
fi
