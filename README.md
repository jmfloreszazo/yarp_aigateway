# Yarp.AiGateway

A declarative **AI Gateway** built as a [YARP](https://github.com/microsoft/reverse-proxy) extension for .NET 10. Drop it into any ASP.NET Core app with a single JSON config file to get intelligent routing, safety guardrails, rate limiting, provider fallback, and audit logging across multiple LLM providers.

[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com)
[![YARP](https://img.shields.io/badge/YARP-2.3.0-blue)](https://github.com/microsoft/reverse-proxy)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

---

## TL;DR

> **3 lines of code. 1 JSON file. Full AI safety.**

```csharp
builder.Services.AddAiGatewayFromJson("aigateway.json");
app.UseAiGateway();
app.Run();
```

| What you get | How |
|---|---|
| **Multi-provider routing** (OpenAI, Azure OpenAI, Mistral) | Declarative rules in `aigateway.json` |
| **Automatic fallback** between providers | `"fallbacks": [{ "from": "openai", "to": "azure-openai" }]` |
| **PII redaction** (emails, SSN, credit cards, DNI, IBAN...) | `{ "type": "pii", "mode": "sanitize" }` |
| **Prompt injection detection** (30+ patterns + structural) | Always enabled by default |
| **Semantic content analysis** (violence, hate, self-harm...) | `{ "type": "semantic", "mode": "block" }` |
| **Secret leak prevention** (API keys, JWTs, connection strings) | `{ "type": "secret-detection", "mode": "block" }` |
| **Rate limiting** per user/tenant/app | `{ "requestsPerMinute": 60, "scope": "user" }` |
| **Audit logging** with latency, tokens, and cost | Built-in, extensible via `IAuditSink` |

```mermaid
graph LR
    App([Your App]) -->|POST /ai/chat| GW[AI Gateway]
    GW --> G[Guardrails<br/>6 input · 2 output]
    G --> R[Smart Router<br/>rules + fallback]
    R --> P1[OpenAI]
    R --> P2[Azure OpenAI]
    R --> P3[Mistral]
```

---

## Table of Contents

- [Yarp.AiGateway](#yarpaigateway)
  - [TL;DR](#tldr)
  - [Table of Contents](#table-of-contents)
  - [Architecture](#architecture)
    - [High-Level Flow](#high-level-flow)
    - [Request Lifecycle](#request-lifecycle)
    - [Guardrail Pipeline](#guardrail-pipeline)
  - [Features](#features)
  - [Project Structure](#project-structure)
  - [Quick Start](#quick-start)
    - [1. Add the NuGet reference](#1-add-the-nuget-reference)
    - [2. Create `aigateway.json`](#2-create-aigatewayjson)
    - [3. Wire it up in `Program.cs`](#3-wire-it-up-in-programcs)
    - [4. Send a request](#4-send-a-request)
    - [Response](#response)
  - [Configuration Reference](#configuration-reference)
    - [Gateway](#gateway)
    - [Providers](#providers)
    - [Routing](#routing)
      - [Conditions](#conditions)
      - [Available Fields](#available-fields)
      - [Fallbacks](#fallbacks)
    - [Guardrails](#guardrails)
    - [Quotas](#quotas)
    - [Telemetry](#telemetry)
  - [Guardrails Deep Dive](#guardrails-deep-dive)
    - [Input Guardrails](#input-guardrails)
      - [Prompt Injection Detection (Order 150)](#prompt-injection-detection-order-150)
      - [Semantic Content Analysis (Order 250)](#semantic-content-analysis-order-250)
      - [PII Detection \& Redaction (Order 300)](#pii-detection--redaction-order-300)
      - [Secret Detection (Order 350)](#secret-detection-order-350)
      - [Max Length (Order 100)](#max-length-order-100)
      - [Blocked Patterns (Order 200)](#blocked-patterns-order-200)
    - [Output Guardrails](#output-guardrails)
  - [Supported Providers](#supported-providers)
  - [Extensibility](#extensibility)
    - [Custom Guardrail](#custom-guardrail)
    - [Custom Provider](#custom-provider)
    - [Custom Audit Sink](#custom-audit-sink)
  - [Testing](#testing)
    - [Test Coverage](#test-coverage)
  - [Roadmap](#roadmap)
  - [Requirements](#requirements)
  - [License](#license)

---

## Architecture

### High-Level Flow

```mermaid
graph LR
    Client([Client App]) -->|POST /ai/chat| MW[AI Gateway Middleware]
    MW --> Pipeline

    subgraph Pipeline[AI Gateway Pipeline]
        direction TB
        Q[Quota Check] --> IG[Input Guardrails]
        IG --> PE[Policy Evaluator]
        PE --> MR[Model Router]
        MR --> PF[Provider Factory]
        PF --> LLM[LLM Provider]
        LLM --> OG[Output Guardrails]
        OG --> AU[Audit Sink]
    end

    Pipeline --> Client

    LLM -.->|Fallback| FB[Fallback Provider]
    FB -.-> OG
```

### Request Lifecycle

```mermaid
sequenceDiagram
    participant C as Client
    participant M as Middleware
    participant Q as QuotaManager
    participant IG as Input Guardrails
    participant R as Model Router
    participant P as Provider
    participant FP as Fallback Provider
    participant OG as Output Guardrails
    participant A as Audit Sink

    C->>M: POST /ai/chat { prompt, ... }
    M->>Q: EnsureAllowedAsync()
    alt Quota Exceeded
        Q-->>M: 429 Too Many Requests
    end
    M->>IG: EvaluateAsync() [ordered pipeline]
    Note over IG: Max Length → Prompt Injection →<br/>Blocked Patterns → Semantic →<br/>PII Detection → Secret Detection
    alt Blocked
        IG-->>M: 422 { blocked: true, reason }
    end
    M->>R: RouteAsync() → { provider, model }
    M->>P: ExecuteAsync(request)
    alt Provider Fails
        P-->>M: Exception
        M->>FP: ExecuteAsync(request) [fallback chain]
    end
    M->>OG: EvaluateAsync(response)
    M->>A: WriteAsync(auditEvent)
    M-->>C: 200 { content, tokens, cost, ... }
```

### Guardrail Pipeline

```mermaid
graph TD
    subgraph Input Guardrails
        direction TB
        ML[100 - Max Length] --> PI[150 - Prompt Injection]
        PI --> BP[200 - Blocked Patterns]
        BP --> SEM[250 - Semantic Analysis]
        SEM --> PII[300 - PII Detection]
        PII --> SD[350 - Secret Detection]
    end

    subgraph Modes
        BLK[🛑 Block — Reject request]
        SAN[🔄 Sanitize — Redact & continue]
        FLG[🏷️ Flag — Annotate & continue]
    end

    Input_Guardrails --> Modes
```

---

## Features

| Category | Capabilities |
|----------|-------------|
| **Multi-Provider Routing** | OpenAI, Azure OpenAI, Mistral — with automatic fallback chains |
| **Declarative Config** | Single `aigateway.json` file with environment variable resolution (`env:VAR_NAME`) |
| **Input Guardrails** | Prompt injection detection, PII redaction, semantic content analysis, secret detection, length limits, blocked patterns |
| **Output Guardrails** | Response PII redaction, blocked pattern filtering |
| **Rate Limiting** | Per-user / per-tenant / per-application quotas (RPM and token-based) |
| **Intelligent Routing** | Condition-based rules (prompt length, tenant, metadata fields) with 9 operators |
| **Audit & Telemetry** | Structured logging of every request with latency, tokens, cost, and flags |
| **Extensibility** | Plug in custom guardrails, providers, routers, and audit sinks via DI |

---

## Project Structure

```
Yarp.AiGateway.slnx
│
├── src/
│   ├── Yarp.AiGateway.Abstractions/     # Interfaces & models (zero dependencies)
│   │   ├── IAiProvider.cs
│   │   ├── IAiGatewayPipeline.cs
│   │   ├── IInputGuardrail.cs
│   │   ├── IOutputGuardrail.cs
│   │   ├── IModelRouter.cs
│   │   ├── IQuotaManager.cs
│   │   ├── IAuditSink.cs
│   │   ├── IPolicyEvaluator.cs
│   │   ├── IProviderFactory.cs
│   │   └── Models/
│   │       ├── AiGatewayRequest.cs       # Provider-agnostic request
│   │       ├── AiGatewayResponse.cs      # Normalized response
│   │       ├── GuardrailDecisions.cs     # Allow/Block decisions
│   │       ├── RouteDecision.cs          # Routing result
│   │       ├── PolicyContext.cs
│   │       └── AuditEvent.cs
│   │
│   ├── Yarp.AiGateway.Core/             # Pipeline, routing, middleware
│   │   ├── Middleware/
│   │   │   └── AiGatewayMiddleware.cs    # ASP.NET Core middleware
│   │   ├── Pipeline/
│   │   │   ├── AiGatewayPipeline.cs      # Orchestrates the full flow
│   │   │   └── DefaultPolicyEvaluator.cs
│   │   ├── Routing/
│   │   │   ├── DefaultModelRouter.cs     # Rule-based model routing
│   │   │   └── ConditionEvaluator.cs     # Typed condition engine
│   │   ├── Configuration/
│   │   │   ├── AiGatewayConfiguration.cs # Config model classes
│   │   │   ├── AiGatewayConfigLoader.cs  # JSON loader + env var resolution
│   │   │   └── AiGatewayConfigValidator.cs
│   │   ├── Quotas/
│   │   │   └── InMemoryQuotaManager.cs   # Sliding-window rate limiter
│   │   ├── Providers/
│   │   │   └── DefaultProviderFactory.cs
│   │   ├── Telemetry/
│   │   │   └── DefaultAuditSink.cs       # ILogger-based audit
│   │   └── Extensions/
│   │       ├── AiGatewayServiceCollectionExtensions.cs
│   │       ├── GuardrailRegistrationExtensions.cs
│   │       └── ProviderRegistrationExtensions.cs
│   │
│   ├── Yarp.AiGateway.Guardrails/        # All guardrail implementations
│   │   ├── Input/
│   │   │   ├── MaxLengthGuardrail.cs
│   │   │   ├── PromptInjectionGuardrail.cs
│   │   │   ├── BlockedPatternsGuardrail.cs
│   │   │   ├── SemanticGuardrail.cs      # Weighted keyword scoring
│   │   │   ├── PiiDetectionGuardrail.cs  # Regex-based PII detection
│   │   │   └── SecretDetectionGuardrail.cs
│   │   └── Output/
│   │       ├── BlockedPatternsOutputGuardrail.cs
│   │       └── PiiOutputGuardrail.cs
│   │
│   ├── Yarp.AiGateway.Providers.OpenAI/
│   ├── Yarp.AiGateway.Providers.AzureOpenAI/
│   └── Yarp.AiGateway.Providers.Mistral/
│
├── samples/
│   └── Sample.MinimalApi/                # Working sample application
│       ├── Program.cs
│       └── aigateway.json
│
└── tests/
    ├── Yarp.AiGateway.Core.Tests/
    └── Yarp.AiGateway.Guardrails.Tests/
```

---

## Quick Start

### 1. Add the NuGet reference

```xml
<ProjectReference Include="Yarp.AiGateway.Core" />
```

### 2. Create `aigateway.json`

```json
{
  "gateway": {
    "listenPath": "/ai",
    "timeoutSeconds": 60
  },
  "providers": {
    "openai": {
      "type": "openai",
      "endpoint": "https://api.openai.com",
      "apiKey": "env:OPENAI_API_KEY",
      "defaultModel": "gpt-4o"
    }
  },
  "routing": {
    "rules": [
      {
        "name": "default",
        "provider": "openai",
        "model": "gpt-4o",
        "conditions": []
      }
    ]
  },
  "guardrails": {
    "input": [
      { "type": "prompt-injection", "mode": "block", "parameters": {} },
      { "type": "pii", "mode": "sanitize", "parameters": {} },
      { "type": "semantic", "mode": "block", "parameters": {} }
    ],
    "output": [
      { "type": "pii", "mode": "sanitize", "parameters": {} }
    ]
  }
}
```

### 3. Wire it up in `Program.cs`

```csharp
using Yarp.AiGateway.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAiGatewayFromJson("aigateway.json");

var app = builder.Build();

app.UseAiGateway();

app.Run();
```

### 4. Send a request

```bash
curl -X POST http://localhost:5000/ai/chat \
  -H "Content-Type: application/json" \
  -d '{
    "prompt": "Explain quantum computing",
    "userId": "user-123",
    "tenantId": "acme",
    "maxTokens": 500
  }'
```

### Response

```json
{
  "provider": "openai",
  "model": "gpt-4o",
  "content": "Quantum computing is...",
  "promptTokens": 12,
  "completionTokens": 150,
  "estimatedCost": 0.0024,
  "blocked": false,
  "correlationId": "d4f5a6b7-..."
}
```

---

## Configuration Reference

### Gateway

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `endpoint` | string | `/ai/chat` | HTTP path the middleware intercepts |
| `timeoutSeconds` | int | `60` | Request timeout |
| `includePromptInAudit` | bool | `false` | Store prompts in audit events |
| `defaultOperation` | string | `chat` | Default operation type |

### Providers

```json
{
  "providers": {
    "azure-openai": {
      "type": "azure-openai",
      "endpoint": "env:AZURE_OPENAI_ENDPOINT",
      "apiKey": "env:AZURE_OPENAI_API_KEY",
      "apiVersion": "2025-01-01-preview",
      "deploymentMap": {
        "gpt-4o": "my-gpt4o-deployment",
        "gpt-4o-mini": "my-gpt4omini-deployment"
      },
      "enabled": true,
      "priority": 1
    }
  }
}
```

> **Environment variables**: Use `"env:VAR_NAME"` syntax to keep secrets out of config files.

### Routing

```mermaid
graph TD
    REQ[Incoming Request] --> CHK{Client requested<br/>specific provider?}
    CHK -- Yes --> USE[Use requested provider/model]
    CHK -- No --> RULES{Evaluate routing rules<br/>in order}
    RULES -- Match --> ROUTE[Use matched provider/model]
    RULES -- No match --> DEF[Use default provider/model]
```

#### Conditions

Rules use typed conditions with the following operators:

| Operator | Description | Example |
|----------|-------------|---------|
| `eq` | Equals (case-insensitive) | `"tenantId" eq "acme"` |
| `neq` | Not equals | `"userId" neq "bot"` |
| `gt` | Greater than | `"prompt.length" gt 8000` |
| `gte` | Greater than or equal | `"maxTokens" gte 1000` |
| `lt` | Less than | `"temperature" lt 0.5` |
| `lte` | Less than or equal | `"prompt.length" lte 32000` |
| `contains` | Substring match | `"tenantId" contains "corp"` |
| `startswith` | Prefix match | `"userId" startswith "admin"` |
| `exists` | Field is not null | `"metadata.priority" exists` |

#### Available Fields

`prompt.length`, `tenantId`, `userId`, `applicationId`, `requestedProvider`, `requestedModel`, `operation`, `maxTokens`, `temperature`, `metadata.*`

#### Fallbacks

```json
{
  "routing": {
    "fallbacks": [
      { "from": "openai", "to": "azure-openai" },
      { "from": "azure-openai", "to": "mistral" }
    ]
  }
}
```

```mermaid
graph LR
    OAI[OpenAI] -- fails --> AZ[Azure OpenAI]
    AZ -- fails --> MI[Mistral]
    MI -- fails --> ERR[Error Response]
```

### Guardrails

Guardrails are evaluated **in order** and support three modes:

| Mode | Behavior |
|------|----------|
| `block` | Reject the request immediately (HTTP 422) |
| `sanitize` | Redact detected content and continue processing |
| `flag` | Annotate metadata with findings and continue |

```json
{
  "guardrails": {
    "input": [
      {
        "type": "semantic",
        "mode": "block",
        "parameters": {
          "thresholds": {
            "violence": 0.7,
            "self-harm": 0.5,
            "hate": 0.5
          }
        }
      },
      {
        "type": "pii",
        "mode": "sanitize",
        "parameters": {
          "customPatterns": ["\\bID-\\d{6}\\b"]
        }
      }
    ]
  }
}
```

### Quotas

```json
{
  "quotas": {
    "enabled": true,
    "requestsPerMinute": 60,
    "tokensPerMinute": 100000,
    "scope": "user"
  }
}
```

Scopes: `user`, `tenant`, `application`, `global`

### Telemetry

```json
{
  "telemetry": {
    "enabled": true,
    "auditRequests": true,
    "logLevel": "Information"
  }
}
```

---

## Guardrails Deep Dive

### Input Guardrails

Executed in order before the request reaches the LLM provider.

#### Prompt Injection Detection (Order 150)

Always enabled. Detects 30+ known injection patterns including:

- **Text patterns**: "ignore previous instructions", "reveal system prompt", "DAN mode", "jailbreak", etc.
- **Structural attacks**: ChatML tokens (`<|im_start|>`), Llama markers (`[INST]`, `<<SYS>>`), null bytes
- **Role override clustering**: Detects multiple role-change attempts in a single prompt

```json
{ "type": "prompt-injection", "mode": "block", "parameters": {} }
```

#### Semantic Content Analysis (Order 250)

Weighted keyword scoring across 7 risk categories with configurable thresholds:

```mermaid
graph LR
    subgraph Categories
        V[Violence]
        SH[Self-Harm]
        SX[Sexual]
        H[Hate Speech]
        IL[Illegal Activity]
        DE[Data Exfiltration]
        MA[Manipulation]
    end

    subgraph Scoring
        KW[Keyword Matching] --> WS[Weighted Score]
        WS --> CB[Clustering Bonus]
        CB --> TH{Score ≥ Threshold?}
    end

    TH -- Yes --> BF[Block or Flag]
    TH -- No --> PASS[Allow]
```

- **Clustering bonus**: 3+ keyword hits → 1.5x multiplier, 5+ hits → 2x
- **Per-category thresholds**: Override the default threshold for specific categories
- **Two modes**: `block` (reject) or `flag` (annotate `semantic_flags` in metadata)

```json
{
  "type": "semantic",
  "mode": "block",
  "parameters": {
    "thresholds": {
      "violence": 0.7,
      "self-harm": 0.5,
      "sexual": 0.6,
      "hate": 0.5,
      "illegal": 0.6,
      "data-exfiltration": 0.5,
      "manipulation": 0.6
    }
  }
}
```

#### PII Detection & Redaction (Order 300)

Detects and optionally redacts personally identifiable information using high-performance `GeneratedRegex` patterns:

| PII Type | Enabled by Default | Redaction |
|----------|-------------------|-----------|
| Email addresses | ✅ | `[REDACTED_EMAIL]` |
| Phone numbers | ✅ | `[REDACTED_PHONE]` |
| Credit card numbers | ✅ | `[REDACTED_CARD]` |
| SSN (US) | ✅ | `[REDACTED_SSN]` |
| IBAN | ✅ | `[REDACTED_IBAN]` |
| Spanish DNI/NIE | ✅ | `[REDACTED_DNI]` |
| IP addresses | ❌ | `[REDACTED_IP]` |
| Passport numbers | ❌ | `[REDACTED_PASSPORT]` |
| Dates of birth | ❌ | `[REDACTED_DOB]` |
| Custom patterns | ❌ | Configurable |

```json
{
  "type": "pii",
  "mode": "sanitize",
  "parameters": {
    "detectIpAddresses": true,
    "customPatterns": ["\\bPAT-\\d{8}\\b"]
  }
}
```

#### Secret Detection (Order 350)

Blocks requests containing leaked credentials:

- Bearer tokens
- Connection strings
- AWS access keys
- Azure storage keys
- GitHub tokens (`ghp_`, `gho_`, `ghs_`)
- Private keys (PEM format)
- JSON Web Tokens (JWT)

```json
{ "type": "secret-detection", "mode": "block", "parameters": {} }
```

#### Max Length (Order 100)

```json
{ "type": "max-length", "mode": "block", "parameters": { "maxLength": 32000 } }
```

#### Blocked Patterns (Order 200)

```json
{
  "type": "blocked-patterns",
  "mode": "block",
  "parameters": {
    "patterns": ["DROP TABLE", "xp_cmdshell", "<script>"]
  }
}
```

### Output Guardrails

Applied to LLM responses before returning to the client.

| Guardrail | Order | Description |
|-----------|-------|-------------|
| **Blocked Patterns** | 100 | Prevents leaking internal errors, stack traces, or system prompt fragments |
| **PII Redaction** | 200 | Redacts emails, credit cards, SSN, and DNI from LLM responses |

---

## Supported Providers

```mermaid
graph TB
    GW[AI Gateway] --> OAI[OpenAI<br/>/v1/chat/completions<br/>Bearer auth]
    GW --> AZ[Azure OpenAI<br/>/openai/deployments/.../chat/completions<br/>api-key header<br/>Deployment mapping]
    GW --> MI[Mistral<br/>/v1/chat/completions<br/>Bearer auth]
```

| Provider | Config Type | Auth | Features |
|----------|------------|------|----------|
| **OpenAI** | `openai` | Bearer token | Standard chat completions |
| **Azure OpenAI** | `azure-openai` | `api-key` header | Deployment mapping, API versioning |
| **Mistral** | `mistral` | Bearer token | OpenAI-compatible API |

All providers implement the `IAiProvider` interface and normalize responses to a common `AiGatewayResponse` format including token counts and estimated cost.

---

## Extensibility

### Custom Guardrail

```csharp
public class ToxicityGuardrail : IInputGuardrail
{
    public int Order => 275;
    public string Name => "toxicity";

    public async Task<InputGuardrailDecision> EvaluateAsync(
        AiGatewayRequest request, CancellationToken ct)
    {
        // Call your ML model or external API
        var score = await _toxicityService.ScoreAsync(request.Prompt, ct);

        return score > 0.8
            ? new InputGuardrailDecision(false, "Toxic content detected")
            : new InputGuardrailDecision(true, UpdatedRequest: request);
    }
}

// Register in DI
builder.Services.AddSingleton<IInputGuardrail, ToxicityGuardrail>();
```

### Custom Provider

```csharp
public class AnthropicProvider : IAiProvider
{
    public string Name => "anthropic";

    public async Task<AiGatewayResponse> ExecuteAsync(
        AiGatewayRequest request, RouteDecision route, CancellationToken ct)
    {
        // Implement Anthropic Messages API
    }
}

// Register with named HttpClient
builder.Services.AddHttpClient<AnthropicProvider>(c =>
    c.BaseAddress = new Uri("https://api.anthropic.com"));
builder.Services.AddSingleton<IAiProvider, AnthropicProvider>();
```

### Custom Audit Sink

```csharp
public class SqlAuditSink : IAuditSink
{
    public async Task WriteAsync(AuditEvent auditEvent, CancellationToken ct)
    {
        // Persist to SQL, Cosmos DB, Event Hubs, etc.
    }
}

builder.Services.AddSingleton<IAuditSink, SqlAuditSink>();
```

---

## Testing

The project includes 63+ unit tests covering guardrails, routing, and configuration:

```bash
dotnet test Yarp.AiGateway.slnx
```

### Test Coverage

| Area | Tests | Description |
|------|-------|-------------|
| **PII Detection** | 14 | All PII types, sanitize/block modes, custom patterns |
| **Semantic Analysis** | 8 | All 7 risk categories, flag/block modes, threshold overrides |
| **Prompt Injection** | 14 | 30+ patterns, structural attacks, role clustering |
| **Condition Evaluator** | 15 | All 9 operators, metadata fields, multi-condition logic |
| **Config Loader** | 4 | JSON parsing, env variable resolution, validation |

---

## Roadmap

- [ ] Streaming support (SSE / chunked responses)
- [ ] Redis-backed distributed quota manager
- [ ] OpenTelemetry metrics and traces
- [ ] Hot-reload configuration without restart
- [ ] Token budget tracking per tenant
- [ ] Anthropic and Google Gemini providers
- [ ] Admin dashboard for real-time monitoring
- [ ] ML-based semantic guardrail (replace keyword scoring)
- [ ] Response caching with semantic similarity
- [ ] NuGet package publishing

---

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- LLM provider API keys (OpenAI, Azure OpenAI, or Mistral)

## License

MIT
