namespace EasyMenu.Web.Models;

public sealed class TableroCocinaViewModel
{
    public List<PedidoCocinaViewModel> Recibidos { get; init; } = [];
    public List<PedidoCocinaViewModel> EnPreparacion { get; init; } = [];
    public List<PedidoCocinaViewModel> Listos { get; init; } = [];
    public string? Mensaje { get; init; }
}

public sealed class PedidoCocinaViewModel
{
    public string Id { get; init; } = string.Empty;
    public string CodigoCorto { get; init; } = string.Empty;
    public string ClienteOMesa { get; init; } = string.Empty;
    public List<ProductoPedidoViewModel> Productos { get; init; } = [];
}

public sealed class ProductoPedidoViewModel
{
    public string Nombre { get; init; } = string.Empty;
    public string Cantidad { get; init; } = string.Empty;
    public string? Notas { get; init; }
}

public sealed class ColumnaPedidosViewModel
{
    public string Titulo { get; init; } = string.Empty;
    public IReadOnlyList<PedidoCocinaViewModel> Pedidos { get; init; } = [];
    public string TextoBoton { get; init; } = string.Empty;
    public string EstadoActual { get; init; } = string.Empty;
    public string SiguienteEstado { get; init; } = string.Empty;
}
