---
title: Backup and Recovery
---

## Docker Images in AKS

When hosting Docker images in Azure Kubernetes Service (AKS), it's crucial to have a backup and recovery strategy to ensure the availability and integrity of your containerized applications. Here are the key steps:
### Backup

#### Using Azure Container Registry (ACR)
- **Export Images**: Use `az acr import` to export images from ACR to a storage account.
- **Automated Backups**: Set up a scheduled task to regularly export images to a storage account.

#### Using Third-Party Tools
- **Velero**: Use Velero to back up and restore Kubernetes cluster resources and persistent volumes.
- **Harbor**: Use Harbor to replicate images to another registry as a backup.

### Recovery

#### From Azure Container Registry
- **Import Images**: Use `az acr import` to import images back from the storage account to ACR.
- **Redeploy**: Update your Kubernetes manifests to redeploy the images from ACR.

#### Using Third-Party Tools
- **Velero**: Restore cluster resources and persistent volumes using Velero.
- **Harbor**: Pull images from the backup registry and push them to your primary registry.

### Backup Procedures
- Regularly test image backups and recovery process every quarter.
- Monitor backup operations and set up alerts for failures.
- Keep backup scripts and tools up to date.
- Document the backup and recovery process and ensure team members are trained.

### Storage Account
- **Choosing a Storage Account**: Use Azure Blob Storage for storing exported Docker images and backups.
- **Configuration**: Ensure the storage account is configured with appropriate access controls and redundancy options.
- **Access**: Use Azure Storage Explorer or Azure CLI to manage and access the storage account.


## Azure Cosmos DB

[Azure Cosmos DB](https://docs.microsoft.com/en-us/azure/cosmos-db/) provides automatic and manual backup options to ensure data protection and recovery. Here are the key features:

### Automatic Backups
- **Frequency**: Azure Cosmos DB automatically takes backups of your data every 4 hours.
- **Retention**: These backups are retained for 30 days.
- **Restoration**: You can request a point-in-time restore by contacting Azure support.

### Manual Backups
- **On-Demand Backups**: On-demand backups using Azure Data Factory before each version upgrade.
- **Export Data**:  Azure Cosmos DB Data Migration tool to export data to a storage account (Azure Blob Storage).

### Recovery Process
- **Contact Support**: For automatic backups, contact Azure support to initiate a restore.
- **Use Exported Data**: For manual backups, use the exported data from storage account to restore database.

### Backup procedures
- Regularly test backups and recovery process every quarter.
- Monitor backup operations and set up alerts for failures.
- Keep backup scripts and tools up to date.



