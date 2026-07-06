#!/usr/bin/env bash
# Render the NAS Alertmanager config from the ignored operator env file.
#
# Local development keeps code/infrastructure/alertmanager/config.yaml, which
# routes to local-only. The NAS profile renders a runtime config so notification
# secrets can come from nas.env without committing them to Git.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
INFRA_DIR="$REPO_ROOT/code/infrastructure"
ENV_FILE="${1:-$INFRA_DIR/nas.env}"
ALERTMANAGER_DIR="$INFRA_DIR/alertmanager"
RUNTIME_DIR="$ALERTMANAGER_DIR/runtime"
SECRETS_DIR="$RUNTIME_DIR/secrets"
CONFIG_OUT="$RUNTIME_DIR/config.yaml"
LOCAL_CONFIG="$ALERTMANAGER_DIR/config.yaml"

env_value() {
  local key="$1"
  awk -v key="$key" '
    function trim(s) {
      sub(/^[ \t\r\n]+/, "", s)
      sub(/[ \t\r\n]+$/, "", s)
      return s
    }
    /^[ \t]*(#|$)/ { next }
    {
      line = $0
      sub(/^[ \t]*export[ \t]+/, "", line)
      pos = index(line, "=")
      if (pos == 0) next
      name = trim(substr(line, 1, pos - 1))
      if (name != key) next
      value = trim(substr(line, pos + 1))
      if ((substr(value, 1, 1) == "\"" && substr(value, length(value), 1) == "\"") ||
          (substr(value, 1, 1) == "\047" && substr(value, length(value), 1) == "\047")) {
        value = substr(value, 2, length(value) - 2)
      }
      print value
      exit
    }
  ' "$ENV_FILE"
}

yaml_quote() {
  local value="$1"
  value=${value//\'/\'\'}
  printf "'%s'" "$value"
}

if [[ ! -f "$ENV_FILE" ]]; then
  echo "ERROR: env file not found: $ENV_FILE" >&2
  exit 1
fi

mkdir -p "$SECRETS_DIR"

email_to="$(env_value ALERTMANAGER_EMAIL_TO)"
email_from="$(env_value ALERTMANAGER_EMAIL_FROM)"
smtp_smarthost="$(env_value ALERTMANAGER_SMTP_SMARTHOST)"
smtp_username="$(env_value ALERTMANAGER_SMTP_USERNAME)"
smtp_password="$(env_value ALERTMANAGER_SMTP_PASSWORD)"
discord_webhook_url="$(env_value ALERTMANAGER_DISCORD_WEBHOOK_URL)"

if [[ -z "$email_to$smtp_password$discord_webhook_url" ]]; then
  cp "$LOCAL_CONFIG" "$CONFIG_OUT"
  echo "Alertmanager external notifications disabled; rendered local-only config."
  echo "Output: $CONFIG_OUT"
  exit 0
fi

email_from="${email_from:-alerts@fairspot.net}"
smtp_smarthost="${smtp_smarthost:-smtp.sendgrid.net:587}"
smtp_username="${smtp_username:-apikey}"

missing=()
[[ -z "$email_to" ]] && missing+=("ALERTMANAGER_EMAIL_TO")
[[ -z "$email_from" ]] && missing+=("ALERTMANAGER_EMAIL_FROM")
[[ -z "$smtp_password" ]] && missing+=("ALERTMANAGER_SMTP_PASSWORD")
[[ -z "$discord_webhook_url" ]] && missing+=("ALERTMANAGER_DISCORD_WEBHOOK_URL")

if [[ ${#missing[@]} -gt 0 ]]; then
  echo "ERROR: Alertmanager notifications are partially configured." >&2
  echo "Set these missing values in $ENV_FILE:" >&2
  printf '  - %s\n' "${missing[@]}" >&2
  exit 1
fi

printf '%s' "$smtp_password" > "$SECRETS_DIR/smtp_password"
printf '%s' "$discord_webhook_url" > "$SECRETS_DIR/discord_webhook_url"
# The prom/alertmanager image runs as nobody. Bind-mounted file secrets must be
# readable by that user on Linux NAS hosts.
chmod 644 "$SECRETS_DIR/smtp_password" "$SECRETS_DIR/discord_webhook_url" 2>/dev/null || true

cat > "$CONFIG_OUT" <<YAML
global:
  resolve_timeout: 5m
  smtp_smarthost: $(yaml_quote "$smtp_smarthost")
  smtp_from: $(yaml_quote "$email_from")
  smtp_auth_username: $(yaml_quote "$smtp_username")
  smtp_auth_password_file: /etc/alertmanager/secrets/smtp_password
  smtp_require_tls: true

route:
  group_by: ['alertname', 'job']
  group_wait: 10s
  group_interval: 1m
  repeat_interval: 30m
  receiver: 'ops-warning-email'

  routes:
    - match:
        severity: critical
      group_wait: 5s
      repeat_interval: 5m
      receiver: 'ops-critical-email-discord'
    - match:
        severity: warning
      receiver: 'ops-warning-email'

receivers:
  - name: 'ops-critical-email-discord'
    email_configs:
      - to: $(yaml_quote "$email_to")
        send_resolved: true
        headers:
          subject: '[FairSpot {{ .Status }}] {{ .CommonLabels.alertname }} {{ .CommonLabels.job }}'
    discord_configs:
      - webhook_url_file: /etc/alertmanager/secrets/discord_webhook_url
        send_resolved: true
        title: 'FairSpot {{ .Status }}: {{ .CommonLabels.alertname }}'
        message: '{{ range .Alerts }}{{ .Annotations.summary }}{{ if .Annotations.description }} - {{ .Annotations.description }}{{ end }}{{ "\n" }}{{ end }}'

  - name: 'ops-warning-email'
    email_configs:
      - to: $(yaml_quote "$email_to")
        send_resolved: true
        headers:
          subject: '[FairSpot {{ .Status }}] {{ .CommonLabels.alertname }} {{ .CommonLabels.job }}'

inhibit_rules:
  - source_match:
      severity: critical
    target_match:
      severity: warning
    equal: ['job']
YAML

chmod 644 "$CONFIG_OUT" 2>/dev/null || true

echo "Alertmanager NAS notification config rendered."
echo "Output: $CONFIG_OUT"
echo "Secrets: $SECRETS_DIR (values not printed)"
