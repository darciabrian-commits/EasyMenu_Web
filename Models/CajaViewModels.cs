namespace EasyMenu.Web.Models;

public sealed class PedidosPorCobrarViewModel
{
    public List<PedidoCajaViewModel> Pedidos { get; init; } = [];
    public string? Mensaje { get; init; }
}

public sealed class PedidoCajaViewModel
{
    public string Id { get; init; } = string.Empty;
    public string CodigoCorto { get; init; } = string.Empty;
    public string ClienteOMesa { get; init; } = string.Empty;
    public string Estado { get; init; } = string.Empty;
    public decimal Total { get; init; }
    public List<ProductoCajaViewModel> Productos { get; init; } = [];
    public string? Mensaje { get; init; }
    public string? MensajePago { get; init; }
    public PagoCajaViewModel? Pago { get; init; }
}

public sealed class PagoCajaViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Estado { get; init; } = string.Empty;
    public string MetodoPago { get; init; } = string.Empty;
    public decimal Total { get; init; }
    public decimal? MontoRecibido { get; init; }
    public decimal? Cambio { get; init; }
    public string? MotivoReembolso { get; init; }
}

public sealed class ProductoCajaViewModel
{
    public string Nombre { get; init; } = string.Empty;
    public string Cantidad { get; init; } = string.Empty;
    public decimal Subtotal { get; init; }
}

public sealed class ComprobanteCajaViewModel
{
    public decimal Total { get; init; }
    public string MetodoPago { get; init; } = string.Empty;
    public decimal? MontoRecibido { get; init; }
    public decimal Cambio { get; init; }
}
