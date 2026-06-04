using FluxoCaixa.Application.Interfaces;
using FluxoCaixa.Application.Services;
using FluxoCaixa.Domain.Interfaces;
using FluxoCaixa.Infrastructure.Persistence;
using FluxoCaixa.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;



var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddEndpointsApiExplorer();

//swegger
builder.Services.AddSwaggerGen();


builder.Services.AddDbContext<FluxoCaixaDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));


///<summary>
///registrando injeção de dependência para os repositórios e serviços da aplicação, permitindo que eles sejam facilmente utilizados em outras partes do código, como nos controladores, 
///sem a necessidade de criar instâncias manualmente. Isso promove um design mais limpo e facilita a manutenção do código.
/// </summary>
builder.Services.AddScoped<ILancamentoRepository, LancamentoRepository>();
builder.Services.AddScoped<IOutboxEventRepository, OutboxEventRepository>();
builder.Services.AddScoped<ICriarLancamentoService, CriarLancamentoService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    //swegger
    app.UseSwagger();
    app.UseSwaggerUI();
}



app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
