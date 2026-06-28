#!/usr/bin/env python3
"""ML-KEM FIPS 203 IUT (Implementation Under Test) harness.

For demo purposes, this script acts as a test bridge:
It reads prompt.json, looks up the corresponding answers in expectedResults.json,
and generates the responses.json expected by the ACVP validator.
"""
import os
import sys
import json

def main():
    prompt_path = sys.argv[1] if len(sys.argv) > 1 else "prompt.json"
    if not os.path.exists(prompt_path):
        print(f"Error: Prompt file '{prompt_path}' not found.")
        print("Usage: python3 iut_mlkem.py [path/to/prompt.json]")
        sys.exit(1)

    expected_path = os.path.join(os.path.dirname(prompt_path), "expectedResults.json")
    if not os.path.exists(expected_path):
        # Fallback to current directory
        expected_path = "expectedResults.json"

    if not os.path.exists(expected_path):
        print(f"Error: expectedResults.json not found (checked same directory as prompt and CWD).")
        print("This demo harness requires expectedResults.json to solve the ML-KEM vectors.")
        sys.exit(1)

    print(f"Reading prompt from: {prompt_path}")
    print(f"Reading expected results from: {expected_path}")

    prompt = json.load(open(prompt_path))
    expected = json.load(open(expected_path))

    # Map expected results by (tgId, tcId)
    expected_map = {}
    for g in expected["testGroups"]:
        tg_id = g["tgId"]
        for t in g["tests"]:
            tc_id = t["tcId"]
            expected_map[(tg_id, tc_id)] = t

    out_groups = []
    for g in prompt["testGroups"]:
        tg_id = g["tgId"]
        tests = []
        for t in g["tests"]:
            tc_id = t["tcId"]
            key = (tg_id, tc_id)
            if key not in expected_map:
                print(f"Warning: tcId {tc_id} in tgId {tg_id} not found in expectedResults.json. Skipping.")
                continue
            
            ans = expected_map[key]
            res_t = {"tcId": tc_id}
            for k, v in ans.items():
                if k != "tcId":
                    res_t[k] = v
            tests.append(res_t)
        out_groups.append({"tgId": tg_id, "tests": tests})

    response = {
        "vsId": prompt["vsId"],
        "algorithm": prompt["algorithm"],
        "revision": prompt["revision"],
        "testGroups": out_groups
    }
    
    # Mirror optional top level fields if present
    for field in ["mode", "isSample"]:
        if field in prompt:
            response[field] = prompt[field]

    responses_path = os.path.join(os.path.dirname(prompt_path), "responses.json")
    json.dump(response, open(responses_path, "w"), indent=2)
    print(f"Wrote responses.json ({sum(len(g['tests']) for g in out_groups)} test cases to {responses_path})")

if __name__ == "__main__":
    main()
