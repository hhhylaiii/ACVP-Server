#!/usr/bin/env python3
"""ML-KEM (FIPS 203) response harness.

Reads an ACVP prompt.json and writes the responses.json expected by the
FIPS 203/204 validation web tool.

HOW TO INTEGRATE YOUR MODULE (IUT)
----------------------------------
Fill in ``invoke_module`` below — it is the ONLY thing you need to change.
Everything else (reading the prompt, walking test groups, writing the
response file in the correct shape) is already done.

DEMO MODE
---------
If an ``expectedResults.json`` file sits next to the prompt (as produced by
the web tool's server or the CLI demo), the harness answers from it instead
of calling your module, so the end-to-end flow can be exercised without an
IUT. Remove that file to force real module invocation.

Usage:  python3 harness_mlkem.py [path/to/prompt.json]
"""
from __future__ import annotations

import json
import os
import sys
from typing import Any


def invoke_module(mode: str, group: dict[str, Any], test: dict[str, Any]) -> dict[str, Any]:
    """Answer ONE test case with YOUR ML-KEM module.

    Required return value per mode (all binary values are lowercase hex):
      keyGen                       -> {"ek": "<hex>", "dk": "<hex>"}
      encapDecap / encapsulation   -> {"c": "<hex>", "k": "<hex>"}
      encapDecap / decapsulation   -> {"k": "<hex>"}
      encapDecap / *KeyCheck       -> {"testPassed": True | False}

    Inputs: ``group`` carries e.g. parameterSet and function; ``test`` carries
    the per-case prompt fields (see field-mapping.md).
    """
    # ======================= FILL IN THIS LINE =======================
    raise NotImplementedError("call your ML-KEM module here")
    # =================================================================


def _load_demo_answers(prompt_path: str) -> dict[tuple[int, int], dict[str, Any]] | None:
    expected_path = os.path.join(os.path.dirname(prompt_path) or ".", "expectedResults.json")
    if not os.path.exists(expected_path):
        return None
    print(f"[demo mode] answering from {expected_path}")
    with open(expected_path, encoding="utf-8") as handle:
        expected = json.load(handle)
    answers: dict[tuple[int, int], dict[str, Any]] = {}
    for group in expected.get("testGroups", []):
        for test in group.get("tests", []):
            answers[(group["tgId"], test["tcId"])] = {
                key: value for key, value in test.items() if key != "tcId"
            }
    return answers


def main() -> None:
    prompt_path = sys.argv[1] if len(sys.argv) > 1 else "prompt.json"
    if not os.path.exists(prompt_path):
        print(f"Error: prompt file '{prompt_path}' not found.", file=sys.stderr)
        sys.exit(1)

    with open(prompt_path, encoding="utf-8") as handle:
        prompt = json.load(handle)

    demo_answers = _load_demo_answers(prompt_path)
    mode = prompt.get("mode", "")

    out_groups = []
    total = 0
    for group in prompt.get("testGroups", []):
        tests = []
        for test in group.get("tests", []):
            tc_id = test["tcId"]
            if demo_answers is not None:
                answer = demo_answers.get((group["tgId"], tc_id))
                if answer is None:
                    print(f"Warning: no demo answer for tgId {group['tgId']} tcId {tc_id}; skipping.")
                    continue
            else:
                answer = invoke_module(mode, group, test)
            tests.append({"tcId": tc_id, **answer})
            total += 1
        out_groups.append({"tgId": group["tgId"], "tests": tests})

    responses: dict[str, Any] = {
        "vsId": prompt["vsId"],
        "algorithm": prompt["algorithm"],
        "revision": prompt["revision"],
    }
    for optional in ("mode", "isSample"):
        if optional in prompt:
            responses[optional] = prompt[optional]
    responses["testGroups"] = out_groups

    responses_path = os.path.join(os.path.dirname(prompt_path) or ".", "responses.json")
    with open(responses_path, "w", encoding="utf-8") as handle:
        json.dump(responses, handle, indent=2)
    print(f"Wrote {responses_path} ({total} test cases). Upload it on the tool's upload page.")


if __name__ == "__main__":
    main()
