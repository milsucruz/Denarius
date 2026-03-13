# Denarius Platform

A study-driven project that simulates a real-world **digital wallet and payment platform**, built using **.NET 10**, **microservices**, and **event-driven architecture**.

The goal of this project is to design and implement a system that resembles the architecture used by modern fintech platforms, focusing on **scalability, resilience, and financial transaction integrity**.

This repository serves as a **learning laboratory** for advanced backend engineering concepts applied to financial systems.

---

# Architecture Overview

The platform is designed using a **domain-oriented microservices architecture** combined with **DDD (Domain Driven Design)** and **event-driven communication**.

Key architectural principles:

* Domain Driven Design (DDD)
* Clean Architecture
* Microservices
* Event-Driven Architecture
* Eventual Consistency
* Idempotent Operations
* Distributed Observability

Services communicate through:

* **HTTP APIs (synchronous communication)**
* **RabbitMQ events (asynchronous communication)**

---

# System Domains

The platform simulates several domains commonly found in fintech systems.

## Identity & Access

Responsible for authentication and authorization.

Capabilities:

* User registration
* Login
* JWT token generation
* Session management

---

## User Management

Responsible for managing user profiles and identity verification.

Capabilities:

* User profile management
* Simplified KYC verification
* Account status tracking

---

## Wallet (Core Domain)

The **wallet domain is the core financial component of the system**.

Instead of storing only balances, the system uses a **financial ledger model**, where every transaction is recorded as a credit or debit entry.

Capabilities:

* Digital wallet creation
* Ledger-based balance tracking
* Transaction history
* Balance calculation

---

## Transfers

Handles peer-to-peer transfers between users.

Capabilities:

* Transfer validation
* Balance verification
* Transfer processing
* Transaction events

---

## Payments

Responsible for external-style payments such as bills or payment requests.

Capabilities:

* Payment processing
* Payment status tracking
* Refund operations

---

## PIX Simulation

Simulates instant payments similar to real-time payment systems.

Capabilities:

* PIX key management
* Instant transfers
* Transaction validation

---

## Fraud Detection

Implements simplified fraud detection rules.

Examples:

* High transaction frequency
* Unusual transfer amounts
* Suspicious account activity

---

## Notifications

Responsible for sending system notifications triggered by events.

Examples:

* Transfer completed
* Payment received
* Account alerts

---

# Technology Stack

Core technologies used in this project:

Backend

* .NET 10
* C#
* ASP.NET Core
* Entity Framework Core

Architecture

* Microservices
* Domain Driven Design
* Clean Architecture
* Event-driven communication

Infrastructure

* SQL Server
* RabbitMQ
* Redis
* Docker

Observability

* Structured logging
* Distributed tracing
* Metrics

Cloud

* Azure (planned deployment)

---

# Repository Structure

```
fintech-platform

src
   services
      identity-service
      wallet-service
      transfer-service
      payment-service
      pix-service
      fraud-service
      notification-service

building-blocks
   shared-kernel
   event-bus
   messaging
   observability

infrastructure
   docker
   database
   rabbitmq
   redis

tests
docs
```

---

# Running the Project (Planned)

The system will run locally using **Docker Compose**.

Planned services:

* SQL Server
* RabbitMQ
* Redis
* Fintech microservices

Command:

```
docker-compose up
```

---

# Learning Objectives

This project focuses on practicing and exploring advanced backend engineering topics:

* Designing financial ledgers
* Event-driven system design
* Distributed transactions
* Idempotent payment processing
* Microservices communication
* Observability in distributed systems

---

# Roadmap

Phase 1
Core wallet domain

* Wallet service
* Ledger entries
* Balance calculation
* Basic API

Phase 2
Transfer system

* Transfer service
* Event-driven transfers
* RabbitMQ integration

Phase 3
Payments

* Payment service
* Refunds
* Transaction lifecycle

Phase 4
Security and fraud

* Authentication
* KYC simulation
* Fraud detection

Phase 5
Platform maturity

* Observability
* Resilience patterns
* Idempotency
* Retry policies

---

# Project Status

This project is currently **under active development** and will evolve incrementally as new architectural components are implemented.

---

# Disclaimer

This project is for **educational purposes only** and does not implement real financial integrations.
