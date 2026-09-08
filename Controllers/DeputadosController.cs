using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
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
            try
            {
                var deputados = await ObterTodosDeputados();
                GravarDeputados(deputados);

                return Ok(deputados);
            }
            catch (Exception ex)
            {
                return BadRequest($"Erro: {ex.Message}");
            }
        }

        // ENDPOINT 2: Sincroniza todos os deputados do OpenAR com a base local
        // Exemplo: POST /api/deputados/sincronizar
        [HttpPost("sincronizar")]
        public async Task<IActionResult> Sincronizar()
        {
            try
            {
                var deputados = await ObterTodosDeputados();
                GravarDeputados(deputados);

                return Ok(new
                {
                    mensagem = "Deputados sincronizados com sucesso.",
                    total = deputados.Count
                });
            }
            catch (Exception ex)
            {
                return BadRequest($"Erro ao sincronizar deputados: {ex.Message}");
            }
        }

        // ENDPOINT 3: Consultar deputados (com filtros opcionais) e gravar no MySQL
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
            var query = new Dictionary<string, string?>
            {
                ["legislatura"] = legislatura,
                ["grupo"] = grupo,
                ["q"] = q,
                ["situacao"] = situacao,
                ["sort"] = sort,
                ["page"] = page?.ToString(),
                ["limit"] = limit?.ToString()
            };
            var url = QueryHelpers.AddQueryString("https://api.openar.pt/v1/deputados", query);

            try
            {
                var resposta = await _httpClient.GetFromJsonAsync<PaginatedDeputados>(url, _jsonOptions);
                var deputados = resposta?.Data ?? new List<Deputado>();

                GravarDeputados(deputados);

                return Ok(new
                {
                    data = deputados,
                    total = resposta?.Total ?? deputados.Count,
                    page = resposta?.Page ?? page ?? 1,
                    limit = resposta?.Limit ?? limit ?? deputados.Count
                });
            }
            catch (Exception ex)
            {
                return BadRequest($"Erro: {ex.Message}");
            }
        }

        // ENDPOINT 4: Detalhe de um deputado específico (só consulta a API externa, não grava)
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

        private async Task<List<Deputado>> ObterTodosDeputados()
        {
            var deputados = new List<Deputado>();
            const int limitePorPagina = 100;
            var pagina = 1;
            var totalEsperado = int.MaxValue;

            while (deputados.Count < totalEsperado)
            {
                var url = QueryHelpers.AddQueryString(
                    "https://api.openar.pt/v1/deputados",
                    new Dictionary<string, string?>
                    {
                        ["page"] = pagina.ToString(),
                        ["limit"] = limitePorPagina.ToString()
                    });
                var resposta = await _httpClient.GetFromJsonAsync<PaginatedDeputados>(url, _jsonOptions);
                var itens = resposta?.Data ?? new List<Deputado>();

                if (resposta is null || itens.Count == 0)
                {
                    break;
                }

                totalEsperado = resposta.Total > 0 ? resposta.Total : totalEsperado;
                deputados.AddRange(itens);

                if (deputados.Count >= totalEsperado || itens.Count < limitePorPagina)
                {
                    break;
                }

                pagina++;
            }

            return deputados;
        }

        // Grava a lista de deputados no MySQL, evitando duplicados
        // (se o id já existir, atualiza os dados em vez de inserir outra vez)
        private void GravarDeputados(List<Deputado> deputados)
        {
            if (deputados.Count == 0) return;

            string connectionString = _config.GetConnectionString("Default")!;

            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            var indice = 0;
            while (indice < deputados.Count)
            {
                var dep = deputados[indice];

                const string sql = "INSERT INTO deputados " +
                    "(id, nomeParlamentar, nomeCompleto, grupoParlamentar, circuloEleitoral, legislaturaId, situacao, updatedAt) " +
                    "VALUES (@id, @nomeParlamentar, @nomeCompleto, @grupoParlamentar, @circuloEleitoral, @legislaturaId, @situacao, @updatedAt) " +
                    "ON DUPLICATE KEY UPDATE " +
                    "nomeParlamentar = VALUES(nomeParlamentar), " +
                    "nomeCompleto = VALUES(nomeCompleto), " +
                    "grupoParlamentar = VALUES(grupoParlamentar), " +
                    "circuloEleitoral = VALUES(circuloEleitoral), " +
                    "legislaturaId = VALUES(legislaturaId), " +
                    "situacao = VALUES(situacao), " +
                    "updatedAt = VALUES(updatedAt)";

                using var cmd = new MySqlCommand(sql, connection, transaction);
                cmd.Parameters.AddWithValue("@id", dep.Id);
                cmd.Parameters.AddWithValue("@nomeParlamentar", dep.NomeParlamentar ?? "");
                cmd.Parameters.AddWithValue("@nomeCompleto", dep.NomeCompleto ?? "");
                cmd.Parameters.AddWithValue("@grupoParlamentar", dep.GrupoParlamentar ?? "");
                cmd.Parameters.AddWithValue("@circuloEleitoral", dep.CirculoEleitoral ?? "");
                cmd.Parameters.AddWithValue("@legislaturaId", dep.LegislaturaId ?? "");
                cmd.Parameters.AddWithValue("@situacao", dep.Situacao ?? "");
                cmd.Parameters.AddWithValue("@updatedAt", dep.UpdatedAt ?? (object)DBNull.Value);

                cmd.ExecuteNonQuery();
                indice++;
            }

            transaction.Commit();
        }
    }
}
