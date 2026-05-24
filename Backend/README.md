# AutoPost Backend Platform

## Project Overview & Value Proposition

The **AutoPost Backend** is a production-grade, multi-platform social media publishing and automation platform built on .NET 9 with Clean Architecture principles. This system enables teams and content creators to:

- **Publish content simultaneously** across multiple social platforms (YouTube, Instagram, Facebook, TikTok, Twitter, Telegram)
- **Automate engagement** through intelligent DM automation rules triggered by comments, follows, and mentions
- **Manage unified inbox** consolidating messages from all platforms into a single interface
- **Schedule posts** with timezone-aware publishing and automatic retry logic
- **Track analytics** with time-series snapshots of post performance metrics
- **Enforce multi-tenancy** through workspace-based isolation with role-based access control (RBAC)

The platform solves the core business problem of **social media fragmentation** by providing a centralized hub for content distribution, audience engagement, and performance analytics across disparate platforms. It implements enterprise-grade security with JWT authentication, refresh token rotation, OAuth 2.0 integrations, and comprehensive rate limiting.

### Core Business Domain

The system is organized around seven primary domain blocks:

1. **Users & Access** - Workspace-based multi-tenancy with RBAC (Owner, Admin, Editor, Viewer roles)
2. **Social Accounts** - OAuth-connected platform accounts with encrypted token storage
3. **Video & Content** - Media upload, processing, and metadata extraction
4. **Publishing** - Post scheduling, multi-platform targeting, and publishing orchestration
5. **Unified Inbox** - Cross-platform message aggregation with conversation management
6. **DM Automation** - Rule-based automation engine for engagement (trigger → condition → action)
7. **Infrastructure** - Webhook buffering, notification preferences, and audit trails

---

## Technology Stack Matrix

| Technology | What It Is | Why/For What It Is Used | Where Implemented |
|-----------|------------|-------------------------|------------------|
| **.NET 9** | Latest .NET framework with performance improvements and C# 12 features | Core runtime for high-performance, cross-platform backend execution | All projects (TargetFramework: net9.0) |
| **ASP.NET Core** | Web framework for building HTTP APIs and real-time services | REST API endpoints, SignalR hubs, middleware pipeline | WebApi layer |
| **Entity Framework Core 9.0** | Modern ORM for .NET with LINQ-based database access | Database operations, migrations, change tracking, query optimization | Infrastructure/Data (ApplicationDbContext) |
| **PostgreSQL + Npgsql** | Advanced open-source relational database | Primary data store for all entities, relationships, and persistence | ConnectionStrings:DefaultConnection |
| **MediatR 14.1** | Mediator pattern implementation for CQRS | Decouples controllers from business logic, implements in-process messaging | Application/CQRS (Commands, Queries, Handlers) |
| **AutoMapper 16.1** | Object-to-object mapping library | Maps domain entities to DTOs and vice versa | Application/Mapper (Profiles) |
| **FluentValidation 12.1** | Validation library with rule-based validators | Request validation before CQRS handlers execute | Application/CQRS/*Validators.cs |
| **ASP.NET Core Identity** | User management and authentication framework | User accounts, password hashing, role management, claims | Infrastructure/Security (Identity setup) |
| **JWT Bearer Authentication** | Token-based authentication using JSON Web Tokens | Stateless API authentication with access/refresh token pattern | WebApi/DependencyInjection (AddJwtBearer) |
| **Redis (StackExchange.Redis)** | In-memory data structure store | Distributed caching, rate limiting, SignalR backplane for scale-out | Infrastructure/Caching (RedisCacheService) |
| **Hangfire 1.8** | Background job processing framework with dashboard | Scheduled post publishing, email notifications, recurring tasks | Infrastructure/BackgroundJobs |
| **Azure Blob Storage** | Cloud object storage service | Video file storage, thumbnail management, CDN integration | Infrastructure/Storage (AzureBlobStorageService) |
| **SignalR** | Real-time web functionality library | Live notifications, inbox updates, dashboard metrics | WebApi/Hubs (NotificationHub) |
| **Polly** | Resilience and transient fault-handling library | HTTP retry policies, circuit breakers for external API calls | Infrastructure/DependencyInjection (GetRetryPolicy, GetCircuitBreakerPolicy) |
| **xUnit 2.9** | Unit testing framework for .NET | Test suite for handlers, repositories, and business logic | Tests layer |
| **Moq 4.20** | Mocking library for unit testing | Mock dependencies in isolated unit tests | Tests layer |
| **FluentAssertions 6.12** | Assertion library for readable test code | Expressive test assertions with natural language syntax | Tests layer |
| **Swagger/OpenAPI** | API documentation specification | Interactive API documentation and testing UI | WebApi/DependencyInjection (AddSwaggerGen) |
| **Data Protection API** | Cryptographic data protection services | Encryption of OAuth tokens, refresh token hashing | Infrastructure/Security (TokenProtectionService) |
| **Health Checks** | Health monitoring middleware | Database connectivity, Redis availability monitoring | Infrastructure/HealthChecks |

---

## Architectural Onboarding Guide (Clean Architecture)

### Layer Overview

The AutoPost Backend follows **Clean Architecture** principles with strict dependency rules:

```
┌─────────────────────────────────────────────────────────────┐
│                         WebApi Layer                         │
│  (Controllers, Middleware, SignalR Hubs, Security)           │
└──────────────────────────┬──────────────────────────────────┘
                           │ depends on
┌──────────────────────────▼──────────────────────────────────┐
│                      Application Layer                       │
│  (CQRS Commands/Queries, Handlers, DTOs, Validators,        │
│   AutoMapper Profiles, Pipeline Behaviors)                  │
└──────────────────────────┬──────────────────────────────────┘
                           │ depends on
┌──────────────────────────▼──────────────────────────────────┐
│                        Domain Layer                          │
│  (Entities, Value Objects, Enums, Domain Services)         │
└──────────────────────────┬──────────────────────────────────┘
                           │ depends on
┌──────────────────────────▼──────────────────────────────────┐
│                     Infrastructure Layer                      │
│  (EF Core DbContext, Repositories, External Integrations,    │
│   Background Jobs, Caching, Storage, Security)               │
└─────────────────────────────────────────────────────────────┘
```

### Dependency Rule

- **Domain Layer** - Zero dependencies on other layers. Contains pure business logic.
- **Application Layer** - Depends only on Domain. Contains use cases and orchestration.
- **Infrastructure Layer** - Depends on Domain and Application. Contains technical implementations.
- **WebApi Layer** - Depends on Application and Infrastructure. Contains HTTP presentation.

### Request Flow: Controller → Database → Response

#### Step 1: HTTP Request Arrives

```
Client → CORS → Rate Limiting → Authentication → Authorization → Controller
```

**Example:** `POST /api/auth/login`

1. **CORS Middleware** (`WebApi/DependencyInjection.UseWebApiPipeline`)
   - Validates origin against allowed origins policy
   - Sets appropriate CORS headers

2. **AuthRateLimitMiddleware** (`WebApi/Middleware/AuthRateLimitMiddleware`)
   - Checks Redis for IP-based rate limit (10 requests per 60 seconds for auth endpoints)
   - Returns 429 if limit exceeded

3. **JWT Authentication** (`Microsoft.AspNetCore.Authentication.JwtBearer`)
   - Validates Authorization header Bearer token
   - Extracts claims (UserId, WorkspaceId, WorkspaceRole)

4. **ClaimsValidationMiddleware** (`WebApi/Middleware/ClaimsValidationMiddleware`)
   - Validates required claims are present
   - Enriches HttpContext with custom claims

5. **Authorization** (`Microsoft.AspNetCore.Authorization`)
   - Checks `[Authorize]` and role-based policies
   - Example: `[Authorize(Roles = "Owner,Admin")]`

6. **Controller** (`WebApi/Controllers/AuthController`)
   - Receives validated request
   - Maps to CQRS command/query

#### Step 2: Controller to Application Layer

```csharp
// WebApi/Controllers/AuthController.cs
[HttpPost("login")]
public async Task<ActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
{
    var command = new LoginCommand(request);
    var result = await Mediator.Send(command, ct);  // MediatR dispatch
    
    if (result.IsSuccess && result.Value is not null)
    {
        SetRefreshTokenCookie(result.Value.RefreshToken);
    }
    
    return HandleResult(result);  // Converts Result<T> to ActionResult
}
```

**MediatR Pipeline Behaviors** (executed in order):

1. **LoggingBehavior** (`Application/Behaviors/LoggingBehavior<T,T>`)
   - Logs request name, start time
   - Logs completion time and duration
   - Logs any exceptions

2. **ValidationBehavior** (`Application/Behaviors/ValidationBehavior<T,T>`)
   - Executes FluentValidation validators
   - Returns ValidationException if validation fails
   - Handler never executes if validation fails

#### Step 3: Application Layer Handler

```csharp
// Application/CQRS/Auth/AuthHandlers.cs
public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<AuthTokensDto>>
{
    private readonly IApplicationUserRepository _userRepo;
    private readonly IJwtTokenGenerator _jwtGenerator;
    private readonly IRefreshTokenHasher _refreshHasher;
    
    public async Task<Result<AuthTokensDto>> Handle(LoginCommand request, CancellationToken ct)
    {
        // 1. Retrieve user
        var user = await _userRepo.GetByEmailAsync(request.Email, ct);
        if (user is null) return Result<AuthTokensDto>.Fail("Invalid credentials", ErrorCode.Unauthorized);
        
        // 2. Verify password
        var passwordValid = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
        if (!passwordValid.Succeeded) return Result<AuthTokensDto>.Fail("Invalid credentials", ErrorCode.Unauthorized);
        
        // 3. Generate tokens
        var accessToken = _jwtGenerator.GenerateToken(user);
        var refreshToken = _refreshHasher.GenerateToken();
        
        // 4. Store refresh token hash
        var tokenHash = _refreshHasher.HashToken(refreshToken);
        user.AddRefreshToken(tokenHash, request.DeviceInfo, request.IpAddress);
        
        // 5. Save changes
        await _unitOfWork.SaveChangesAsync(ct);
        
        // 6. Return result
        return Result<AuthTokensDto>.Ok(new AuthTokensDto(accessToken, refreshToken));
    }
}
```

#### Step 4: Infrastructure Layer (Repository & Database)

```csharp
// Infrastructure/Repositories/ApplicationUserRepository.cs
public class ApplicationUserRepository : IApplicationUserRepository
{
    private readonly ApplicationDbContext _context;
    
    public async Task<ApplicationUser?> GetByEmailAsync(string email, CancellationToken ct)
    {
        return await _context.ApplicationUsers
            .Include(u => u.WorkspaceMemberships)
                .ThenInclude(wm => wm.Workspace)
            .FirstOrDefaultAsync(u => u.Email == email, ct);
    }
}
```

**EF Core Query Execution:**

```sql
-- Generated SQL (simplified)
SELECT u.*, wm.*, w.*
FROM "ApplicationUsers" u
LEFT JOIN "WorkspaceMembers" wm ON u."Id" = wm."UserId"
LEFT JOIN "Workspaces" w ON wm."WorkspaceId" = w."Id"
WHERE u."Email" = @p0
```

#### Step 5: Response Flow

```
Handler → Result<T> → Controller → ActionResult → HTTP Response
```

**Result<T> Pattern** (`Application/Common/Result.cs`):

```csharp
public class Result<T>
{
    public bool IsSuccess { get; private set; }
    public T? Value { get; private set; }
    public string? Error { get; private set; }
    public ErrorCode? Code { get; private set; }  // NotFound, Forbidden, Conflict, etc.
}
```

**Controller Base Handler** (`WebApi/Controllers/ApiControllerBase.cs`):

```csharp
protected ActionResult HandleResult<T>(Result<T> result)
{
    if (!result.IsSuccess)
    {
        var statusCode = result.Code switch
        {
            ErrorCode.NotFound => StatusCodes.Status404NotFound,
            ErrorCode.Forbidden => StatusCodes.Status403Forbidden,
            ErrorCode.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorCode.Validation => StatusCodes.Status400BadRequest,
            ErrorCode.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };
        
        return StatusCode(statusCode, new ProblemDetails
        {
            Title = "Request failed",
            Detail = result.Error,
            Status = statusCode
        });
    }
    
    return Ok(result.Value);
}
```

---

## Core Enterprise Classes & Components

### Domain Entities (25+ Aggregate Roots)

#### Block I: Users & Access

- **Workspace** - Multi-tenant isolation unit with subscription plans (Free, Pro, Business, Enterprise)
- **ApplicationUser** - User account with email, password hash, timezone, locale
- **WorkspaceMember** - Junction entity with RBAC roles (Owner, Admin, Editor, Viewer)
- **RefreshToken** - SHA-256 hashed session tokens with rotation support

#### Block II: Social Accounts

- **SocialAccount** - OAuth-connected platform accounts with encrypted tokens
- **SocialAccountInsight** - Time-series analytics snapshots (followers, engagement metrics)

#### Block III: Video & Content

- **Video** - Uploaded media files with soft delete and processing status
- **VideoMetadata** - Technical metadata (duration, resolution, codec, aspect ratio)

#### Block IV: Publishing

- **Post** - Central aggregate root for content publication
- **PostContent** - Value object with title, description, tags, visibility
- **Schedule** - Value object with UTC scheduled time and timezone
- **PostTarget** - Per-platform publication target with independent status
- **PostTargetResult** - Publication outcome with external post ID and URL
- **PublishingJob** - Audit trail of publication attempts with raw API responses
- **PostAnalyticsSnapshot** - Performance metrics collected over time

#### Block V: Unified Inbox

- **InboxConversation** - Aggregated conversation (DM, comment, mention reply)
- **InboxMessage** - Individual messages with direction (Inbound/Outbound)
- **ConversationAssignment** - Team member assignment for inbox management

#### Block VI: DM Automation

- **AutomationRule** - Rule definition with trigger, conditions, and actions
- **TriggerCondition** - Rule conditions (AND logic: all must match)
- **AutomationAction** - Actions executed on rule trigger (SendDM, LikeComment, etc.)
- **PendingDMQueue** - Deferred DM queue for private accounts and rate limits
- **AutomationExecutionLog** - Audit trail of rule executions with idempotency

#### Block VII: Infrastructure

- **WebhookEvent** - Buffered webhook payloads for async processing
- **NotificationPreference** - User notification settings per event type

### Infrastructure Components

#### Security Middleware Pipeline

**Location:** `WebApi/DependencyInjection.UseWebApiPipeline()`

```csharp
// Execution order (critical for security):
1. UseExceptionHandler()           // Global exception handling
2. UseHttpsRedirection()          // Force HTTPS
3. Security Headers Injection    // OWASP compliance headers
4. UseCors()                     // Cross-origin policy
5. UseMiddleware<AuthRateLimitMiddleware>()  // Auth endpoint protection
6. UseAuthentication()            // JWT validation
7. UseMiddleware<ClaimsValidationMiddleware>() // Claims enrichment
8. UseRateLimiter()              // General rate limiting
9. UseAuthorization()             // Role-based authorization
10. UseHangfireDashboard()       // Background job monitoring
```

#### Background Job System (Hangfire)

**Location:** `Infrastructure/BackgroundJobs/`

- **HangfireBackgroundJobScheduler** - Schedules one-time jobs (post publishing)
- **InfrastructureRecurringJobRegistrar** - Registers recurring jobs (analytics collection, token cleanup)
- **InfrastructureHeartbeatJob** - System health monitoring
- **SendEmailJob** - Email notification delivery
- **SendPushNotificationJob** - Push notification delivery

**Queues:** critical, default, low (priority-based processing)

#### Repository Pattern Implementation

**Location:** `Infrastructure/Repositories/`

All repositories implement interfaces from `Application/Abstractions/Repositories/`:

- 20+ repository classes (ApplicationUserRepository, PostRepository, etc.)
- Unit of Work pattern (`EfUnitOfWork`)
- EF Core with PostgreSQL provider
- Global query filters for soft delete (Video.DeletedAt)

#### External Platform Integrations

**Location:** `Infrastructure/Integrations/`

- **PlatformOAuthClientFactory** - Factory for platform-specific OAuth clients
- **PlatformIntegrationService** - Unified interface for platform operations
- **PlatformTokenValidationService** - Token refresh and validation
- **PlatformPublisherFactory** - Factory for platform-specific publishers
- **PlatformMessagingService** - DM and comment operations

**Supported Platforms:** YouTube, Instagram, Facebook, TikTok, Twitter, Telegram

**Resilience:** Polly retry (3 attempts with exponential backoff) + Circuit Breaker (2 failures → 30s break)

#### Caching Layer

**Location:** `Infrastructure/Caching/`

- **RedisCacheService** - Distributed cache implementation
- **RedisRateLimitService** - Rate limiting with sliding window algorithm
- **IDistributedCache** abstraction for testability

#### Storage Layer

**Location:** `Infrastructure/Storage/`

- **AzureBlobStorageService** - Video upload, download, deletion
- **SAS token generation** for secure temporary access
- **Container separation:** videos, thumbnails

#### Notification System

**Location:** `Infrastructure/Notifications/`

- **HangfireEmailService** - Email via SMTP (configured in appsettings.json)
- **HangfirePushNotificationService** - Push notifications for mobile
- **SmtpEmailTransport** - SMTP client wrapper
- **PushNotificationTransport** - Push gateway abstraction

### Application Layer Components

#### CQRS Structure

**Location:** `Application/CQRS/`

Each domain module has 4 files:
- **ModuleCommands.cs** - Command definitions (IRequest<Result<T>>)
- **ModuleQueries.cs** - Query definitions (IRequest<Result<T>>)
- **ModuleHandlers.cs** - Command/Query handlers (IRequestHandler)
- **ModuleValidators.cs** - FluentValidation validators (AbstractValidator<T>)

**Modules:** Auth, Workspace, SocialAccounts, Videos, Posts, Inbox, Automation, Analytics, Notifications, Webhooks

#### Pipeline Behaviors

**Location:** `Application/Behaviors/`

- **LoggingBehavior<T,T>** - Request timing and error logging
- **ValidationBehavior<T,T>** - FluentValidation execution

#### DTOs & Mapping

**Location:** `Application/DTOs/` and `Application/Mapper/`

- Request/Response DTOs for each module
- AutoMapper Profiles for entity ↔ DTO mapping
- Profile classes: AuthMappingProfile, PostMappingProfile, etc.

### WebApi Layer Components

#### Controllers (11 Controllers)

**Location:** `WebApi/Controllers/`

- **AuthController** - Register, login, refresh, logout, profile, sessions
- **WorkspaceController** - Workspace CRUD, member management
- **SocialAccountController** - OAuth connect, disconnect, list
- **VideoController** - Upload, processing status, metadata
- **PostController** - Create, schedule, edit, cancel, list
- **InboxController** - Conversations, messages, assignments
- **AutomationController** - Rules CRUD, execution logs
- **AnalyticsController** - Post metrics, account insights
- **NotificationController** - Preferences, history
- **WebhookController** - Platform webhook endpoints
- **ApiControllerBase** - Base class with Mediator and Result handling

#### SignalR Real-time

**Location:** `WebApi/Hubs/` and `WebApi/Realtime/`

- **NotificationHub** - Real-time notification delivery
- **SignalRRealtimeNotificationService** - Service abstraction
- **Redis backplane** for multi-server scale-out

#### Security Services

**Location:** `WebApi/Security/`

- **HttpCurrentUserContext** - Claims extraction from HttpContext
- **RefreshTokenCookieService** - HttpOnly cookie management
- **IRefreshTokenCookieService** - Abstraction for testing

#### Exception Handling

**Location:** `WebApi/ExceptionHandling/`

- **GlobalExceptionHandler** - Catches all exceptions, converts to ProblemDetails (RFC 7807)
- Includes trace ID and timestamp in error responses

### Seeders

**Location:** `WebApi/Seeds/`

- **DefaultRoles.cs** - System role definitions (Administrator, Manager, User)
- **RoleSeeder.cs** - Seeds Identity roles on application startup

**Execution:** Called in `Program.cs` after database migration

---

## Getting Started & Configuration

### Prerequisites

- **.NET 9.0 SDK** - [Download from Microsoft](https://dotnet.microsoft.com/download/dotnet/9.0)
- **PostgreSQL 15+** - Database server
- **Redis 7+** - Caching and rate limiting
- **Azure Storage Account** (or Azurite for local development)

### Database Setup

1. **Configure Connection String** in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=AutoPostDb;Username=postgres;Password=your_password"
  }
}
```

2. **Run Migrations** (automatic on startup):

```bash
cd Backend/WebApi
dotnet run
```

The application automatically applies pending migrations on startup (`Program.cs` line 36-38).

3. **Manual Migration Commands** (if needed):

```bash
cd Backend/Infrastructure
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### Redis Configuration

```json
{
  "Redis": {
    "ConnectionString": "localhost:6379,abortConnect=false",
    "InstanceName": "AutoPost:",
    "RateLimitKeyPrefix": "rate-limit"
  }
}
```

**Local Development:** Use Docker:

```bash
docker run -d -p 6379:6379 redis:7-alpine
```

### JWT Configuration

```json
{
  "Jwt": {
    "Secret": "YourSuperSecretKeyThatIsAtLeast32CharactersLong!",
    "Issuer": "https://localhost:5001",
    "Audience": "https://localhost:5001",
    "AccessTokenLifetimeMinutes": 15
  }
}
```

**Security Note:** Change the Secret in production to a cryptographically secure random string (minimum 32 characters).

### Refresh Token Configuration

```json
{
  "RefreshToken": {
    "CookieName": "autopost.refresh_token",
    "LifetimeDays": 90,
    "CookiePath": "/",
    "SameSite": "None",
    "SecurePolicy": "Always",
    "HttpOnly": true,
    "IsEssential": true
  }
}
```

### Hangfire Configuration

```json
{
  "Hangfire": {
    "ConnectionString": "Host=localhost;Database=AutoPostDb;Username=postgres;Password=postgres",
    "DashboardPath": "/hangfire",
    "WorkerCount": 4,
    "Queues": [ "critical", "default", "low" ]
  }
}
```

**Dashboard Access:** `https://localhost:5001/hangfire` (read-only in production)

### Azure Blob Storage Configuration

```json
{
  "AzureBlobStorage": {
    "ConnectionString": "UseDevelopmentStorage=true",
    "VideosContainerName": "videos",
    "ThumbnailsContainerName": "thumbnails"
  }
}
```

**Local Development:** Use Azurite (Azure Storage Emulator):

```bash
docker run -d -p 10000:10000 -p 10001:10001 -p 10002:10002 mcr.microsoft.com/azure-storage/azurite
```

**Production:** Replace with actual connection string from Azure Portal.

### SMTP Configuration (Email)

```json
{
  "Smtp": {
    "Host": "localhost",
    "Port": 25,
    "FromEmail": "noreply@autopost.local",
    "FromName": "AutoPost",
    "Username": "",
    "Password": "",
    "EnableSsl": false
  }
}
```

### Platform OAuth Configuration

Each platform requires OAuth credentials:

```json
{
  "PlatformOAuth": {
    "YouTube": {
      "Enabled": false,
      "ClientId": "youtube-client-id",
      "ClientSecret": "youtube-client-secret",
      "AuthorizationEndpoint": "https://accounts.google.com/o/oauth2/v2/auth",
      "TokenEndpoint": "https://oauth2.googleapis.com/token",
      "ApiBaseUrl": "https://www.googleapis.com/",
      "CallbackPath": "/api/social-accounts/callback/youtube",
      "Scopes": [ "https://www.googleapis.com/auth/youtube.upload" ]
    },
    // ... similar for Instagram, Facebook, TikTok, Twitter, Telegram
  }
}
```

**To Enable:** Set `Enabled: true` and provide valid ClientId/ClientSecret from platform developer portals.

### Webhook Configuration

```json
{
  "Webhooks": {
    "YouTube": {
      "Enabled": false,
      "SigningSecret": "youtube-placeholder-secret",
      "SignatureHeaderName": "X-Hub-Signature-256",
      "SignaturePrefix": "sha256="
    },
    // ... similar for other platforms
  }
}
```

**Security:** Configure SigningSecret from platform webhook settings to verify request authenticity.

### CORS Configuration

```json
{
  "Cors": {
    "AllowedOrigins": [ "http://localhost:3000", "http://localhost:5173" ]
  }
}
```

**Development:** Flexible policy allows all origins if empty.
**Production:** Strict matching against configured origins.

### Rate Limiting Configuration

```json
{
  "RateLimiting": {
    "GeneralPermitLimit": 60,
    "GeneralWindowSeconds": 60,
    "AuthPermitLimit": 10,
    "AuthWindowSeconds": 60,
    "UploadTokenLimit": 100,
    "UploadTokensPerPeriod": 20,
    "UploadReplenishmentSeconds": 10,
    "RejectionRetryAfterSeconds": 60
  }
}
```

**Strategies:**
- **Fixed Window** - General API throttling (60 req/min)
- **Token Bucket** - Upload bursting (100 tokens, replenishes 20/10s)

### Running the Application

```bash
cd Backend/WebApi
dotnet restore
dotnet build
dotnet run
```

**Default URLs:**
- API: `https://localhost:5001`
- Swagger: `https://localhost:5001/swagger`
- Hangfire Dashboard: `https://localhost:5001/hangfire`
- Health Check: `https://localhost:5001/health`

### Running Tests

```bash
cd Backend/Tests
dotnet restore
dotnet test
```

**Test Coverage:**
- Handler tests (`Tests/Handlers/`) - CQRS command/query logic
- Repository tests (`Tests/Repositories/`) - Data access layer
- Uses xUnit, Moq, FluentAssertions
- In-memory EF Core for isolated database testing

### Seeding Default Roles

Roles are seeded automatically on startup (`Program.cs` line 40-42):

```csharp
logger.LogInformation("Seeding default system security roles...");
await RoleSeeder.SeedRolesAsync(services);
logger.LogInformation("Security roles seeded successfully.");
```

**Roles Created:**
- **Administrator** - Full global access
- **Manager** - Multi-workspace oversight
- **User** - Workspace-scoped access

### Environment-Specific Configuration

**Development:** Use `appsettings.Development.json` for local overrides.

**Production:** Set environment variable:
```bash
export ASPNETCORE_ENVIRONMENT=Production
```

Or use `--environment` flag:
```bash
dotnet run --environment Production
```

### Docker Deployment (Optional)

```dockerfile
# Dockerfile example
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["WebApi/WebApi.csproj", "WebApi/"]
COPY ["Application/Application.csproj", "Application/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]
COPY ["Domain/Domain.csproj", "Domain/"]
RUN dotnet restore "WebApi/WebApi.csproj"
COPY . .
WORKDIR "/src/WebApi"
RUN dotnet build "WebApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WebApi.dll"]
```

**Build and Run:**
```bash
docker build -t autopost-backend .
docker run -p 5001:80 --env-file .env autopost-backend
```

---

## Architecture Diagrams

### Clean Architecture Dependency Graph

```
                    ┌─────────────────┐
                    │   WebApi Layer  │
                    │  (Presentation) │
                    └────────┬────────┘
                             │
                    ┌────────▼────────┐
                    │  Application    │
                    │  (Use Cases)   │
                    └────────┬────────┘
                             │
                    ┌────────▼────────┐
                    │    Domain       │
                    │  (Business)    │
                    └────────┬────────┘
                             │
                    ┌────────▼────────┐
                    │ Infrastructure │
                    │  (Technical)   │
                    └─────────────────┘
```

### Request Processing Pipeline

```
HTTP Request
    │
    ├─► CORS Middleware
    │
    ├─► Rate Limiting (Redis)
    │
    ├─► JWT Authentication
    │
    ├─► Claims Validation
    │
    ├─► Authorization (RBAC)
    │
    ├─► Controller Action
    │
    ├─► MediatR.Send(Command/Query)
    │
    ├─► LoggingBehavior (timing)
    │
    ├─► ValidationBehavior (FluentValidation)
    │
    ├─► Handler (Business Logic)
    │
    ├─► Repository (EF Core)
    │
    ├─► Database (PostgreSQL)
    │
    └─► Response (Result<T> → ActionResult)
```

### Background Job Processing

```
Hangfire Dashboard
    │
    ├─► Queue: critical (immediate)
    │   └─► Post Publishing
    │
    ├─► Queue: default (standard)
    │   ├─► Email Notifications
    │   ├─► Push Notifications
    │   └─► Webhook Processing
    │
    └─► Queue: low (batch)
        ├─► Analytics Collection
        ├─► Token Cleanup
        └─► Pending DM Queue Processing
```

---

## Key Design Patterns

### CQRS (Command Query Responsibility Segregation)

- **Commands** - Write operations that change state (RegisterUserCommand, CreatePostCommand)
- **Queries** - Read operations that return data (GetUserByIdQuery, ListPostsQuery)
- **Handlers** - Single responsibility classes implementing IRequestHandler
- **Separation** - Clear distinction between read and write models

### Repository Pattern

- **Interface** - Defined in Application layer (IApplicationUserRepository)
- **Implementation** - Concrete class in Infrastructure layer (ApplicationUserRepository)
- **Unit of Work** - EfUnitOfWork coordinates multiple repository changes
- **Testability** - Interfaces enable mocking in unit tests

### Specification Pattern (via LINQ)

- Repository methods accept specification expressions
- Example: `GetByWorkspaceAsync(Guid workspaceId, CancellationToken ct)`
- EF Core translates LINQ to SQL at runtime

### Factory Pattern

- **PlatformOAuthClientFactory** - Creates platform-specific OAuth clients
- **PlatformPublisherFactory** - Creates platform-specific publishers
- **WebhookPayloadParserFactory** - Creates platform-specific webhook parsers

### Strategy Pattern

- **IRateLimitService** - Multiple implementations (Redis, in-memory)
- **IEmailService** - Multiple transports (SMTP, SendGrid, AWS SES)
- **IPushNotificationService** - Multiple providers (FCM, APNS)

### Observer Pattern (SignalR)

- **NotificationHub** - Broadcasts real-time updates
- **Clients subscribe** to workspace-specific channels
- **Server pushes** notifications when events occur

---

## Security Considerations

### Authentication & Authorization

- **JWT Access Tokens** - 15-minute lifetime, stateless validation
- **Refresh Tokens** - 90-day lifetime, SHA-256 hashed in database, HttpOnly cookies
- **Token Rotation** - Refresh tokens invalidated after use, new token issued
- **Session Revocation** - "Logout all devices" invalidates all user tokens
- **RBAC** - Workspace-based roles (Owner, Admin, Editor, Viewer)
- **Claims-Based Authorization** - WorkspaceId and WorkspaceRole in JWT claims

### Data Protection

- **OAuth Token Encryption** - Encrypted via ASP.NET Core Data Protection API
- **Password Hashing** - ASP.NET Core Identity with PBKDF2
- **Sensitive Data** - Never logged or exposed in API responses
- **PII Protection** - Email addresses hashed in cache keys

### Rate Limiting

- **Auth Endpoints** - 10 requests per 60 seconds per IP
- **General API** - 60 requests per 60 seconds per user
- **Upload Endpoints** - Token bucket (100 tokens, replenishes 20/10s)
- **Redis-Backed** - Distributed rate limiting across multiple servers

### OWASP Compliance

- **Security Headers** - X-Content-Type-Options, X-Frame-Options, Referrer-Policy
- **HTTPS Enforcement** - HSTS in production
- **CORS** - Strict origin validation in production
- **SQL Injection** - Protected via EF Core parameterized queries
- **XSS Protection** - HttpOnly cookies, input validation, output encoding

### Webhook Security

- **Signature Verification** - HMAC-SHA256 validation against platform secret
- **Idempotency** - ExternalTriggerEventId prevents duplicate processing
- **Async Processing** - WebhookEvent buffer prevents timing attacks

---

## Performance Optimizations

### Database

- **Connection Pooling** - EF Core with Npgsql connection pooling
- **Query Optimization** - Include() for eager loading, projection for specific fields
- **Indexing** - Strategic indexes on foreign keys and frequently queried columns
- **Global Query Filters** - Automatic soft delete filtering (Video.DeletedAt)

### Caching

- **Redis Distributed Cache** - Session data, user profiles, rate limits
- **Cache-Aside Pattern** - Check cache first, fallback to database
- **TTL Policies** - Configurable expiration per cache key type

### Background Processing

- **Hangfire Queues** - Priority-based job processing (critical, default, low)
- **Retry Policies** - Automatic retry with exponential backoff
- **Circuit Breaker** - Prevents cascade failures on external API outages

### HTTP Client Resilience

- **Polly Retry** - 3 attempts with exponential backoff (2^retry seconds)
- **Circuit Breaker** - Opens after 2 failures, stays open for 30 seconds
- **Timeout** - 30-second timeout per external API request

---

## Monitoring & Observability

### Health Checks

- **PostgreSQL Health Check** - Database connectivity
- **Redis Health Check** - Cache connectivity
- **Endpoint** - `/health` returns aggregated health status

### Logging

- **Structured Logging** - Microsoft.Extensions.Logging
- **Request Timing** - LoggingBehavior captures duration for all CQRS operations
- **Error Logging** - GlobalExceptionHandler logs all unhandled exceptions
- **Audit Trail** - AutomationExecutionLog, PublishingJob for debugging

### Hangfire Dashboard

- **Job Monitoring** - View all background jobs, retries, failures
- **Execution Statistics** - Throughput, success/failure rates
- **Server Monitoring** - Worker status, queue lengths
- **Read-Only in Production** - Prevents accidental job modifications

---

## Troubleshooting

### Common Issues

**Migration Fails:**
```bash
# Check connection string in appsettings.json
# Ensure PostgreSQL is running
dotnet ef database update
```

**Redis Connection Refused:**
```bash
# Verify Redis is running
docker ps | grep redis
# Check connection string
# Ensure abortConnect=false in connection string
```

**Hangfire Jobs Not Executing:**
```bash
# Check Hangfire Dashboard at /hangfire
# Verify WorkerCount > 0 in appsettings.json
# Check database connection for Hangfire tables
```

**OAuth Callback Fails:**
```bash
# Verify PlatformOAuth.Enabled is true
# Check ClientId and ClientSecret are correct
# Ensure CallbackPath matches platform developer console
```

**Webhook Verification Fails:**
```bash
# Check Webhooks.*.SigningSecret matches platform
# Verify SignatureHeaderName is correct
# Check webhook payload format matches platform documentation
```

---

## Contributing Guidelines

### Code Style

- **C# Conventions** - Follow Microsoft C# coding conventions
- **XML Documentation** - Public methods must have XML comments
- **Naming** - PascalCase for public members, camelCase for private fields
- **Async/Await** - Use ConfigureAwait(false) in library code

### Testing Requirements

- **Unit Tests** - All handlers must have corresponding unit tests
- **Repository Tests** - Test with InMemoryDbContextFactory
- **Mock External Dependencies** - Use Moq for external services
- **Test Coverage** - Aim for >80% coverage on business logic

### Pull Request Process

1. Create feature branch from `main`
2. Implement changes with tests
3. Run full test suite: `dotnet test`
4. Update documentation if needed
5. Submit PR with clear description

---

## License

[Specify your license here - e.g., MIT, Apache 2.0, Proprietary]

---

## Contact & Support

- **Documentation** - This README.md
- **API Documentation** - Swagger UI at `/swagger`
- **Issue Tracking** - [Specify your issue tracker]
- **Email** - [Specify support email]

---

**Version:** 1.0.0  
**Last Updated:** 2025  
**.NET Version:** 9.0  
**Architecture:** Clean Architecture with CQRS
