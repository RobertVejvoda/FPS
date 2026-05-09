---
title: Feedback
---

The Feedback component handles the collection and management of user feedback. It allows users to submit feedback on various aspects of the service, and manages the storage and analysis of this feedback to improve service quality.

![Software Architecture - Feedback](../images/fps-software-arch-feedback.png)

## REST API Endpoints

| Endpoint | Method | Description | Response | Status |
|----------|--------|-------------|----------|---------|
| `/api/feedback` | POST | Submit new feedback | Returns created feedback object with ID | 201 Created |
| `/api/feedback` | GET | Retrieve all feedback entries | Returns array of feedback objects | 200 OK |
| `/api/feedback/{id}` | GET | Get specific feedback by ID | Returns single feedback object | 200 OK |
| `/api/feedback/{id}` | PUT | Update feedback status or details | Returns updated feedback object | 200 OK |
| `/api/feedback/{id}/responses` | POST | Add response to feedback | Returns response object | 201 Created |
| `/api/feedback/categories` | GET | List available feedback categories | Returns array of category objects | 200 OK |
| `/api/feedback/search` | GET | Search feedback with filters | Returns filtered array of feedback | 200 OK |
| `/api/feedback/statistics` | GET | Get feedback analytics and metrics | Returns statistics object | 200 OK |

## Software Components

| Software Component | Type | Purpose | Technology |
|-------------------|------|----------|------------|
| feedback-api      | API  | External interface for feedback operations | Web API (REST) |
| feedback-data     | Data | Feedback data access and persistence | Document DB |

## Service Exchanges

| Interface           | Consumer   | Producer   | No. of calls / day | Auth. method | Type / Protocol   | Comments |
|---------------------|------------|------------|--------------------|--------------|-------------------|----------|
| User Authentication | Consumer 1 | Producer 1 | 800                | OAuth 2.0    | REST / HTTPS      |          |
| Interface 2         | Consumer 2 | Producer 2 | 300                | API Key      | SOAP / HTTPS      |          |
| Interface 3         | Consumer 3 | Producer 3 | 1500               | JWT          | GraphQL / HTTPS   |          |

## Message Exchanges

| Message Type       | Sender     | Receiver   | Frequency           | Format       | Protocol         | Comments |
|--------------------|------------|------------|---------------------|--------------|------------------|----------|
| Event Notification | Service A  | Service B  | Real-time           | JSON         | WebSocket        |          |
| Data Sync          | Service C  | Service D  | Every 10 minutes    | XML          | AMQP             |          |
| Alert Message      | Service E  | Service F  | On Event            | Plain Text   | MQTT             |          |

## File Exchanges

| File Name          | Source      | Destination | Frequency          | Format       | Transfer Method | Comments |
|--------------------|-------------|-------------|--------------------|--------------|-----------------|----------|
| Feedback Export    | System A    | System B    | Daily              | CSV          | SFTP            |          |
| Log Files          | System C    | System D    | Hourly             | JSON         | FTP             |          |
| Backup Archives    | System E    | System F    | Weekly             | ZIP          | HTTPS           |          |

## Packaging

![Feedback](../images/fps-software-pack-feedback.png)