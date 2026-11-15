# ADR 0001 C Sharp and ASP NET Core

**Status:** Implemented, management approval required against the original Python wording.

**Decision:** Implement the middleware in C# on ASP.NET Core using the same canonical HTTP boundary required by the parent specification.

**Reason:** The team selected C#, and HTTP clients written in Odoo/Python remain language-independent. ASP.NET Core provides mature authentication, validation, dependency injection, OpenAPI and background services.

**Consequences:** Operations require a supported .NET runtime and C# capability. The governing requirements must formally record the language change before production approval.
