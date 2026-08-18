from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

import yaml

ROOT = Path(__file__).resolve().parents[1]
workflow_dir = ROOT / ".github" / "workflows"
required_files = [
    workflow_dir / "ci.yml",
    workflow_dir / "publish-images.yml",
    workflow_dir / "deploy-vm.yml",
    workflow_dir / "report-validation.yml",
    ROOT / "deploy" / "backend.Dockerfile",
    ROOT / "deploy" / "frontend.Dockerfile",
    ROOT / "deploy" / "nginx.conf",
    ROOT / "deploy" / "production.compose.yml",
    ROOT / "deploy" / ".env.production.example",
    ROOT / "docs" / "CICD_SETUP.md",
    ROOT / "docs" / "DEPLOYMENT_RUNBOOK.md",
    ROOT / "docs" / "SSRS-CICD.md",
    ROOT / "docs" / "REPOSITORY_SECRETS.md",
    ROOT / "docs" / "RELEASE_CHECKLIST.md",
]

missing = [str(path.relative_to(ROOT)) for path in required_files if not path.exists()]
if missing:
    raise SystemExit(f"Missing required CI/CD files: {missing}")

yaml_files = sorted(workflow_dir.glob("*.yml")) + [
    ROOT / "docker-compose.yml",
    ROOT / "deploy" / "production.compose.yml",
]
for path in yaml_files:
    with path.open(encoding="utf-8") as handle:
        document = yaml.safe_load(handle)
    if not isinstance(document, dict):
        raise SystemExit(f"{path.relative_to(ROOT)}: YAML root must be a mapping")
    if path.parent == workflow_dir and ("jobs" not in document or not document["jobs"]):
        raise SystemExit(f"{path.name}: workflow must define jobs")
    print(f"validated YAML: {path.relative_to(ROOT)}")

sensitive_patterns = [
    re.compile(r"Password=(?!REPLACE_ME|\$[A-Za-z_][A-Za-z0-9_]*)[^\s;]+", re.IGNORECASE),
    re.compile(r"BEGIN (RSA|OPENSSH|EC|DSA) PRIVATE KEY"),
    re.compile(r"ghp_[A-Za-z0-9]{20,}"),
]
allowed_password_placeholder = "Password=REPLACE_ME"
for path in required_files:
    text = path.read_text(encoding="utf-8")
    for pattern in sensitive_patterns:
        for match in pattern.findall(text):
            if match != allowed_password_placeholder:
                raise SystemExit(f"Potential secret in {path.relative_to(ROOT)}: {match}")

for rdl in sorted((ROOT / "reports").glob("*.rdl")):
    ET.parse(rdl)
    print(f"validated report XML: {rdl.relative_to(ROOT)}")

print(f"validated {len(list(workflow_dir.glob('*.yml')))} workflows, {len(yaml_files)} YAML files, and {len(required_files)} required files")
