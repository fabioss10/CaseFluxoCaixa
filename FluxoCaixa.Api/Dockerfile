# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /app

# Otimizações de telemetria e comportamento do SDK
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV MSBUILDDISABLENODEREUSE=1

# Copia apenas os arquivos de projeto (.csproj), Isso evita que alterações no código-fonte invalidem o cache do 'dotnet restore'
COPY ["FluxoCaixa.Domain/FluxoCaixa.Domain.csproj", "FluxoCaixa.Domain/"]
COPY ["FluxoCaixa.Application/FluxoCaixa.Application.csproj", "FluxoCaixa.Application/"]
COPY ["FluxoCaixa.Infrastructure/FluxoCaixa.Infrastructure.csproj", "FluxoCaixa.Infrastructure/"]
COPY ["FluxoCaixa.Api/FluxoCaixa.Api.csproj", "FluxoCaixa.Api/"]

# Restaura as dependências do NuGet utilizando cache compartilhado e trancado.
# Reduz o tempo de build em execuções subsequentes na mesma máquina/runner.
RUN --mount=type=cache,id=nuget-shared,target=/root/.nuget/packages,sharing=locked \
  dotnet restore "FluxoCaixa.Api/FluxoCaixa.Api.csproj" -v minimal


COPY . .

# Publica a aplicação em modo Release otimizando o tamanho e a performance dos binários.
# Reutiliza o cache do NuGet para garantir que nenhuma dependência externa seja baixada de novo.
RUN --mount=type=cache,id=nuget-shared,target=/root/.nuget/packages,sharing=locked \
   dotnet publish "FluxoCaixa.Api/FluxoCaixa.Api.csproj" -c Release -o /app/publish -v minimal


# Usa uma imagem enxuta e segura, contendo apenas o necessário para rodar o app.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# Segurança e redução de tamanho: Copia apenas os artefatos compilados finais.
COPY --from=build-env /app/publish .
ENTRYPOINT ["dotnet", "FluxoCaixa.Api.dll"]
