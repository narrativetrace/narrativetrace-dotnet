#!/usr/bin/env bash
# SPDX-License-Identifier: BUSL-1.1
# Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
# Copyright (c) 2026 Empower Agile
# Gate grader for the published init prompt — the whole of it lives in grade_the_prompt.sh, shared
# with the registry cases. This case's fixture defines its service boundary as IOrderService (the
# "Install and first trace" block / sixty-seconds.md). World-state, never text equality.
set -euo pipefail
exec bash "$(dirname "$0")/../../grade_the_prompt.sh" IOrderService
