Below is a clean and concise copilot-instructions.md that can be included in the D2R2 repository to guide GitHub Copilot/Copilot Chat during the modernization and migration effort.

# D2R2 Modernisation & Migration - Copilot Instructions

## Objective

Modernise and migrate the D2R2 application from legacy Windows/IIS hosting to the Defra AWS platform while preserving existing business functionality and user experience.

The migration should prioritise:
- Security by design
- Cloud-native deployment patterns
- Removal of unsupported technologies
- Operational simplicity
- Infrastructure as Code (IaC)
- Maintainability and supportability

---

# Migration Principles

## Migration Strategy

- Rehost and modernise the existing application with minimal functional change.
- Containerise application components for deployment on AWS ECS Fargate.
- Use managed AWS services wherever possible.
- Preserve existing business workflows, disease prioritisation logic, review processes, and reporting outcomes.
- Avoid introducing new business capabilities during migration.

## Architecture Principles

- All workloads must be deployable as containers.
- Follow Defra CCoE AWS reference architecture patterns.
- Use Hub and Spoke VPC architecture.
- Treat infrastructure as code.
- Externalise all configuration.
- Store secrets outside source code.
- Encrypt all data in transit and at rest.
- Implement least-privilege access controls.

---

# Target Architecture

## Hosting

### Application

- Deploy D2R2 to AWS ECS Fargate.
- Run in private subnets within the Spoke VPC.
- Place application behind internal Application Load Balancers.
- Expose services through:
  - Route 53
  - AWS WAF
  - Internet-facing ALB in Hub VPC
  - Transit Gateway connectivity

### Database

- Migrate SQL Server databases to AWS RDS for SQL Server.
- Retain existing database schema and stored procedures where possible.
- Migrate all existing stored procedures.
- Enforce TLS for all database connections.

---

# Technology Standards

## Runtime

Target platform:

- .NET 10 (Long-Term Support version when available)
- ASP.NET application architecture
- Containerised deployment model

Avoid:

- Unsupported .NET Framework versions
- IIS-specific dependencies
- Windows-only hosting requirements

---

## UI Layer

Retain where practical:

- Existing D2R2 functionality
- Telerik UI components

Implement:

- HTML-first rendering patterns
- Razor views for report generation

---

## Service Layer

Replace legacy service technologies:

| Legacy | Target |
|----------|----------|
| ASMX | REST APIs |
| WCF/SOAP | REST APIs |

Requirements:

- Stateless services
- RESTful API patterns
- JSON payloads
- Internal communication via private networking

---

# Authentication & Identity

## Internal Users

Authentication source:

- Microsoft Entra ID

Requirements:

- SAML 2.0 authentication
- Validate assertions
- Extract identity and role claims
- Implement RBAC using claims

## External Users

Authentication source:

- Gov.UK One Login via Defra Customer Identity

Requirements:

- OIDC / OAuth2
- JWT validation
- Validate:
  - signature
  - issuer
  - audience
  - expiry
  - nonce

---

# Authorisation

Apply:

- Role-based access control (RBAC)
- Claims-based authorisation
- Least-privilege principles

AWS services must use:

- IAM Roles
- Task Roles
- No embedded credentials

---

# Configuration Management

## Mandatory Rules

Do not store:

- Passwords
- Secrets
- Tokens
- Connection strings
- Certificates

inside source code or configuration files.

Store:

### AWS Parameter Store

- Non-sensitive configuration

### AWS Secrets Manager

- Database credentials
- API secrets
- Authentication secrets
- Certificates

Support environments:

- DEV
- TEST
- PRE-PROD
- PROD

---

# PDF Generation

## Deprecated Technology

Remove:

- TallPDF

## Replacement Approach

Generate reports using:

1. Razor Views
2. HTML Rendering
3. Playwright PDF generation

Requirements:

- Support all existing reports
- Produce equivalent output
- Store generated PDFs in Amazon S3

---

# Email Notifications

Replace legacy SMTP implementations.

Standard:

- Microsoft Graph API
- Microsoft 365 mail services

Requirements:

- Application-triggered email notifications
- Profile review notifications
- User alert notifications

---

# Data Access Standards

Requirements:

- Continue using existing SQL Server data model.
- Use encrypted connections.
- Retrieve credentials from Secrets Manager.
- Support connection pooling.
- Keep data access logic isolated from UI concerns.

---

# Business Logic

Retain:

- CSLA business layer
- Validation rules
- Disease prioritisation logic
- Review workflows
- Approval workflows

Avoid:

- Functional redesign
- Business process changes

---

# Security Requirements

Mandatory controls:

- TLS everywhere
- Encryption at rest
- Encryption in transit
- AWS WAF protection
- Centralised monitoring
- Centralised logging
- Security event auditing
- Vulnerability management
- Least-privilege access

Do not:

- Store secrets in code
- Hardcode credentials
- Disable certificate validation
- Bypass authentication controls

---

# Monitoring & Operations

Implement:

- CloudWatch logging
- CloudWatch metrics
- Health checks
- ECS service monitoring
- Application audit logging
- Alerting for operational failures

---

# CI/CD Standards

Replace manual deployment activities with:

- Automated build pipelines
- Automated deployment pipelines
- Infrastructure as Code
- Repeatable releases
- Environment promotion controls

Preferred tooling:

- Git-based workflows
- AWS-native deployment capabilities
- Automated validation and testing

---

# Out of Scope

Do NOT introduce solutions for:

## Business Changes

- Workflow redesign
- Process redesign
- Policy changes

## Functional Enhancements

- New user-facing features
- New reporting capabilities
- Changes to business rules

## New Integrations

- New third-party integrations
- New external system dependencies

Exceptions:

- Microsoft Entra ID integration
- Gov.UK One Login integration
- Defra Customer Identity integration
- Microsoft Graph API integration

## Performance Engineering

Exclude:

- Large-scale optimisation
- Architectural re-engineering
- Functional redesign for performance

Only perform:

- Baseline tuning required for stable AWS operation

---

# Development Guidance for Copilot

When generating code:

1. Prefer cloud-native patterns.
2. Prefer dependency injection.
3. Use async APIs where appropriate.
4. Follow SOLID principles.
5. Design for container execution.
6. Avoid machine-specific assumptions.
7. Avoid filesystem dependencies unless required.
8. Externalise configuration.
9. Use managed services over self-hosted alternatives.
10. Ensure code is production-ready and secure by default.

Always favour secure, maintainable, cloud-native solutions aligned with the target AWS architecture and Defra platform standards.


This version is structured specifically for GitHub Copilot/Copilot Chat guidance, focusing on architectural guardrails, modernization decisions, remediation requirements, security controls, and explicit out-of-scope boundaries.