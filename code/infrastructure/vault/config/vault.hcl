# Vault server config for the NAS/hosted profile (durable, manually unsealed).
#
# Used only by the NAS overlay (docker-compose.nas.yml). Local development keeps
# `vault -dev` (auto-unseal, no ceremony). See the NAS runbook for the one-time
# init/unseal/enable-kv sequence.
#
# TLS is terminated at the Cloudflare edge; Vault is reachable only on the private
# Docker network, so the internal listener runs plain HTTP. Vault host ports are
# loopback-bound (see docker-compose.yaml), never exposed on the LAN.

storage "raft" {
  path    = "/vault/file"
  node_id = "fps-nas-vault"
}

listener "tcp" {
  address     = "0.0.0.0:8200"
  tls_disable = "true"
}

api_addr     = "http://vault:8200"
cluster_addr = "http://vault:8201"

ui = true

# The container has IPC_LOCK, but disabling mlock keeps server mode portable
# across NAS hosts where memory locking may be restricted.
disable_mlock = true
