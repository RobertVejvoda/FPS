# Network Security

Network security protects public entry points, internal service traffic, state stores, brokers, secret stores, object storage, and observability surfaces from unauthorized access. It is defined by profile capabilities, not by a single cloud provider.

## Public Edge

- Public endpoints must use HTTPS.
- Release 1 uses Cloudflare Tunnel/WAF for the hosted evaluation path.
- The DigitalOcean follow-up may keep Cloudflare in front or use a DigitalOcean Load Balancer only when the profile explicitly requires it.
- WAF/rate-limit rules must protect login, API, admin, and debug-sensitive paths.

## Internal Boundary

- Internal services, Dapr sidecars, databases, brokers, secret stores, Keycloak admin, and observability backends must not be public.
- Service-to-service traffic should use Dapr mTLS or an approved equivalent for hosted profiles.
- Administrative access must be local, VPN/tunnel-protected, Cloudflare Access-protected, or client-approved.

## Segmentation

- Separate public ingress from internal service networks.
- Keep state stores, brokers, cache/session stores, object storage, and secret stores reachable only by approved services.
- Client-owned production may use the client's VPC/VNet/Kubernetes/network-segmentation model as long as the same boundaries are enforced.

## Monitoring

Operators should monitor:

- ingress/WAF/rate-limit events;
- unusual authentication failures;
- unexpected public port exposure;
- Dapr sidecar/component health;
- database, broker, secret-store, and object-storage connection failures;
- network saturation and suspicious egress.
