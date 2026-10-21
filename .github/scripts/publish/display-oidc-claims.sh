#!/usr/bin/env bash
# Prints the non-secret claims of the GitHub OIDC token (audience sts.amazonaws.com) so an AWS trust-policy
# mismatch can be diagnosed from the job log. Best effort: the calling step is continue-on-error.
#
# Called by the publish job in build-test-publish-images.yml.
#
# Environment: ACTIONS_ID_TOKEN_REQUEST_URL, ACTIONS_ID_TOKEN_REQUEST_TOKEN (set by the runner)
set -euo pipefail
 
RESPONSE="$(curl --silent --show-error --fail \
  --header "Authorization: bearer ${ACTIONS_ID_TOKEN_REQUEST_TOKEN}" \
  "${ACTIONS_ID_TOKEN_REQUEST_URL}&audience=sts.amazonaws.com")"
TOKEN="$(echo "${RESPONSE}" | jq -er '.value')"
PAYLOAD="$(echo "${TOKEN}" | cut -d '.' -f 2)"
 
TOKEN_PAYLOAD="${PAYLOAD}" python3 - <<'PY'
import base64
import json
import os
 
payload = os.environ["TOKEN_PAYLOAD"]
payload += "=" * (-len(payload) % 4)
claims = json.loads(base64.urlsafe_b64decode(payload))
 
for key in [
    "aud",
    "sub",
    "repository",
    "repository_owner_id",
    "repository_id",
    "ref",
    "event_name",
    "environment",
]:
    print(f"{key}={claims.get(key)}")
PY
