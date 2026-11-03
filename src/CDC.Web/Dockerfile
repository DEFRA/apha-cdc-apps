# syntax=docker/dockerfile:1

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0-windowsservercore-ltsc2022 AS build
WORKDIR /src

COPY ["src/CDC.Web/CDC.Web.csproj", "src/CDC.Web/"]
COPY ["src/CDC.Common/CDC.Common.csproj", "src/CDC.Common/"]
COPY ["src/CDC.Auth.Cidm/CDC.Auth.Cidm.csproj", "src/CDC.Auth.Cidm/"]
COPY ["src/CDC.Auth.Entra/CDC.Auth.Entra.csproj", "src/CDC.Auth.Entra/"]

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "src/CDC.Web/CDC.Web.csproj"

COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish "src/CDC.Web/CDC.Web.csproj" -c Release -o /app/publish \
    --no-restore /p:UseAppHost=false

# ---- .NET runtime source ----
# Only its runtime files are used; the final stage has a different base image.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-windowsservercore-ltsc2022 AS dotnet-runtime

# ---- Runtime stage ----
# Chromium (used for the PDF reports) cannot run in a Windows Server Core image - it exits as soon as it opens a page - so
# this stage uses the full Windows Server image and copies in the ASP.NET Core runtime, which that image does not include.
FROM mcr.microsoft.com/windows/server:ltsc2022 AS final
WORKDIR /app
# Must match the ECS task definition's containerPort (and ALB target group
# port, if fronted by one) - coordinate any change here with infra.
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_EnableDiagnostics=0 \
    PLAYWRIGHT_BROWSERS_PATH=C:\\ms-playwright

COPY --from=dotnet-runtime ["/Program Files/dotnet", "/Program Files/dotnet"]
RUN setx /M PATH "%PATH%;C:\Program Files\dotnet"

# FortiGate egress performs TLS inspection on outbound calls to CIDM/Entra ID - without trusting its
# inspection CA, the OIDC handler's metadata/JWKS fetch fails with PartialChain.
COPY ["build/certs/fortigate-root-ca.cer", "C:/certs/fortigate-root-ca.cer"]
COPY ["build/certs/fortigate-ca.cer", "C:/certs/fortigate-ca.cer"]
RUN certutil -addstore -f Root C:/certs/fortigate-root-ca.cer && certutil -addstore -f CA C:/certs/fortigate-ca.cer

COPY --from=build /app/publish .

# Playwright's headless Chromium prints the Razor-rendered reports (for example the contributions
# report) to PDF. The browser is installed at image build time into PLAYWRIGHT_BROWSERS_PATH so the
# container never downloads it at runtime.
RUN powershell -NoProfile -ExecutionPolicy Bypass -File .\playwright.ps1 install chromium

# Fails the build if the browser cannot print a page; DEBUG logs the browser's exit code and errors to the build output.
RUN set DEBUG=pw:browser&& powershell -NoProfile -ExecutionPolicy Bypass -File .\playwright.ps1 pdf about:blank C:\smoke.pdf && del C:\smoke.pdf

USER $APP_UID
ENTRYPOINT ["dotnet", "CDC.Web.dll"]
