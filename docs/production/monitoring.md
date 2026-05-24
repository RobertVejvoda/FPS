# Monitoring

Monitoring describes how FairSpot proves that the system is healthy, fair, auditable, and ready for client operation. The monitoring model must work across local development, demo, and client-owned production.

## Telemetry Boundary

FairSpot should instrument services with OpenTelemetry-compatible metrics, logs, and traces. Deployment profiles decide where telemetry is stored and visualized:

| Profile | Default monitoring target | Purpose |
| --- | --- | --- |
| Local | Prometheus, Grafana, and local tracing such as Jaeger. | Fast developer feedback and service-level debugging. |
| Demo | Low-cost dashboards and trace/log retention sufficient for evaluation. | Prove usage, performance, error rate, notification delivery, draw duration, and audit/reporting behavior. |
| Client production | Client observability platform through OpenTelemetry Collector/exporters. | Integrate with existing client operations, alerting, SIEM, and incident processes. |

Client production examples include Dynatrace, Azure Monitor/Application Insights, Grafana/Prometheus, Splunk, Datadog, New Relic, CloudWatch, or equivalent. FairSpot should not require one vendor-specific SDK in application code.

## Required Signals

| Signal | Examples |
| --- | --- |
| Usage | active tenants, active users, booking requests, cancellations, confirmations, no-shows, admin policy changes |
| API health | request count, latency percentiles, error rate, authentication failures, rate-limit events |
| Draw processing | scheduled draw count, draw duration, eligible request count, allocated/rejected/pending counts, deterministic fallback count |
| Messaging | published events, consumer lag/backlog, dead-letter count, retry count |
| Notification | in-app events, SSE reconnects, email send attempts, delivery failures, preference suppressions |
| Audit and reporting | audit write count, audit query latency, reporting projection lag, export count |
| Infrastructure | container restarts, CPU/memory, storage growth, cache health, broker health, Dapr sidecar health |
| Security | privileged access, secret access, failed authorization, GDPR erasure requests, data export access |

## Technical Telemetry And Business Activity

Production monitoring must keep technical telemetry and business activity evidence separate:

| Evidence | Storage | Primary consumer | Production use |
| --- | --- | --- | --- |
| Technical logs | Client observability platform or local Loki equivalent. | Operators and developers. | Diagnose service errors, dependencies, retries, and request failures. |
| Metrics | Prometheus-compatible backend or client APM. | Operators, SRE, selected admins. | Dashboards, alerts, SLO evidence, trend analysis. |
| Traces | OpenTelemetry-compatible tracing backend. | Operators and developers. | Cross-service diagnosis and latency analysis. |
| Business activity | FairSpot Audit service. | Auditors, HR/facility managers, tenant admins, security reviewers. | Business accountability, compliance evidence, dispute resolution, and export. |

Business-facing audit views must not expose raw technical logs. They should query the Audit service and show stable business actions, actor hash or approved actor display, affected entity, result, reason code, and timestamp.

Technical telemetry and business activity may be linked by correlation metadata:

- `traceId` and `spanId` from the origin OpenTelemetry activity;
- `sourceEventId`, command ID, or business event ID;
- `correlationId` or workflow ID where present.

These identifiers are support links only. They do not replace tenant scoping, actor pseudonymisation, authorization, audit retention, or idempotency.

## Health Checks

All FairSpot services expose `GET /health` returning a JSON body with overall status and per-check results:

```sh
# Smoke all service health endpoints after starting the local harness
for port in 5192 5131 5197 5157 5161 5171 5141; do
  status=$(curl -s http://localhost:$port/health | python3 -c "import sys,json; d=json.load(sys.stdin); print(d['status'])" 2>/dev/null || echo "UNREACHABLE")
  echo ":$port $status"
done
```

Expected result when all services are up: each port returns `Healthy`.

## OpenTelemetry Export (Follow-Up Gap)

FairSpot services use ASP.NET Core's built-in request logging (`ILogger`) which produces structured log output to stdout. This is visible in container logs and can be forwarded by any log shipper. However, OTLP trace and metric export requires the OpenTelemetry SDK and exporter packages to be registered in each service — this has not been added in this slice.

**What this slice delivers:** `GET /health` endpoints on all services (implemented). Structured logs to stdout (built-in).

**What a follow-up slice should add** to activate OTLP traces and metrics:

1. Add NuGet packages to SharedKernel (or each service):
   - `OpenTelemetry.Extensions.Hosting`
   - `OpenTelemetry.Instrumentation.AspNetCore`
   - `OpenTelemetry.Exporter.OpenTelemetryProtocol`

2. Register in each service `Program.cs`:
   ```csharp
   builder.Services.AddOpenTelemetry()
       .WithTracing(b => b
           .AddAspNetCoreInstrumentation()
           .AddOtlpExporter())
       .WithMetrics(b => b
           .AddAspNetCoreInstrumentation()
           .AddOtlpExporter());
   ```

3. Set env vars before starting services:
   ```sh
   export OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4318
   export OTEL_SERVICE_NAME=fps-booking   # one per service
   ```

Once the SDK is registered, pointing `OTEL_EXPORTER_OTLP_ENDPOINT` at a client's OpenTelemetry Collector (Dynatrace, Azure Monitor, Grafana, Splunk, etc.) requires only a config change — no application code change.

**Redaction:** configure the OTLP collector to drop `http.request.header.authorization` and similar credential-bearing attributes. See [Integration Evidence](./integration-evidence) for redaction guidance.

## Open Source Monitoring

Open source tools are the preferred local baseline and a valid client-production option when the client operates them:

### Prometheus
Prometheus is an open-source systems monitoring and alerting toolkit. It is particularly well-suited for monitoring dynamic cloud environments and microservices.

### Grafana
Grafana is an open-source platform for monitoring and observability. It allows you to query, visualize, alert on, and understand your metrics no matter where they are stored. It is often used in conjunction with Prometheus.

### Jaeger
Jaeger is an open-source, end-to-end distributed tracing tool. It is used for monitoring and troubleshooting microservices-based distributed systems.

---

## Monitoring in Amazon AWS

The monitoring of the application hosted in AWS is performed using various AWS services and tools. Below are the key components used for monitoring:

### Amazon CloudWatch
Amazon CloudWatch is used to monitor the application's performance and operational health. It collects and tracks metrics, collects and monitors log files, and sets alarms.

### AWS CloudTrail
AWS CloudTrail is used to log, continuously monitor, and retain account activity related to actions across the AWS infrastructure.

### AWS X-Ray
AWS X-Ray helps with analyzing and debugging production, distributed applications, such as those built using a microservices architecture.

### Amazon SNS
Amazon Simple Notification Service (SNS) is used to send notifications from CloudWatch alarms to the operations team.

### AWS Config
AWS Config provides a detailed view of the configuration of AWS resources in the account. It helps with compliance auditing, security analysis, and resource change tracking.

### AWS Trusted Advisor
AWS Trusted Advisor provides real-time guidance to help provision resources following AWS best practices. It helps optimize the AWS environment by reducing costs, increasing performance, and improving security.

### Third-Party Tools
In addition to AWS native tools, third-party monitoring tools such as Dynatrace, Datadog, New Relic, and Splunk can be integrated through OpenTelemetry exporters where possible.

### Custom Dashboards
Custom dashboards can be created in CloudWatch or third-party tools to visualize key metrics and logs for better insight into the application's performance and health.

### Incident Response
Automated incident response can be set up using AWS Lambda to trigger specific actions based on CloudWatch alarms.

---

## Monitoring in MS Azure

Azure provides a comprehensive set of monitoring tools and services to ensure the performance and health of applications hosted in the Azure cloud. Below are some key Azure monitoring tools:

### Azure Monitor
Azure Monitor collects and analyzes telemetry data from Azure resources, applications, and on-premises environments. It provides insights into the performance and health of your applications and infrastructure.

### Azure Log Analytics
Azure Log Analytics is a service within Azure Monitor that helps collect and analyze log data from various sources. It enables you to query and visualize log data to gain insights into your system's operations.

### Azure Application Insights
Azure Application Insights is an application performance management (APM) service that monitors live applications. It helps detect and diagnose performance issues and understand how users interact with your application.

### Azure Security Center
Azure Security Center provides advanced threat protection across your hybrid workloads in the cloud and on-premises. It helps detect and respond to threats and provides security recommendations.

### Azure Service Health
Azure Service Health provides personalized alerts and guidance when Azure service issues affect you. It helps you stay informed about the health of your Azure services.

### Azure Automation
Azure Automation allows you to automate frequent, time-consuming, and error-prone cloud management tasks. It can be used to create runbooks for automated incident response and remediation.

### Azure Sentinel
Azure Sentinel is a scalable, cloud-native security information and event management (SIEM) and security orchestration automated response (SOAR) solution. It provides intelligent security analytics and threat intelligence across the enterprise.

### Azure Network Watcher
Azure Network Watcher provides tools to monitor, diagnose, and gain insights into your network performance and health. It helps with network troubleshooting and diagnostics.
