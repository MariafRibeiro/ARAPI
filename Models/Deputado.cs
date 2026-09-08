namespace MinhaApi.Models
{
    // Representa um deputado, tal como vem no campo "data" da resposta da API externa
    public class Deputado
    {
        public int Id { get; set; }
        public string? NomeParlamentar { get; set; }
        public string? NomeCompleto { get; set; }
        public string? GrupoParlamentar { get; set; }
        public string? CirculoEleitoral { get; set; }
        public string? LegislaturaId { get; set; }
        public string? Situacao { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    // Representa a resposta completa da API externa (envelope paginado)
    public class PaginatedDeputados
    {
        public List<Deputado> Data { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int Limit { get; set; }
    }
}
