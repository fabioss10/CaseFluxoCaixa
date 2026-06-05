IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [Lancamentos] (
    [Id] uniqueidentifier NOT NULL,
    [Tipo] int NOT NULL,
    [Valor] decimal(18,2) NOT NULL,
    [DataCriacao] datetime2 NOT NULL,
    CONSTRAINT [PK_Lancamentos] PRIMARY KEY ([Id])
);

CREATE TABLE [OutboxEvents] (
    [Id] uniqueidentifier NOT NULL,
    [LancamentoId] uniqueidentifier NOT NULL,
    [EventType] nvarchar(max) NOT NULL,
    [Payload] nvarchar(max) NOT NULL,
    [Status] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [ProcessedAt] datetime2 NULL,
    CONSTRAINT [PK_OutboxEvents] PRIMARY KEY ([Id])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260603223859_caseFluxoCaixa', N'10.0.8');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [SaldosConsolidados] (
    [Data] date NOT NULL,
    [TotalCreditos] decimal(18,2) NOT NULL,
    [TotalDebitos] decimal(18,2) NOT NULL,
    [Saldo] decimal(18,2) NOT NULL,
    [UltimaAtualizacao] datetime2 NOT NULL,
    CONSTRAINT [PK_SaldosConsolidados] PRIMARY KEY ([Data])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260604174425_AddSaldoConsolidado', N'10.0.8');

COMMIT;
GO

