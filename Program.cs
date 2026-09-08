var builder = WebApplication.CreateBuilder(args);

// Adicionar suporte para Controllers e para o Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Registar o serviço do HttpClient (exigido para comunicar com a API externa)
builder.Services.AddHttpClient();

var app = builder.Build();

// Ativar o Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
