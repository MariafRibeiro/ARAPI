using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using MinhaApi.Models;
using System.Text.Json;

namespace MinhaApi.Controllers
{
    [ApiController]
    [Route("api/deputados")]
    public class DeputadosController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        // Opções para o JSON: a API externa usa "nomeParlamentar", as nossas
        // classes usam "NomeParlamentar" — isto faz o "match" entre os dois.
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public DeputadosController(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _config = config;
        }

        // ENDPOINT 1: Listar deputados (sem filtros) e gravar no MySQL
        // Exemplo: /api/deputados
        [HttpGet]
        public async Task<IActionResult> Listar()
        {
            var url = "https://api.openar.pt/v1/deputados";

            try
            {
                var resposta = await _httpClient.GetFromJsonAsync<PaginatedDeputados>(url, _jsonOptions);
                var deputados = resposta?.Data ?? new List<Deputado>();

                GravarDeputados(deputados);

                return Ok(deputados);
            }
            catch (Exception ex)
            {
                return BadRequest($"Erro: {ex.Message}");
            }
        }

        // ENDPOINT 2: Consultar deputados (com filtros opcionais) e gravar no MySQL
        // Exemplo: /api/deputados/consultar?legislatura=XVII&grupo=PS&limit=10
        [HttpGet("consultar")]
        public async Task<IActionResult> Consultar(
            [FromQuery] string? legislatura,
            [FromQuery] string? grupo,
            [FromQuery] string? q,
            [FromQuery] string? situacao,
            [FromQuery] string? sort,
            [FromQuery] int? page,
            [FromQuery] int? limit)
        {
            // Só adicionamos à query os parâmetros que o utilizador realmente enviou
            var partes = new List<string>();

            if (!string.IsNullOrWhiteSpace(legislatura)) partes.Add($"legislatura={legislatura}");
            if (!string.IsNullOrWhiteSpace(grupo)) partes.Add($"grupo={grupo}");
            if (!string.IsNullOrWhiteSpace(q)) partes.Add($"q={q}");
            if (!string.IsNullOrWhiteSpace(situacao)) partes.Add($"situacao={situacao}");
            if (!string.IsNullOrWhiteSpace(sort)) partes.Add($"sort={sort}");
            if (page.HasValue) partes.Add($"page={page}");
            if (limit.HasValue) partes.Add($"limit={limit}");

            var queryString = partes.Count > 0 ? "?" + string.Join("&", partes) : "";
            var url = $"https://api.openar.pt/v1/deputados{queryString}";

            try
            {
                var resposta = await _httpClient.GetFromJsonAsync<PaginatedDeputados>(url, _jsonOptions);
                var deputados = resposta?.Data ?? new List<Deputado>();

                GravarDeputados(deputados);

                return Ok(deputados);
            }
            catch (Exception ex)
            {
                return BadRequest($"Erro: {ex.Message}");
            }
        }

        // ENDPOINT 3: Detalhe de um deputado específico (só consulta a API externa, não grava)
        // Exemplo: /api/deputados/12345
        [HttpGet("{id}")]
        public async Task<IActionResult> GetDeputado(string id)
        {
            var url = $"https://api.openar.pt/v1/deputados/{id}";

            try
            {
                var dados = await _httpClient.GetFromJsonAsync<object>(url);
                return Ok(dados);
            }
            catch (Exception ex)
            {
                return BadRequest($"Erro: {ex.Message}");
            }
        }

        // Grava a lista de deputados no MySQL, evitando duplicados
        // (se o id já existir, atualiza os dados em vez de inserir outra vez)
        private void GravarDeputados(List<Deputado> deputados)
        {
            if (deputados.Count == 0) return;

            string connectionString = _config.GetConnectionString("Default")!;

            using var connection = new MySqlConnection(connectionString);
            connection.Open();

            foreach (var dep in deputados)
            {
                // 1. Verificar se o deputado já existe na base de dados
                string sqlExiste = "SELECT COUNT(*) FROM deputados WHERE id = @id";
                using var cmdExiste = new MySqlCommand(sqlExiste, connection);
                cmdExiste.Parameters.AddWithValue("@id", dep.Id);
                long existe = (long)cmdExiste.ExecuteScalar();

                string sql;
                if (existe > 0)
                {
                    // 2a. Já existe -> atualizar os dados
                    sql = "UPDATE deputados SET " +
                          "nomeParlamentar = @nomeParlamentar, " +
                          "nomeCompleto = @nomeCompleto, " +
                          "grupoParlamentar = @grupoParlamentar, " +
                          "circuloEleitoral = @circuloEleitoral, " +
                          "legislaturaId = @legislaturaId, " +
                          "situacao = @situacao, " +
                          "updatedAt = @updatedAt " +
                          "WHERE id = @id";
                }
                else
                {
                    // 2b. Ainda não existe -> inserir um novo registo
                    sql = "INSERT INTO deputados " +
                          "(id, nomeParlamentar, nomeCompleto, grupoParlamentar, circuloEleitoral, legislaturaId, situacao, updatedAt) " +
                          "VALUES (@id, @nomeParlamentar, @nomeCompleto, @grupoParlamentar, @circuloEleitoral, @legislaturaId, @situacao, @updatedAt)";
                }

                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@id", dep.Id);
                cmd.Parameters.AddWithValue("@nomeParlamentar", dep.NomeParlamentar ?? "");
                cmd.Parameters.AddWithValue("@nomeCompleto", dep.NomeCompleto ?? "");
                cmd.Parameters.AddWithValue("@grupoParlamentar", dep.GrupoParlamentar ?? "");
                cmd.Parameters.AddWithValue("@circuloEleitoral", dep.CirculoEleitoral ?? "");
                cmd.Parameters.AddWithValue("@legislaturaId", dep.LegislaturaId ?? "");
                cmd.Parameters.AddWithValue("@situacao", dep.Situacao ?? "");
                cmd.Parameters.AddWithValue("@updatedAt", dep.UpdatedAt ?? (object)DBNull.Value);

                cmd.ExecuteNonQuery();
            }
        }
    }
}
